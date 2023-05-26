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
using System.IO;
using Brushes = System.Windows.Media.Brushes;
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
using System.Collections.Specialized;
using System.Diagnostics;

namespace ImageConverterPlus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string version = "1.0 Alpha";

        public static MainWindow Static { get; private set; }
        private MainWindowViewModel viewModel;

        private Timer? ClipboardTimer;

        public static Logging Logging { get; private set; }

        ConvertManager convMgr => ConvertManager.Instance;

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
            convMgr.Delay = previewNew.animationDuration.TotalMilliseconds;

            this.Title = $"SE Image Converter+ v{version}";
            AppBigTitle.Content = "SE Image Converter+";

            UpdatePreviewGrid();

            Logging.Log("MainWindow initialized");
        }

        public void BrowseImageFiles()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Image files (*.jpeg, *.jpg, *.jfif, *.png, *.tiff, *.bmp, *.gif, *.ico, *.webp)|*.jpeg;*.jpg;*.jfif;*.png;*.tiff;*.bmp;*.gif;*.ico;*.webp",
            };

            if (dialog.ShowDialog() == true)
            {
                if (TryGetImageInfo(dialog.FileName, out Bitmap? result) && result is not null)
                {
                    convMgr.SourceImage = result;
                    UpdateBrowseImagesBtn(dialog.SafeFileName, dialog.FileName);
                    if (convMgr.SourceImage != null)
                    {
                        convMgr.ImageSplitSize = new Size(1, 1);
                        convMgr.ProcessImage(delegate
                        {
                            ResetZoomAndPan(false);
                        });
                    }
                }
                else
                {
                    ShowAcrylDialog("This file type is not supported!");
                }
            }
        }

        private bool TryGetImageInfo(string filePath, out Bitmap? result)
        {
            try
            {
                IsFileSupportedEnum supEnum = IsFileTypeSupported(filePath);
                if (supEnum == IsFileSupportedEnum.Supported)
                {
                    result = new Bitmap(filePath);
                    return true;
                }
                else if (supEnum == IsFileSupportedEnum.Webp)
                {
                    result = DecodeWebpImage(filePath);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log(e.ToString());
                result = null;
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
        /// Gets the settings, converts, and updates the preview and ConvertedImageStr. Diaplays error dialogs automagically
        /// </summary>
        /// <param name="image"></param>
        /// <returns>whether or not the operation succeeded</returns>
        //public bool TryConvertImageThreadedOld(System.Drawing.Image image, Action<string> convertCallback)
        //{
        //    try
        //    {
        //        if (image != null)
        //        {
        //            //if (ConvertTask != null && !ConvertTask.IsCompleted)
        //            //{
        //            //    ConvertCancellationTokenSource.Cancel();
        //            //}
        //
        //            //scale the bitmap size to the lcd size
        //
        //            var lcdSize = convMgr.ConvertedSize;
        //
        //            double imageToLcdWidthRatio = (double)image.Width / lcdSize.Width;
        //            double imageToLcdHeightRatio = (double)image.Height / lcdSize.Height;
        //
        //            //get the bigger ratio taking into account the image split
        //            double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / convMgr.ImageSplitSize.Width, imageToLcdHeightRatio / convMgr.ImageSplitSize.Height);
        //
        //            double scaledImageWidth = image.Width / biggerImageToLcdRatio;
        //            double scaledImageHeight = image.Height / biggerImageToLcdRatio;
        //
        //            //apply preview scale (zoom)
        //            scaledImageWidth *= previewNew.Scale;
        //            scaledImageHeight *= previewNew.Scale;
        //
        //            //turn the size from above into lcd width/height % ratio
        //            double scaledImageToLcdWidthRatio = scaledImageWidth / lcdSize.Width;
        //            double scaledImageToLcdHeightRatio = scaledImageHeight / lcdSize.Height;
        //
        //            double biggerScaledImageToLcdRatio = Math.Max(scaledImageToLcdWidthRatio, scaledImageToLcdHeightRatio);
        //
        //            int xOffset = Convert.ToInt32(previewNew.Offset.X / (previewNew.ActualWidth / lcdSize.Width) * convMgr.ImageSplitSize.Width - (lcdSize.Width * convMgr.SelectedSplitPos.X));
        //            int yOffset = Convert.ToInt32(previewNew.Offset.Y / (previewNew.ActualHeight / lcdSize.Height) * convMgr.ImageSplitSize.Height - (lcdSize.Height * convMgr.SelectedSplitPos.Y));
        //            
        //            ConvertManager.Instance.ProcessImage();
        //
        //            var options = new ConvertOptions
        //            {
        //                Dithering = convMgr.EnableDithering,
        //                BitsPerChannel = (int)convMgr.BitDepth,
        //                ConvertedSize = convMgr.ConvertedSize,
        //                Interpolation = convMgr.Interpolation,
        //                Scale = biggerScaledImageToLcdRatio,
        //                TopLeft = new System.Drawing.Point(xOffset, yOffset),
        //            };
        //            ConvertCancellationTokenSource = new CancellationTokenSource();
        //            //ConvertManager.ConvertImageOld(image, options, convertCallback, ConvertCancellationTokenSource.Token);
        //
        //            return true;
        //        }
        //        else
        //        {
        //            Logging.Log($"Image is null {new StackTrace()}");
        //            ShowAcrylDialog("Error occurred during image conversion! (image.Image is null)");
        //            return false;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logging.Log(e.ToString());
        //        ShowAcrylDialog($"Exception occurred during image conversion! {e}");
        //        return false;
        //    }
        //}

        public void CopyToClipClicked(object? param)
        {
            convMgr.ConvertImage(SetClipboardDelayed);
        }

        private void SetClipboardDelayed(string text)
        {
            if (text == null)
            {
                throw new NullReferenceException(nameof(text));
            }

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
            this.Dispatcher.Invoke(() =>
            {
                try { Clipboard.SetDataObject(text, true); }
                catch { ShowAcrylDialog("Clipboard error, try again!"); }
                finally { ClipboardTimer = null; }
            });
        }

        public static bool isMouseOverSizeTextbox => Static.viewModel.IsMouseOverScrollableTextBox;

        public void PasteFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                Bitmap image = Helpers.BitmapSourceToBitmap(Clipboard.GetImage());
                convMgr.SourceImage = image;
                if (convMgr.SourceImage != null)
                {
                    convMgr.ProcessImage(bitmap =>
                    {
                        ResetZoomAndPan(false);
                        if (bitmap != null)
                        {
                            Logging.Log("Image loaded from Clipboard (Bitmap)");
                        }
                        else
                        {
                            ConversionFailedDialog();
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
                    if (file != null && TryGetImageInfo(file, out Bitmap? result) && result is not null)
                    {
                        convMgr.SourceImage = result;
                        convMgr.ImageSplitSize = new Size(1, 1);
                        convMgr.ProcessImage(bitmap =>
                        {
                            ResetZoomAndPan(false);
                            if (bitmap != null)
                            {
                                UpdateBrowseImagesBtn(System.IO.Path.GetFileName(file), file);
                                Logging.Log("Loaded from Clipboard (FileDrop)");
                            }
                            else
                            {
                                ConversionFailedDialog();
                            }
                        });
                        return;
                    }
                }
                ShowAcrylDialog("This file type is not supported!");
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
            if (convMgr.SourceImage != null && convMgr.SourceImageSize is Size imgSize)
            {
                lock(convMgr.SourceImage)
                {
                    convMgr.SourceImage.RotateFlip(type);
                }
                convMgr.SourceImageChanged();

                if (type == RotateFlipType.Rotate90FlipNone && imgSize.Width != imgSize.Height)
                {
                    convMgr.ProcessImage(lcdStr =>
                    {
                        ResetZoomAndPan(false);
                    });
                }
                else
                {
                    convMgr.ProcessImage();
                }

                Logging.Log($"Image Transformed ({type.ToString()})");
            }
        }

        public void LCDSizeChanged(object? sender, int newWidth, int newHeight)
        {
            convMgr.ImageSplitSize = new Size(1, 1);
            ResetZoomAndPan(false);

            UpdatePreviewContainerSize();
        }

        public void ImageSplitSizeChanged(object? sender, Size newSize)
        {
            ResetZoomAndPan(false);
            UpdatePreviewGrid();
        }

        public void UpdatePreviewContainerSize()
        {
            Size lcd = convMgr.ConvertedSize;
            Size split = convMgr.ImageSplitSize;
            if (lcd.Width * split.Width > lcd.Height * split.Height)
            {
                previewNew.Width = PreviewContainerGridSize;
                previewNew.Height = PreviewContainerGridSize * ((double)(lcd.Height * split.Height) / (lcd.Width * split.Width));
            }
            else
            {
                previewNew.Width = PreviewContainerGridSize * ((double)(lcd.Width * split.Width) / (lcd.Height * split.Height));
                previewNew.Height = PreviewContainerGridSize;
            }
        }

        public static void ConversionFailedDialog() => ShowAcrylDialog(new StackTrace(1).ToString());
    }
}
