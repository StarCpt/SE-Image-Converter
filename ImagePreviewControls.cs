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
using Point = System.Windows.Point;
using System.Timers;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace SEImageToLCD_15BitColor
{
    partial class MainWindow
    {
        private Point origin;
        private Point start;

        private int PreviewBorderWidth;
        private int PreviewBorderHeight;
        private Point PreviewTopLeft;
        public static float imagePreviewZoom = 1f;

        private Size ImageSplitSize = new Size(1, 1);

        private Timer PreviewConvertTimer;

        private Dictionary<ToggleButton, int[]> splitCtrlBtns = new Dictionary<ToggleButton, int[]>();
        private int[] checkedSplitBtnPos = new int[] { 0, 0 };

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

            ImagePreviewBorder.SizeChanged += UpdatePreviewGrid;

            PreviewGrid.Visibility = Visibility.Hidden;
            ShowSplitGridBtn.IsChecked = false;

            PreviewBorderWidth = 350;
            PreviewBorderHeight = 350;

            TransformGroup group = new TransformGroup();
            ScaleTransform st = new ScaleTransform();
            group.Children.Add(st);
            TranslateTransform tt = new TranslateTransform();
            group.Children.Add(tt);
            ImagePreview.RenderTransform = group;
            ImagePreview.RenderTransformOrigin = new Point(0.0, 0.0);
        }

        public void ResetPreviewZoomAndPan(bool update)
        {
            ImagePreviewBorder.Visibility = Visibility.Visible;

            var st = GetScaleTransform(ImagePreview);
            st.ScaleX = 1d;
            st.ScaleY = 1d;

            var tt = GetTranslateTransform(ImagePreview);
            tt.X = 0d;
            tt.Y = 0d;

            imagePreviewZoom = 1f;

            if (update)
            {
                UpdatePreviewDelayed(false, 0);
            }
        }

        public void ResetPreviewSplit(object sender, RoutedEventArgs e) => ResetImageSplit();

        public void ResetImageSplit()
        {
            if (init)
            {
                ImageSplit_X.Text = "1";
                ImageSplit_Y.Text = "1";

                ImageSplitSize = new Size(1, 1);

                //UpdatePreviewDelayed(0);
                //UpdatePreviewGrid();
            }
        }

        public void UpdatePreview(ImageInfo image, Size lcdSize, InterpolationMode interpolationMode, ConvertThread.BitDepth colorDepth, ConvertThread.DitherMode ditherMode, PreviewConvertCallback previewConvertCallback)
        {
            float scale = Math.Min((float)lcdSize.Width * ImageSplitSize.Width / image.Image.Size.Width, (float)lcdSize.Height * ImageSplitSize.Height / image.Image.Size.Height);
            scale *= imagePreviewZoom;

            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                PreviewConvertThread.CancelTask();
            }

            PreviewConvertThread = new PreviewConvertThread(image.Image, ditherMode, colorDepth, interpolationMode, previewConvertCallback, scale);
            PreviewConvertTask = Task.Run(PreviewConvertThread.ConvertPreviewThreadedFast);
        }

        public void UpdatePreviewDelayed(bool resetZoom, ushort delay)
        {
            if (resetZoom)
            {
                ResetPreviewZoomAndPan(false);
            }

            if (ImageCache.Image == null ||
                !TryGetLCDSize(out Size? size) ||
                !TryGetInterpolationMode(out InterpolationMode interpolation) ||
                !TryGetColorBitDepth(out ConvertThread.BitDepth depth) ||
                !TryGetDitherMode(out ConvertThread.DitherMode dither))
            {
                return;
            }
            float scale = Math.Min((float)size.Value.Width * ImageSplitSize.Width / ImageCache.Image.Size.Width, (float)size.Value.Height * ImageSplitSize.Height / ImageCache.Image.Size.Height);
            scale *= imagePreviewZoom;

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

            double zoomAmount = 0.2;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                zoomAmount = 0.02;
            }

            double zoom = e.Delta > 0 ? zoomAmount : -zoomAmount;

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

            imagePreviewZoom = (float)st.ScaleX;

            tt.X = (absX - relative.X * st.ScaleX).ClampDoubleExt(PreviewTopLeft.X, PreviewTopLeft.X + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * st.ScaleX));
            tt.Y = (absY - relative.Y * st.ScaleY).ClampDoubleExt(PreviewTopLeft.Y, PreviewTopLeft.Y + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * st.ScaleY));

            start = e.GetPosition(ImagePreviewBorder);
            origin = new Point(tt.X, tt.Y);

            UpdatePreviewDelayed(false, 100);

            e.Handled = true;
        }

        private void SetPreviewZoom(double zoom)
        {
            ScaleTransform st = GetScaleTransform(ImagePreview);
            TranslateTransform tt = GetTranslateTransform(ImagePreview);

            st.ScaleX = zoom;
            st.ScaleY = zoom;

            imagePreviewZoom = (float)zoom;

            UpdatePreviewDelayed(false, 0);

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

        private void ChangePreviewThreadSafe(BitmapImage image)
        {
            image.Freeze();
            ImagePreview.Dispatcher.Invoke(() =>
            {
                ImagePreview.Source = image;

                if (TryGetLCDSize(out Size? lcdSize))
                {
                    if (lcdSize.Value.Width * ImageSplitSize.Width > lcdSize.Value.Height * ImageSplitSize.Height)
                    {
                        ImagePreviewBorder.Width = PreviewBorderWidth;
                        ImagePreviewBorder.Height = PreviewBorderHeight * (((double)lcdSize.Value.Height * ImageSplitSize.Height) / ((double)lcdSize.Value.Width * ImageSplitSize.Width));
                    }
                    else
                    {
                        ImagePreviewBorder.Width = PreviewBorderWidth * (((double)lcdSize.Value.Width * ImageSplitSize.Width) / ((double)lcdSize.Value.Height * ImageSplitSize.Height));
                        ImagePreviewBorder.Height = PreviewBorderHeight;
                    }
                }

                ImagePreviewBorder.Visibility = Visibility.Visible;
                ImagePreviewLabel.Visibility = Visibility.Hidden;
            });
        }

        private void RemovePreview(object sender, RoutedEventArgs e) => RemovePreview();

        private void RemovePreview()
        {
            ImagePreview.Source = null;
            ConvertedImageStr = string.Empty;
            CopyToClipBtn.IsEnabled = false;
            ResetPreviewZoomAndPan(false);

            ImagePreviewBorder.Visibility = Visibility.Hidden;
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
                    UpdateBrowseImagesBtn("Drag & Droped Image", string.Empty);
                    UpdateCurrentConvertBtnToolTip("Drag & Droped Image", true);
                    Logging.Log("Image Drag & Dropped (Bitmap)");
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                HandleHtmlDropThreadAsync(e.Data);
            }
            else
            {
                ShowAcrylDialog("Clipboard does not contain any images");
            }
        }

        private async Task HandleHtmlDropThreadAsync(IDataObject Data)
        {
            if (await UrlContainsImageAsync(WebUtility.HtmlDecode((string)Data.GetData(DataFormats.Text))))
            {
                string url = WebUtility.HtmlDecode((string)Data.GetData(DataFormats.Text));

                Bitmap image = await DownloadImageFromUrlAsync(url);
                if (image != null && TryConvertImageThreaded(new ImageInfo(image, url, false), true, convertCallback, previewConvertCallback))
                {
                    UpdateBrowseImagesBtn("Loaded from URL", url);
                    UpdateCurrentConvertBtnToolTip("Image loaded from image URL", true);
                    Logging.Log($"Image loaded from image URL ({url})");
                }
            }
            else
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml((string)Data.GetData(DataFormats.Html));
                HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");

                if (imgNodes != null && imgNodes.Count > 0)
                {
                    string src = imgNodes[0].GetAttributeValue("src", null);
                    src = WebUtility.HtmlDecode(src);
                    Bitmap image = await DownloadImageFromUrlAsync(src);
                    if (image != null && TryConvertImageThreaded(new ImageInfo(image, src, false), true, convertCallback, previewConvertCallback))
                    {
                        UpdateBrowseImagesBtn("Loaded from HTML", src);
                        UpdateCurrentConvertBtnToolTip("Image loaded from HTML", true);
                        Logging.Log($"Image loaded from HTML ({src})");
                    }
                }
                else
                {
                    ShowAcrylDialog("Dropped html does not contain any image links!");
                }
            }
        }

        private async Task<bool> UrlContainsImageAsync(string url)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "HEAD";
            using (var response = await request.GetResponseAsync())
            {
                return response.ContentType
                    .ToLowerInvariant()
                    .StartsWith("image/");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">Returns null if anything fails for whatever reason</param>
        /// <returns></returns>
        private async Task<Bitmap> DownloadImageFromUrlAsync(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                using (var response = await request.GetResponseAsync())
                {
                    string imageType = response.ContentType
                        .ToLowerInvariant()
                        .Replace("image/", "");
                    if (SupportedFileTypes.Any(t => t.Equals(imageType)))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await response.GetResponseStream().CopyToAsync(ms);
                            ms.Position = 0;

                            if (imageType == "webp")
                            {
                                SixLabors.ImageSharp.Formats.Webp.WebpDecoder webpDecoder = new SixLabors.ImageSharp.Formats.Webp.WebpDecoder();
                                SixLabors.ImageSharp.Image webpImg = webpDecoder.Decode(SixLabors.ImageSharp.Configuration.Default, ms, System.Threading.CancellationToken.None);

                                SixLabors.ImageSharp.Formats.Bmp.BmpEncoder enc = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();

                                using (MemoryStream stream = new MemoryStream())
                                {
                                    await webpImg.SaveAsync(stream, enc);
                                    return new Bitmap(stream);
                                }
                            }
                            else
                            {
                                return new Bitmap(ms);
                            }
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                ShowAcrylDialog("Error occurred while decoding the image! (It might be a video?)");
                return null;
            }
        }

        public delegate void PreviewConvertCallback(BitmapImage resultPreviewImg);
        private void PreviewConvertResultCallback(BitmapImage resultPreviewImg) => ChangePreviewThreadSafe(resultPreviewImg);

        private void ResetZoomBtn_Click(object sender, RoutedEventArgs e) => ResetPreviewZoomAndPan(true);

        private void ZoomToFit_Click(object sender, RoutedEventArgs e)
        {
            if (imagePreviewZoom != 1f)
            {
                SetPreviewZoom(1.0);
            }
        }

        private void ZoomToFill_Click(object sender, RoutedEventArgs e)
        {
            double zoom = ImageCache.Image.Width > ImageCache.Image.Height ? (double)ImageCache.Image.Width / ImageCache.Image.Height : (double)ImageCache.Image.Height / ImageCache.Image.Width;
            if (imagePreviewZoom != zoom)
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
        }

        private void ImageSplit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (init)
            {
                TextBox box = sender as TextBox;

                if (box.Text.Length > 1)
                {
                    box.Text = "9";
                    box.CaretIndex = 1;
                }

                if (InstantChanges)
                {
                    TryGetSplitSize(out ImageSplitSize);
                    UpdatePreviewGrid();
                    ApplyInstantChanges(true, 50);
                }
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
                int num = int.Parse(thisTextBox.Text);
                int changeDirection = e.Delta > 0 ? 1 : -1;
                thisTextBox.Text = (num + changeDirection).Clamp(1, 9).ToString();
            }
        }

        private bool TryGetSplitSize(out Size result)
        {
            try
            {
                int sizeX = int.Parse(ImageSplit_X.Text);
                int sizeY = int.Parse(ImageSplit_Y.Text);
                result = new Size(sizeX, sizeY);
                return true;
            }
            catch
            {
                result = Size.Empty;
                return false;
            }
        }

        private void UpdatePreviewGrid(object sender, SizeChangedEventArgs e) => UpdatePreviewGrid();

        private void UpdatePreviewGrid()
        {
            TryGetSplitSize(out ImageSplitSize);
            PreviewGrid.Width = ImagePreviewBorder.Width;
            PreviewGrid.Height = ImagePreviewBorder.Height;
            PreviewGrid.Children.Clear();
            splitCtrlBtns.Clear();

            checkedSplitBtnPos = new int[] { 0, 0 };
            bool firstBtn = true;

            ContextMenu imgSplitMenu = new ContextMenu
            {
                Style = (Style)FindResource("CustomMenu"),
            };
            MenuItem menuItemCopyToClip = new MenuItem
            {
                Style = (Style)FindResource("CustomMenuItem"),
                Header = "Copy to Clipboard",
            };
            menuItemCopyToClip.Click += PreviewGridCopyToClip;
            imgSplitMenu.Items.Add(menuItemCopyToClip);
            MenuItem menuItemResetSplit = new MenuItem
            {
                Style = (Style)FindResource("CustomMenuItem"),
                Header = "Reset Image Split",
            };
            menuItemResetSplit.Click += ResetPreviewSplit;
            imgSplitMenu.Items.Add(menuItemResetSplit);

            for (int x = 0; x < ImageSplitSize.Width; x++)
            {
                StackPanel column = new StackPanel();
                for (int y = 0; y < ImageSplitSize.Height; y++)
                {
                    ToggleButton btn = new ToggleButton
                    {
                        Name = $"x{x.ToString()}y{y.ToString()}",
                        Style = (Style)FindResource("PreviewSplitBtn"),
                        Width = ImagePreviewBorder.Width / ImageSplitSize.Width,
                        Height = ImagePreviewBorder.Height / ImageSplitSize.Height,
                    };

                    if (firstBtn)
                    {
                        btn.IsChecked = true;
                        firstBtn = false;
                    }
                    btn.Click += SplitCtrlBtn_Click;
                    btn.ContextMenu = imgSplitMenu;
                    column.Children.Add(btn);
                    splitCtrlBtns.Add(btn, new int[]{ x, y });
                }
                PreviewGrid.Children.Add(column);
            }
        }

        private void SplitCtrlBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var btn in splitCtrlBtns)
            {
                if (btn.Key == sender as ToggleButton)
                {
                    btn.Key.IsChecked = true;
                    checkedSplitBtnPos = btn.Value;
                }
                else
                {
                    btn.Key.IsChecked = false;
                }
            }
        }

        private void ShowSplitGrid_Click(object sender, RoutedEventArgs e)
        {
            PreviewGrid.Visibility = (bool)(sender as ToggleButton).IsChecked ? Visibility.Visible : Visibility.Hidden;
        }

        private void PreviewGridCopyToClip(object sender, RoutedEventArgs e)
        {
            ToggleButton openedOver = (ToggleButton)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget;
            if (openedOver != null && splitCtrlBtns.ContainsKey(openedOver))
            {
                foreach (var btn in splitCtrlBtns)
                {
                    if (btn.Key == openedOver)
                    {
                        btn.Key.IsChecked = true;
                        checkedSplitBtnPos = btn.Value;
                    }
                    else
                    {
                        btn.Key.IsChecked = false;
                    }
                }

                if (ImageCache.Image == null || !TryConvertImageThreaded(ImageCache, false, new ConvertCallback(ConvertCallbackCopyToClip), previewConvertCallback))
                {
                    ShowAcrylDialog($"Convert an image first!");
                }
            }
            else
            {
                ShowAcrylDialog("Could not find square menu was opened over!");
            }
        }
    }
}
