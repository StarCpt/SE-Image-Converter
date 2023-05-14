﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using BitDepth = ImageConverterPlus.ImageConverter.BitDepth;
using DitherMode = ImageConverterPlus.ImageConverter.DitherMode;
using Size = System.Drawing.Size;
using Timer = System.Timers.Timer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using ImageSharp = SixLabors.ImageSharp;
using System.Threading;
using System.Timers;
using ImageConverterPlus.ViewModels;
using System.ComponentModel;
using ImageConverterPlus.ImageConverter;

namespace ImageConverterPlus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string version = "1.0 Alpha";

        private string? ConvertedImageStr;
        public static ImageInfo ImageCache;//load image here first then convert so it can be used again
        public static MainWindow Static { get; private set; }
        private MainWindowViewModel viewModel;

        private readonly Dictionary<ToggleButton, InterpolationMode> scaleButtons;
        private readonly Dictionary<ToggleButton, BitDepth> colorBitDepthButtons;

        private Timer ClipboardTimer;
        private Timer ConvertTimer;

        public static Logging Logging { get; private set; }

        private CancellationTokenSource ConvertCancellationTokenSource;
        private CancellationTokenSource PreviewConvertCancellationTokenSource;
        private Task ConvertTask;
        private Task PreviewConvertTask;

        public enum ImageInfoType
        {
            UploadedFile = 1,
            DraggedFile = 2,
            DraggedBitmap = 4,
            DraggedWebHTML = 8,
            ClipboardFile = 16,
            ClipboardBitmap = 32,
        }

        public struct ImageInfo
        {
            public Bitmap? Image;
            public string FileNameOrImageSource;
            public bool IsFile;

            public ImageInfo(Bitmap? image, string fileNameOrOther, bool isFile)
            {
                Image = image;
                FileNameOrImageSource = fileNameOrOther;
                IsFile = isFile;
            }
        }

        public MainWindow()
        {
            Logging = new Logging(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log", 1000, true);

            Static = this;
            InitializeComponent();
            viewModel = (MainWindowViewModel)this.DataContext;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ToggleBtn_3BitColor.IsChecked = true;
            ToggleBtn_ScaleBicubic.IsChecked = true;
            CopyToClipBtn.IsEnabled = !string.IsNullOrEmpty(ConvertedImageStr);
            //OpenLogBtnToolTip.Content = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log");

            //add buttons to their respective dictionaries.
            scaleButtons = new()
            {
                { ToggleBtn_ScaleNearest, InterpolationMode.NearestNeighbor },
                { ToggleBtn_ScaleBilinear, InterpolationMode.HighQualityBilinear },
                { ToggleBtn_ScaleBicubic, InterpolationMode.HighQualityBicubic },
            };

            colorBitDepthButtons = new()
            {
                { ToggleBtn_3BitColor, BitDepth.Color3 },
                { ToggleBtn_5BitColor, BitDepth.Color5 },
            };

            this.Title = $"SE Image Converter+ v{version}";
            AppBigTitle.Content = "SE Image Converter+";

            InitImagePreview();

            Logging.Log("MainWindow initialized");
        }

        public void BrowseImageFiles()
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "Image files (*.jpeg, *.jpg, *.jfif, *.png, *.tiff, *.bmp, *.gif, *.ico, *.webp)|*.jpeg;*.jpg;*.jfif;*.png;*.tiff;*.bmp;*.gif;*.ico;*.webp",
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                var supportedFlag = IsFileTypeSupported(dialog.FileName);
                if (supportedFlag != IsFileSupportedEnum.NotSupported && TryGetImageInfo(dialog.FileName, supportedFlag, out ImageCache))
                {
                    UpdateBrowseImagesBtn(dialog.SafeFileName, dialog.FileName);
                    if (ImageCache.Image != null)
                    {
                        viewModel.ImageSplitSize = new Size(1, 1);
                        UpdatePreviewDelayed(true, 0);
                        //TryConvertImageThreaded(ImageCache, true, convertCallback, previewConvertCallback);
                    }
                }
                else
                {
                    ShowAcrylDialog("This file type is not supported!");
                }
            }
        }

        private bool TryGetImageInfo(string filePath, IsFileSupportedEnum supEnum, out ImageInfo result)
        {
            try
            {
                if (supEnum == IsFileSupportedEnum.Supported)
                {
                    Bitmap bitmap = new(filePath);
                    result = new ImageInfo(bitmap, filePath, true);
                    return true;
                }
                else if (supEnum == IsFileSupportedEnum.Webp)
                {
                    Bitmap bitmap = DecodeWebpImage(filePath);
                    result = new ImageInfo(bitmap, filePath, true);
                    return true;
                }
                else
                {
                    result = new ImageInfo(null, filePath, true);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log(e.ToString());
                result = new ImageInfo(null, filePath, true);
                return false;
            }
        }

        private enum IsFileSupportedEnum
        {
            NotSupported = 0,
            Supported = 1,
            Webp = 2,
        }

        public static readonly string[] SupportedFileTypes = 
            { "png", "jpg", "jpeg", "jfif", "tiff", "bmp", "gif", "ico", "webp" };

        private IsFileSupportedEnum IsFileTypeSupported(string file)
        {
            try
            {
                string fileExtension = file.Split('.').Last();

                if (SupportedFileTypes.Any(i => i.Equals(fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    if (fileExtension.Equals("webp", StringComparison.OrdinalIgnoreCase))
                    {
                        return IsFileSupportedEnum.Webp;
                    }
                    else
                    {
                        return IsFileSupportedEnum.Supported;
                    }
                }
                else
                {
                    return IsFileSupportedEnum.NotSupported;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at MainWindow.IsFileTypeSupported(string) ({file})");
                Logging.Log(e.ToString());
                return IsFileSupportedEnum.NotSupported;
            }
        }

        private Bitmap DecodeWebpImage(string filePath)
        {
            WebpDecoder webpDecoder = new WebpDecoder();
            ImageSharp.Image webpImg = webpDecoder.Decode(Configuration.Default, new FileStream(filePath, FileMode.Open, FileAccess.Read), CancellationToken.None);

            ImageSharp.Formats.Bmp.BmpEncoder enc = new ImageSharp.Formats.Bmp.BmpEncoder();

            using (MemoryStream stream = new MemoryStream())
            {
                webpImg.Save(stream, enc);
                return new Bitmap(stream);
            }
        }

        /// <summary>
        /// displays error dialogs automatically. does file type checks as well. does not check if path is empty/null
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private bool TryConvertFromFile(string imagePath)
        {
            try
            {
                IsFileSupportedEnum supportedFlag = IsFileTypeSupported(imagePath);
                if (supportedFlag != IsFileSupportedEnum.NotSupported)
                {
                    if (TryGetImageInfo(imagePath, supportedFlag, out ImageInfo bitImageInfo))
                    {
                        viewModel.ImageSplitSize = new Size(1, 1);
                        return TryConvertImageThreaded(bitImageInfo, true, ConvertResultCallback, PreviewConvertResultCallback);
                    }
                    else
                    {
                        ShowAcrylDialog("This file type is not supported!");
                        return false;
                    }
                }
                else
                {
                    ShowAcrylDialog("This file type is not supported! (2)");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at TryConvertFromFile(string, bool, bool) ({imagePath})");
                Logging.Log(e.ToString());
                ShowAcrylDialog("Error occurred while decoding the file! Make sure file type is valid.");
                return false;
            }
        }

        /// <summary>
        /// Gets the settings, converts, and updates the preview and ConvertedImageStr. Diaplays error dialogs automagically
        /// </summary>
        /// <param name="image"></param>
        /// <returns>whether or not the operation succeeded</returns>
        public bool TryConvertImageThreaded(ImageInfo image, bool resetZoom, Action<string> convertCallback, Action<BitmapImage> previewConvertCallback)
        {
            try
            {
                int bitsPerColor;
                if (!TryGetColorBitDepth(out BitDepth colorDepth))
                {
                    bitsPerColor = 3;
                    ShowAcrylDialog("Color depth error! Defaulting to 3 bits.");
                }
                else
                {
                    bitsPerColor = (int)colorDepth;
                }

                if (!TryGetInterpolationMode(out InterpolationMode interpolationMode))
                {
                    interpolationMode = InterpolationMode.NearestNeighbor;
                    ShowAcrylDialog("Scaling mode error! Defaulting to Nearest.");
                }

                Size convertedSize = GetLCDSize();

                if (image.Image != null)
                {
                    if (resetZoom)
                    {
                        ResetPreviewZoomAndPan(false);
                    }

                    if (ConvertTask != null && !ConvertTask.IsCompleted)
                    {
                        ConvertCancellationTokenSource.Cancel();
                    }

                    var tt = GetTranslateTransform(ImagePreview);

                    //scale the bitmap size to the lcd size

                    double imageToLcdWidthRatio = (double)image.Image.Width / convertedSize.Width;
                    double imageToLcdHeightRatio = (double)image.Image.Height / convertedSize.Height;

                    //get the bigger ratio taking into account the image split
                    double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / ImageSplitSize.Width, imageToLcdHeightRatio / ImageSplitSize.Height);

                    double scaledImageWidth = image.Image.Width / biggerImageToLcdRatio;
                    double scaledImageHeight = image.Image.Height / biggerImageToLcdRatio;

                    //apply preview scale (zoom)
                    scaledImageWidth *= imagePreviewScale;
                    scaledImageHeight *= imagePreviewScale;

                    //turn the size from above into lcd width/height % ratio
                    double scaledImageToLcdWidthRatio = scaledImageWidth / convertedSize.Width;
                    double scaledImageToLcdHeightRatio = scaledImageHeight / convertedSize.Height;

                    double biggerScaledImageToLcdRatio = Math.Max(scaledImageToLcdWidthRatio, scaledImageToLcdHeightRatio);

                    ///

                    //double widthScale = imageToLcdWidthRatio / ImageSplitSize.Width;
                    //double heightScale = imageToLcdHeightRatio / ImageSplitSize.Height;
                    //double biggerScale = Math.Max(widthScale, heightScale);
                    //
                    //double scaledImageWidthToLcdWidthRatio = imageToLcdWidthRatio / biggerScale;
                    //double scaledImageHeightToLcdHeightRatio = imageToLcdHeightRatio / biggerScale;
                    //
                    //double scale = Math.Max(scaledImageWidthToLcdWidthRatio, scaledImageHeightToLcdHeightRatio) * imagePreviewScale;
                    //
                    //widthScale /= biggerScale;
                    //heightScale /= biggerScale;
                    //
                    //double bitmapWidthToWidthRatio = widthScale * ImageSplitSize.Width;
                    //double bitmapHeightToHeightRatio = heightScale * ImageSplitSize.Height;

                    int xOffset = Convert.ToInt32((tt.X - PreviewTopLeft.X) / ImagePreviewBorder.ActualWidth / convertedSize.Width * ImageSplitSize.Width + (convertedSize.Width * checkedSplitBtnPos.X));
                    int yOffset = Convert.ToInt32((tt.Y - PreviewTopLeft.Y) / ImagePreviewBorder.ActualHeight / convertedSize.Height * ImageSplitSize.Height - (convertedSize.Height * checkedSplitBtnPos.Y));

                    var options = new ConvertOptions
                    {
                        Dithering = viewModel.EnableDithering,
                        BitsPerChannel = bitsPerColor,
                        ConvertedSize = convertedSize,
                        Interpolation = interpolationMode,
                        Scale = biggerScaledImageToLcdRatio,
                        TopLeft = new System.Drawing.Point(xOffset, yOffset),
                    };
                    ConvertCancellationTokenSource = new CancellationTokenSource();

                    //var convertThread = new ConvertThread(
                    //    image.Image,               // image
                    //    viewModel.EnableDithering, // dither
                    //    bitsPerColor,              // colorDepth
                    //    convertedSize,             // lcdSize
                    //    ImageSplitSize,            // imageSplitSize
                    //    checkedSplitBtnPos,        // splitPos
                    //    interpolationMode,         // interpolationMode
                    //    convertCallback,           // callback
                    //    (tt.X - PreviewTopLeft.X) / ImagePreviewBorder.ActualWidth / convertedSize.Width,   // xOffset
                    //    (tt.Y - PreviewTopLeft.Y) / ImagePreviewBorder.ActualHeight / convertedSize.Height, // yOffset
                    //    ConvertCancellationTokenSource.Token); //token

                    var convertThread = new ConvertThread(image.Image, options, convertCallback, ConvertCancellationTokenSource.Token);

                    ConvertTask = Task.Run(convertThread.ConvertNew);

                    UpdatePreview(image.Image, convertedSize, (int)bitsPerColor, interpolationMode, previewConvertCallback);

                    ImageCache = image;

                    return true;
                }
                else
                {
                    Logging.Log($"Caught exception at TryConvertImage(Bitmap, bool, bool), ({(image.FileNameOrImageSource == null ? "FileNameOrImageSource is Null" : image.FileNameOrImageSource.ToString())}), Image is null");
                    ShowAcrylDialog("Error occurred during image conversion! (image.Image is null)");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at TryConvertImage(Bitmap, bool, bool), ({image.FileNameOrImageSource.ToString()})");
                Logging.Log(e.ToString());
                ShowAcrylDialog("Error occurred during image conversion! (Exception)");
                return false;
            }
        }
        
        public void ConvertResultCallback(string resultStr)
        {
            ConvertedImageStr = resultStr;
            CopyToClipBtn.Dispatcher.Invoke(() => CopyToClipBtn.IsEnabled = true);
        }

        private bool TryGetColorBitDepth(out BitDepth result)
        {
            if (colorBitDepthButtons.Any(b => b.Key.IsChecked == true))
            {
                result = colorBitDepthButtons.FirstOrDefault(b => b.Key.IsChecked == true).Value;
                return true;
            }
            else //failsafe. shouldn't ever be triggered
            {
                result = BitDepth.Invalid;
                return false;
            }
        }
        private bool TryGetInterpolationMode(out InterpolationMode result)
        {
            if (scaleButtons.Any(b => b.Key.IsChecked == true))
            {
                result = scaleButtons.FirstOrDefault(b => b.Key.IsChecked == true).Value;
                return true;
            }
            else
            {
                result = InterpolationMode.Invalid;
                return false;
            }
        }
        private Size GetLCDSize()
        {
            return new Size(viewModel.LCDWidth, viewModel.LCDHeight);
        }
        [Obsolete]
        private DitherMode GetDitherMode()
        {
            if (viewModel.EnableDithering)
            {
                return DitherMode.FloydSteinberg;
            }
            else //PLACEHOLDER!! CHANGE WHEN THERE ARE MORE THAN 1 OPTION
            {
                return DitherMode.NoDither;
            }
        }

        public void CopyToClipClicked(object? param)
        {
            if (!TryConvertImageThreaded(ImageCache, false, ConvertCallbackCopyToClip, PreviewConvertResultCallback))
            {
                ShowAcrylDialog($"Convert {(ImageCache.Image != null ? "the" : "an")} image first!");
            }
        }

        private void SetClipDelayed(string text)
        {
            if (ClipboardTimer != null)
            {
                ClipboardTimer.Enabled = false;
                ClipboardTimer.Dispose();
            }

            ClipboardTimer = new Timer(150)
            {
                Enabled = true,
                AutoReset = false,
            };
            ClipboardTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            CopyToClipBtn.Dispatcher.Invoke(() =>
            {
                try { Clipboard.SetDataObject(text, true); }
                catch { ShowAcrylDialog("Clipboard error, try again!"); }
                finally { ClipboardTimer = null; }
            });
        }

        private void ConvertCallbackCopyToClip(string resultStr)
        {
            ConvertedImageStr = resultStr;
            //CopyToClipBtn.Dispatcher.Invoke(() =>
            //{
            //    CopyToClipBtn.IsEnabled = true;
            //});

            SetClipDelayed(resultStr);
        }

        private void ColorDepthOption_Clicked(object sender, RoutedEventArgs e)
        {
            ToggleButton thisBtn = (ToggleButton)sender;
            foreach (var btn in colorBitDepthButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            UpdatePreviewDelayed(false, 0);
        }

        public static bool isMouseOverSizeTextbox => Static.viewModel.IsMouseOverScrollableTextBox;

        private void ScaleOption_Clicked(object sender, RoutedEventArgs e)
        {
            ToggleButton thisBtn = (ToggleButton)sender;
            foreach (var btn in scaleButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            UpdatePreviewDelayed(false, 0);
        }

        private void EnableDitheringChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreviewDelayed(false, 0);
        }

        private void ContextMenuItem_DeleteCache(object sender, RoutedEventArgs e)
        {
            ImageCache.Image = null;
            UpdateBrowseImagesBtn(string.Empty, null);
        }

        private void PasteFromClipboard(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                var image = Helpers.BitmapSourceToBitmap(Clipboard.GetImage());
                if (TryConvertImageThreaded(new ImageInfo(image, "Image loaded from Clipboard", false), true, ConvertResultCallback, PreviewConvertResultCallback))
                {
                    UpdateBrowseImagesBtn("Loaded from Clipboard", null);
                    Logging.Log("Image loaded from Clipboard (Bitmap)");
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection filedroplist = Clipboard.GetFileDropList();
                for (int i = 0; i < filedroplist.Count; i++)
                {
                    if (TryConvertFromFile(filedroplist[i]))
                    {
                        UpdateBrowseImagesBtn(System.IO.Path.GetFileName(filedroplist[i]), filedroplist[i]);
                        Logging.Log("Loaded from Clipboard (FileDrop)");
                        break;
                    }
                }
                //foreach (string file in filedroplist)
                //{
                //    if (TryConvertFromFile(file))
                //    {
                //        UpdateBrowseImagesBtn(file.GetFileName(), file);
                //        UpdateCurrentConvertBtnToolTip(file, true);
                //        Logging.Log("Loaded from Clipboard (FileDrop)");
                //        break;
                //    }
                //}
            }
            else
            {
                ShowAcrylDialog("Unsupported File");
            }
        }

        public static void ShowAcrylDialog(string message) => new AcrylicDialog(Static, message).ShowDialog();

        /// <summary>
        /// does not check if instant change is enabled!
        /// </summary>
        /// <param name="resetZoom"></param>
        /// <param name="delay"></param>
        private void ApplyInstantChanges(bool resetZoom, ushort delay)
        {
            if (ConvertTimer != null)
            {
                ConvertTimer.Enabled = false;
                ConvertTimer.Dispose();
                ConvertTimer = null;
            }

            if (delay == 0)
            {
                if (ImageCache.Image != null)
                {
                    TryConvertImageThreaded(ImageCache, resetZoom, ConvertResultCallback, PreviewConvertResultCallback);
                }
                return;
            }

            ConvertTimer = new Timer(delay)
            {
                Enabled = true,
                AutoReset = false,
            };

            ConvertTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                if (ImageCache.Image != null)
                {
                    Task.Run(() => this.Dispatcher.Invoke(() =>
                    {
                        TryConvertImageThreaded(ImageCache, resetZoom, ConvertResultCallback, PreviewConvertResultCallback);
                        ConvertTimer = null;
                    }));
                }
            };
            ConvertTimer.Start();
        }

        public void UpdateBrowseImagesBtn(string text, string fullpath)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (!string.IsNullOrEmpty(fullpath))
                {
                    BrowseFilesToolTip.Content = fullpath;
                }
                else
                {
                    BrowseFilesToolTip.Content = text;
                }
                if (text.Length > 20)
                {
                    text = text.Substring(0, 20) + "...";
                }
                BrowseFilesBtn.Content = text;
                BrowseFilesBtn.FontSize = 12;
                BrowseFilesBtn.Foreground = Brushes.DarkGray;
            }
            else
            {
                BrowseFilesToolTip.Content = "Browse Files";
                BrowseFilesBtn.Content = "Browse Files";
                BrowseFilesBtn.FontSize = 15;
                BrowseFilesBtn.Foreground = Brushes.White;
            }
        }

        private void OpenAppDirBtn_Click(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);

        public void TransformImage(RotateFlipType type)
        {
            if (ImageCache.Image != null)
            {
                ImageCache.Image.RotateFlip(type);

                if (type == RotateFlipType.Rotate90FlipNone && ImageCache.Image.Width != ImageCache.Image.Height)
                {
                    TryConvertImageThreaded(ImageCache, true, ConvertResultCallback, PreviewConvertResultCallback);
                }
                else
                {
                    UpdatePreviewDelayed(false, 0);
                }

                Logging.Log($"Image Transformed ({type.ToString()})");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableDithering):
                    EnableDitheringChanged(sender, e);
                    break;
                case nameof(MainWindowViewModel.LCDWidth) or nameof(MainWindowViewModel.LCDHeight):
                    LCDSizeChanged(sender, e);
                    break;
                case nameof(MainWindowViewModel.ImageSplitSize):
                    ImageSplitSizeChanged(sender, e);
                    break;

            }
        }

        private void LCDSizeChanged(object? sender, PropertyChangedEventArgs e)
        {
            viewModel.ImageSplitSize = new Size(1, 1);

            UpdatePreviewDelayed(true, 50);
        }

        private void ImageSplitSizeChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreviewGrid();
            if (ImageCache.Image != null)
            {
                UpdatePreviewDelayed(true, 100);
            }
        }
    }
}
