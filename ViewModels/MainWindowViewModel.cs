using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using RotateFlipType = System.Drawing.RotateFlipType;
using Size = System.Windows.Size;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        public enum LCDSizeEnum
        {
            Custom = 0,
            LCDPanel = 1,
            TextPanel = 2,
            WideLCDPanel = 3,
            WideLCDPanelTall = 4,
        }

        private static MainWindow view => MainWindow.Static; //temp

        public bool EnableDithering { get => enableDithering; set => SetValue(ref enableDithering, value); }
        public bool InstantChanges { get => instantChanges; set => SetValue(ref instantChanges, value); }
        public LCDSizeEnum LCDPresetType { get => lcdPresetType; set => SetValue(ref lcdPresetType, value); }
        public System.Drawing.Size LCDSize
        {
            get => lcdSize;
            set
            {
                bool widthChanged = lcdSize.Width != value.Width;
                bool heightChanged = lcdSize.Height != value.Height;

                SetValue(ref lcdSize, value);

                if (widthChanged)
                    RaisePropertyChanged(nameof(LCDWidth));

                if (heightChanged)
                    RaisePropertyChanged(nameof(LCDHeight));
            }
        }
        public int LCDWidth
        {
            get => LCDSize.Width;
            set
            {
                LCDSize = new System.Drawing.Size(value, LCDHeight);
                LCDPresetType = LCDSizeEnum.Custom;
            }
        }
        public int LCDHeight
        {
            get => LCDSize.Height;
            set
            {
                LCDSize = new System.Drawing.Size(LCDWidth, value);
                LCDPresetType = LCDSizeEnum.Custom;
            }
        }
        public bool LCDSizePresetPicked { get => lcdSizePresetPicked; set => SetValue(ref lcdSizePresetPicked, value); }
        public System.Drawing.Size ImageSplitSize
        {
            get => imageSplitSize;
            set
            {
                bool widthChanged = imageSplitSize.Width != value.Width;
                bool heightChanged = imageSplitSize.Height != value.Height;

                SetValue(ref imageSplitSize, value);

                if (widthChanged)
                    RaisePropertyChanged(nameof(ImageSplitWidth));

                if (heightChanged)
                    RaisePropertyChanged(nameof(ImageSplitHeight));
            }
        }
        public int ImageSplitWidth
        {
            get => ImageSplitSize.Width;
            set
            {
                ImageSplitSize = new System.Drawing.Size(value, ImageSplitHeight);
            }
        }
        public int ImageSplitHeight
        {
            get => ImageSplitSize.Height;
            set
            {
                ImageSplitSize = new System.Drawing.Size(ImageSplitWidth, value);
            }
        }
        public bool ShowPreviewGrid { get => showPreviewGrid; set => SetValue(ref showPreviewGrid, value); }
        public bool IsMouseOverScrollableTextBox { get => isMouseOverScrollableTextBox; set => SetValue(ref isMouseOverScrollableTextBox, value); }

        public ICommand OpenLogsCommand { get; }
        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand SetLCDSizeCommand { get; }
        public ICommand ConvertImageCommand { get; }
        public ICommand CopyImageToClipboardCommand { get; }

        private bool enableDithering = true;
        private bool instantChanges = true;
        private LCDSizeEnum lcdPresetType = LCDSizeEnum.LCDPanel;
        private System.Drawing.Size lcdSize = new System.Drawing.Size(178, 178);
        private bool lcdSizePresetPicked = true;
        private bool showPreviewGrid = false;
        private System.Drawing.Size imageSplitSize = new System.Drawing.Size(1, 1);
        private bool isMouseOverScrollableTextBox = false;

        public MainWindowViewModel()
        {
            OpenLogsCommand = new ButtonCommand(ExecuteOpenLogsCommand);
            BrowseFilesCommand = new ButtonCommand(ExecuteBrowseFilesCommand);
            ZoomToFitCommand = new ButtonCommand(ExecuteZoomToFitCommand);
            ZoomToFillCommand = new ButtonCommand(ExecuteZoomToFillCommand);
            ResetZoomAndPanCommand = new ButtonCommand(ExecuteResetZoomAndPanCommand);
            ImageTransformCommand = new ButtonCommand(ExecuteImageTransformCommand);
            SetLCDSizeCommand = new ButtonCommand(ExecuteSetLCDSizeCommand);
            ConvertImageCommand = new ButtonCommand(ExecuteConvertImageCommand);
            CopyImageToClipboardCommand = new ButtonCommand(ExecuteCopyImageToClipboardCommand);
        }

        private void ExecuteOpenLogsCommand(object? param)
        {
            MainWindow.Logging.OpenLogFileAsync();
        }

        private void ExecuteBrowseFilesCommand(object? param)
        {
            view.BrowseImageFiles();
        }

        private void ExecuteZoomToFitCommand(object? param)
        {
            view.ZoomToFit();
        }

        private void ExecuteZoomToFillCommand(object? param)
        {
            view.ZoomToFill();
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

        private void ExecuteSetLCDSizeCommand(object? param)
        {
            if (param is int[] size)
            {
                LCDSize = new System.Drawing.Size(size[0], size[1]);

                if (LCDSize.Width == 178 && LCDSize.Height == 178)
                    LCDPresetType = LCDSizeEnum.LCDPanel;
                else if (LCDSize.Width == 178 && LCDSize.Height == 107)
                    LCDPresetType = LCDSizeEnum.TextPanel;
                else if (LCDSize.Width == 356 && LCDSize.Height == 178)
                    LCDPresetType = LCDSizeEnum.WideLCDPanel;
                else if (LCDSize.Width == 178 && LCDSize.Height == 356)
                    LCDPresetType = LCDSizeEnum.WideLCDPanelTall;
            }
        }

        private void ExecuteConvertImageCommand(object? param)
        {
            view.OnConvertClicked(param);
        }

        private void ExecuteCopyImageToClipboardCommand(object? param)
        {
            view.CopyToClipClicked(param);
        }
    }
}
