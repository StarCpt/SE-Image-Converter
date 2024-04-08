using ImageConverterPlus.ImageConverter;
using System;
using System.Collections.Generic;
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
using ImageConverterPlus.Data;
using ImageConverterPlus.Services.interfaces;
using System.Windows;
using System.Collections.Specialized;
using System.Windows.Media.Imaging;

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

        private static MainWindow _view => (MainWindow)App.Current.MainWindow; //temp

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
        [Reactive]
        public bool ShowPreviewGrid { get; set; } = false;
        [Reactive]
        public ImageSource? PreviewImageSource { get; set; }
        public bool PreviewImageLoaded => PreviewImageSource != null;
        public double PreviewScale
        {
            get => _convertManager.Scale;
            set => _convertManager.Scale = value;
        }
        public Point PreviewOffsetRatio
        {
            get => _convertManager.TopLeftRatio;
            set => _convertManager.TopLeftRatio = value;
        }
        public WindowTitleBarViewModel TitleBarContext { get; }
        [Reactive]
        public string? CurrentImagePath { get; set; }
        [Reactive]
        public string? CurrentImagePathLong { get; set; }

        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand CopyImageToClipboardCommand { get; }
        public ICommand ConvertFromClipboardCommand { get; }
        public ICommand ResetImageSplitCommand { get; }
        public ICommand CopySplitImagePieceToClipboardCommand { get; }
        public ICommand SetImageSplitWidthCommand { get; }
        public ICommand SetImageSplitHeightCommand { get; }

        private readonly ConvertManagerService _convertManager;
        private readonly ClipboardService _clipService;
        private readonly IDialogService _dialogService;
        private readonly LogService _logger;

        public MainWindowViewModel(ConvertManagerService convertManager, WindowTitleBarViewModel titleBarViewModel, ClipboardService clipService, IDialogService dialogService, LogService logger)
        {
            _convertManager = convertManager;
            TitleBarContext = titleBarViewModel;
            _clipService = clipService;
            _dialogService = dialogService;
            _logger = logger;

            BrowseFilesCommand = new RelayCommand(ExecuteBrowseFiles);
            ZoomToFitCommand = new RelayCommand(ExecuteZoomToFit);
            ZoomToFillCommand = new RelayCommand(ExecuteZoomToFill);
            ResetZoomAndPanCommand = new RelayCommand(ExecuteResetZoomAndPan);
            ImageTransformCommand = new RelayCommand<RotateFlipType>(ExecuteImageTransform, i => i is not RotateFlipType.RotateNoneFlipNone);
            CopyImageToClipboardCommand = new RelayCommand(ExecuteCopyImageToClipboard);
            ConvertFromClipboardCommand = new RelayCommand(ExecuteConvertFromClipboard);
            ResetImageSplitCommand = new RelayCommand(ExecuteResetImageSplit);
            CopySplitImagePieceToClipboardCommand = new RelayCommand<Int32Point>(ExecuteCopySplitImagePieceToClipboard);
            SetImageSplitWidthCommand = new RelayCommand<int>(i => ImageSplitSize = new Int32Size(i, ImageSplitSize.Height));
            SetImageSplitHeightCommand = new RelayCommand<int>(i => ImageSplitSize = new Int32Size(ImageSplitSize.Width, i));

            this.WhenAnyValue(x => x.PreviewImageSource)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewImageLoaded)));

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
                });
            _convertManager.WhenAnyValue(x => x.ImageSplitSize)
                .Skip(1)
                .Subscribe(i => this.RaisePropertyChanged(nameof(ImageSplitSize)));
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
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Image files (*.jpeg, *.jpg, *.jfif, *.png, *.tiff, *.bmp, *.gif, *.ico, *.webp)|*.jpeg;*.jpg;*.jfif;*.png;*.tiff;*.bmp;*.gif;*.ico;*.webp",
            };

            if (dialog.ShowDialog() == true)
            {
                if (Helpers.TryLoadImage(dialog.FileName, out var result) && result is not null)
                {
                    _convertManager.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                    CurrentImagePath = dialog.SafeFileName;
                    CurrentImagePathLong = dialog.FileName;
                    if (_convertManager.SourceImage != null)
                    {
                        _convertManager.ImageSplitSize = new Int32Size(1, 1);
                        _convertManager.ProcessImage(delegate
                        {
                            ResetImageZoomAndPanNoAnim();
                        });
                    }
                }
                else
                {
                    _dialogService.ShowAsync(new MessageDialogViewModel("Error", "This file type is not supported!"));
                }
            }
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

        public void ResetImageZoomAndPanNoAnim()
        {
            _view.ResetZoomAndPan(false);
        }

        private void ExecuteImageTransform(RotateFlipType transform)
        {
            if (_convertManager.SourceImage is BitmapSource img)
            {
                _convertManager.SourceImage = Helpers.TransformBitmap(transform, img);

                if (transform == RotateFlipType.Rotate90FlipNone && img.PixelWidth != img.PixelHeight)
                {
                    _convertManager.ProcessImage(lcdStr =>
                    {
                        ResetImageZoomAndPanNoAnim();
                    });
                }
                else
                {
                    _view.previewNew.image.SizeChanged -= _view.SizeChangedOTEHandler;
                    _convertManager.ProcessImage();
                }

                _logger.Log($"Image Transformed ({transform})");
            }
        }

        private void ExecuteCopyImageToClipboard()
        {
            _convertManager.ConvertImage(lcdStr => _clipService.SetClipboardDelayed(lcdStr, 150));
        }

        private void ExecuteConvertFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                _convertManager.SourceImage = Clipboard.GetImage();
                if (_convertManager.SourceImage != null)
                {
                    _convertManager.ProcessImage(bitmap =>
                    {
                        ResetImageZoomAndPanNoAnim();
                        if (bitmap != null)
                        {
                            _logger.Log("Image loaded from clipboard (Bitmap)");
                        }
                        else
                        {
                            ShowConversionFailedDialog();
                        }
                    });
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                StringCollection filedroplist = Clipboard.GetFileDropList();
                for (int i = 0; i < filedroplist.Count; i++)
                {
                    string? file = filedroplist[i];
                    if (file != null && Helpers.TryLoadImage(file, out var result) && result is not null)
                    {
                        _convertManager.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                        _convertManager.ImageSplitSize = new Int32Size(1, 1);
                        _convertManager.ProcessImage(bitmap =>
                        {
                            ResetImageZoomAndPanNoAnim();
                            if (bitmap != null)
                            {
                                CurrentImagePath = System.IO.Path.GetFileName(file);
                                CurrentImagePathLong = file;
                                _logger.Log("Image loaded from clipboard (FileDrop)");
                            }
                            else
                            {
                                ShowConversionFailedDialog();
                            }
                        });
                        return;
                    }
                }

                _dialogService.ShowAsync(new MessageDialogViewModel("Error", "This file type is not supported!"));
            }
            else
            {
                _dialogService.ShowAsync(new MessageDialogViewModel("Error", "Unsupported File"));
            }
        }

        private void ExecuteResetImageSplit()
        {
            _convertManager.ImageSplitSize = new Int32Size(1, 1);
        }

        private void ExecuteCopySplitImagePieceToClipboard(Int32Point pos)
        {
            _convertManager.SelectedSplitPos = pos;
            _convertManager.ConvertImage(lcdStr =>
            {
                if (lcdStr != null)
                    _clipService.SetClipboardDelayed(lcdStr, 150);
                else
                    ShowConversionFailedDialog();
            });
        }

        private void ShowConversionFailedDialog() => _dialogService.ShowAsync(new MessageDialogViewModel("Error", new System.Diagnostics.StackTrace(1).ToString()));
    }
}
