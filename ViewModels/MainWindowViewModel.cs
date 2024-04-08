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
using CommunityToolkit.Mvvm.Input;

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

        private static MainWindow _view => MainWindow.Static; //temp

        public bool EnableDithering
        {
            get => _enableDithering;
            set
            {
                if (SetValue(ref _enableDithering, value))
                {
                    ConvertManager.Instance.EnableDithering = value;
                }
            }
        }
        public BitDepth ColorDepth
        {
            get => _colorDepth;
            set
            {
                if (SetValue(ref _colorDepth, value))
                {
                    ConvertManager.Instance.BitDepth = value;
                }
            }
        }
        public InterpolationMode InterpolationMode
        {
            get => _interpolationMode;
            set
            {
                if (SetValue(ref _interpolationMode, value))
                {
                    ConvertManager.Instance.Interpolation = value;
                }
            }
        }
        public LCDPresetType SelectedLCD { get => _selectedLCD; set => SetValue(ref _selectedLCD, value); }
        public int LCDWidth
        {
            get => _lcdWidth;
            set
            {
                if (SetValue(ref _lcdWidth, value))
                {
                    ConvertManager.Instance.ConvertedSize = new Int32Size(value, LCDHeight);
                    _view.LCDSizeChanged(this, value, LCDHeight);
                }
            }
        }
        public int LCDHeight
        {
            get => _lcdHeight;
            set
            {
                if (SetValue(ref _lcdHeight, value))
                {
                    ConvertManager.Instance.ConvertedSize = new Int32Size(LCDWidth, value);
                    _view.LCDSizeChanged(this, LCDWidth, value);
                }
            }
        }
        public Int32Size ImageSplitSize
        {
            get => _imageSplitSize;
            set
            {
                bool widthChanged = _imageSplitSize.Width != value.Width;
                bool heightChanged = _imageSplitSize.Height != value.Height;

                if (SetValue(ref _imageSplitSize, value))
                {
                    ConvertManager.Instance.ImageSplitSize = value;
                    _view.ImageSplitSizeChanged(this, value);
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
        public bool ShowPreviewGrid { get => _showPreviewGrid; set => SetValue(ref _showPreviewGrid, value); }
        public bool IsMouseOverScrollableTextBox { get => _isMouseOverScrollableTextBox; set => SetValue(ref _isMouseOverScrollableTextBox, value); }
        public ImageSource PreviewImageSource
        {
            get => _previewImageSource;
            set
            {
                if (SetValue(ref _previewImageSource, value))
                {
                    RaisePropertyChanged(nameof(PreviewImageLoaded));
                }
            }
        }
        public bool PreviewImageLoaded => _previewImageSource != null;
        public double PreviewScale
        {
            set
            {
                if (SetValue(ref _previewScale, value))
                {
                    ConvertManager.Instance.Scale = value;
                }
            }
        }
        public System.Windows.Point PreviewOffsetRatio
        {
            set
            {
                if (SetValue(ref _previewOffset, value))
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

        private bool _enableDithering;
        private BitDepth _colorDepth;
        private InterpolationMode _interpolationMode;
        private LCDPresetType _selectedLCD;
        private int _lcdWidth;
        private int _lcdHeight;
        private bool _showPreviewGrid;
        private Int32Size _imageSplitSize;
        private bool _isMouseOverScrollableTextBox;
        private ImageSource _previewImageSource;
        private double _previewScale;
        private System.Windows.Point _previewOffset;

        public MainWindowViewModel()
        {
            BrowseFilesCommand = new RelayCommand(ExecuteBrowseFiles);
            ZoomToFitCommand = new RelayCommand(ExecuteZoomToFit);
            ZoomToFillCommand = new RelayCommand(ExecuteZoomToFill);
            ResetZoomAndPanCommand = new RelayCommand(ExecuteResetZoomAndPan);
            ImageTransformCommand = new RelayCommand<RotateFlipType>(ExecuteImageTransform, i => i is not RotateFlipType.RotateNoneFlipNone);
            CopyImageToClipboardCommand = new RelayCommand<object>(ExecuteCopyImageToClipboard);
            ConvertFromClipboardCommand = new RelayCommand(ExecuteConvertFromClipboard);

            _colorDepth = BitDepth.Color3;
            _enableDithering = true;
            _interpolationMode = InterpolationMode.HighQualityBicubic;
            _selectedLCD = LCDPresetType.LCDPanel;
            _lcdWidth = 178;
            _lcdHeight = 178;
            _showPreviewGrid = false;
            _imageSplitSize = new Int32Size(1, 1);
            _isMouseOverScrollableTextBox = false;

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

        private void ExecuteBrowseFiles()
        {
            _view.BrowseImageFiles();
        }

        private void ExecuteZoomToFit()
        {
            _view.ZoomToFit();
        }

        private void ExecuteZoomToFill()
        {
            _view.ZoomToFill();
        }

        private void ExecuteResetZoomAndPan()
        {
            _view.ResetZoomAndPan(true);
        }

        private void ExecuteImageTransform(RotateFlipType type)
        {
            _view.TransformImage(type);
        }

        private void ExecuteCopyImageToClipboard(object? param)
        {
            _view.CopyToClipClicked(param);
        }

        private void ExecuteConvertFromClipboard()
        {
            _view.PasteFromClipboard();
        }
    }
}
