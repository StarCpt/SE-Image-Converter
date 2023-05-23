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
using System.IO;
using Bitmap = System.Drawing.Bitmap;
using Size = System.Drawing.Size;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;
using Point = System.Windows.Point;
using System.Timers;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using ImageConverterPlus.ImageConverter;
using System.Threading;
using System.Windows.Threading;

namespace ImageConverterPlus
{
    partial class MainWindow
    {
        //private const int PreviewContainerWidth = 350;
        //private const int PreviewContainerHeight = 350;

        private double PreviewContainerGridSize => Math.Min(PreviewContainerGrid.ActualWidth, PreviewContainerGrid.ActualHeight);

        private Size ImageSplitSize => viewModel.ImageSplitSize;

        private System.Drawing.Point checkedSplitBtnPos = System.Drawing.Point.Empty;

        private ContextMenu PreviewGridMenu;

        public void InitImagePreview()
        {
            previewNew.ScaleChanged += PreviewNew_ScaleChanged;
            UpdatePreviewGrid();
        }

        public void ResetPreviewSplit(object sender, RoutedEventArgs e) => viewModel.ImageSplitSize = new Size(1, 1);

        private void UpdatePreview(System.Drawing.Image imageToConvert, Size convertedSize, int bitsPerColor, InterpolationMode interpolationMode, Action<Bitmap> callback)
        {
            if (PreviewConvertTask != null && !PreviewConvertTask.IsCompleted)
            {
                PreviewConvertCancellationTokenSource?.Cancel();
            }

            //scale the bitmap size to the lcd size

            Monitor.Enter(imageToConvert);

            double imageToLcdWidthRatio = (double)imageToConvert.Width / convertedSize.Width;
            double imageToLcdHeightRatio = (double)imageToConvert.Height / convertedSize.Height;

            //get the bigger ratio taking into account the image split
            double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / ImageSplitSize.Width, imageToLcdHeightRatio / ImageSplitSize.Height);

            double scaledImageWidth = imageToConvert.Width / biggerImageToLcdRatio;
            double scaledImageHeight = imageToConvert.Height / biggerImageToLcdRatio;

            Monitor.Exit(imageToConvert);

            //apply preview scale (zoom)
            scaledImageWidth *= previewNew.Scale;
            scaledImageHeight *= previewNew.Scale;

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
            PreviewConvertTask = Task.Run(() => ConvertManager.ConvertToBitmap(imageToConvert, options, callback, PreviewConvertCancellationTokenSource.Token));
        }

        public void UpdatePreviewDelayed(bool resetZoom, double delay)
        {
            if (resetZoom)
            {
                ResetZoomAndPan();
            }

            if (ImageCache?.Image == null)
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
                UpdatePreview(ImageCache.Image, GetLCDSize(), (int)viewModel.ColorDepth, viewModel.InterpolationMode, PreviewConvertResultCallback);
                return;
            }

            PreviewConvertTimer = new System.Timers.Timer(delay)
            {
                Enabled = true,
                AutoReset = false,
            };
            PreviewConvertTimer.Elapsed +=
                (object? sender, ElapsedEventArgs e) =>
                previewNew.Dispatcher.Invoke(
                    () => UpdatePreview(ImageCache.Image, GetLCDSize(), (int)viewModel.ColorDepth, viewModel.InterpolationMode, PreviewConvertResultCallback));
            PreviewConvertTimer.Start();
        }

        private void PreviewNew_ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePreviewDelayed(false, previewNew.animationDuration.TotalMilliseconds);
        }

        private void UpdatePreviewSourceThreadSafe(BitmapImage image)
        {
            image.Freeze();
            previewNew.Dispatcher.Invoke(() =>
            {
                viewModel.PreviewImageSource = image;
                CopyToClipBtn.IsEnabled = true;

                Size lcdSize = GetLCDSize();
                if (lcdSize.Width * ImageSplitSize.Width > lcdSize.Height * ImageSplitSize.Height)
                {
                    previewNew.Width = PreviewContainerGridSize;
                    previewNew.Height = PreviewContainerGridSize * (((double)lcdSize.Height * ImageSplitSize.Height) / ((double)lcdSize.Width * ImageSplitSize.Width));
                }
                else
                {
                    previewNew.Width = PreviewContainerGridSize * (((double)lcdSize.Width * ImageSplitSize.Width) / ((double)lcdSize.Height * ImageSplitSize.Height));
                    previewNew.Height = PreviewContainerGridSize;
                }

                previewNew.Visibility = Visibility.Visible;
                ImagePreviewTextBlock.Visibility = Visibility.Hidden;
            });
        }

        private void RemovePreview(object sender, RoutedEventArgs e) => RemovePreview();

        private void RemovePreview()
        {
            viewModel.PreviewImageSource = null;
            ConvertedImageStr = string.Empty;
            CopyToClipBtn.IsEnabled = false;
            ResetZoomAndPan();

            previewNew.Visibility = Visibility.Hidden;
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
                ResetZoomAndPan();
                ImageCache = new ImageInfo(bitImage, "Drag & Droped Image Bitmap");
                if (TryConvertImageThreaded(ImageCache.Image, ConvertResultCallback, PreviewConvertResultCallback))
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

        public void PreviewConvertResultCallback(Bitmap resultPreviewImg) => UpdatePreviewSourceThreadSafe(Helpers.BitmapToBitmapImage(resultPreviewImg));

        public void ZoomToFit()
        {
            if (ImageCache?.Image != null)
            {
                double scaleOld = previewNew.Scale;
                double scaleChange = -scaleOld + 1;
                previewNew.SetScaleAnimated(1.0, previewNew.animationDuration);

                Monitor.Enter(ImageCache.Image);
                
                double imageToContainerWidthRatio = ImageCache.Image.Width / previewNew.ActualWidth;
                double imageToContainerHeightRatio = ImageCache.Image.Height / previewNew.ActualHeight;
                
                //get the bigger ratio taking into account the image split
                double biggerImageToContainerRatio = Math.Max(imageToContainerWidthRatio, imageToContainerHeightRatio);
                
                double scaledImageWidth = ImageCache.Image.Width / biggerImageToContainerRatio;
                double scaledImageHeight = ImageCache.Image.Height / biggerImageToContainerRatio;
                
                Monitor.Exit(ImageCache.Image);

                Point offsetTo;
                if (scaledImageWidth < scaledImageHeight)
                    offsetTo = new Point(previewNew.Offset.X - scaledImageWidth / 2 * scaleChange, previewNew.Offset.Y);
                else
                    offsetTo = new Point(previewNew.Offset.X, previewNew.Offset.Y - scaledImageHeight / 2 * scaleChange);
                
                previewNew.SetOffsetAnimated(previewNew.ClampOffset(offsetTo, previewNew.Scale), previewNew.animationDuration);
            }
        }

        public void ZoomToFill()
        {
            if (ImageCache?.Image != null)
            {
                Monitor.Enter(ImageCache.Image);

                double imageToLCDWidthRatio = (double)ImageCache.Image.Width / viewModel.LCDWidth * viewModel.ImageSplitWidth;
                double imageToLCDHeightRatio = (double)ImageCache.Image.Height / viewModel.LCDHeight * viewModel.ImageSplitHeight;
                double minRatio = Math.Min(imageToLCDWidthRatio, imageToLCDHeightRatio);

                var convertedImageSize = new System.Windows.Size(ImageCache.Image.Width / minRatio, ImageCache.Image.Height / minRatio);
                convertedImageSize = convertedImageSize.Round();

                Monitor.Exit(ImageCache.Image);

                double imageToContainerWidthRatio = convertedImageSize.Width / previewNew.ActualWidth;
                double imageToContainerHeightRatio = convertedImageSize.Height / previewNew.ActualHeight;

                double scale =
                    imageToContainerWidthRatio > imageToContainerHeightRatio ?
                    imageToContainerWidthRatio / imageToContainerHeightRatio :
                    imageToContainerHeightRatio / imageToContainerWidthRatio;

                double scaleOld = previewNew.Scale;
                double scaleChange = -scaleOld + scale;

                Point offsetTo;
                if (imageToContainerWidthRatio > imageToContainerHeightRatio)
                    offsetTo = new Point(previewNew.Offset.X - previewNew.ActualWidth / 2 * scaleChange, previewNew.Offset.Y);
                else
                    offsetTo = new Point(previewNew.Offset.X, previewNew.Offset.Y - previewNew.ActualHeight / 2 * scaleChange);

                previewNew.SetScaleAnimated(scale, previewNew.animationDuration);
                previewNew.SetOffsetAnimated(previewNew.ClampOffset(offsetTo, previewNew.Scale), previewNew.animationDuration);
            }
        }

        public void ResetZoomAndPan()
        {
            if (ImageCache?.Image != null)
            {
                previewNew.SetScaleAnimated(1.0, previewNew.animationDuration);

                Monitor.Enter(ImageCache.Image);

                double imageToContainerWidthRatio = ImageCache.Image.Width / previewNew.ActualWidth;
                double imageToContainerHeightRatio = ImageCache.Image.Height / previewNew.ActualHeight;

                //get the bigger ratio taking into account the image split
                double biggerImageToContainerRatio = Math.Max(imageToContainerWidthRatio, imageToContainerHeightRatio);

                double scaledImageWidth = ImageCache.Image.Width / biggerImageToContainerRatio;
                double scaledImageHeight = ImageCache.Image.Height / biggerImageToContainerRatio;

                Monitor.Exit(ImageCache.Image);

                Point offsetTo = new Point(
                    (previewNew.ActualWidth - scaledImageWidth) / 2,
                    (previewNew.ActualHeight - scaledImageHeight) / 2);

                previewNew.SetOffsetAnimated(offsetTo, previewNew.animationDuration);
            }
        }

        private void UpdatePreviewGrid()
        {
            Size lcdSize = GetLCDSize();

            int splitX = viewModel.ImageSplitWidth;
            int splitY = viewModel.ImageSplitHeight;

            if (lcdSize.Width * splitX > lcdSize.Height * splitY)
            {
                previewNew.Width = PreviewContainerGridSize;
                previewNew.Height = PreviewContainerGridSize * ((double)(lcdSize.Height * splitY) / (lcdSize.Width * splitX));
            }
            else
            {
                previewNew.Width = PreviewContainerGridSize * ((double)(lcdSize.Width * splitX) / (lcdSize.Height * splitY));
                previewNew.Height = PreviewContainerGridSize;
            }

            PreviewGrid.Children.Clear();

            checkedSplitBtnPos = System.Drawing.Point.Empty;

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
                for (int y = 0; y < ImageSplitSize.Height; y++)
                {
                    ToggleButton btn = new ToggleButton
                    {
                        Style = (Style)FindResource("PreviewSplitBtn"),
                        Tag = new System.Drawing.Point(x, y),
                        IsChecked = x == 0 && y == 0,
                        ContextMenu = PreviewGridMenu,
                    };
                    Grid.SetColumn(btn, x);
                    Grid.SetRow(btn, y);
                    btn.Click += SplitCtrlBtn_Click;
                    PreviewGrid.Children.Add(btn);
                }
            }
        }

        private void SplitCtrlBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = (ToggleButton)sender;
            foreach (var tb in PreviewGrid.Children.OfType<ToggleButton>())
            {
                tb.IsChecked = tb == btn;
            }
            checkedSplitBtnPos = (System.Drawing.Point)btn.Tag;
        }

        private void PreviewGridCopyToClip(object sender, RoutedEventArgs e)
        {
            ToggleButton openedOver = (ToggleButton)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget;
            if (openedOver != null && PreviewGrid.Children.Contains(openedOver))
            {
                foreach (var tb in PreviewGrid.Children.OfType<ToggleButton>())
                {
                    tb.IsChecked = tb == openedOver;
                }
                checkedSplitBtnPos = (System.Drawing.Point)openedOver.Tag;

                if (ImageCache.Image == null || !TryConvertImageThreaded(ImageCache.Image, ConvertCallbackCopyToClip, PreviewConvertResultCallback))
                {
                    ShowAcrylDialog($"Convert an image first!");
                }
            }
            else
            {
                ShowAcrylDialog("Could not find square menu was opened over!");
            }
        }

        private void PreviewContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Size lcdSize = GetLCDSize();
            if (lcdSize.Width * ImageSplitSize.Width > lcdSize.Height * ImageSplitSize.Height)
            {
                previewNew.Width = PreviewContainerGridSize;
                previewNew.Height = PreviewContainerGridSize * (((double)lcdSize.Height * ImageSplitSize.Height) / ((double)lcdSize.Width * ImageSplitSize.Width));
            }
            else
            {
                previewNew.Width = PreviewContainerGridSize * (((double)lcdSize.Width * ImageSplitSize.Width) / ((double)lcdSize.Height * ImageSplitSize.Height));
                previewNew.Height = PreviewContainerGridSize;
            }

            if (e.PreviousSize.Width != 0 && e.PreviousSize.Height != 0)
            {
                previewNew.Offset = new Point(
                    previewNew.Offset.X * (e.NewSize.Width / e.PreviousSize.Width),
                    previewNew.Offset.Y * (e.NewSize.Height / e.PreviousSize.Height));
            }

            ImagePreviewBackground.Width = PreviewContainerGridSize;
            ImagePreviewBackground.Height = PreviewContainerGridSize;
        }
    }
}
