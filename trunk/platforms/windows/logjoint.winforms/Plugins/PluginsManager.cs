﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using LogJoint.UI;

namespace LogJoint
{
	class PluginsManager: IDisposable
	{
		readonly List<PluginBase> plugins = new List<PluginBase>();
		readonly LJTraceSource tracer;
		readonly ILogJointApplication entryPoint;
		readonly TabControl menuTabControl;

		public PluginsManager(
			LJTraceSource tracer, 
			ILogJointApplication entryPoint,
			TabControl menuTabControl,
			UI.Presenters.MainForm.IPresenter mainFormPresenter)
		{
			this.tracer = tracer;
			this.entryPoint = entryPoint;
			this.menuTabControl = menuTabControl;
			
			InitPlugins();
			LoadTabExtensions();

			menuTabControl.Selected += (s, e) =>
			{
				var t = menuTabControl.SelectedTab;
				if (t == null)
					return;
				var ext = t.Tag as IMainFormTabExtension;
				if (ext == null)
					return;
				ext.OnTabPageSelected();
			};
			mainFormPresenter.Closing += (s, e) => Dispose();
		}

		private void InitPlugins()
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
						plugin.Init(entryPoint);
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
				foreach (IMainFormTabExtension ext in plugin.MainFormTabExtensions)
				{
					TabPage tab = new TabPage(ext.Caption) { Tag = ext };
					menuTabControl.TabPages.Add(tab);
					tab.Controls.Add(ext.PageControl);
				}
			}
		}

		public void Dispose()
		{
			foreach (PluginBase plugin in plugins)
			{
				plugin.Dispose();
			}
			plugins.Clear();
		}
	}
}
