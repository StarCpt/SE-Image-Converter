using ImageConverterPlus.ImageConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
            get => _enableDithering.Value;
            set => _convertManager.EnableDithering = value;
        }
        public BitDepth ColorDepth
        {
            get => _colorDepth.Value;
            set => _convertManager.BitDepth = value;
        }
        public InterpolationMode InterpolationMode
        {
            get => _interpolationMode.Value;
            set => _convertManager.Interpolation = value;
        }
        [Reactive]
        public LCDPresetType SelectedLCD { get; set; } = LCDPresetType.LCDPanel;
        public int LCDWidth
        {
            get => _lcdWidth.Value;
            set => _convertManager.ConvertedSize = new Int32Size(value, LCDHeight);
        }
        public int LCDHeight
        {
            get => _lcdHeight.Value;
            set => _convertManager.ConvertedSize = new Int32Size(LCDWidth, value);
        }
        public int ImageSplitWidth
        {
            get => _imageSplitWidth.Value;
            set => _convertManager.ImageSplitSize = new Int32Size(value, ImageSplitHeight);
        }
        public int ImageSplitHeight
        {
            get => _imageSplitHeight.Value;
            set => _convertManager.ImageSplitSize = new Int32Size(ImageSplitWidth, value);
        }
        public Int32Point SelectedSplitPos
        {
            get => _selectedSplitPos.Value;
            set => _convertManager.SelectedSplitPos = value;
        }
        [Reactive]
        public bool ShowPreviewGrid { get; set; } = false;
        [Reactive]
        public BitmapSource? PreviewImageSource { get; set; }
        public Int32Size? PreviewImageSize => PreviewImageSource is BitmapSource src ? new Int32Size(src.PixelWidth, src.PixelHeight) : null;
        public double PreviewScale
        {
            get => _previewScale.Value;
            set => _convertManager.Scale = value;
        }
        public Point PreviewOffsetRatio
        {
            get => _previewOffsetRatio.Value;
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
        public ICommand ImageDropCommand { get; }

        private readonly ObservableAsPropertyHelper<bool> _enableDithering;
        private readonly ObservableAsPropertyHelper<BitDepth> _colorDepth;
        private readonly ObservableAsPropertyHelper<InterpolationMode> _interpolationMode;
        private readonly ObservableAsPropertyHelper<int> _lcdWidth;
        private readonly ObservableAsPropertyHelper<int> _lcdHeight;
        private readonly ObservableAsPropertyHelper<int> _imageSplitWidth;
        private readonly ObservableAsPropertyHelper<int> _imageSplitHeight;
        private readonly ObservableAsPropertyHelper<Int32Point> _selectedSplitPos;
        private readonly ObservableAsPropertyHelper<double> _previewScale;
        private readonly ObservableAsPropertyHelper<Point> _previewOffsetRatio;

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
            ImageDropCommand = new RelayCommand<DragEventArgs>(ExecuteImageDrop!, CanExecuteImageDrop);

            _enableDithering = _convertManager.WhenAnyValue(x => x.EnableDithering)
                .ToProperty(this, x => x.EnableDithering);
            _colorDepth = _convertManager.WhenAnyValue(x => x.BitDepth)
                .ToProperty(this, x => x.ColorDepth);
            _interpolationMode = _convertManager.WhenAnyValue(x => x.Interpolation)
                .ToProperty(this, x => x.InterpolationMode);
            _lcdWidth = _convertManager.WhenAnyValue(x => x.ConvertedSize)
                .Select(size => size.Width)
                .ToProperty(this, x => x.LCDWidth);
            _lcdHeight = _convertManager.WhenAnyValue(x => x.ConvertedSize)
                .Select(size => size.Height)
                .ToProperty(this, x => x.LCDHeight);
            _imageSplitWidth = _convertManager.WhenAnyValue(x => x.ImageSplitSize)
                .Select(size => size.Width)
                .ToProperty(this, x => x.ImageSplitWidth);
            _imageSplitHeight = _convertManager.WhenAnyValue(x => x.ImageSplitSize)
                .Select(size => size.Height)
                .ToProperty(this, x => x.ImageSplitHeight);
            _selectedSplitPos = _convertManager.WhenAnyValue(x => x.SelectedSplitPos)
                .ToProperty(this, x => x.SelectedSplitPos);
            _previewScale = _convertManager.WhenAnyValue(x => x.Scale)
                .ToProperty(this, x => x.PreviewScale);
            _previewOffsetRatio = _convertManager.WhenAnyValue(x => x.TopLeftRatio)
                .ToProperty(this, x => x.PreviewOffsetRatio);

            this.WhenAnyValue(x => x.PreviewImageSource)
                .Subscribe(i => this.RaisePropertyChanged(nameof(PreviewImageSize)));

            _convertManager.WhenAnyValue(x => x.ProcessedImageFull)
                .Where(i => i != null)
                .Subscribe(i => this.PreviewImageSource = i);
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
                    ShowErrorDialog("This file type is not supported!");
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

                ShowErrorDialog("This file type is not supported!");
            }
            else
            {
                ShowErrorDialog("Unsupported File");
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

        private static bool CanExecuteImageDrop(DragEventArgs? e)
        {
            if (e is null)
                return false;

            return (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                ((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault() is string file &&
                Helpers.IsImageFileSupported(file) is not IsFileSupportedEnum.NotSupported) ||
                e.Data.GetDataPresent(DataFormats.Bitmap) ||
                e.Data.GetDataPresent(DataFormats.Html);
        }

        private void ExecuteImageDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (Helpers.TryLoadImage(file, out var result) && result is not null)
                    {
                        _convertManager.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                        _convertManager.ImageSplitSize = new Int32Size(1, 1);
                        _convertManager.ProcessImage(delegate
                        {
                            ResetImageZoomAndPanNoAnim();
                            CurrentImagePath = System.IO.Path.GetFileName(file);
                            CurrentImagePathLong = file;
                            _logger.Log("Image Drag & Dropped (FileDrop)");
                        });
                        return;
                    }
                }

                //when file type doesnt match
                ShowErrorDialog("This file type is not supported!");
            }
            else if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                var image = (System.Drawing.Bitmap)e.Data.GetData(DataFormats.Bitmap);
                _convertManager.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                _convertManager.ProcessImage(delegate
                {
                    ResetImageZoomAndPanNoAnim();
                    CurrentImagePath = CurrentImagePathLong = "Drag & Droped Image";
                    _logger.Log("Image Drag & Dropped (Bitmap)");
                });
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                _ = WebHelpers.HandleHtmlDropThreadAsync(e.Data, _convertManager);
            }
            else
            {
                ShowErrorDialog("Clipboard does not contain any images");
            }
        }

        private void ShowConversionFailedDialog() => ShowErrorDialog(new System.Diagnostics.StackTrace(1).ToString());
        private void ShowErrorDialog(string error) => _dialogService.ShowAsync(new MessageDialogViewModel("Error", error));
    }
}
