using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;
using System.Net;
using System.IO;
//using System.Drawing;
using Bitmap = System.Drawing.Bitmap;
using Size = System.Drawing.Size;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;
using System.Timers;

namespace SEImageToLCD_15BitColor
{
    partial class MainWindow
    {
        private Point origin;
        private Point start;

        private int PreviewBorderWidth;
        private int PreviewBorderHeight;

        private Point PreviewTopLeft;

        public static float imageZoom = 1f;

        private Timer PreviewConvertTimer;

        public void InitImagePreview()
        {
            ImagePreview.MouseWheel += Preview_MouseWheel;
            ImagePreview.MouseLeftButtonDown += Preview_MouseLeftBtnDown;
            ImagePreview.MouseLeftButtonUp += Preview_MouseLeftBtnUp;
            ImagePreview.MouseMove += Preview_MouseMove;
            ImagePreviewBorder.SizeChanged += UpdatePreviewTopLeft;
            ImagePreview.SizeChanged += UpdatePreviewTopLeft;

            PreviewBorderWidth = 350;
            PreviewBorderHeight = 350;

            TransformGroup group = new();
            ScaleTransform st = new();
            group.Children.Add(st);
            TranslateTransform tt = new();
            group.Children.Add(tt);
            ImagePreview.RenderTransform = group;
            ImagePreview.RenderTransformOrigin = new Point(0d, 0d);
        }

        public void ResetPreviewZoomAndPan()
        {
            var st = GetScaleTransform(ImagePreview);
            st.ScaleX = 1d;
            st.ScaleY = 1d;

            var tt = GetTranslateTransform(ImagePreview);
            tt.X = 0d;
            tt.Y = 0d;

            imageZoom = 1f;
        }

        public void ChangePreviewZoom(float zoom)
        {
            if (!TryGetLCDSize(out Size? size) || 
                !TryGetInterpolationMode(out InterpolationMode interpolation) || 
                !TryGetColorBitDepth(out ConvertThread.BitDepth depth) || 
                !TryGetDitherMode(out ConvertThread.DitherMode dither))
            {
                return;
            }
            float scale = Math.Min((float)size.Value.Width / ImageCache.ImageSize.Width, (float)size.Value.Height / ImageCache.ImageSize.Height);
            scale *= zoom;

            var NewThread = new PreviewConvertThread(ImageCache.Image, dither, depth, interpolation, new PreviewConvertCallback(PreviewConvertResultCallback), scale);

            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                QueuedPreviewConversion = NewThread;
            }
            else
            {
                sw.Restart();
                PreviewConvertThread = NewThread;
                PreviewConvertTask = Task.Run(PreviewConvertThread.ConvertPreviewThreadedFast);
            }
        }

        public void UpdatePreviewTopLeft(object sender, SizeChangedEventArgs e)
        {
            PreviewTopLeft = new Point((-ImagePreviewBorder.ActualWidth + ImagePreview.ActualWidth) / 2, (-ImagePreviewBorder.ActualHeight + ImagePreview.ActualHeight) / 2);
        }

        public void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScaleTransform st = GetScaleTransform(ImagePreview);
            TranslateTransform tt = GetTranslateTransform(ImagePreview);

            double zoom = e.Delta > 0 ? 0.2 : -0.2;

            if (!(e.Delta > 0) && (st.ScaleX <= 1 || st.ScaleY <= 1))
            {
                return;
            }

            Point relative = e.GetPosition(ImagePreview);
            double absX;
            double absY;

            absX = relative.X * st.ScaleX + tt.X;
            absY = relative.Y * st.ScaleY + tt.Y;

            st.ScaleX += zoom;
            st.ScaleY += zoom;

            imageZoom = (float)st.ScaleX;
            DoInstantChange(false);

            //ChangePreviewZoom((float)st.ScaleX);
            if (PreviewConvertTimer != null)
            {
                PreviewConvertTimer.Dispose();
                PreviewConvertTimer = null;
            }
            PreviewConvertTimer = new Timer(100);
            PreviewConvertTimer.Elapsed += (object sender, ElapsedEventArgs e) => ImagePreview.Dispatcher.Invoke(() => ChangePreviewZoom((float)st.ScaleX));
            PreviewConvertTimer.Enabled = true;
            PreviewConvertTimer.AutoReset = false;
            PreviewConvertTimer.Start();

            tt.X = (absX - relative.X * st.ScaleX).ClampDoubleExt(PreviewTopLeft.X, PreviewTopLeft.X + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * st.ScaleX));
            tt.Y = (absY - relative.Y * st.ScaleY).ClampDoubleExt(PreviewTopLeft.Y, PreviewTopLeft.Y + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY));
        }

        public void Preview_MouseLeftBtnDown(object sender, MouseButtonEventArgs e)
        {
            TranslateTransform tt = GetTranslateTransform(ImagePreview);
            start = e.GetPosition(ImagePreviewBorder);
            origin = new Point(tt.X, tt.Y);
            ImagePreview.Cursor = Cursors.Hand;
            ImagePreview.CaptureMouse();
        }

        public void Preview_MouseLeftBtnUp(object sender, MouseButtonEventArgs e)
        {
            ImagePreview.ReleaseMouseCapture();
            ImagePreview.Cursor = Cursors.Arrow;
        }

        public void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (ImagePreview.IsMouseCaptured)
            {
                TranslateTransform tt = GetTranslateTransform(ImagePreview);
                ScaleTransform st = GetScaleTransform(ImagePreview);
                Vector vec = start - e.GetPosition(ImagePreviewBorder);
                
                tt.X = (origin.X - vec.X).ClampDoubleExt(PreviewTopLeft.X, PreviewTopLeft.X + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * st.ScaleX));
                tt.Y = (origin.Y - vec.Y).ClampDoubleExt(PreviewTopLeft.Y, PreviewTopLeft.Y + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY));

                //tt.Y = (origin.Y - vec.Y).ClampDoubleExt((-ImagePreviewBorder.ActualHeight + ImagePreview.ActualHeight) / 2, ImagePreviewBorder.ActualHeight / 2 + ImagePreview.ActualHeight * (0.5 - st.ScaleY));
                //(-ImagePreviewBorder.ActualHeight + ImagePreview.ActualHeight) / 2 + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY)
                //.Clamp(-ImagePreview.ActualHeight * (st.ScaleY - 1), 0);

                DoInstantChange(false);
            }
        }

        public TranslateTransform GetTranslateTransform(Image image)
        {
            return (TranslateTransform)((TransformGroup)image.RenderTransform)
                .Children.First(tr => tr is TranslateTransform);
        }

        public ScaleTransform GetScaleTransform(Image image)
        {
            return (ScaleTransform)((TransformGroup)image.RenderTransform)
                .Children.First(tr => tr is ScaleTransform);
        }

        private void ChangePreviewFromOtherThread(BitmapImage image, Size lcdSize, bool resetZoom)
        {
            double otherAxisScale = lcdSize.Width > lcdSize.Height ? (double)lcdSize.Height / lcdSize.Width : (double)lcdSize.Width / lcdSize.Height;
            image.Freeze();
            ImagePreview.Dispatcher.Invoke(() => 
            {
                ImagePreview.Source = image;
                if (lcdSize.Width > lcdSize.Height)
                {
                    ImagePreviewBorder.Height = (int)(PreviewBorderHeight * otherAxisScale);
                    ImagePreviewBorder.Width = PreviewBorderWidth;
                }
                else
                {
                    ImagePreviewBorder.Height = PreviewBorderHeight;
                    ImagePreviewBorder.Width = (int)(PreviewBorderWidth * otherAxisScale);
                }
                ImagePreviewBorder.Background = Brushes.Black;
                if (resetZoom)
                {
                    ResetPreviewZoomAndPan();
                }
            });

            CopyToClipBtn.Dispatcher.Invoke(() => CopyToClipBtn.IsEnabled = true);

        }

        private void UpdatePreviewFromOtherThread (BitmapImage image)
        {
            image.Freeze();
            ImagePreview.Dispatcher.Invoke(() =>
            {
                ImagePreview.Source = image;
            });
        }

        private void RemovePreview(object sender, RoutedEventArgs e) => RemovePreview();

        private void RemovePreview()
        {
            ImagePreview.Source = null;
            ConvertedImageStr = string.Empty;
            CopyToClipBtn.IsEnabled = false;
            ResetPreviewZoomAndPan();
        }

        private void Preview_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (TryConvertFromFile(file, false, true))
                    {
                        UpdateBrowseImagesBtn(file.GetFileName(), file);
                        UpdateCurrentConvertBtnToolTip(file, true);
                        Logging.Log("Drag & Drop Image (FileDrop)");
                        return;
                    }
                }

                //when file type doesnt match
                ShowAcrylDialog("This file type is not supported!");
            }
            else if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                Bitmap bitImage = (Bitmap)e.Data.GetData(DataFormats.Bitmap);
                if (TryConvertImageThreaded(new ImageInfo(bitImage, bitImage.Size, "Drag & Droped Image Bitmap", false), true, true, true))
                {
                    UpdateBrowseImagesBtn("Drag & Droped Image", null);
                    UpdateCurrentConvertBtnToolTip("Drag & Droped Image", true);
                    Logging.Log("Drag & Drop Image (Bitmap)");
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                try
                {
                    HtmlDocument doc = new();
                    doc.LoadHtml((string)e.Data.GetData(DataFormats.Html));
                    HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");
                    if (imgNodes != null)
                    {
                        string imgSrc = imgNodes.First().GetAttributeValue("src", null);

                        WebClient client = new();
                        Stream stream = client.OpenRead(imgSrc);//try downloading the image
                        Bitmap bitmap = new(stream);
                        if (TryConvertImageThreaded(new ImageInfo(bitmap, bitmap.Size, imgSrc, false), true, true, true))
                        {
                            UpdateBrowseImagesBtn("Loaded from the web", null);
                            UpdateCurrentConvertBtnToolTip("Image loaded from the web", true);
                            Logging.Log($"Image loaded from HTML ({imgSrc})");
                        }
                    }
                    else
                    {
                        ShowAcrylDialog("This item is not supported!");
                    }
                }
                catch (Exception excep)
                {
                    Logging.Log("Caught exception while parsing HTML");
                    Logging.Log(excep.ToString());
                    ShowAcrylDialog("Invalid Web Item");
                }
            }
            else
            {
                ShowAcrylDialog("Clipboard does not contain any images");
            }
        }

        public delegate void PreviewConvertCallback(BitmapImage resultPreviewImg);


        public void PreviewConvertResultCallback(BitmapImage resultPreviewImg)
        {
            if (QueuedPreviewConversion != null)
            {
                sw.Restart();
                PreviewConvertThread = QueuedPreviewConversion;
                QueuedPreviewConversion = null;
                PreviewConvertTask = Task.Run(PreviewConvertThread.ConvertPreviewThreadedFast);
            }
            else
            {
                sw.Stop();
                Logging.Log($"Conversion took {sw.ElapsedMilliseconds} ms");
                UpdatePreviewFromOtherThread(resultPreviewImg);
            }
        }
    }
}
