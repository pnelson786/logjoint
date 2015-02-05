using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;


namespace LogJoint.Telemetry
{
	public class TelemetryCollector : ITelemetryCollector
	{	
		static readonly LJTraceSource trace = new LJTraceSource("Telemetry");
		static readonly string sessionsRegistrySectionName = "sessions";
		static readonly string sessionsRegistrySessionElementName = "session";
		readonly Persistence.IStorageManager storage;
		readonly ITelemetryUploader telemetryUploader;
		readonly Persistence.IStorageEntry telemetryStorageEntry;
		readonly IInvokeSynchronization synchronization;

		readonly string currentSessionId;
		readonly Dictionary<string, string> staticTelemetryProperties = new Dictionary<string,string>();

		readonly CancellationTokenSource workerCancellation;
		readonly TaskCompletionSource<int> workerCancellationTask;
		readonly Task worker;

		readonly object sync = new object();
		Dictionary<string, XElement> sessionsAwaitingUploading = new Dictionary<string, XElement>();
		TaskCompletionSource<int> sessionsAwaitingUploadingChanged = new TaskCompletionSource<int>();
		HashSet<string> uploadedSessions = new HashSet<string>();

		readonly int sessionStartedMillis;
		int totalNfOfLogs;
		int maxNfOfSimultaneousLogs;

		bool disposed;

		public TelemetryCollector(
			Persistence.IStorageManager storage,
			ITelemetryUploader telemetryUploader,
			IInvokeSynchronization synchronization,
			IModel model)
		{
			this.storage = storage;
			this.telemetryUploader = telemetryUploader;
			this.synchronization = synchronization;

			this.telemetryStorageEntry = storage.GetEntry("telemetry");
			this.sessionStartedMillis = Environment.TickCount;

			this.currentSessionId = telemetryUploader.IsConfigured ? 
				("session" + Guid.NewGuid().ToString("n")) : null;

			model.OnDisposing += (s, e) => ((IDisposable)this).Dispose();

			if (currentSessionId != null)
			{
				CreateCurrentSessionSection();
				InitStatiTelemetryProperties();

				model.SourcesManager.OnLogSourceAdded += (s, e) =>
				{
					++totalNfOfLogs;
					var nfOfSimultaneousLogs = model.SourcesManager.Items.Count();
					maxNfOfSimultaneousLogs = Math.Max(maxNfOfSimultaneousLogs, nfOfSimultaneousLogs);
				};
			}

			if (telemetryUploader.IsConfigured) // todo: && isPrimaryInstance
			{
				this.workerCancellation = new CancellationTokenSource();
				this.workerCancellationTask = new TaskCompletionSource<int>();
				this.worker = TaskUtils.StartInThreadPoolTaskScheduler(Worker);
			}
		}

		void IDisposable.Dispose()
		{
			if (disposed)
				return;
			trace.Info("disposing telemetry");
			if (worker != null)
			{
				workerCancellation.Cancel();
				workerCancellationTask.TrySetResult(1);
				bool workerCompleted = false;
				try
				{
					workerCompleted = worker.Wait(TimeSpan.FromSeconds(10));
				}
				catch (AggregateException e)
				{
					trace.Error(e, "telemetry worker failed");
				}
				trace.Info("telemetry collector worker {0}", workerCompleted ? "stopped" : "did not stop");
			}
			if (currentSessionId != null)
			{
				DoSessionsRegistryTransaction(TransactionFlag.FinalizeCurrentSession);
			}
			disposed = true;
		}

		private void CreateCurrentSessionSection()
		{
			bool telemetryStorageJustInitialized = false;
			using (var sessions = telemetryStorageEntry.OpenXMLSection(sessionsRegistrySectionName,
				Persistence.StorageSectionOpenFlag.ReadWrite))
			{
				string installationId;
				if (sessions.Data.Root == null)
				{
					telemetryStorageJustInitialized = true;
					installationId = Guid.NewGuid().ToString("n");
					sessions.Data.Add(new XElement("root",
						new XAttribute("installationId", installationId)
					));
				}
				else
				{
					installationId = sessions.Data.Root.AttributeValue("installationId");
				}
				staticTelemetryProperties["installationId"] = installationId;

				sessions.Data.Root.Add(new XElement(sessionsRegistrySessionElementName,
					new XAttribute("id", currentSessionId),
					new XAttribute("started", DateTime.UtcNow.ToString("o"))
				));
			}
			if (telemetryStorageJustInitialized)
				telemetryStorageEntry.AllowCleanup();
		}

		private void InitStatiTelemetryProperties()
		{
			staticTelemetryProperties["timezone"] = TimeZoneInfo.Local.StandardName;

			var buildInfoResourceName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
				.FirstOrDefault(n => n.Contains("BuildInfo"));
			if (buildInfoResourceName != null)
			{
				using (var reader = new StreamReader(
					Assembly.GetExecutingAssembly().GetManifestResourceStream(buildInfoResourceName), Encoding.ASCII, false, 1024, true))
				{
					for (var lineNr = 0; ; ++lineNr)
					{
						var line = reader.ReadLine();
						if (line == null)
							break;
						if (lineNr == 0)
							staticTelemetryProperties["buildTime"] = line;
						else if (lineNr == 1)
							staticTelemetryProperties["sourceRevision"] = line;
					}
				}
			}
		}

		[Flags]
		enum TransactionFlag
		{
			Default = 0,
			FinalizeCurrentSession = 1,
		};

		private void DoSessionsRegistryTransaction(TransactionFlag flags)
		{
			if (disposed)
				return;

			using (var sessions = telemetryStorageEntry.OpenXMLSection(sessionsRegistrySectionName,
				Persistence.StorageSectionOpenFlag.ReadWrite))
			{
				var currentSessionElt = sessions.Data.
					Elements().
					Elements(sessionsRegistrySessionElementName).
					Where(e => GetSessionId(e) == currentSessionId).
					FirstOrDefault();
				if (currentSessionElt != null)
				{
					UpdateTelemtrySessionNode(currentSessionElt);
					if ((flags & TransactionFlag.FinalizeCurrentSession) != 0)
						currentSessionElt.SetAttributeValue("finalized", "true");
				}

				bool sessionsAwaitingUploadingAdded = false;
				lock (sync)
				{
					var uploadedSessionsElemenets =
						sessions.Data.
						Elements().
						Elements(sessionsRegistrySessionElementName).
						Where(e => uploadedSessions.Contains(GetSessionId(e))).
						ToArray();
					foreach (var e in uploadedSessionsElemenets)
					{
						e.Remove();
						trace.Info("submitted telemtry session {0} removed from registry", GetSessionId(e));
					}
					uploadedSessions.Clear();

					foreach (var sessionElement in
						sessions.Data.
						Elements().
						Elements(sessionsRegistrySessionElementName).
						Where(e => IsFinalizedOrOldUnfinalizedSession(e)))
					{
						var id = GetSessionId(sessionElement);
						if (!sessionsAwaitingUploading.ContainsKey(id))
						{
							sessionsAwaitingUploading.Add(id, new XElement(sessionElement));
							trace.Info("new telemtry session {0} read registry and is awaiting submission", id);
							sessionsAwaitingUploadingAdded = true;
						}
					}
				}
				if (sessionsAwaitingUploadingAdded)
					sessionsAwaitingUploadingChanged.TrySetResult(1);
			}
		}

		void UpdateTelemtrySessionNode(XElement sessionNode)
		{
			sessionNode.SetAttributeValue("duration", Environment.TickCount - sessionStartedMillis);
			sessionNode.SetAttributeValue("totalNfOfLogs", totalNfOfLogs);
			sessionNode.SetAttributeValue("maxNfOfSimultaneousLogs", maxNfOfSimultaneousLogs);
		}

		static DateTime? GetSessionStartTime(XElement sessionElement)
		{
			DateTime started;
			if (DateTime.TryParseExact(sessionElement.AttributeValue("started"), "o", null, 
					System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out started))
				return started;
			return null;
		}

		static string GetSessionId(XElement sessionElement)
		{
			return sessionElement.AttributeValue("id");
		}

		static bool IsFinalizedOrOldUnfinalizedSession(XElement e)
		{
			if (e.Attribute("finalized") != null)
				return true;
			DateTime? started = GetSessionStartTime(e);
			if (!started.HasValue)
				return true;
			if ((DateTime.UtcNow - started.Value) > TimeSpan.FromDays(30))
				return true;
			return false;
		}

		private async Task Worker()
		{
			try
			{
				var transactionInvoker = new AsyncInvokeHelper(synchronization, 
					(Action)(() => DoSessionsRegistryTransaction(TransactionFlag.Default)), new object[0]);

				for (; !workerCancellation.IsCancellationRequested; )
				{
					var sleepTask = Task.Delay(
						TimeSpan.FromSeconds(30), 
						workerCancellation.Token);
					await Task.WhenAny(
						sessionsAwaitingUploadingChanged.Task,
						sleepTask,
						workerCancellationTask.Task
					);
					if (workerCancellation.IsCancellationRequested)
						break;
					if (sessionsAwaitingUploadingChanged.Task.IsCompleted)
						sessionsAwaitingUploadingChanged = new TaskCompletionSource<int>();
					if (sleepTask.IsCompleted)
						transactionInvoker.Invoke();
					if (await HandleFinalizedSessionsQueues() > 0)
						transactionInvoker.Invoke();
				}

			}
			catch (TaskCanceledException)
			{
				trace.Info("telemetry worker cancelled");
			}
			catch (OperationCanceledException)
			{
				trace.Info("telemetry worker cancelled");
			}
		}

		private async Task<int> HandleFinalizedSessionsQueues()
		{
			for (int recordsSubmitted = 0; ; )
			{
				XElement sessionAwaitingUploading;
				lock (sync)
				{
					sessionAwaitingUploading = sessionsAwaitingUploading.Values.FirstOrDefault();
				}
				if (sessionAwaitingUploading == null)
					return recordsSubmitted;

				var timestamp = GetSessionStartTime(sessionAwaitingUploading);
				var sessionId = GetSessionId(sessionAwaitingUploading);
				bool recordSubmittedOk = true;
				if (!string.IsNullOrEmpty(sessionId) && timestamp.HasValue)
				{
					trace.Info("submitting telemtry record {0}", sessionId);
					TelemetryUploadResult uploadResult = TelemetryUploadResult.Failure;
					try
					{
						uploadResult = await telemetryUploader.Upload(
							timestamp.Value,
							sessionId,
							staticTelemetryProperties.Union(
								sessionAwaitingUploading.
									Attributes().
									Select(a => new KeyValuePair<string, string>(a.Name.LocalName, a.Value))
							).ToDictionary(a => a.Key, a => a.Value),
							workerCancellation.Token
						);
					}
					catch (Exception e)
					{
						trace.Error(e, "Failed to upload telemetry session {0}", sessionId);
					}
					trace.Info("Telemtry session {0} submitted with result {1}", sessionId, uploadResult);
					recordSubmittedOk =
						uploadResult == TelemetryUploadResult.Success || uploadResult == TelemetryUploadResult.Duplicate;
				}
				if (recordSubmittedOk && !string.IsNullOrEmpty(sessionId))
				{
					++recordsSubmitted;
					lock (sync)
					{
						sessionsAwaitingUploading.Remove(sessionId);
						uploadedSessions.Add(sessionId);
					}
				}
			}
		}
	};
}