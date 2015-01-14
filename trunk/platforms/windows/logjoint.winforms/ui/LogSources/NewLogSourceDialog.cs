using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using LogJoint.UI.Presenters.NewLogSourceDialog;

namespace LogJoint.UI
{
	public partial class NewLogSourceDialog : Form, IDialog
	{
		LogTypeEntry current;
		IRecentlyUsedLogs mru;
		Preprocessing.IPreprocessingUserRequests userRequests;
		IModel model;
		Presenters.Help.IPresenter help;
		Preprocessing.ILogSourcesPreprocessingManager preprocessingManager;

		abstract class LogTypeEntry: IDisposable
		{
			public ILogProviderFactoryUI UI;

			public abstract object GetIdentityObject();
			public abstract string GetDescription();
			public abstract ILogProviderFactoryUI CreateUI(IModel model);

			public void Dispose()
			{
				if (UI != null)
					UI.Dispose();
			}
		};

		class FixedLogTypeEntry : LogTypeEntry
		{
			public override string ToString() { return LogProviderFactoryRegistry.ToString(Factory); }

			public override object GetIdentityObject() { return Factory; }

			public override string GetDescription() { return Factory.FormatDescription; }

			public override ILogProviderFactoryUI CreateUI(IModel model) { return Factory.CreateUI(new UIFactory(), model); }

			public ILogProviderFactory Factory;
		};

		class AutodetectedLogTypeEntry : LogTypeEntry
		{
			public Preprocessing.ILogSourcesPreprocessingManager preprocessingManager;
			public Preprocessing.IPreprocessingUserRequests userRequests;

			public override string ToString() { return name; }

			public override object GetIdentityObject() { return name; }

			public override string GetDescription() { return "Pick a file or URL and LogJoint will detect log format by trying all known formats"; }

			public override ILogProviderFactoryUI CreateUI(IModel model) { return new AnyLogFormatUI(preprocessingManager, userRequests); }

			private static string name = "Any known log format";
		};

		public NewLogSourceDialog(IModel model, Preprocessing.IPreprocessingUserRequests userRequests, Presenters.Help.IPresenter help)
		{
			InitializeComponent();

			this.model = model;
			this.mru = model.MRU;
			this.preprocessingManager = model.LogSourcesPreprocessings;
			this.userRequests = userRequests;
			this.help = help;

			formatNameLabel.Text = "";
		}

		void IDialog.Show()
		{
			Execute();
		}

		void UpdateList()
		{
			logTypeListBox.BeginUpdate();
			try
			{
				object oldSelection = current != null ? current.GetIdentityObject() : null;
				SetCurrent(null);

				logTypeListBox.Items.Clear();
				logTypeListBox.Items.Add(new AutodetectedLogTypeEntry() { preprocessingManager = this.preprocessingManager, userRequests = this.userRequests });
				foreach (ILogProviderFactory fact in mru.SortFactoriesMoreRecentFirst(model.LogProviderFactoryRegistry.Items))
				{
					FixedLogTypeEntry entry = new FixedLogTypeEntry();
					entry.Factory = fact;
					logTypeListBox.Items.Add(entry);
				}

				int newSelectedIdx = 0;
				if (oldSelection != null)
				{
					for (int i = 0; i < logTypeListBox.Items.Count; ++i)
					{
						if (Get(i).GetIdentityObject() == oldSelection)
						{
							newSelectedIdx = i;
							break;
						}
					}
				}

				if (newSelectedIdx < logTypeListBox.Items.Count)
				{
					logTypeListBox.SelectedIndex = newSelectedIdx;
				}
			}
			finally
			{
				logTypeListBox.EndUpdate();
			}
		}

		public void Execute()
		{
			UpdateList();
			ShowDialog();
		}

		LogTypeEntry Get(int idx)
		{
			return (LogTypeEntry)logTypeListBox.Items[idx];
		}

		LogTypeEntry GetSelected()
		{
			if (logTypeListBox.SelectedIndex >= 0)
				return Get(logTypeListBox.SelectedIndex);
			return null;
		}

		void SetCurrent(LogTypeEntry entry)
		{
			LogTypeEntry tmp = entry;

			if (tmp == current)
				return;

			if (current != null)
			{
				if (current.UI != null)
					(current.UI.UIControl as Control).Visible = false;
			}
			current = tmp;
			if (current != null)
			{
				this.formatNameLabel.Text = current.ToString();
				this.formatDescriptionLabel.Text = current.GetDescription();
				ILogProviderFactoryUI ui = current.UI;
				if (current.UI == null)
				{
					ui = current.UI = current.CreateUI(model);
				}
				if (current.UI != null)
				{
					Control ctrl = ui.UIControl as Control;
					ctrl.Parent = this.hostPanel;
					ctrl.Dock = DockStyle.Fill;
					ctrl.Visible = true;
				}
			}
		}

		private void logTypeListBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			LogTypeEntry tmp = GetSelected();
			SetCurrent(tmp);
		}

		bool Apply()
		{
			// todo: handle errors
			if (current.UI != null)
				current.UI.Apply(model);
			return true;
		}

		private void okButton_Click(object sender, EventArgs e)
		{
			if (Apply())
				this.DialogResult = DialogResult.OK;
		}

		private void applyButton_Click(object sender, EventArgs e)
		{
			Apply();
		}

		private void manageFormatsButton_Click(object sender, EventArgs e)
		{
			using (ManageFormatsWizard w = new ManageFormatsWizard(model, help))
			{
				w.ExecuteWizard();
			}
			if (model.UserDefinedFormatsManager.ReloadFactories() > 0)
			{
				UpdateList();
			}
		}
	}

	public class NewLogSourceDialogView : IView
	{
		IModel model;
		Preprocessing.IPreprocessingUserRequests userRequests;
		Presenters.Help.IPresenter helpPresenters;

		public NewLogSourceDialogView(IModel model, Preprocessing.IPreprocessingUserRequests userRequests, Presenters.Help.IPresenter helpPresenters)
		{
			this.model = model;
			this.userRequests = userRequests;
			this.helpPresenters = helpPresenters;
		}

		IDialog IView.CreateDialog()
		{
			return new NewLogSourceDialog(model, userRequests, helpPresenters);
		}
	};
}