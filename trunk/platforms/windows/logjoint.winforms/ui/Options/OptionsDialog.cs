﻿using LogJoint.UI.Presenters.Options.Dialog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LogJoint.UI
{
	public partial class OptionsDialog : Form, IDialog
	{
		IPresenterEvents presenter;

		public OptionsDialog(IPresenterEvents presenter)
		{
			this.presenter = presenter;
			InitializeComponent();
		}

		void IDialog.Show()
		{
			this.ShowDialog();
		}

		void IDialog.Hide()
		{
			DialogResult = DialogResult.OK;
		}

		Presenters.Options.MemAndPerformancePage.IView IDialog.MemAndPerformancePage
		{
			get { return memAndPerformanceSettingsView; }
		}

		Presenters.Options.Appearance.IView IDialog.ApperancePage
		{
			get { return appearanceSettingsView1; }
		}

		Presenters.Options.UpdatesAndFeedback.IView IDialog.UpdatesAndFeedbackPage
		{
			get { return updatesAndFeedbackView1; }
		}

		void IDialog.SetUpdatesAndFeedbackPageVisibility(bool value)
		{
			if (value == (tabControl1.TabPages.IndexOf(updatesAndFeedbackTabPage) >= 0))
				return;
			if (!value)
				tabControl1.TabPages.Remove(updatesAndFeedbackTabPage);
			else
				tabControl1.TabPages.Add(updatesAndFeedbackTabPage);
		}

		void IDisposable.Dispose()
		{
			base.Dispose();
		}

		private void okButton_Click(object sender, EventArgs e)
		{
			presenter.OnOkPressed();
		}

		private void cancelButton_Click(object sender, EventArgs e)
		{
			presenter.OnCancelPressed();
		}
	}

	public class OptionsDialogView : IView
	{
		IPresenterEvents presenter;

		void IView.SetPresenter(IPresenterEvents presenter)
		{
			this.presenter = presenter;
		}

		IDialog IView.CreateDialog()
		{
			return new OptionsDialog(presenter);
		}
	};
}
