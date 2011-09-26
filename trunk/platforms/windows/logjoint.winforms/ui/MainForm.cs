using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using MessageFlag = LogJoint.MessageBase.MessageFlag;

namespace LogJoint.UI
{
	public partial class MainForm : Form, IModelHost, IUINavigationHandler, Presenters.ThreadsList.Presenter.ICallback,
		IMessagePropertiesFormHost, Preprocessing.IPreprocessingUserRequests
	{
		LJTraceSource tracer = LJTraceSource.EmptyTracer;
		readonly InvokeSynchronization invokingSynchronization;
		Model model;
		NewLogSourceDialog newLogSourceDialog;
		int liveUpdateLock;
		StatusReport statusReport;
		StatusReport autoHideStatusReport;
		int updateTimeTick = 0;
		int liveLogsTick = 0;
		DateTime? lastTimeTabHasBeenUsed;
		TempFilesManager tempFilesManager;
		int filterIndex;
		Control focusedControlBeforeWaitState;
		bool isAnalizing;
		List<PluginBase> plugins = new List<PluginBase>();
		SearchHistory searchHistory = new SearchHistory();
		LogJointApplication pluginEntryPoint;
		
		object credentialCacheLock = new object();
		NetworkCredentialsStorage credentialCache = null;

		LogJoint.UI.Presenters.ThreadsList.Presenter threadsListPresenter;
		LogJoint.UI.Presenters.LogViewer.Presenter viewerPresenter;
		LogJoint.UI.Presenters.SourcesList.Presenter sourcesListPresenter;
		MessagePropertiesForm propertiesForm;

		public MainForm()
		{
			Thread.CurrentThread.Name = "Main thread";

			try
			{
				tracer = new LJTraceSource("TraceSourceApp");
			}
			catch (Exception)
			{
				Debug.Write("Failed to create tracer");
			}

			using (tracer.NewFrame)
			{
				invokingSynchronization = new InvokeSynchronization(this);

				tempFilesManager = LogJoint.TempFilesManager.GetInstance();

				InitializeComponent();
				AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
				{
					string msg = "Unhahdled domain exception occured";
					if (e.ExceptionObject is Exception)
						tracer.Error((Exception)e.ExceptionObject, msg);
					else
						tracer.Error("{0}: ({1}) {2}", msg, e.ExceptionObject.GetType(), e.ExceptionObject);
				};

				model = new Model(this);

				timeLineControl.SetHost(model);
				timeLineControl.BeginTimeRangeDrag += delegate(object sender, EventArgs args)
				{
					liveUpdateLock++;
				};
				timeLineControl.EndTimeRangeDrag += delegate(object sender, EventArgs args)
				{
					liveUpdateLock--;
				};

				viewerPresenter = new Presenters.LogViewer.Presenter(
					new Presenters.LogViewer.DefaultModelImpl(model), logViewerControl, null);
				logViewerControl.SetPresenter(viewerPresenter);
				logViewerControl.ManualRefresh += delegate(object sender, EventArgs args)
				{
					using (tracer.NewFrame)
					{
						tracer.Info("----> User Command: Refresh");
						model.Refresh();
					}
				};
				viewerPresenter.BeginShifting += delegate(object sender, EventArgs args)
				{
					SetWaitState(true);
					GetStatusReport().SetStatusString("Moving in-memory window...");
					cancelShiftingLabel.Visible = true;
					cancelShiftingDropDownButton.Visible = true;
				};
				viewerPresenter.EndShifting += delegate(object sender, EventArgs args)
				{
					cancelShiftingLabel.Visible = false;
					cancelShiftingDropDownButton.Visible = false;
					GetStatusReport().Dispose();
					SetWaitState(false);
				};
				viewerPresenter.FocusedMessageChanged += delegate(object sender, EventArgs args)
				{
					timeLineControl.Invalidate();
					model.OnCurrentViewPositionChanged(viewerPresenter.FocusedMessageTime);
					sourcesListView.InvalidateFocusedMessageArea();
					pluginEntryPoint.FireFocusedMessageChanged();
					if (FocusedMessageChanged != null)
						FocusedMessageChanged(sender, args);
					if (GetPropertiesForm() != null)
						GetPropertiesForm().UpdateView(FocusedMessage);
				};

				threadsListPresenter = new UI.Presenters.ThreadsList.Presenter(model, threadsListView, this);
				threadsListView.SetPresenter(threadsListPresenter);

				sourcesListPresenter = new Presenters.SourcesList.Presenter(model, sourcesListView, null);
				sourcesListView.SetHost(model);
				sourcesListView.SetPresenter(sourcesListPresenter);
				sourcesListView.SelectionChanged += delegate(object sender, EventArgs args)
				{
					deleteButton.Enabled = sourcesListView.SelectedCount > 0;
					trackChangesCheckBox.Enabled = sourcesListView.SelectedSources.Count() > 0;
					UpdateTrackChangesCheckBox();
				};
				sourcesListView.DeleteRequested += delegate(object sender, EventArgs args)
				{
					DeleteSelectedSources();
				};

				timeLineControl.RangeChanged += delegate(object sender, EventArgs args)
				{
					logViewerControl.ShowMilliseconds = timeLineControl.AreMillisecondsVisible;
					model.Updates.InvalidateTimeGaps();
				};

				displayFiltersListView.SetHost(model.DisplayFiltersListViewHost);
				displayFiltersListView.FilterChecked += delegate(object sender, EventArgs args)
				{
					UpdateView(false);
				};
				displayFiltersListView.SelectionChanged += delegate(object sender, EventArgs args)
				{
					int count = displayFiltersListView.SelectedCount;
					deleteDisplayFilterButton.Enabled = count > 0;
					moveDisplayFilterDownButton.Enabled = count == 1;
					moveDisplayFilterUpButton.Enabled = count == 1;
				};

				hlFiltersListView.SetHost(model.HighlightFiltersListViewHost);
				hlFiltersListView.FilterChecked += delegate(object sender, EventArgs args)
				{
					UpdateView(false);
				};
				hlFiltersListView.SelectionChanged += delegate(object sender, EventArgs args)
				{
					int count = hlFiltersListView.SelectedCount;
					deleteHLFilterButton.Enabled = count > 0;
					moveHLFilterUpButton.Enabled = count == 1;
					moveHLFilterDownButton.Enabled = count == 1;
				};
				searchTextBox.Search += delegate(object sender, EventArgs args)
				{
					DoSearch(false);
				};
				bookmarksView.SetHost(model.BookmarksViewHost);

				timelineControlPanel.SetHost(model);
				timelineControlPanel.Zoom += delegate(object sender, TimelineControlEventArgs args)
				{
					timeLineControl.Zoom(args.Delta);
				};
				timelineControlPanel.Scroll += delegate(object sender, TimelineControlEventArgs args)
				{
					timeLineControl.Scroll(args.Delta);
				};
				timelineControlPanel.ZoomToViewAll += delegate(object sender, EventArgs args)
				{
					timeLineControl.ZoomToViewAll();
				};
				timelineControlPanel.ViewTailMode += delegate(object sender, ViewTailModeRequestEventArgs args)
				{
					if (args.ViewTailModeRequested)
						timeLineControl.TrySwitchOnViewTailMode();
					else
						timeLineControl.TrySwitchOffViewTailMode();
				};

				InitLogFactories();
				UserDefinedFormatsManager.DefaultInstance.ReloadFactories();

				CreatePluginEntryPoint();

				InitPlugins();

				LoadTabExtensions();
			}
		}

		MessagePropertiesForm GetPropertiesForm()
		{
			if (propertiesForm != null)
				if (propertiesForm.IsDisposed)
					propertiesForm = null;
			return propertiesForm;
		}

		public void ShowMessageProperties()
		{
			if (GetPropertiesForm() == null)
			{
				propertiesForm = new MessagePropertiesForm(this);
				components.Add(propertiesForm);
				AddOwnedForm(propertiesForm);
			}
			propertiesForm.UpdateView(FocusedMessage);
			propertiesForm.Show();
		}

		#region IModelHost

		public LJTraceSource Tracer { get { return tracer; } }

		public IInvokeSynchronization Invoker { get { return invokingSynchronization; } }

		public void OnIdleWhileShifting()
		{
			Application.DoEvents();
		}

		public ITempFilesManager TempFilesManager 
		{
			get { return tempFilesManager; }
		}

		public void OnNewProvider(ILogProvider provider)
		{
			model.MRU.RegisterRecentLogEntry(provider);
		}

		public void OnUpdateView()
		{
			UpdateView(false);
		}

		public IStatusReport GetStatusReport()
		{
			if (statusReport != null)
				statusReport.Dispose();
			statusReport = new StatusReport(this);
			return statusReport;
		}

		public DateTime? CurrentViewTime
		{
			get { return viewerPresenter.FocusedMessageTime; }
		}

		public void SetCurrentViewTime(DateTime? time, NavigateFlag flags)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("time={0}, flags={1}", time, flags);
				UI.LogViewerControl ctrl = logViewerControl;
				NavigateFlag origin = flags & NavigateFlag.OriginMask;
				NavigateFlag align = flags & NavigateFlag.AlignMask;
				switch (origin)
				{
					case NavigateFlag.OriginDate:
						tracer.Info("Selecting the line at the certain time");
						viewerPresenter.SelectMessageAt(time.Value, align);
						break;
					case NavigateFlag.OriginStreamBoundaries:
						switch (align)
						{
							case NavigateFlag.AlignTop:
								tracer.Info("Selecting the first line");
								viewerPresenter.SelectFirstMessage();
								break;
							case NavigateFlag.AlignBottom:
								tracer.Info("Selecting the last line");
								viewerPresenter.SelectLastMessage();
								break;
						}
						break;
				}
			}
		}

		public MessageBase FocusedMessage
		{
			get
			{
				return viewerPresenter.FocusedMessage;
			}
		}

		public bool FocusRectIsRequired
		{
			get 
			{
				if (!lastTimeTabHasBeenUsed.HasValue)
					return false;
				if (DateTime.Now - lastTimeTabHasBeenUsed > TimeSpan.FromSeconds(10))
				{
					lastTimeTabHasBeenUsed = null;
					return false;
				}
				return true;
			}
		}

		public IUINavigationHandler UINavigationHandler
		{
			get { return this; }
		}

		#endregion

		#region IMessagePropertiesFormHost Members

		public bool BookmarksSupported
		{
			get { return model.Bookmarks != null; }
		}

		public bool NavigationOverHighlightedIsEnabled
		{
			get
			{
				return model.HighlightFilters.FilteringEnabled &&
					model.HighlightFilters.Count > 0;
			}
		}

		public void ToggleBookmark(MessageBase line)
		{
			viewerPresenter.ToggleBookmark(line);
		}

		public void FindBegin(FrameEnd end)
		{
			viewerPresenter.GoToParentFrame();
		}

		public void FindEnd(FrameBegin end)
		{
			viewerPresenter.GoToEndOfFrame();
		}

		public void Next()
		{
			viewerPresenter.Next();
		}

		public void Prev()
		{
			viewerPresenter.Prev();
		}

		public void NextHighlighted()
		{
			viewerPresenter.GoToNextHighlightedMessage();
		}

		public void PrevHighlighted()
		{
			viewerPresenter.GoToPrevHighlightedMessage();
		}

		#endregion

		public void ExecuteThreadPropertiesDialog(IThread thread)
		{
			using (UI.ThreadPropertiesForm f = new UI.ThreadPropertiesForm(thread, this))
			{
				f.ShowDialog();
			} 
		}

		public void ForceViewUpdateAfterThreadChecked()
		{
			Action action = () => UpdateView(false);
			BeginInvoke(action);
		}

		public event EventHandler FocusedMessageChanged;

		void UpdateTrackChangesCheckBox()
		{
			bool f1 = false;
			bool f2 = false;
			foreach (ILogSource s in sourcesListView.SelectedSources)
			{
				if (s.Visible && s.TrackingEnabled)
					f1 = true;
				else
					f2 = true;
				if (f1 && f2)
					break;
			}
			CheckState newState;
			if (f1 && f2)
				newState = CheckState.Indeterminate;
			else if (f1)
				newState = CheckState.Checked;
			else
				newState = CheckState.Unchecked;

			if (trackChangesCheckBox.CheckState != newState)
				trackChangesCheckBox.CheckState = newState;
		}

		void UpdateFilteringView()
		{
			displayFiltersListView.UpdateView();
			enableFilteringCheckBox.Checked = model.DisplayFilters.FilteringEnabled;
		}

		void UpdateHightlightingView()
		{
			hlFiltersListView.UpdateView();
			enableHighlightingCheckBox.Checked = model.HighlightFilters.FilteringEnabled;
			bool enableNextPrev = NavigationOverHighlightedIsEnabled;
			nextHightlightedButton.Enabled = enableNextPrev;
			prevHightlightedButton.Enabled = enableNextPrev;
		}

		void UpdateBookmarksView()
		{
			bookmarksView.UpdateView();
		}

		static class Win32Native
		{
			public const int SC_CLOSE = 0xF060;
			public const int MF_GRAYED = 0x1;
			public const int MF_ENABLED = 0x0;

			[DllImport("user32.dll")]
			public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

			[DllImport("user32.dll")]
			public static extern int EnableMenuItem(IntPtr hMenu, int wIDEnableItem, int wEnable);

			[DllImport("user32.dll")]
			public static extern IntPtr GetFocus();
		}

		void SetWaitState(bool wait)
		{
			using (tracer.NewFrame)
			{
				if (wait)
				{
					tracer.Info("Setting wait state");
					focusedControlBeforeWaitState = Control.FromHandle(Win32Native.GetFocus());
				}
				else
				{
					tracer.Info("Exiting from wait state");
				}
				splitContainer2.Enabled = !wait;
				splitContainer2.ForeColor = wait ? Color.Gray : Color.Black;
				Win32Native.EnableMenuItem(Win32Native.GetSystemMenu(this.Handle, false), Win32Native.SC_CLOSE,
					wait ? Win32Native.MF_GRAYED : Win32Native.MF_ENABLED);
				if (!wait)
				{
					if (focusedControlBeforeWaitState != null
					 && !focusedControlBeforeWaitState.IsDisposed
					 && focusedControlBeforeWaitState.Enabled)
					{
						focusedControlBeforeWaitState.Focus();
					}
					focusedControlBeforeWaitState = null;
				}
			}
		}

		static readonly MessageFlag[] checkListBoxFlags = new MessageFlag[]
		{ 
			MessageFlag.Content | MessageFlag.Error, 
			MessageFlag.Content | MessageFlag.Warning,
			MessageFlag.Content | MessageFlag.Info,
			MessageFlag.StartFrame | MessageFlag.EndFrame
		};

		void DoSearch(bool invertDirection)
		{
			LogJoint.UI.Presenters.LogViewer.SearchOptions so;
			so.Template = this.searchTextBox.Text;
			so.WholeWord = this.wholeWordCheckbox.Checked;
			so.ReverseSearch = this.searchUpCheckbox.Checked;
			if (invertDirection)
				so.ReverseSearch = !so.ReverseSearch;
			so.Regexp = this.regExpCheckBox.Checked;
			so.SearchHiddenText = this.hiddenTextCheckBox.Checked;
			so.SearchWithinThisThread = null;
			if (this.searchWithinCurrentThreadCheckbox.Checked
			 && viewerPresenter.FocusedMessage != null)
			{
				so.SearchWithinThisThread = viewerPresenter.FocusedMessage.Thread;
			}
			so.TypesToLookFor = MessageFlag.None;
			so.MatchCase = this.matchCaseCheckbox.Checked;
			so.StartFrom = new LogJoint.UI.Presenters.LogViewer.SearchOptions.SearchPosition?();
			so.HighlightResult = true;
			foreach (int i in this.messageTypesCheckedListBox.CheckedIndices)
				so.TypesToLookFor |= checkListBoxFlags[i];
			LogJoint.UI.Presenters.LogViewer.SearchResult sr;
			try
			{
				sr = viewerPresenter.Search(so);
			}
			catch (LogJoint.UI.Presenters.LogViewer.SearchTemplateException)
			{
				MessageBox.Show("Error in search template", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			if (!sr.Succeeded)
			{
				IStatusReport rpt = GetStatusReport();
				rpt.AutoHide = true;
				rpt.Blink = true;
				rpt.SetStatusString(GetUnseccessfulSearchMessage(so));
			}
			searchHistory.Add(so.Template);
		}

		string GetUnseccessfulSearchMessage(LogJoint.UI.Presenters.LogViewer.SearchOptions so)
		{
			List<string> options = new List<string>();
			if (!string.IsNullOrEmpty(so.Template))
				options.Add(so.Template);
			if ((so.TypesToLookFor & MessageFlag.StartFrame) != 0)
				options.Add("Frames");
			if ((so.TypesToLookFor & MessageFlag.Info) != 0)
				options.Add("Infos");
			if ((so.TypesToLookFor & MessageFlag.Warning) != 0)
				options.Add("Warnings");
			if ((so.TypesToLookFor & MessageFlag.Error) != 0)
				options.Add("Errors");
			if (so.WholeWord)
				options.Add("Whole word");
			if (so.MatchCase)
				options.Add("Match case");
			if (so.ReverseSearch)
				options.Add("Search up");
			StringBuilder msg = new StringBuilder();
			msg.Append("No messages found");
			if (options.Count > 0)
			{
				msg.Append(" (");
				for (int optIdx = 0; optIdx < options.Count; ++optIdx)
					msg.AppendFormat("{0}{1}", (optIdx > 0 ? ", " : ""), options[optIdx]);
				msg.Append(")");
			}
			return msg.ToString();
		}

		private void doSearchButton_Click(object sender, EventArgs e)
		{
			DoSearch(false);
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			using (tracer.NewFrame)
			{
				SetWaitState(true);
				try
				{
					model.Dispose();
					DisposePlugins();
				}
				finally
				{
					SetWaitState(false);
				}
			}
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			string[] args = Environment.GetCommandLineArgs();

			if (args.Length > 1)
			{
				model.LogSourcesPreprocessings.Preprocess(
					args.Skip(1).Select(f => new Preprocessing.FormatDetectionStep(f)),
					this
				);
			}
		}

		private void recentButton_Click(object sender, EventArgs e)
		{
			UserDefinedFormatsManager.DefaultInstance.ReloadFactories();
			mruContextMenuStrip.Items.Clear();
			foreach (RecentLogEntry entry in model.MRU.GetMRUList())
			{
				ToolStripMenuItem item = new ToolStripMenuItem(entry.Factory.GetUserFriendlyConnectionName(entry.ConnectionParams));
				item.Tag = entry.ToString();
				mruContextMenuStrip.Items.Add(item);
			}
			if (mruContextMenuStrip.Items.Count == 0)
			{
				mruContextMenuStrip.Items.Add("<No recent files>").Enabled = false;
			}

			mruContextMenuStrip.Show(recentButton, new Point(0, recentButton.Height));
		}

		private void mruContextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			// Hide the menu here to simplify debugging. Without Hide() the menu remains 
			// on the screen if execution stops at a breakepoint.
			mruContextMenuStrip.Hide(); 

			string connectString = e.ClickedItem.Tag as string;
			if (string.IsNullOrEmpty(connectString))
				return;
			RecentLogEntry entry = new RecentLogEntry(connectString);
			try
			{
				//var reader = model.LoadFrom(entry);
				//model.MRU.RegisterRecentLogEntry(reader);
				model.LogSourcesPreprocessings.Preprocess(entry, this);
			}
			catch (Exception)
			{
				System.Windows.Forms.MessageBox.Show(
					string.Format("Failed to open file"), "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private void deleteButton_Click(object sender, EventArgs e)
		{
			DeleteSelectedSources();
		}

		private void DeleteSelectedSources()
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Delete sources");

				var toDelete = new List<ILogSource>();
				var toDelete2 = new List<Preprocessing.ILogSourcePreprocessing>();
				foreach (ILogSource s in sourcesListView.SelectedSources)
				{
					if (s.IsDisposed)
						continue;
					tracer.Info("-- source to delete: {0}", s.ToString());
					toDelete.Add(s);
				}
				foreach (Preprocessing.ILogSourcePreprocessing p in sourcesListView.SelectedPreprocessings)
				{
					if (p.IsDisposed)
						continue;
					tracer.Info("-- preprocessing to delete: {0}", p.ToString());
					toDelete2.Add(p);
				}

				if (toDelete.Count == 0 && toDelete2.Count == 0)
				{
					tracer.Info("Nothing to delete");
					return;
				}

				if (MessageBox.Show(
					string.Format("You are about to remove {0} log source(s).\nAre you sure?", toDelete.Count + toDelete2.Count),
					this.Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					tracer.Info("User didn't confirm the deletion");
					return;
				}

				SetWaitState(true);
				try
				{
					model.DeleteLogs(toDelete.ToArray());
					model.DeletePreprocessings(toDelete2.ToArray());
				}
				finally
				{
					SetWaitState(false);
				}
			}
		}

		void UpdateView(bool liteUpdate)
		{
			if (!liteUpdate)
			{
				if (model.Updates.ValidateThreads())
				{
					threadsListPresenter.UpdateView();
					//threadsListView.UpdateView();
					model.Bookmarks.PurgeBookmarksForDisposedThreads();
				}

				if (model.Updates.ValidateTimeline())
				{
					timeLineControl.UpdateView();
					timelineControlPanel.UpdateView();
				}

				if (!model.AtLeastOneSourceIsBeingLoaded())
				{
					if (model.Updates.ValidateMessages())
					{
						viewerPresenter.UpdateView();
						model.SetCurrentViewPositionIfNeeded();
					}
				}

				if (model.Updates.ValidateFilters())
				{
					UpdateFilteringView();
				}

				if (model.Updates.ValidateHighlightFilters())
				{
					UpdateHightlightingView();
				}

				if (model.Updates.ValidateTimeGaps())
				{
					model.TimeGaps.Update(timeLineControl.TimeRange);
				}

				if (model.Updates.ValidateBookmarks())
				{
					UpdateBookmarksView();
				}

				SetAnalizingIndication(model.TimeGaps.IsWorking);
			}
			if (model.Updates.ValidateSources())
			{
				sourcesListView.UpdateView();
				UpdateTrackChangesCheckBox();
				pluginEntryPoint.FireSourcesChanged();
			}
		}

		private void updateViewTimer_Tick(object sender, EventArgs e)
		{
			++updateTimeTick;
			++liveLogsTick;

			if (liveUpdateLock == 0)
			{
				UpdateView((updateTimeTick % 3) != 0);
				if ((liveLogsTick % 6) == 0)
					model.Refresh();
			}

			if (autoHideStatusReport != null)
			{
				autoHideStatusReport.AutoHideIfItIsTime();
			}
		}

		void InitLogFactories()
		{
			Assembly[] asmsToAnalize = new Assembly[] { Assembly.GetEntryAssembly(), typeof(Model).Assembly };

			foreach (Assembly asm in asmsToAnalize)
			{
				foreach (Type t in asm.GetTypes())
				{
					if (t.IsClass && typeof(ILogProviderFactory).IsAssignableFrom(t))
					{
						System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
					}
				}
			}
		}

		void CreatePluginEntryPoint()
		{
			pluginEntryPoint = new LogJointApplication(model, this, logViewerControl);
		}

		void InitPlugins()
		{
			using (tracer.NewFrame)
			{
				string thisPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				tracer.Info("Plugins directory: {0}", thisPath);
				foreach (string pluginPath in Directory.GetFiles(thisPath, "*.plugin.dll"))
				{
					tracer.Info("---> Plugin found {0}", pluginPath);
					Assembly pluginAsm;
					try
					{
						pluginAsm = Assembly.LoadFrom(pluginPath);
					}
					catch (Exception e)
					{
						tracer.Error(e, "Filed to load plugin");
						continue;
					}
					Type pluginType = pluginAsm.GetType("LogJoint.Plugin");
					if (pluginType == null)
					{
						tracer.Warning("Plugin class not found in plugin assembly");
						continue;
					}
					if (!typeof(PluginBase).IsAssignableFrom(pluginType))
					{
						tracer.Warning("Plugin object doesn't support IPlugin");
						continue;
					}
					PluginBase plugin;
					try
					{
						plugin = (PluginBase)Activator.CreateInstance(pluginType);
					}
					catch (Exception e)
					{
						tracer.Error(e, "Filed to create an instance of plugin");
						continue;
					}
					try
					{
						plugin.Init(pluginEntryPoint);
					}
					catch (Exception e)
					{
						plugin.Dispose();
						tracer.Error(e, "Filed to init an instance of plugin");
						continue;
					}
					plugins.Add(plugin);
				}
			}
		}

		void LoadTabExtensions()
		{
			foreach (PluginBase plugin in plugins)
			{
				foreach (IMainFormTabExtension ext in plugin.MainFormTagExtensions)
				{
					TabPage tab = new TabPage(ext.Caption);
					menuTabControl.TabPages.Add(tab);
					tab.Controls.Add(ext.TapPage);
				}
			}
		}

		void DisposePlugins()
		{
			foreach (PluginBase plugin in plugins)
			{
				plugin.Dispose();
			}
		}

		private void addNewLogButton_Click(object sender, EventArgs e)
		{
			UserDefinedFormatsManager.DefaultInstance.ReloadFactories();
			if (newLogSourceDialog == null)
				newLogSourceDialog = new NewLogSourceDialog(model, model.MRU);
			newLogSourceDialog.Execute();
		}

		void SetAnalizingIndication(bool analizing)
		{
			if (isAnalizing == analizing)
				return;
			toolStripAnalizingImage.Visible = analizing;
			toolStripAnalizingLabel.Visible = analizing;
			isAnalizing = analizing;
		}

		class StatusReport : IStatusReport
		{
			MainForm owner;
			int ticksWhenAutoHideStarted;

			public StatusReport(MainForm owner)
			{
				this.owner = owner;
			}

			#region IStatusReport Members

			public void SetStatusString(string text)
			{
				owner.toolStripStatusLabel.Text = !string.IsNullOrEmpty(text) ? text : "";
			}

			public bool AutoHide
			{
				get 
				{
					return IsAutoHide; 
				}
				set 
				{
					if (!IsActive)
						return;
					if (value == AutoHide)
						return;
					if (value)
					{
						ticksWhenAutoHideStarted = Environment.TickCount;
						owner.autoHideStatusReport = this;
					}
					else
					{
						owner.autoHideStatusReport = this;
					}
				}
			}

			public bool Blink
			{
				get 
				{ 
					return IsBlinking; 
				}
				set 
				{
					if (IsBlinking == value)
						return;
					if (!IsActive)
						return;
					owner.toolStripStatusImage.Visible = value;
				}
			}

			#endregion

			#region IDisposable Members

			public void Dispose()
			{
				if (IsAutoHide)
				{
					owner.autoHideStatusReport = null;
				}
				if (IsBlinking)
				{
					owner.toolStripStatusImage.Visible = false;
				}
				if (IsActive)
				{
					SetStatusString(null);
					owner.statusReport = null;
				}
			}

			#endregion

			public void AutoHideIfItIsTime()
			{
				if (Environment.TickCount - this.ticksWhenAutoHideStarted > 1000 * 3)
					Dispose();
			}

			bool IsActive
			{
				get { return owner.statusReport == this; }
			}
			bool IsAutoHide
			{
				get { return owner.autoHideStatusReport == this; }
			}
			bool IsBlinking
			{
				get { return IsActive && owner.toolStripStatusImage.Visible; }
			}
		};

		protected override bool ProcessTabKey(bool forward)
		{
			this.lastTimeTabHasBeenUsed = DateTime.Now;
			return base.ProcessTabKey(forward);
		}

		private void DoToggleBookmark()
		{
			MessageBase l = viewerPresenter.FocusedMessage;
			if (l != null)
			{
				model.Bookmarks.ToggleBookmark(l);
				UpdateView(false);
			}
			else
			{
				tracer.Warning("There is no lines selected");
			}
		}

		private void toggleBookmarkButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Toggle Bookmark.");
				DoToggleBookmark();
			}
		}

		private void deleteAllBookmarksButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Clear Bookmarks.");

				if (model.Bookmarks.Count == 0)
				{
					tracer.Info("Nothing to clear");
					return;
				}

				if (MessageBox.Show(
					string.Format("You are about to delete ({0}) bookmark(s).\nAre you sure?", model.Bookmarks.Count),
					this.Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					tracer.Info("User didn't confirm the cleaning");
					return;
				}

				model.Bookmarks.Clear();
				UpdateView(false);
			}
		}

		void NextBookmark(bool forward)
		{
			if (!viewerPresenter.NextBookmark(forward))
			{
				IStatusReport rpt = GetStatusReport();
				rpt.SetStatusString("Bookmark not found");
				rpt.AutoHide = true;
				rpt.Blink = true;
			}
		}

		private void prevBookmarkButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Prev Bookmark.");
				NextBookmark(false);
			}
		}

		private void nextBookmarkButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Next Bookmark.");
				NextBookmark(true);
			}
		}

		private void timeLineControl1_Navigate(object sender, LogJoint.UI.TimeNavigateEventArgs args)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Navigate from timeline. Date='{0}', Flags={1}.", args.Date, args.Flags);
				model.NavigateTo(args.Date, args.Flags);
			}
		}

		private void cancelShiftingDropDownButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Cancel Shifting");
				model.CancelShifting();
			}
		}

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (cancelShiftingDropDownButton.Visible && e.KeyData == Keys.Escape)
				cancelShiftingDropDownButton_Click(sender, e);
			if (e.KeyData == (Keys.F | Keys.Control))
			{
				menuTabControl.SelectedTab = searchTabPage;
				searchTextBox.Focus();
				searchTextBox.SelectAll();
			}
			else if (e.KeyData == Keys.F3)
			{
				DoSearch(false);
			}
			else if (e.KeyData == (Keys.F3 | Keys.Shift))
			{
				DoSearch(true);
			}
			else if (e.KeyData == (Keys.K | Keys.Control))
			{
				DoToggleBookmark();
			}
			else if (e.KeyData == Keys.F2)
			{
				NextBookmark(true);
			}
			else if (e.KeyData == (Keys.F2 | Keys.Shift))
			{
				NextBookmark(false);
			}
		}

		#region IUINavigationHandler Members

		public void ShowLine(IBookmark bmk)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("Boomark={0}", bmk);
				viewerPresenter.SelectMessageAt(bmk);
			}
		}

		public void ShowThread(IThread thread)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("Thread={0}", thread.DisplayName);
				menuTabControl.SelectedTab = threadsTabPage;
				threadsListPresenter.Select(thread);
			}
		}

		public void ShowLogSource(ILogSource source)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("Source={0}", source.DisplayName);
				menuTabControl.SelectedTab = sourcesTabPage;
				sourcesListView.Select(source);
			}
		}

		public void ShowFiltersView()
		{
			using (tracer.NewFrame)
			{
				menuTabControl.SelectedTab = filtersTabPage;
			}
		}

		public void SaveLogSourceAs(ILogSource logSource)
		{
			ISaveAs saveAs = logSource.Provider as ISaveAs;
			if (saveAs == null || !saveAs.IsSavableAs)
				return;
			var dlg = saveFileDialog1;
			dlg.FileName = saveAs.SuggestedFileName ?? "log.txt";
			if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				return;
			try
			{
				saveAs.SaveAs(dlg.FileName);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save file: " + ex.Message);
			}
		}

		#endregion

		private void addDisplayFilterClick(object sender, EventArgs e)
		{
			using (FilterDialog d = new FilterDialog(model.DisplayFiltersListViewHost))
			{
				Filter f = new Filter(FilterAction.Include, string.Format("New filter {0}", ++filterIndex), true, "", false, false, false);
				try
				{
					if (!d.Execute(f))
					{
						return;
					}
					try
					{
						model.DisplayFilters.Insert(0, f);
					}
					catch (TooManyFiltersException)
					{
						MessageBox.Show("Too many filters", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
					f = null;
					UpdateView(false);
				}
				finally
				{
					if (f != null)
					{
						f.Dispose();
					}
				}
			}
		}

		void DeleteFiltersInternal(FiltersListView lv, FiltersList fl)
		{
			List<Filter> toDelete = new List<Filter>();
			foreach (Filter f in lv.SelectedFilters)
			{
				tracer.Info("-- filter to delete: {0}", f.ToString());
				toDelete.Add(f);
			}

			if (toDelete.Count == 0)
			{
				tracer.Info("Nothing to delete");
				return;
			}

			if (MessageBox.Show(
				string.Format("You are about to delete ({0}) filter(s).\nAre you sure?", toDelete.Count),
				this.Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				tracer.Info("User didn't confirm the deletion");
				return;
			}

			fl.Delete(toDelete);
			UpdateView(false);
		}

		private void deleteDisplayFilterButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Delete filters");
				DeleteFiltersInternal(displayFiltersListView, model.DisplayFilters);
			}
		}

		void MoveFilterInternal(FiltersListView lv, FiltersList fl, bool up)
		{
			foreach (Filter f in lv.SelectedFilters)
			{
				if (fl.Move(f, up))
				{
					UpdateView(false);
				}
				break;
			}
		}

		private void moveDisplayFilterUpButton_Click(object sender, EventArgs e)
		{
			MoveFilterInternal(displayFiltersListView, model.DisplayFilters, sender == this.moveDisplayFilterUpButton);
		}

		private void aboutLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Right)
			{
				DoDebugStuff();
				return;
			}
			using (AboutBox aboutBox = new AboutBox())
			{
				aboutBox.ShowDialog();
			}
		}

		IMediaBasedReaderFactory CreateColelibLogFactory()
		{
			var factory = LogProviderFactoryRegistry.DefaultInstance.Find("Skype", "Deobfuscated corelib log");
			return factory as IMediaBasedReaderFactory;
		}

		private void DoDebugStuff()
		{
			var msgs = new LinkedList<MessageBase>();// (200000);
			

			using (LogSourceThreads threads = new LogSourceThreads())
			using (SimpleFileMedia media = new SimpleFileMedia(SimpleFileMedia.CreateConnectionParamsFromFileName(
				@"c:\Work\CALLING-393\2011-09-06T150539-Windows-debug-20110906-1459.log"), new MediaInitParams(null)))
			using (IPositionedMessagesReader reader = CreateColelibLogFactory().CreateMessagesReader(threads, media))
			{
				reader.UpdateAvailableBounds(false);

				using (var parser = reader.CreateParser(new CreateParserParams(
					reader.BeginPosition, null, 
					MessagesParserFlag.HintParserWillBeUsedForMassiveSequentialReading 
					//| MessagesParserFlag.DisableMultithreading 
					| MessagesParserFlag.DisableDejitter
					, 
					MessagesParserDirection.Forward)))
				{
					for (; ; )
					{
						var msg = parser.ReadNext();
						if (msg == null)
							break;
						//msgs.AddLast(msg);
					}
				}
			}

			var distinctBuffers = msgs.Select(m => m.Text.Buffer).Distinct().ToArray();
			var aveBufLength = distinctBuffers.Length > 0 ? distinctBuffers.Average(s => s.Length) : 0;
			var totalTextLen = msgs.Aggregate(0, (i, m) => i + m.Text.Length);

			StringBuilder report = new StringBuilder();
			report.AppendFormat(" total: {0}", msgs.Count);
			report.AppendFormat(" distinctBuffers: {0}", distinctBuffers.Length);
			report.AppendFormat(" aveBufLength: {0}", aveBufLength);
			report.AppendFormat(" totalTextLen: {0}", totalTextLen);

			MessageBox.Show(report.ToString());
		}

		private void trackChangesCheckBox_Click(object sender, EventArgs e)
		{
			bool value = trackChangesCheckBox.CheckState == CheckState.Unchecked;
			foreach (ILogSource s in sourcesListView.SelectedSources)
				s.TrackingEnabled = value;
			UpdateTrackChangesCheckBox();
		}

		private void removeHLButton_Click(object sender, EventArgs e)
		{
			using (tracer.NewFrame)
			{
				tracer.Info("----> User Command: Delete highlight filters");
				DeleteFiltersInternal(hlFiltersListView, model.HighlightFilters);
			}
		}

		private void moveHLFilterUpButton_Click(object sender, EventArgs e)
		{
			MoveFilterInternal(hlFiltersListView, model.HighlightFilters, sender == moveHLFilterUpButton);
		}

		private void addHLFilterButton_Click(object sender, EventArgs e)
		{
			using (FilterDialog d = new FilterDialog(model.HighlightFiltersListViewHost))
			{
				Filter f = new Filter(
					FilterAction.Include, 
					string.Format("New filter {0}", ++filterIndex), 
					true, "", false, false, false);
				try
				{
					if (!d.Execute(f))
					{
						return;
					}
					try
					{
						model.HighlightFilters.Insert(0, f);
					}
					catch (TooManyFiltersException)
					{
						MessageBox.Show("Too many highlighting rules", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
					f = null;
					UpdateView(false);
				}
				finally
				{
					if (f != null)
					{
						f.Dispose();
					}
				}
			}
		}

		private void enableFilteringCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			model.DisplayFilters.FilteringEnabled = enableFilteringCheckBox.Checked;
			UpdateView(false);
		}

		private void enableHighlightingCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			model.HighlightFilters.FilteringEnabled = enableHighlightingCheckBox.Checked;
			UpdateView(false);
		}

		private void searchTextBox_DropDown(object sender, EventArgs e)
		{
			searchTextBox.Items.Clear();
			foreach (string entry in searchHistory.Items)
				searchTextBox.Items.Add(entry);
		}

		private void prevHightlightedButton_Click(object sender, EventArgs e)
		{
			this.viewerPresenter.GoToPrevHighlightedMessage();
		}

		private void nextHightlightedButton_Click(object sender, EventArgs e)
		{
			this.viewerPresenter.GoToNextHighlightedMessage();
		}

		class UrlDragDrop
		{
			public static bool IsUriDataPresent(IDataObject dataObj)
			{
				foreach (string format in Formats)
					if (dataObj.GetDataPresent(format))
						return true;
				return false;
			}

			public static IEnumerable<string> GetURLs(IDataObject dataObj)
			{
				return
					(from rawUrl in GetRawURLs(dataObj)
					 let url = ValidateURL(rawUrl)
					 where url != null
					 select url).Take(1);
			}

			static readonly string[] Formats = new string[] {
				"UniformResourceLocatorW",
				"UniformResourceLocator",
				"text/x-moz-url",
			};

			static string ValidateURL(string url)
			{
				if (url != null)
				{
					url = url.Trim();
					if (url.Length == 0)
						url = null;
				}
				return url;
			}

			static IEnumerable<string> GetRawURLs(IDataObject dataObj)
			{
				foreach (string format in Formats)
				{
					if (dataObj.GetDataPresent(format))
					{
						MemoryStream dataStream = dataObj.GetData(format) as MemoryStream;
						if (dataStream == null)
							continue;
						byte[] data = dataStream.ToArray();
						switch (format)
						{
							case "UniformResourceLocatorW":
								yield return Encoding.Unicode.GetString(data).TrimEnd('\0');
								break;
							case "UniformResourceLocator":
								yield return Encoding.ASCII.GetString(data).TrimEnd('\0');
								break;
							case "text/x-moz-url":
								yield return Encoding.Unicode.GetString(data).Split('\r', '\n').FirstOrDefault();
								break;
						}
					}
				}
			}
		};

		private void MainForm_DragOver(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
				e.Effect = DragDropEffects.All;
			else if (UrlDragDrop.IsUriDataPresent(e.Data))
				e.Effect = DragDropEffects.All;		
		}

		private void MainForm_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
			{
				foreach (var file in (e.Data.GetData(DataFormats.FileDrop) as string[]))
					model.LogSourcesPreprocessings.Preprocess(
						Enumerable.Repeat(new Preprocessing.FormatDetectionStep(file), 1),
						this
					);
			}
			else if (UrlDragDrop.IsUriDataPresent(e.Data))
			{
				model.LogSourcesPreprocessings.Preprocess(
					UrlDragDrop.GetURLs(e.Data).Select(
						url => new Preprocessing.URLTypeDetectionStep(url)), 
					this					
				);
			}
		}

		public System.Net.NetworkCredential QueryCredentials(Uri uri, string authType)
		{
			lock (credentialCacheLock)
			{
				if (credentialCache == null)
					credentialCache = new NetworkCredentialsStorage();
				var cred = credentialCache.GetCredential(uri);
				if (cred != null)
					return cred;
				using (var dlg = new CredentialsDialog())
				{
					if (!dlg.Execute(NetworkCredentialsStorage.StripToPrefix(uri).ToString()))
						return null;
					var ret = new System.Net.NetworkCredential(dlg.UserName, dlg.Password);
					credentialCache.Add(uri, ret);
					credentialCache.StoreSecurely();
					return ret;
				}
			}
		}

		public void InvalidCredentials(Uri site, string authType)
		{
			lock (credentialCacheLock)
			{
				if (credentialCache == null)
					credentialCache = new NetworkCredentialsStorage();
				if (credentialCache.Remove(site))
					credentialCache.StoreSecurely();
			}
		}

		public bool[] SelectItems(string prompt, string[] items)
		{
			using (var dlg = new FilesSelectionDialog())
				return dlg.Execute(prompt, items);
		}
	}

}