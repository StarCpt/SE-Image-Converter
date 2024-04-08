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
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive.Linq;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
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
            get => ConvertManager.Instance.EnableDithering;
            set => ConvertManager.Instance.EnableDithering = value;
        }
        public BitDepth ColorDepth
        {
            get => ConvertManager.Instance.BitDepth;
            set => ConvertManager.Instance.BitDepth = value;
        }
        public InterpolationMode InterpolationMode
        {
            get => ConvertManager.Instance.Interpolation;
            set => ConvertManager.Instance.Interpolation = value;
        }
        [Reactive]
        public LCDPresetType SelectedLCD { get; set; } = LCDPresetType.LCDPanel;
        public int LCDWidth
        {
            get => ConvertManager.Instance.ConvertedSize.Width;
            set => ConvertManager.Instance.ConvertedSize = new Int32Size(value, LCDHeight);
        }
        public int LCDHeight
        {
            get => ConvertManager.Instance.ConvertedSize.Height;
            set => ConvertManager.Instance.ConvertedSize = new Int32Size(LCDWidth, value);
        }
        public Int32Size ImageSplitSize
        {
            get => ConvertManager.Instance.ImageSplitSize;
            set => ConvertManager.Instance.ImageSplitSize = value;
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
        [Reactive]
        public bool ShowPreviewGrid { get; set; } = false;
        [Reactive]
        public bool IsMouseOverScrollableTextBox { get; set; } = false;
        [Reactive]
        public ImageSource? PreviewImageSource { get; set; }
        public bool PreviewImageLoaded => PreviewImageSource != null;
        public double PreviewScale
        {
            get => ConvertManager.Instance.Scale;
            set => ConvertManager.Instance.Scale = value;
        }
        public System.Windows.Point PreviewOffsetRatio
        {
            get => ConvertManager.Instance.TopLeftRatio;
            set => ConvertManager.Instance.TopLeftRatio = value;
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

        public MainWindowViewModel()
        {
            BrowseFilesCommand = new RelayCommand(ExecuteBrowseFiles);
            ZoomToFitCommand = new RelayCommand(ExecuteZoomToFit);
            ZoomToFillCommand = new RelayCommand(ExecuteZoomToFill);
            ResetZoomAndPanCommand = new RelayCommand(ExecuteResetZoomAndPan);
            ImageTransformCommand = new RelayCommand<RotateFlipType>(ExecuteImageTransform, i => i is not RotateFlipType.RotateNoneFlipNone);
            CopyImageToClipboardCommand = new RelayCommand<object>(ExecuteCopyImageToClipboard);
            ConvertFromClipboardCommand = new RelayCommand(ExecuteConvertFromClipboard);

            App.DebugStateChanged += (sender, e) => this.RaisePropertyChanged(nameof(Debug));

            this.WhenAnyValue(x => x.PreviewImageSource)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewImageLoaded)));
            this.WhenAnyValue(x => x.ImageSplitSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    this.RaisePropertyChanged(nameof(ImageSplitWidth));
                    this.RaisePropertyChanged(nameof(ImageSplitHeight));
                });

            ConvertManager.Instance.WhenAnyValue(x => x.EnableDithering)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(EnableDithering)));
            ConvertManager.Instance.WhenAnyValue(x => x.BitDepth)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(ColorDepth)));
            ConvertManager.Instance.WhenAnyValue(x => x.Interpolation)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(InterpolationMode)));
            ConvertManager.Instance.WhenAnyValue(x => x.ConvertedSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    this.RaisePropertyChanged(nameof(LCDWidth));
                    this.RaisePropertyChanged(nameof(LCDHeight));
                    _view.LCDSizeChanged(this, i.Width, i.Height);
                });
            ConvertManager.Instance.WhenAnyValue(x => x.ImageSplitSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    _view.ImageSplitSizeChanged(this, i);
                    this.RaisePropertyChanged(nameof(ImageSplitSize));
                });
            ConvertManager.Instance.WhenAnyValue(x => x.ProcessedImageFull)
                .Skip(1)
                .Subscribe(i =>
                {
                    if (i != null)
                    {
                        this.PreviewImageSource = i;
                    }
                });
            ConvertManager.Instance.WhenAnyValue(x => x.Scale)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewScale)));
            ConvertManager.Instance.WhenAnyValue(x => x.TopLeftRatio)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewOffsetRatio)));
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
