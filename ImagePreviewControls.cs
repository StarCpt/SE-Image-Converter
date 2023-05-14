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
using Bitmap = System.Drawing.Bitmap;
using Size = System.Drawing.Size;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;
using Point = System.Windows.Point;
using System.Timers;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.ComponentModel;
using BitDepth = ImageConverterPlus.ImageConverter.BitDepth;
using ImageConverterPlus.ImageConverter;
using System.Threading;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Windows.Threading;

namespace ImageConverterPlus
{
    partial class MainWindow
    {
        private Point origin;
        private Point start;

        private const int PreviewBorderWidth = 350;
        private const int PreviewBorderHeight = 350;
        private Point PreviewTopLeft;
        public static double imagePreviewScale { get; private set; } = 1f;

        private Size ImageSplitSize => viewModel.ImageSplitSize;

        private System.Timers.Timer PreviewConvertTimer;

        private Dictionary<ToggleButton, System.Drawing.Point> splitCtrlBtns = new Dictionary<ToggleButton, System.Drawing.Point>();
        private System.Drawing.Point checkedSplitBtnPos = System.Drawing.Point.Empty;

        private ContextMenu PreviewGridMenu;

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
            //ImagePreviewBorder.Visibility = Visibility.Visible;

            var st = GetScaleTransform(ImagePreview);
            st.ScaleX = 1d;
            st.ScaleY = 1d;

            var tt = GetTranslateTransform(ImagePreview);
            tt.X = 0d;
            tt.Y = 0d;

            imagePreviewScale = 1.0d;

            if (update)
            {
                UpdatePreviewDelayed(false, 0);
            }
        }

        public void ResetPreviewSplit(object sender, RoutedEventArgs e) => viewModel.ImageSplitSize = new Size(1, 1);

        private void UpdatePreview(System.Drawing.Image imageToConvert, Size convertedSize, int bitsPerColor, InterpolationMode interpolationMode, Action<BitmapImage> callback)
        {
            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                PreviewConvertCancellationTokenSource?.Cancel();
            }

            //scale the bitmap size to the lcd size

            double imageToLcdWidthRatio = (double)imageToConvert.Width / convertedSize.Width;
            double imageToLcdHeightRatio = (double)imageToConvert.Height / convertedSize.Height;

            //get the bigger ratio taking into account the image split
            double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / ImageSplitSize.Width, imageToLcdHeightRatio / ImageSplitSize.Height);

            double scaledImageWidth = imageToConvert.Width / biggerImageToLcdRatio;
            double scaledImageHeight = imageToConvert.Height / biggerImageToLcdRatio;

            //apply preview scale (zoom)
            scaledImageWidth *= imagePreviewScale;
            scaledImageHeight *= imagePreviewScale;

            //turn the size from above into lcd width/height % ratio
            double scaledImageToLcdWidthRatio = scaledImageWidth / convertedSize.Width;
            double scaledImageToLcdHeightRatio = scaledImageHeight / convertedSize.Height;

            double biggerScaledImageToLcdRatio = Math.Max(scaledImageToLcdWidthRatio, scaledImageToLcdHeightRatio);

            ConvertOptions options = new ConvertOptions
            {
                Dithering = viewModel.EnableDithering,
                Interpolation = interpolationMode,
                BitsPerChannel = bitsPerColor,
                ConvertedSize = new Size(
                    Convert.ToInt32(scaledImageWidth),
                    Convert.ToInt32(scaledImageHeight)),
                Scale = 1,
            };
            PreviewConvertCancellationTokenSource = new CancellationTokenSource();
            var previewConverter = new PreviewConvertThread(imageToConvert, options, callback, PreviewConvertCancellationTokenSource.Token);
            PreviewConvertTask = Task.Run(previewConverter.ConvertPreviewNew);
        }

        public void UpdatePreviewDelayed(bool resetZoom, ushort delay)
        {
            if (resetZoom)
            {
                ResetPreviewZoomAndPan(false);
            }

            if (ImageCache.Image == null ||
                !TryGetInterpolationMode(out InterpolationMode interpolation) ||
                !TryGetColorBitDepth(out BitDepth depth))
            {
                return;
            }

            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                PreviewConvertCancellationTokenSource.Cancel();
            }

            if (PreviewConvertTimer != null)
            {
                PreviewConvertTimer.Stop();
                PreviewConvertTimer.Dispose();
                PreviewConvertTimer = null;
            }

            if (delay == 0)
            {
                UpdatePreview(ImageCache.Image, GetLCDSize(), (int)depth, interpolation, PreviewConvertResultCallback);
                return;
            }

            PreviewConvertTimer = new System.Timers.Timer(delay)
            {
                Enabled = true,
                AutoReset = false,
            };
            PreviewConvertTimer.Elapsed +=
                (object sender, ElapsedEventArgs e) =>
                ImagePreview.Dispatcher.Invoke(
                    () => UpdatePreview(ImageCache.Image, GetLCDSize(), (int)depth, interpolation, PreviewConvertResultCallback));
            PreviewConvertTimer.Start();
        }

        public void UpdatePreviewTopLeft(object sender, SizeChangedEventArgs e)
        {
            PreviewTopLeft = new Point((-ImagePreviewBorder.ActualWidth + ImagePreview.ActualWidth) / 2, (-ImagePreviewBorder.ActualHeight + ImagePreview.ActualHeight) / 2);
        }

        public void Preview_OnMouseWheelChanged(object sender, MouseWheelEventArgs e)
        {
            if (viewModel.ShowPreviewGrid)
            {
                return;
            }

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



            st.ScaleX = Math.Clamp(st.ScaleX + zoom, 0.4, 10);
            st.ScaleY = Math.Clamp(st.ScaleY + zoom, 0.4, 10);

            imagePreviewScale = st.ScaleX;

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

            imagePreviewScale = zoom;

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
                Vector vec = start - e.GetPosition(ImagePreviewBorder);

                tt.X = (origin.X - vec.X).ClampDoubleExt(PreviewTopLeft.X, PreviewTopLeft.X + (ImagePreviewBorder.ActualWidth - ImagePreview.ActualWidth * imagePreviewScale));
                tt.Y = (origin.Y - vec.Y).ClampDoubleExt(PreviewTopLeft.Y, PreviewTopLeft.Y + (ImagePreviewBorder.ActualHeight - ImagePreview.ActualHeight * imagePreviewScale));
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
                CopyToClipBtn.IsEnabled = true;

                Size lcdSize = GetLCDSize();
                if (lcdSize.Width * ImageSplitSize.Width > lcdSize.Height * ImageSplitSize.Height)
                {
                    ImagePreviewBorder.Width = PreviewBorderWidth;
                    ImagePreviewBorder.Height = PreviewBorderHeight * (((double)lcdSize.Height * ImageSplitSize.Height) / ((double)lcdSize.Width * ImageSplitSize.Width));
                }
                else
                {
                    ImagePreviewBorder.Width = PreviewBorderWidth * (((double)lcdSize.Width * ImageSplitSize.Width) / ((double)lcdSize.Height * ImageSplitSize.Height));
                    ImagePreviewBorder.Height = PreviewBorderHeight;
                }

                ImagePreviewBorder.Visibility = Visibility.Visible;
                ImagePreviewTextBlock.Visibility = Visibility.Hidden;
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
            ImagePreviewTextBlock.Visibility = Visibility.Visible;
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
                        UpdateBrowseImagesBtn(Path.GetFileName(file), file);
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
                if (TryConvertImageThreaded(new ImageInfo(bitImage, "Drag & Droped Image Bitmap", false), true, ConvertResultCallback, PreviewConvertResultCallback))
                {
                    UpdateBrowseImagesBtn("Drag & Droped Image", string.Empty);
                    Logging.Log("Image Drag & Dropped (Bitmap)");
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                WebHelpers.HandleHtmlDropThreadAsync(e.Data);
            }
            else
            {
                ShowAcrylDialog("Clipboard does not contain any images");
            }
        }

        public void PreviewConvertResultCallback(BitmapImage resultPreviewImg) => ChangePreviewThreadSafe(resultPreviewImg);

        public void ZoomToFit()
        {
            if (imagePreviewScale != 1.0d)
            {
                SetPreviewZoom(1.0d);
            }
        }

        public void ZoomToFill()
        {
            if (ImageCache.Image != null)
            {
                Size lcdSize = GetLCDSize();
                double imageXRatio = (double)ImageCache.Image.Width / (lcdSize.Width * ImageSplitSize.Width);
                double imageYRatio = (double)ImageCache.Image.Height / (lcdSize.Height * ImageSplitSize.Height);
                double zoom = imageXRatio > imageYRatio ? imageXRatio / imageYRatio : imageYRatio / imageXRatio;
                if (imagePreviewScale != zoom)
                {
                    SetPreviewZoom(zoom);
                }
            }
        }

        [Obsolete]
        private Size GetImageSplitSize()
        {
            return viewModel.ImageSplitSize;
        }

        private void UpdatePreviewGrid(object sender, SizeChangedEventArgs e) => UpdatePreviewGrid();

        private void UpdatePreviewGrid()
        {
            Size lcdSize = GetLCDSize();

            int splitX = viewModel.ImageSplitWidth;
            int splitY = viewModel.ImageSplitHeight;

            if (lcdSize.Width * splitX > lcdSize.Height * splitY)
            {
                ImagePreviewBorder.Width = PreviewBorderWidth;
                ImagePreviewBorder.Height = PreviewBorderHeight * (((double)lcdSize.Height * splitY) / ((double)lcdSize.Width * splitX));
            }
            else
            {
                ImagePreviewBorder.Width = PreviewBorderWidth * (((double)lcdSize.Width * splitX) / ((double)lcdSize.Height * splitY));
                ImagePreviewBorder.Height = PreviewBorderHeight;
            }

            PreviewGrid.Children.Clear();
            splitCtrlBtns.Clear();

            checkedSplitBtnPos = System.Drawing.Point.Empty;
            bool firstBtn = true;

            if (PreviewGridMenu == null)
            {
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
                MenuItem menuItemConvertFromClip = new MenuItem
                {
                    Style = (Style)FindResource("CustomMenuItem"),
                    Header = "Convert From Clipboard",
                };
                menuItemConvertFromClip.Click += PasteFromClipboard;
                imgSplitMenu.Items.Add(menuItemConvertFromClip);
                MenuItem menuItemResetSplit = new MenuItem
                {
                    Style = (Style)FindResource("CustomMenuItem"),
                    Header = "Reset Image Split",
                };
                menuItemResetSplit.Click += ResetPreviewSplit;
                imgSplitMenu.Items.Add(menuItemResetSplit);

                PreviewGridMenu = imgSplitMenu;
            }

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
                    btn.ContextMenu = PreviewGridMenu;
                    column.Children.Add(btn);
                    splitCtrlBtns.Add(btn, new System.Drawing.Point(x, y));
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

                if (ImageCache.Image == null || !TryConvertImageThreaded(ImageCache, false, ConvertCallbackCopyToClip, PreviewConvertResultCallback))
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
