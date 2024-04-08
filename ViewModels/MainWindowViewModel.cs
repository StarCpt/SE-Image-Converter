using ImageConverterPlus.Base;
using ImageConverterPlus.ImageConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using RotateFlipType = System.Drawing.RotateFlipType;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        public enum LCDPresetType
        {
            Custom = 0,
            LCDPanel = 1,
            TextPanel = 2,
            WideLCDPanel = 3,
            WideLCDPanelTall = 4,
        }

        private static MainWindow view => MainWindow.Static; //temp

        public bool EnableDithering
        {
            get => enableDithering;
            set
            {
                if (SetValue(ref enableDithering, value))
                {
                    ConvertManager.Instance.EnableDithering = value;
                }
            }
        }
        public BitDepth ColorDepth
        {
            get => colorDepth;
            set
            {
                if (SetValue(ref colorDepth, value))
                {
                    ConvertManager.Instance.BitDepth = value;
                }
            }
        }
        public InterpolationMode InterpolationMode
        {
            get => interpolationMode;
            set
            {
                if (SetValue(ref interpolationMode, value))
                {
                    ConvertManager.Instance.Interpolation = value;
                }
            }
        }
        public LCDPresetType SelectedLCD { get => selectedLCD; set => SetValue(ref selectedLCD, value); }
        public int LCDWidth
        {
            get => lcdWidth;
            set
            {
                if (SetValue(ref lcdWidth, value))
                {
                    ConvertManager.Instance.ConvertedSize = new Int32Size(value, LCDHeight);
                    view.LCDSizeChanged(this, value, LCDHeight);
                }
            }
        }
        public int LCDHeight
        {
            get => lcdHeight;
            set
            {
                if (SetValue(ref lcdHeight, value))
                {
                    ConvertManager.Instance.ConvertedSize = new Int32Size(LCDWidth, value);
                    view.LCDSizeChanged(this, LCDWidth, value);
                }
            }
        }
        public Int32Size ImageSplitSize
        {
            get => imageSplitSize;
            set
            {
                bool widthChanged = imageSplitSize.Width != value.Width;
                bool heightChanged = imageSplitSize.Height != value.Height;

                if (SetValue(ref imageSplitSize, value))
                {
                    ConvertManager.Instance.ImageSplitSize = value;
                    view.ImageSplitSizeChanged(this, value);
                }

                if (widthChanged)
                    RaisePropertyChanged(nameof(ImageSplitWidth));

                if (heightChanged)
                    RaisePropertyChanged(nameof(ImageSplitHeight));
            }
        }
        public int ImageSplitWidth
        {
            get => ImageSplitSize.Width;
            set => ImageSplitSize = new Int32Size(value, ImageSplitHeight);
        }
        public int ImageSplitHeight
        {
            get => ImageSplitSize.Height;
            set => ImageSplitSize = new Int32Size(ImageSplitWidth, value);
        }
        public bool ShowPreviewGrid { get => showPreviewGrid; set => SetValue(ref showPreviewGrid, value); }
        public bool IsMouseOverScrollableTextBox { get => isMouseOverScrollableTextBox; set => SetValue(ref isMouseOverScrollableTextBox, value); }
        public ImageSource PreviewImageSource
        {
            get => previewImageSource;
            set
            {
                if (SetValue(ref previewImageSource, value))
                {
                    RaisePropertyChanged(nameof(PreviewImageLoaded));
                }
            }
        }
        public bool PreviewImageLoaded => previewImageSource != null;
        public double PreviewScale
        {
            set
            {
                if (SetValue(ref previewScale, value))
                {
                    ConvertManager.Instance.Scale = value;
                }
            }
        }
        public System.Windows.Point PreviewOffsetRatio
        {
            set
            {
                if (SetValue(ref previewOffset, value))
                {
                    ConvertManager.Instance.TopLeftRatio = value;
                }
            }
        }
        public bool Debug
        {
            get => App.Instance.Debug;
            set => App.Instance.Debug = value;
        }

        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand CopyImageToClipboardCommand { get; }
        public ICommand ConvertFromClipboardCommand { get; }

        private bool enableDithering;
        private BitDepth colorDepth;
        private InterpolationMode interpolationMode;
        private LCDPresetType selectedLCD;
        private int lcdWidth;
        private int lcdHeight;
        private bool showPreviewGrid;
        private Int32Size imageSplitSize;
        private bool isMouseOverScrollableTextBox;
        private ImageSource previewImageSource;
        private double previewScale;
        private System.Windows.Point previewOffset;

        public MainWindowViewModel()
        {
            BrowseFilesCommand = new ButtonCommand(ExecuteBrowseFilesCommand);
            ZoomToFitCommand = new ButtonCommand(ExecuteZoomToFitCommand);
            ZoomToFillCommand = new ButtonCommand(ExecuteZoomToFillCommand);
            ResetZoomAndPanCommand = new ButtonCommand(ExecuteResetZoomAndPanCommand);
            ImageTransformCommand = new ButtonCommand(ExecuteImageTransformCommand);
            CopyImageToClipboardCommand = new ButtonCommand(ExecuteCopyImageToClipboardCommand);
            ConvertFromClipboardCommand = new ButtonCommand(ExecuteConvertFromClipboardCommand);

            colorDepth = BitDepth.Color3;
            enableDithering = true;
            interpolationMode = InterpolationMode.HighQualityBicubic;
            selectedLCD = LCDPresetType.LCDPanel;
            lcdWidth = 178;
            lcdHeight = 178;
            showPreviewGrid = false;
            imageSplitSize = new Int32Size(1, 1);
            isMouseOverScrollableTextBox = false;

            ConvertManager.Instance.PropertyChanged += ConvertManager_PropertyChanged;
            App.DebugStateChanged += (sender, e) => RaisePropertyChanged(nameof(Debug));
        }

        private void ConvertManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ConvertManager mgr)
                return;

            switch (e.PropertyName)
            {
                case nameof(ConvertManager.BitDepth):
                    this.ColorDepth = mgr.BitDepth;
                    break;
                case nameof(ConvertManager.EnableDithering):
                    this.EnableDithering = mgr.EnableDithering;
                    break;
                case nameof(ConvertManager.Interpolation):
                    this.InterpolationMode = mgr.Interpolation;
                    break;
                case nameof(ConvertManager.ConvertedSize):
                    this.LCDWidth = mgr.ConvertedSize.Width;
                    this.LCDHeight = mgr.ConvertedSize.Height;
                    break;
                case nameof(ConvertManager.ImageSplitSize):
                    this.ImageSplitSize = mgr.ImageSplitSize;
                    break;
                case nameof(ConvertManager.ProcessedImageFull):
                    if (mgr.ProcessedImageFull != null)
                    {
                        this.PreviewImageSource = mgr.ProcessedImageFull;
                    }
                    break;
            }
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
            view.ResetZoomAndPan(true);
        }

        private void ExecuteImageTransformCommand(object? param)
        {
            if (param is RotateFlipType type)
            {
                view.TransformImage(type);
            }
        }

        private void ExecuteCopyImageToClipboardCommand(object? param)
        {
            view.CopyToClipClicked(param);
        }

        private void ExecuteConvertFromClipboardCommand(object? param)
        {
            view.PasteFromClipboard();
        }
    }
}
