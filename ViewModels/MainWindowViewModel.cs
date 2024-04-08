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
using ImageConverterPlus.Services;

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
            get => _convertManager.EnableDithering;
            set => _convertManager.EnableDithering = value;
        }
        public BitDepth ColorDepth
        {
            get => _convertManager.BitDepth;
            set => _convertManager.BitDepth = value;
        }
        public InterpolationMode InterpolationMode
        {
            get => _convertManager.Interpolation;
            set => _convertManager.Interpolation = value;
        }
        [Reactive]
        public LCDPresetType SelectedLCD { get; set; } = LCDPresetType.LCDPanel;
        public int LCDWidth
        {
            get => _convertManager.ConvertedSize.Width;
            set => _convertManager.ConvertedSize = new Int32Size(value, LCDHeight);
        }
        public int LCDHeight
        {
            get => _convertManager.ConvertedSize.Height;
            set => _convertManager.ConvertedSize = new Int32Size(LCDWidth, value);
        }
        public Int32Size ImageSplitSize
        {
            get => _convertManager.ImageSplitSize;
            set => _convertManager.ImageSplitSize = value;
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
            get => _convertManager.Scale;
            set => _convertManager.Scale = value;
        }
        public System.Windows.Point PreviewOffsetRatio
        {
            get => _convertManager.TopLeftRatio;
            set => _convertManager.TopLeftRatio = value;
        }
        public bool Debug
        {
            get => ((App)App.Current).Debug;
            set => ((App)App.Current).Debug = value;
        }

        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand CopyImageToClipboardCommand { get; }
        public ICommand ConvertFromClipboardCommand { get; }

        private readonly ConvertManagerService _convertManager;

        public MainWindowViewModel(ConvertManagerService convertManager)
        {
            _convertManager = convertManager;

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

            _convertManager.WhenAnyValue(x => x.EnableDithering)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(EnableDithering)));
            _convertManager.WhenAnyValue(x => x.BitDepth)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(ColorDepth)));
            _convertManager.WhenAnyValue(x => x.Interpolation)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(InterpolationMode)));
            _convertManager.WhenAnyValue(x => x.ConvertedSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    this.RaisePropertyChanged(nameof(LCDWidth));
                    this.RaisePropertyChanged(nameof(LCDHeight));
                    _view.LCDSizeChanged(this, i.Width, i.Height);
                });
            _convertManager.WhenAnyValue(x => x.ImageSplitSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    _view.ImageSplitSizeChanged(this, i);
                    this.RaisePropertyChanged(nameof(ImageSplitSize));
                });
            _convertManager.WhenAnyValue(x => x.ProcessedImageFull)
                .Skip(1)
                .Subscribe(i =>
                {
                    if (i != null)
                    {
                        this.PreviewImageSource = i;
                    }
                });
            _convertManager.WhenAnyValue(x => x.Scale)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewScale)));
            _convertManager.WhenAnyValue(x => x.TopLeftRatio)
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
