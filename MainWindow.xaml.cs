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
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using ImageSharp = SixLabors.ImageSharp;
using System.Threading;
using ImageConverterPlus.ViewModels;
using System.Collections.Specialized;
using Bitmap = System.Drawing.Bitmap;
using RotateFlipType = System.Drawing.RotateFlipType;

namespace ImageConverterPlus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Static { get; private set; }
        public static bool isMouseOverSizeTextbox => Static.viewModel.IsMouseOverScrollableTextBox;

        private MainWindowViewModel viewModel;
        ImageConverter.ConvertManager convMgr => ImageConverter.ConvertManager.Instance;

        public MainWindow()
        {
            Static = this;
            InitializeComponent();
            viewModel = (MainWindowViewModel)this.DataContext;
            convMgr.Delay = previewNew.animationDuration.TotalMilliseconds;
            convMgr.SourceImageChanged += ConvMgr_SourceImageChanged;

            this.Title = $"{App.AppName} v{App.AppVersion}";
            AppBigTitle.Content = App.AppName;

            UpdatePreviewGrid();

            App.Instance.Log.Log("MainWindow initialized");
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
                    convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                    UpdateBrowseImagesBtn(dialog.SafeFileName, dialog.FileName);
                    if (convMgr.SourceImage != null)
                    {
                        convMgr.ImageSplitSize = new Int32Size(1, 1);
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
                App.Instance.Log.Log(e.ToString());
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
                App.Instance.Log.Log($"Caught exception at MainWindow.IsFileTypeSupported(string) ({file})");
                App.Instance.Log.Log(e.ToString());
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

        public void CopyToClipClicked(object? param)
        {
            convMgr.ConvertImage(lcdStr => SetClipboardDelayed(lcdStr, 150));
        }

        object tokenSourceLock = new object();
        CancellationTokenSource? clipboardDelaySetTokenSource = null;
        private async void SetClipboardDelayed(string text, int delayMs)
        {
            if (text == null)
            {
                throw new NullReferenceException(nameof(text));
            }

            CancellationToken myToken;
            lock (tokenSourceLock)
            {
                clipboardDelaySetTokenSource?.Cancel();
                clipboardDelaySetTokenSource?.Dispose();
                clipboardDelaySetTokenSource = new CancellationTokenSource();
                myToken = clipboardDelaySetTokenSource.Token;
            }
            await Task.Delay(delayMs);

            if (myToken.IsCancellationRequested)
                return;

            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch (Exception e)
            {
                ShowAcrylDialog($"Clipboard error, try again! {e}");
            }
        }

        public void PasteFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                convMgr.SourceImage = Clipboard.GetImage();
                if (convMgr.SourceImage != null)
                {
                    convMgr.ProcessImage(bitmap =>
                    {
                        ResetZoomAndPan(false);
                        if (bitmap != null)
                        {
                            App.Instance.Log.Log("Image loaded from Clipboard (Bitmap)");
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
                        convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                        convMgr.ImageSplitSize = new Int32Size(1, 1);
                        convMgr.ProcessImage(bitmap =>
                        {
                            ResetZoomAndPan(false);
                            if (bitmap != null)
                            {
                                UpdateBrowseImagesBtn(System.IO.Path.GetFileName(file), file);
                                App.Instance.Log.Log("Loaded from Clipboard (FileDrop)");
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

        public static void ShowAcrylDialog(string message) => new AcrylicDialog(App.Current.MainWindow, message).ShowDialog();

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
            if (convMgr.SourceImage != null && convMgr.SourceImageSize is Int32Size imgSize)
            {
                convMgr.SourceImage = ApplyTransform(type, convMgr.SourceImage);

                convMgr.OnSourceImageChanged();

                if (type == RotateFlipType.Rotate90FlipNone && imgSize.Width != imgSize.Height)
                {
                    convMgr.ProcessImage(lcdStr =>
                    {
                        ResetZoomAndPan(false);
                    });
                }
                else
                {
                    previewNew.image.SizeChanged -= SizeChangedOTEHandler;
                    convMgr.ProcessImage();
                }

                App.Instance.Log.Log($"Image Transformed ({type.ToString()})");
            }
        }

        BitmapSource ApplyTransform(RotateFlipType type, BitmapSource bitmap)
        {
            Transform transform;
            switch (type)
            {
                case RotateFlipType.Rotate90FlipNone:
                    transform = new RotateTransform(90);
                    return new TransformedBitmap(bitmap, transform);
                case RotateFlipType.RotateNoneFlipX:
                    transform = new ScaleTransform(-1, 1, 0, 0);
                    return new TransformedBitmap(bitmap, transform);
                case RotateFlipType.RotateNoneFlipY:
                    transform = new ScaleTransform(1, -1, 0, 0);
                    return new TransformedBitmap(bitmap, transform);
            }
            return bitmap;
        }

        public void LCDSizeChanged(object? sender, int newWidth, int newHeight)
        {
            convMgr.ImageSplitSize = new Int32Size(1, 1);
            UpdatePreviewContainerSize();
            ResetZoomAndPanOnPreviewNewSizeChanged(); //so jank
            //ResetZoomAndPan(false);
        }

        public void ImageSplitSizeChanged(object? sender, Int32Size newSize)
        {
            UpdatePreviewGrid();
            ResetZoomAndPanOnPreviewNewSizeChanged();
            //ResetZoomAndPan(false);
        }

        public void UpdatePreviewContainerSize()
        {
            Int32Size lcd = convMgr.ConvertedSize;
            Int32Size split = convMgr.ImageSplitSize;
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

        public static void ConversionFailedDialog() => ShowAcrylDialog(new System.Diagnostics.StackTrace(1).ToString());

        private void ConvMgr_SourceImageChanged(BitmapSource? sourceImg)
        {
            if (viewModel.PreviewImageSource == null)
            {
                ResetZoomAndPanOnPreviewNewImageSizeChanged();
            }
            viewModel.PreviewImageSource = null;
            //ResetZoomAndPanOnPreviewNewImageSizeChanged();
        }

        private void ResetZoomAndPanOnPreviewNewSizeChanged()
        {
            previewNew.SizeChanged += SizeChangedOTEHandler;
        }

        private void ResetZoomAndPanOnPreviewNewImageSizeChanged()
        {
            previewNew.image.SizeChanged += SizeChangedOTEHandler; //so damn jank dude
        }
        /// <summary>
        /// please for the love of god dont use this thing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SizeChangedOTEHandler(object sender, SizeChangedEventArgs e)
        {
            ResetZoomAndPan(false);
            previewNew.SizeChanged -= SizeChangedOTEHandler;
            previewNew.image.SizeChanged -= SizeChangedOTEHandler;
        }
    }
}
