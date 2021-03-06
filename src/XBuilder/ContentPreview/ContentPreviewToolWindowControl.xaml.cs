﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Xna.Framework.Content;
using XBuilder.ContentPreview.Rendering;
using XBuilder.Vsx;
using XBuilder.Xna;
using XBuilder.Xna.Building;

namespace XBuilder.ContentPreview
{
    public partial class ContentPreviewToolWindowControl : UserControl
    {
    	private ContentBuilder _contentBuilder;
		private ContentManager _contentManager;
    	private XBuilderPackage _package;

    	private bool _loaded;
    	private AssetHandlers _assetHandlers;

        public ContentPreviewToolWindowControl()
        {
        	InitializeComponent();

        	IVsUIShell2 shell = (IVsUIShell2) Package.GetGlobalService(typeof(SVsUIShell));
        	uint vsBackColor;
        	shell.GetVSSysColorEx((int) __VSSYSCOLOREX.VSCOLOR_TOOLWINDOW_BACKGROUND,
        		out vsBackColor);
        	System.Drawing.Color backColor = System.Drawing.ColorTranslator.FromWin32((int) vsBackColor);
			graphicsDeviceControl.BackColor = backColor;
        }

		public void Initialize(XBuilderPackage package)
		{
			_package = package;
			graphicsDeviceControl.Initialize(_package);
			_contentBuilder = new ContentBuilder(package);
			_contentManager = new ContentManager(graphicsDeviceControl.Services, _contentBuilder.OutputDirectory);

			Loaded += (sender, e) =>
			{
				if (!_loaded) // Can't find a WPF event which only fires once when the window is FIRST loaded.
				{
					_assetHandlers = new AssetHandlers(_contentManager, graphicsDeviceControl);
					windowsFormsHost.Visibility = Visibility.Collapsed;
					_loaded = true;

					_assetHandlers.Initialize(package);
				}
			};
		}

		/// <summary>
		/// Loads a new XNA asset file into the ModelViewerControl.
		/// </summary>
		public AssetType LoadFile(string fileName, XnaBuildProperties buildProperties)
		{
			if (!_loaded)
				return AssetType.None;

			// Unload any existing content.
			graphicsDeviceControl.AssetRenderer = null;
			AssetHandler assetHandler = _assetHandlers.GetAssetHandler(fileName);
			assetHandler.ResetRenderer();

			windowsFormsHost.Visibility = Visibility.Collapsed;
			txtInfo.Text = "Loading...";
			txtInfo.Visibility = Visibility.Visible;

			// Load asynchronously.
			var ui = TaskScheduler.FromCurrentSynchronizationContext();
			Task<string> loadTask = new Task<string>(() =>
			{
				_contentManager.Unload();

				// Tell the ContentBuilder what to build.
				_contentBuilder.Clear();
				_contentBuilder.SetReferences(buildProperties.ProjectReferences);

				string assetName = fileName;
				foreach (char c in Path.GetInvalidFileNameChars())
					assetName = assetName.Replace(c.ToString(), string.Empty);
				assetName = Path.GetFileNameWithoutExtension(assetName);
				_contentBuilder.Add(fileName, assetName, buildProperties.Importer,
					buildProperties.Processor ?? assetHandler.ProcessorName,
					buildProperties.ProcessorParameters);

				// Build this new model data.
				string buildErrorInternal = _contentBuilder.Build();

				if (string.IsNullOrEmpty(buildErrorInternal))
				{
					// If the build succeeded, use the ContentManager to
					// load the temporary .xnb file that we just created.
					assetHandler.LoadContent(assetName);

					graphicsDeviceControl.AssetRenderer = assetHandler.Renderer;
				}

				return buildErrorInternal;
			});

			loadTask.ContinueWith(t =>
			{
				string buildError = t.Result;
				if (!string.IsNullOrEmpty(buildError))
				{
					// If the build failed, display an error message.
					txtInfo.Text = "Uh-oh. Something went wrong. Check the Output window for details.";
					XBuilderWindowPane.WriteLine(buildError);
				}
				else
				{
					windowsFormsHost.Visibility = Visibility.Visible;
					txtInfo.Visibility = Visibility.Hidden;
				}
			}, ui);

			loadTask.Start();

			return _assetHandlers.GetAssetType(fileName);
		}

    	public bool IsModelLoaded
    	{
			get { return graphicsDeviceControl.AssetRenderer != null && graphicsDeviceControl.AssetRenderer is ModelRenderer; }
    	}

    	public void ChangeFillMode(ShadingMode wireframe)
    	{
    		graphicsDeviceControl.ChangeFillMode(wireframe);
    	}

		public void ChangePrimitive(Primitive primitive)
		{
			graphicsDeviceControl.ChangePrimitive(primitive);
		}

		public void ShowNormals(bool show)
		{
			graphicsDeviceControl.ShowNormals(show);
		}

		public void ShowBoundingBox(bool show)
		{
			graphicsDeviceControl.ShowBoundingBox(show);
		}

		public void ToggleAlphaBlend(bool show)
		{
			graphicsDeviceControl.ToggleAlphaBlend(show);
		}

        public void ToggleTextureSize(bool show)
        {
            graphicsDeviceControl.ToggleTextureSize(show);
        }

		protected override void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
		{
			graphicsDeviceControl.RaiseMouseWheel(e);
		}
    }
}