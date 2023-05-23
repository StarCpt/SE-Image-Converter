using System;
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

        private Timer ClipboardTimer;

        public static Logging Logging { get; private set; }

        public ConvertManager LcdConvertManager { get; } = new ConvertManager();
        //public ConvertManager PreviewConvertManager { get; } = new ConvertManager();
 
        private CancellationTokenSource ConvertCancellationTokenSource;

        private Timer PreviewConvertTimer;
        private CancellationTokenSource PreviewConvertCancellationTokenSource;
        private Task PreviewConvertTask;

        public class ImageInfo
        {
            public Bitmap? Image;
            public string FileNameOrImageSource;

            public ImageInfo(Bitmap? image, string fileNameOrOther)
            {
                Image = image;
                FileNameOrImageSource = fileNameOrOther;
            }
        }

        public MainWindow()
        {
            Logging = new Logging(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log", 1000, true);

            Static = this;
            InitializeComponent();
            viewModel = (MainWindowViewModel)this.DataContext;
            CopyToClipBtn.IsEnabled = !string.IsNullOrEmpty(ConvertedImageStr);
            //OpenLogBtnToolTip.Content = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log");

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
                    result = new ImageInfo(bitmap, filePath);
                    return true;
                }
                else if (supEnum == IsFileSupportedEnum.Webp)
                {
                    Bitmap bitmap = DecodeWebpImage(filePath);
                    result = new ImageInfo(bitmap, filePath);
                    return true;
                }
                else
                {
                    result = new ImageInfo(null, filePath);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log(e.ToString());
                result = new ImageInfo(null, filePath);
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
                        ResetZoomAndPan();
                        ImageCache = bitImageInfo;
                        return TryConvertImageThreaded(ImageCache.Image, ConvertResultCallback, PreviewConvertResultCallback);
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
        public bool TryConvertImageThreaded(System.Drawing.Image image, Action<string> convertCallback, Action<Bitmap> previewConvertCallback)
        {
            try
            {
                if (image != null)
                {
                    //if (ConvertTask != null && !ConvertTask.IsCompleted)
                    //{
                    //    ConvertCancellationTokenSource.Cancel();
                    //}

                    //scale the bitmap size to the lcd size

                    var lcdSize = LcdConvertManager.ConvertedSize;

                    double imageToLcdWidthRatio = (double)image.Width / lcdSize.Width;
                    double imageToLcdHeightRatio = (double)image.Height / lcdSize.Height;

                    //get the bigger ratio taking into account the image split
                    double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / ImageSplitSize.Width, imageToLcdHeightRatio / ImageSplitSize.Height);

                    double scaledImageWidth = image.Width / biggerImageToLcdRatio;
                    double scaledImageHeight = image.Height / biggerImageToLcdRatio;

                    //apply preview scale (zoom)
                    scaledImageWidth *= previewNew.Scale;
                    scaledImageHeight *= previewNew.Scale;

                    //turn the size from above into lcd width/height % ratio
                    double scaledImageToLcdWidthRatio = scaledImageWidth / lcdSize.Width;
                    double scaledImageToLcdHeightRatio = scaledImageHeight / lcdSize.Height;

                    double biggerScaledImageToLcdRatio = Math.Max(scaledImageToLcdWidthRatio, scaledImageToLcdHeightRatio);

                    int xOffset = Convert.ToInt32((previewNew.Offset.X) / (previewNew.ActualWidth / lcdSize.Width) * ImageSplitSize.Width - (lcdSize.Width * checkedSplitBtnPos.X));
                    int yOffset = Convert.ToInt32((previewNew.Offset.Y) / (previewNew.ActualHeight / lcdSize.Height) * ImageSplitSize.Height - (lcdSize.Height * checkedSplitBtnPos.Y));

                    UpdatePreview(image, lcdSize, (int)viewModel.ColorDepth, viewModel.InterpolationMode, previewConvertCallback);

                    var options = new ConvertOptions
                    {
                        Dithering = LcdConvertManager.EnableDithering,
                        BitsPerChannel = (int)viewModel.ColorDepth,
                        ConvertedSize = lcdSize,
                        Interpolation = LcdConvertManager.Interpolation,
                        Scale = biggerScaledImageToLcdRatio,
                        TopLeft = new System.Drawing.Point(xOffset, yOffset),
                    };
                    ConvertCancellationTokenSource = new CancellationTokenSource();
                    ConvertManager.ConvertToString(image, options, convertCallback, ConvertCancellationTokenSource.Token);

                    return true;
                }
                else
                {
                    Logging.Log($"Caught exception at {nameof(TryConvertImageThreaded)}, Image is null");
                    ShowAcrylDialog("Error occurred during image conversion! (image.Image is null)");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at {nameof(TryConvertImageThreaded)}");
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

        private Size GetLCDSize()
        {
            return new Size(viewModel.LCDWidth, viewModel.LCDHeight);
        }

        public void CopyToClipClicked(object? param)
        {
            if (!TryConvertImageThreaded(ImageCache?.Image, ConvertCallbackCopyToClip, PreviewConvertResultCallback))
            {
                ShowAcrylDialog($"Convert {(ImageCache?.Image != null ? "the" : "an")} image first!");
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
            ClipboardTimer.Elapsed += (object? sender, ElapsedEventArgs e) =>
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

        public void ColorDepthChanged(object? sender, BitDepth newColorDepth)
        {
            LcdConvertManager.BitDepth = newColorDepth;
            UpdatePreviewDelayed(false, 0);
        }

        public static bool isMouseOverSizeTextbox => Static.viewModel.IsMouseOverScrollableTextBox;

        public void ScaleModeChanged(object? sender, InterpolationMode newScaleMode)
        {
            LcdConvertManager.Interpolation = newScaleMode;
            UpdatePreviewDelayed(false, 0);
        }

        public void EnableDitheringChanged(object? sender, bool newValue)
        {
            LcdConvertManager.EnableDithering = newValue;
            UpdatePreviewDelayed(false, 0);
        }

        private void PasteFromClipboard(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                Bitmap image = Helpers.BitmapSourceToBitmap(Clipboard.GetImage());
                ImageCache = new ImageInfo(image, "Image loaded from Clipboard");
                if (TryConvertImageThreaded(image, ConvertResultCallback, PreviewConvertResultCallback))
                {
                    UpdateBrowseImagesBtn("Loaded from Clipboard", "");
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

        public void TransformImage(RotateFlipType type)
        {
            if (ImageCache?.Image != null)
            {
                ImageCache.Image.RotateFlip(type);

                if (type == RotateFlipType.Rotate90FlipNone && ImageCache.Image.Width != ImageCache.Image.Height)
                {
                    ResetZoomAndPan();
                    TryConvertImageThreaded(ImageCache.Image, ConvertResultCallback, PreviewConvertResultCallback);
                }
                else
                {
                    UpdatePreviewDelayed(false, 0);
                }

                Logging.Log($"Image Transformed ({type.ToString()})");
            }
        }

        public void LCDSizeChanged(object? sender, int newWidth, int newHeight)
        {
            viewModel.ImageSplitSize = new Size(1, 1);
            LcdConvertManager.ConvertedSize = new Size(newWidth, newHeight);

            UpdatePreviewDelayed(true, 50);
        }

        public void ImageSplitSizeChanged(object? sender, Size newSize)
        {
            UpdatePreviewGrid();
            if (ImageCache?.Image != null)
            {
                UpdatePreviewDelayed(true, 100);
            }
        }
    }
}
