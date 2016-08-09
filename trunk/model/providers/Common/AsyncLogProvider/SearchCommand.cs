using System;
using System.Threading;
using System.Threading.Tasks;

namespace LogJoint
{
	internal class SearchCommand : IAsyncLogProviderCommandHandler
	{
		public SearchCommand(
			SearchAllOccurencesParams searchParams,
			Func<IMessage, bool> callback,
			Progress.IProgressEventsSink progress,
			IModelThreads modelThreads
		)
		{
			this.searchParams = searchParams;
			this.callback = callback;
			this.progress = progress;
			this.modelThreads = modelThreads;
		}

		public Task Task { get { return task.Task; } }

		bool IAsyncLogProviderCommandHandler.RunSynchroniously(CommandContext ctx)
		{
			if (ctx.Cache == null)
				return false;

			if (continuationToken != null)
				return false; // only reader knows how to handle its continuation tokens

			if (!ctx.Cache.AvailableRange.Equals(ctx.Cache.MessagesRange))
				return false; // speed up only fully cached logs. partial optimization it's noticable.

			var preprocessedSearchOptions = searchParams.Options.TryPreprocess();
			if (preprocessedSearchOptions != null)
			{
				var bulkSearchState = new Search.BulkSearchState();
				using (var threadsBulkProcessing = modelThreads.StartBulkProcessing())
				{
					foreach (var loadedMsg in ((IMessagesCollection)ctx.Cache.Messages).Forward(0, int.MaxValue))
					{
						var msg = loadedMsg.Message;
						var threadsBulkProcessingResult = threadsBulkProcessing.ProcessMessage(msg);
						if (!LogJoint.Search.SearchInMessageText(msg, preprocessedSearchOptions, bulkSearchState).HasValue)
							continue;
						if (!callback(msg.Clone()))
							break;
					}
				}
			}
			
			return true;
		}

		void IAsyncLogProviderCommandHandler.ContinueAsynchroniously(CommandContext ctx)
		{
			using (var innerCancellation = CancellationTokenSource.CreateLinkedTokenSource(ctx.Cancellation, ctx.Preemption))
			{
				var searchRange = new FileRange.Range(
					searchParams.FromPosition.GetValueOrDefault(ctx.Reader.BeginPosition), ctx.Reader.EndPosition);

				var parserParams = new CreateSearchingParserParams()
				{
					Range = searchRange,
					SearchParams = searchParams,
					Cancellation = innerCancellation.Token,
					ContinuationToken = continuationToken,
					ProgressHandler = pos => UpdateSearchCompletionPercentage(progress, pos, searchRange, false)
				};

				try
				{
					using (var parser = ctx.Reader.CreateSearchingParser(parserParams))
					{
						for (; ; )
						{
							var msg = parser.ReadNext();
							if (msg == null || !callback(msg))
								break;
						}
					}
				}
				catch (SearchCancelledException e) // todo: impl it for xml reader
				{
					if (ctx.Preemption.IsCancellationRequested)
						continuationToken = e.ContinuationToken;
					throw;
				}
			}
		}

		void IAsyncLogProviderCommandHandler.Complete(Exception e)
		{
			if (e != null)
				task.SetException(e);
			else
				task.SetResult(0);
		}

		private void UpdateSearchCompletionPercentage(
			Progress.IProgressEventsSink progress,
			long lastHandledPosition,
			FileRange.Range fullSearchPositionsRange,
			bool skipMessagesCountCheck)
		{
			if (progress == null)
				return;
			if (!skipMessagesCountCheck && (messagesReadSinceCompletionPercentageUpdate % 256) != 0)
			{
				++messagesReadSinceCompletionPercentageUpdate;
			}
			else
			{
				double value;
				if (fullSearchPositionsRange.Length > 0)
					value = Math.Max(0d, (double)(lastHandledPosition - fullSearchPositionsRange.Begin) / (double)fullSearchPositionsRange.Length);
				else
					value = 0;
				progress.SetValue(value);
				messagesReadSinceCompletionPercentageUpdate = 0;
			}
		}

		readonly TaskCompletionSource<int> task = new TaskCompletionSource<int>();
		readonly SearchAllOccurencesParams searchParams;
		readonly Func<IMessage, bool> callback;
		readonly Progress.IProgressEventsSink progress;
		readonly IModelThreads modelThreads;
		object continuationToken;
		int messagesReadSinceCompletionPercentageUpdate;
	};
}