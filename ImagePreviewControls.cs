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
        public static float previewImageZoom = 1f;
        public static bool previewChanged = true;

        private Timer PreviewConvertTimer;

        public void InitImagePreview()
        {
            ImagePreview.PreviewMouseWheel += Preview_OnMouseWheelChanged;
            ImagePreview.MouseLeftButtonDown += Preview_OnMouseLeftBtnDown;
            ImagePreview.MouseLeftButtonUp += Preview_OnMouseLeftBtnUp;
            ImagePreview.MouseMove += Preview_OnMouseMove;
            ImagePreview.SizeChanged += UpdatePreviewTopLeft;

            ImagePreviewBorder.PreviewMouseWheel += Preview_OnMouseWheelChanged;
            ImagePreviewBorder.MouseLeftButtonDown += Preview_OnMouseLeftBtnDown;
            ImagePreviewBorder.MouseLeftButtonUp += Preview_OnMouseLeftBtnUp;
            ImagePreviewBorder.MouseMove += Preview_OnMouseMove;
            ImagePreviewBorder.SizeChanged += UpdatePreviewTopLeft;

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
            ImagePreviewBorder.Background = Brushes.Black;

            var st = GetScaleTransform(ImagePreview);
            st.ScaleX = 1d;
            st.ScaleY = 1d;

            var tt = GetTranslateTransform(ImagePreview);
            tt.X = 0d;
            tt.Y = 0d;

            previewImageZoom = 1f;

            UpdatePreviewTimed(0);
        }

        public void UpdatePreview(ImageInfo image, Size lcdSize, InterpolationMode interpolationMode, ConvertThread.BitDepth colorDepth, ConvertThread.DitherMode ditherMode, PreviewConvertCallback previewConvertCallback)
        {
            float scale = Math.Min((float)lcdSize.Width / image.Image.Size.Width, (float)lcdSize.Height / image.Image.Size.Height);
            scale *= previewImageZoom;

            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                PreviewConvertThread.CancelTask();
            }

            PreviewConvertThread = new PreviewConvertThread(image.Image, ditherMode, colorDepth, interpolationMode, previewConvertCallback, scale);
            PreviewConvertTask = Task.Run(PreviewConvertThread.ConvertPreviewThreadedFast);
        }

        public void UpdatePreviewTimed(ushort delay)
        {
            if (ImageCache.Image == null ||
                !TryGetLCDSize(out Size? size) ||
                !TryGetInterpolationMode(out InterpolationMode interpolation) ||
                !TryGetColorBitDepth(out ConvertThread.BitDepth depth) ||
                !TryGetDitherMode(out ConvertThread.DitherMode dither))
            {
                return;
            }
            float scale = Math.Min((float)size.Value.Width / ImageCache.Image.Size.Width, (float)size.Value.Height / ImageCache.Image.Size.Height);
            scale *= previewImageZoom;

            if (PreviewConvertTimer != null)
            {
                PreviewConvertTimer.Stop();
                PreviewConvertTimer.Dispose();
                PreviewConvertTimer = null;
            }

            if (delay == 0)
            {
                UpdatePreviewInternal();
                return;
            }

            PreviewConvertTimer = new Timer(delay)
            {
                Enabled = true,
                AutoReset = false,
            };
            PreviewConvertTimer.Elapsed += (object sender, ElapsedEventArgs e) => ImagePreview.Dispatcher.Invoke(UpdatePreviewInternal);
            PreviewConvertTimer.Start();

            void UpdatePreviewInternal()
            {
                if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
                {
                    PreviewConvertThread.CancelTask();
                }

                PreviewConvertThread = new PreviewConvertThread(ImageCache.Image, dither, depth, interpolation, new PreviewConvertCallback(PreviewConvertResultCallback), scale);
                PreviewConvertTask = Task.Run(PreviewConvertThread.ConvertPreviewThreadedFast);
            }
        }

        public void UpdatePreviewTopLeft(object sender, SizeChangedEventArgs e)
        {
            PreviewTopLeft = new Point((-ImagePreviewBorder.ActualWidth + ImagePreview.ActualWidth) / 2, (-ImagePreviewBorder.ActualHeight + ImagePreview.ActualHeight) / 2);
        }

        public void Preview_OnMouseWheelChanged(object sender, MouseWheelEventArgs e)
        {
            ScaleTransform st = GetScaleTransform(ImagePreview);
            TranslateTransform tt = GetTranslateTransform(ImagePreview);

            double zoom = e.Delta > 0 ? 0.2 : -0.2;

            if ((e.Delta < 0 && st.ScaleX <= 0.4) || (e.Delta > 0 && st.ScaleX >= 10))
            {
                return;
            }

            Point relative = e.GetPosition(ImagePreview);
            double absX;
            double absY;

            absX = relative.X * st.ScaleX + tt.X;
            absY = relative.Y * st.ScaleY + tt.Y;

            st.ScaleX = (st.ScaleX + zoom).Clamp(0.4, 10);
            st.ScaleY = (st.ScaleY + zoom).Clamp(0.4, 10);

            previewImageZoom = (float)st.ScaleX;

            UpdatePreviewTimed(100);

            tt.X = (absX - relative.X * st.ScaleX).ClampDoubleExt(PreviewTopLeft.X, PreviewTopLeft.X + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * st.ScaleX));
            tt.Y = (absY - relative.Y * st.ScaleY).ClampDoubleExt(PreviewTopLeft.Y, PreviewTopLeft.Y + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY));

            start = e.GetPosition(ImagePreviewBorder);
            origin = new Point(tt.X, tt.Y);

            e.Handled = true;
        }


        private void SetPreviewZoom(double zoom)
        {
            ScaleTransform st = GetScaleTransform(ImagePreview);
            TranslateTransform tt = GetTranslateTransform(ImagePreview);

            st.ScaleX = zoom;
            st.ScaleY = zoom;

            previewImageZoom = (float)zoom;

            UpdatePreviewTimed(0);

            tt.X = ((PreviewTopLeft.X * 2) + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * st.ScaleX)) / 2;
            tt.Y = ((PreviewTopLeft.Y * 2) + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY)) / 2;
        }

        public void Preview_OnMouseLeftBtnDown(object sender, MouseButtonEventArgs e)
        {
            TranslateTransform tt = GetTranslateTransform(ImagePreview);
            start = e.GetPosition(ImagePreviewBorder);
            origin = new Point(tt.X, tt.Y);
            ImagePreview.Cursor = Cursors.Hand;
            ImagePreview.CaptureMouse();
        }

        public void Preview_OnMouseLeftBtnUp(object sender, MouseButtonEventArgs e)
        {
            ImagePreview.ReleaseMouseCapture();
            ImagePreview.Cursor = Cursors.Arrow;
        }

        public void Preview_OnMouseMove(object sender, MouseEventArgs e)
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

        private void ChangePreviewThreadSafe(BitmapImage image, bool resetZoom)
        {
            previewChanged = true;
            image.Freeze();
            ImagePreview.Dispatcher.Invoke(() =>
            {
                ImagePreview.Source = image;

                if (TryGetLCDSize(out Size? lcdSize))
                {
                    if (lcdSize.Value.Width > lcdSize.Value.Height)
                    {
                        ImagePreviewBorder.Width = PreviewBorderWidth;
                        ImagePreviewBorder.Height = (PreviewBorderHeight * ((double)lcdSize.Value.Height / lcdSize.Value.Width)).ToRoundedInt();
                    }
                    else
                    {
                        ImagePreviewBorder.Width = (PreviewBorderWidth * ((double)lcdSize.Value.Width / lcdSize.Value.Height)).ToRoundedInt();
                        ImagePreviewBorder.Height = PreviewBorderHeight;
                    }
                }

                ImagePreviewLabel.Visibility = Visibility.Hidden;

                if (resetZoom)
                {
                    ResetPreviewZoomAndPan();
                }
            });
        }

        private void RemovePreview(object sender, RoutedEventArgs e) => RemovePreview();

        private void RemovePreview()
        {
            ImagePreview.Source = null;
            ConvertedImageStr = string.Empty;
            CopyToClipBtn.IsEnabled = false;
            ResetPreviewZoomAndPan();
            ImagePreviewBorder.Background = Brushes.Transparent;
            ImagePreviewLabel.Visibility = Visibility.Visible;
        }

        private void Preview_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (TryConvertFromFile(file))
                    {
                        UpdateBrowseImagesBtn(file.GetFileName(), file);
                        UpdateCurrentConvertBtnToolTip(file, true);
                        Logging.Log("Image Drag & Dropped (FileDrop)");
                        return;
                    }
                }

                //when file type doesnt match
                ShowAcrylDialog("This file type is not supported!");
            }
            else if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                Bitmap bitImage = (Bitmap)e.Data.GetData(DataFormats.Bitmap);
                if (TryConvertImageThreaded(new ImageInfo(bitImage, "Drag & Droped Image Bitmap", false), true, convertCallback, previewConvertCallback))
                {
                    UpdateBrowseImagesBtn("Drag & Droped Image", null);
                    UpdateCurrentConvertBtnToolTip("Drag & Droped Image", true);
                    Logging.Log("Image Drag & Dropped (Bitmap)");
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                try
                {
                    HtmlDocument doc = new();
                    doc.LoadHtml((string)e.Data.GetData(DataFormats.Html));
                    HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");

                    if (imgNodes != null && imgNodes.Count > 0)
                    {
                        WebClient web = new WebClient();
                        string src = imgNodes[0].GetAttributeValue("src", null);
                        src = WebUtility.HtmlDecode(src);
                        Stream stream = web.OpenRead(src);
                        Bitmap bitmap = new Bitmap(stream);
                        if (TryConvertImageThreaded(new ImageInfo(bitmap, src, false), true, convertCallback, previewConvertCallback))
                        {
                            UpdateBrowseImagesBtn("Loaded from the web", null);
                            UpdateCurrentConvertBtnToolTip("Image loaded from the web", true);
                            Logging.Log($"Image loaded from HTML ({src})");
                        }
                    }
                    else
                    {
                        ShowAcrylDialog("Dropped html does not contain any image links!");
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
        private void PreviewConvertResultCallback(BitmapImage resultPreviewImg)
        {
            ChangePreviewThreadSafe(resultPreviewImg, false);
        }

        private void ResetZoomBtn_Click(object sender, RoutedEventArgs e) => ResetPreviewZoomAndPan();

        private void ZoomToFit_Click(object sender, RoutedEventArgs e)
        {
            double zoom = Math.Min(ImagePreviewBorder.ActualWidth / ImagePreview.ActualWidth, ImagePreviewBorder.ActualHeight / ImagePreview.ActualHeight);
            if (previewImageZoom != zoom)
            {
                SetPreviewZoom(zoom);
            }
        }

        private void ZoomToFill_Click(object sender, RoutedEventArgs e)
        {
            double zoom = Math.Max(ImagePreviewBorder.ActualWidth / ImagePreview.ActualWidth, ImagePreviewBorder.ActualHeight / ImagePreview.ActualHeight);
            if (previewImageZoom != zoom)
            {
                SetPreviewZoom(zoom);
            }
        }

        public static bool IsOneToNine(string str)
        {
            return !str.Any(c => c < '1' || c > '9');
        }

        private void ImageSplit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsOneToNine(e.Text))
            {
                e.Handled = true;
            }
            else
            {
                lcdPicked = false;
            }
        }

        private void ImageSplit_PreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = e.Key == Key.Space;

        private void ImageSplit_TextChanged_Manually(object sender, TextChangedEventArgs e)
        {
            if (!lcdPicked)
            {
                TextBox box = sender as TextBox;
                //int trimmedTextLength = box.Text.TrimStart('0').Length;
                //if (trimmedTextLength > 1)
                //{
                //    box.Text = "9";
                //    box.CaretIndex = 1;
                //}
                //else if (trimmedTextLength != box.Text.Length)
                //{
                //    int removeLength = (box.Text.Length - 1).Clamp(0, 1);
                //    box.Text = box.Text.Substring(removeLength);
                //    box.CaretIndex = e.Changes.Last().Offset - removeLength + e.Changes.Last().AddedLength;
                //}

                //if (lcdButtons != null)
                //{
                //    foreach (var btn in lcdButtons)
                //    {
                //        btn.Key.IsChecked = false;
                //    }
                //    ImageWidthSetting.Foreground = Brushes.White;
                //    ImageHeightSetting.Foreground = Brushes.White;
                //}
                if (box.Text.Length > 1)
                {
                    box.Text = "9";
                    box.CaretIndex = 1;
                }

                //DoInstantChangeTimed(true, 0);
            }
        }

        private void ImageSplit_Pasting(object sender, DataObjectPastingEventArgs e)
        {

            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                string text = (string)e.SourceDataObject.GetData(typeof(string));
                if (!IsNumeric(text))
                {
                    e.CancelCommand();
                }
                else
                {
                    lcdPicked = false;
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        public void ImageSplit_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            TextBox thisTextBox = sender as TextBox;

            if (!string.IsNullOrEmpty(thisTextBox.Text))
            {
                lcdPicked = false;
                int num = int.Parse(thisTextBox.Text);
                int changeDirection = e.Delta > 0 ? 1 : -1;
                thisTextBox.Text = (num + changeDirection).Clamp(1, 9).ToString();
            }
        }
    }
}
