using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogJoint.Settings;
using ColoringMode = LogJoint.Settings.Appearance.ColoringMode;
using LogFontSize = LogJoint.Settings.Appearance.LogFontSize;

namespace LogJoint.UI.Presenters.Options.Appearance
{
	public class Presenter : IPresenter, IViewEvents
	{
		public Presenter(
			IModel model,
			IView view)
		{
			this.model = model;
			this.view = view;
			this.settingsAccessor = model.GlobalSettings;

			this.sampleMessagesCollection = new MessagesContainers.RangesManagingCollection();
			this.sampleMessagesBaseTime = DateTime.UtcNow;
			this.colorTable = new AdjustingColorsGenerator(new PastelColorsGenerator(), PaletteBrightness.Normal);
			this.sampleThreads = new ModelThreads(colorTable);
			this.sampleLogViewerPresenter = new LogViewer.Presenter(
				new LogViewer.DummyModel(threads: sampleThreads, messages: sampleMessagesCollection), 
				view.PreviewLogView,
				null);
			this.sampleLogViewerPresenter.ShowTime = false;
			this.sampleLogViewerPresenter.ShowRawMessages = false;
			this.sampleLogViewerPresenter.DisabledUserInteractions =
				LogViewer.UserInteraction.FontResizing | 
				LogViewer.UserInteraction.RawViewSwitching | 
				LogViewer.UserInteraction.FramesNavigationMenu |
				LogViewer.UserInteraction.CopyMenu;

			this.viewFonts = view.PreviewLogView;

			view.SetPresenter(this);

			InitView();

			UpdateSampleLogView(fullUpdate: true);
		}

		bool IPresenter.Apply()
		{
			settingsAccessor.Appearance = new Settings.Appearance()
			{
				Coloring = ReadColoringModeControl(),
				FontFamily = ReadFontNameControl(),
				FontSize = ReadFontSizeControl(),
				ColoringBrightness = ReadColoringPaletteControl()
			};
			return true;
		}

		void IViewEvents.OnSelectedValueChanged(ViewControl ctrl)
		{
			UpdateSampleLogView(fullUpdate: ctrl == ViewControl.PaletteSelector);
		}

		void IViewEvents.OnFontSizeValueChanged()
		{
			UpdateSampleLogView(fullUpdate: false);
		}

		#region Implementation

		void InitView()
		{
			var appearance = settingsAccessor.Appearance;

			view.SetSelectorControl(ViewControl.ColoringSelector, coloringModes, (int)appearance.Coloring);

			view.SetSelectorControl(ViewControl.FontFamilySelector, viewFonts.AvailablePreferredFamilies,
				viewFonts.AvailablePreferredFamilies.IndexOf(f => string.Compare(f, appearance.FontFamily ?? "", true) == 0).GetValueOrDefault(0));

			view.SetFontSizeControl(
				viewFonts.FontSizes
					.Select(p => p.Value)
					.ToArray(),
				viewFonts.FontSizes
					.Where(p => p.Key == appearance.FontSize)
					.Select(p => p.Value)
					.FirstOrDefault(viewFonts.FontSizes[0].Value));

			view.SetSelectorControl(ViewControl.PaletteSelector, coloringPalettes, (int)appearance.ColoringBrightness);
		}

		void FillSampleMessagesCollection()
		{
			foreach (var t in sampleThreads.Items.ToArray())
				t.Dispose();

			sampleMessagesCollection.Clear();
			sampleMessagesCollection.SetActiveRange(0, 10);
			using (var range = sampleMessagesCollection.GetNextRangeToFill())
			{
				DateTime baseTime = sampleMessagesBaseTime;
				var t1 = sampleThreads.RegisterThread("thread1", null);
				var t2 = sampleThreads.RegisterThread("thread2", null);
				var t3 = sampleThreads.RegisterThread("thread3", null);
				range.Add(new Content(0, t1, new MessageTimestamp(baseTime.AddSeconds(0)), new StringSlice("sample message 0"), SeverityFlag.Info), false);
				range.Add(new Content(1, t2, new MessageTimestamp(baseTime.AddSeconds(1)), new StringSlice("sample message 1"), SeverityFlag.Info), false);
				range.Add(new Content(2, t1, new MessageTimestamp(baseTime.AddSeconds(2)), new StringSlice("warning: sample message 2"), SeverityFlag.Warning), false);
				range.Add(new Content(3, t3, new MessageTimestamp(baseTime.AddSeconds(3)), new StringSlice("sample message 3"), SeverityFlag.Info), false);
				range.Add(new Content(4, t2, new MessageTimestamp(baseTime.AddSeconds(4)), new StringSlice("error: sample message 4"), SeverityFlag.Error), false);
				range.Add(new Content(5, t1, new MessageTimestamp(baseTime.AddSeconds(5)), new StringSlice("sample message 5"), SeverityFlag.Info), false);
				range.Complete();
			}
		}

		void UpdateSampleLogView(bool fullUpdate)
		{
			sampleLogViewerPresenter.FontName = ReadFontNameControl();
			sampleLogViewerPresenter.FontSize = ReadFontSizeControl();
			sampleLogViewerPresenter.Coloring = ReadColoringModeControl();
			if (fullUpdate)
			{
				colorTable.Brightness = ReadColoringPaletteControl();
				FillSampleMessagesCollection();
				sampleLogViewerPresenter.UpdateView();
			}
		}

		LogFontSize ReadFontSizeControl()
		{
			return viewFonts.FontSizes
				.Where(p => p.Value == view.GetFontSizeControlValue())
				.Select(p => p.Key)
				.FirstOrDefault(LogFontSize.Normal);
		}

		string ReadFontNameControl()
		{
			int selectedFont = view.GetSelectedValue(ViewControl.FontFamilySelector);
			var availableFonts = viewFonts.AvailablePreferredFamilies;
			return (selectedFont >= 0 && selectedFont < availableFonts.Length) ? availableFonts[selectedFont] : null;
		}

		ColoringMode ReadColoringModeControl()
		{
			return (ColoringMode)view.GetSelectedValue(ViewControl.ColoringSelector);
		}

		PaletteBrightness ReadColoringPaletteControl()
		{
			return (PaletteBrightness)view.GetSelectedValue(ViewControl.PaletteSelector);
		}

		readonly string[] coloringModes = new[] 
		{
			"White backgound",
			"Background color represents message thread",
			"Background color represents message log source",
		};
		readonly string[] coloringPalettes = new[] 
		{
			"Dark",
			"Normal",
			"Bright",
		};

		readonly IModel model;
		readonly IView view;
		readonly IGlobalSettingsAccessor settingsAccessor;
		readonly LogViewer.IViewFonts viewFonts;
		readonly LogViewer.IPresenter sampleLogViewerPresenter;
		readonly IAdjustingColorsGenerator colorTable;
		readonly IModelThreads sampleThreads;
		readonly MessagesContainers.RangesManagingCollection sampleMessagesCollection;
		readonly DateTime sampleMessagesBaseTime;

		#endregion

	};
};