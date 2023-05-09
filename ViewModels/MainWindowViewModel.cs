using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        private static MainWindow view => MainWindow.Static; //temp

        public bool EnableDithering { get => enableDithering; set => SetValue(ref enableDithering, value); }
        public bool InstantChanges { get => instantChanges; set => SetValue(ref instantChanges, value); }
        public int LCDWidth { get => lcdWidth; set => SetValue(ref lcdWidth, value); }
        public int LCDHeight { get => lcdHeight; set => SetValue(ref lcdHeight, value); }
        public bool LCDSizePresetPicked { get => lcdSizePresetPicked; set => SetValue(ref lcdSizePresetPicked, value); }
        public bool ShowPreviewGrid { get => showPreviewGrid; set => SetValue(ref showPreviewGrid, value); }

        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand OpenLogsCommand { get; }

        private bool enableDithering = true;
        private bool instantChanges = true;
        private int lcdWidth = 178;
        private int lcdHeight = 178;
        private bool lcdSizePresetPicked = true;
        private bool showPreviewGrid = false;

        public MainWindowViewModel()
        {
            BrowseFilesCommand = new ButtonCommand(ExecuteBrowseFilesCommand);
            ZoomToFitCommand = new ButtonCommand(ExecuteZoomToFitCommand);
            ZoomToFillCommand = new ButtonCommand(ExecuteZoomToFillCommand);
            ResetZoomAndPanCommand = new ButtonCommand(ExecuteResetZoomAndPanCommand);
            ImageTransformCommand = new ButtonCommand(ExecuteImageTransformCommand);
            OpenLogsCommand = new ButtonCommand(ExecuteOpenLogsCommand);
        }

        private void ExecuteBrowseFilesCommand(object? param)
        {
            view.BrowseImageFiles();
        }

        private void ExecuteZoomToFitCommand(object? param)
        {
            view.ZoomToFit_Click();
        }

        private void ExecuteZoomToFillCommand(object? param)
        {
            view.ZoomToFill_Click();
        }

        private void ExecuteResetZoomAndPanCommand(object? param)
        {
            view.ResetPreviewZoomAndPan(true);
        }

        private void ExecuteImageTransformCommand(object? param)
        {
            if (param is RotateFlipType type)
            {
                view.TransformImage(type);
            }
        }

        private void ExecuteOpenLogsCommand(object? param)
        {
            MainWindow.Logging.OpenLogFileAsync();
        }
    }
}
