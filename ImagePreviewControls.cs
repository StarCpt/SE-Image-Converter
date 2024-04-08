﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using System.Windows.Controls.Primitives;
using ImageConverterPlus.Data;

namespace ImageConverterPlus
{
    partial class MainWindow
    {
        private double PreviewContainerGridSize => Math.Min(PreviewContainerGrid.ActualWidth, PreviewContainerGrid.ActualHeight);

        private void Preview_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (TryGetImageInfo(file, out Bitmap? result) && result is not null)
                    {
                        convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(result, true);
                        convMgr.ImageSplitSize = new Int32Size(1, 1);
                        convMgr.ProcessImage(delegate
                        {
                            ResetZoomAndPan(false);
                            UpdateBrowseImagesBtn(System.IO.Path.GetFileName(file), file);
                            App.Log.Log("Image Drag & Dropped (FileDrop)");
                        });
                        return;
                    }
                }

                //when file type doesnt match
                ShowAcrylDialog("This file type is not supported!");
            }
            else if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                Bitmap image = (Bitmap)e.Data.GetData(DataFormats.Bitmap);
                convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                convMgr.ProcessImage(delegate
                {
                    ResetZoomAndPan(false);
                    UpdateBrowseImagesBtn("Drag & Droped Image", string.Empty);
                    App.Log.Log("Image Drag & Dropped (Bitmap)");
                });
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                _ = WebHelpers.HandleHtmlDropThreadAsync(e.Data, convMgr);
            }
            else
            {
                ShowAcrylDialog("Clipboard does not contain any images");
            }
        }

        public void ZoomToFit()
        {
            if (convMgr.SourceImageSize is Int32Size imgSize)
            {
                double scaleOld = previewNew.Scale;
                double scaleChange = -scaleOld + 1;
                previewNew.SetScaleAnimated(1.0, previewNew.animationDuration);
                
                double imageToContainerWidthRatio = imgSize.Width / previewNew.ActualWidth;
                double imageToContainerHeightRatio = imgSize.Height / previewNew.ActualHeight;
                
                //get the bigger ratio taking into account the image split
                double biggerImageToContainerRatio = Math.Max(imageToContainerWidthRatio, imageToContainerHeightRatio);
                
                double scaledImageWidth = imgSize.Width / biggerImageToContainerRatio;
                double scaledImageHeight = imgSize.Height / biggerImageToContainerRatio;

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
            if (convMgr.SourceImageSize is Int32Size imgSize)
            {
                double imageToLCDWidthRatio = (double)imgSize.Width / convMgr.ConvertedSize.Width * convMgr.ImageSplitSize.Width;
                double imageToLCDHeightRatio = (double)imgSize.Height / convMgr.ConvertedSize.Height * convMgr.ImageSplitSize.Height;
                double minRatio = Math.Min(imageToLCDWidthRatio, imageToLCDHeightRatio);

                var convertedImageSize = new Size(imgSize.Width / minRatio, imgSize.Height / minRatio);
                convertedImageSize = convertedImageSize.Round();

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

        public void ResetZoomAndPan(bool animate)
        {
            if (convMgr.SourceImageSize is Int32Size imgSize)
            {
                if (animate)
                    previewNew.SetScaleAnimated(1.0, previewNew.animationDuration);
                else
                    previewNew.SetScaleNoAnim(1.0);

                double imageToContainerWidthRatio = imgSize.Width / previewNew.ActualWidth;
                double imageToContainerHeightRatio = imgSize.Height / previewNew.ActualHeight;

                //get the bigger ratio taking into account the image split
                double biggerImageToContainerRatio = Math.Max(imageToContainerWidthRatio, imageToContainerHeightRatio);

                double scaledImageWidth = imgSize.Width / biggerImageToContainerRatio;
                double scaledImageHeight = imgSize.Height / biggerImageToContainerRatio;

                Point offsetTo = new Point(
                    (previewNew.ActualWidth - scaledImageWidth) / 2,
                    (previewNew.ActualHeight - scaledImageHeight) / 2);

                if (animate)
                    previewNew.SetOffsetAnimated(offsetTo, previewNew.animationDuration);
                else
                    previewNew.SetOffsetNoAnim(offsetTo);
            }
        }

        private void UpdatePreviewGrid()
        {
            UpdatePreviewContainerSize();

            PreviewGrid.Children.Clear();

            convMgr.SelectedSplitPos = new Int32Point(0, 0);

            ContextMenu imgSplitMenu = new ContextMenu();
            MenuItem menuItemCopyToClip = new MenuItem
            {
                Header = "Copy to Clipboard",
            };
            menuItemCopyToClip.Click += PreviewGridCopyToClip;
            imgSplitMenu.Items.Add(menuItemCopyToClip);
            MenuItem menuItemConvertFromClip = new MenuItem
            {
                Header = "Convert From Clipboard",
            };
            menuItemConvertFromClip.Click += (sender, e) => PasteFromClipboard();
            imgSplitMenu.Items.Add(menuItemConvertFromClip);
            MenuItem menuItemResetSplit = new MenuItem
            {
                Header = "Reset Image Split",
            };
            menuItemResetSplit.Click += delegate { convMgr.ImageSplitSize = new Int32Size(1, 1); };
            imgSplitMenu.Items.Add(menuItemResetSplit);

            for (int x = 0; x < convMgr.ImageSplitSize.Width; x++)
            {
                for (int y = 0; y < convMgr.ImageSplitSize.Height; y++)
                {
                    ToggleButton btn = new ToggleButton
                    {
                        Style = (Style)FindResource("PreviewSplitBtn"),
                        Tag = new Int32Point(x, y),
                        IsChecked = x == 0 && y == 0,
                        ContextMenu = imgSplitMenu,
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
            convMgr.SelectedSplitPos = (Int32Point)btn.Tag;
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
                convMgr.SelectedSplitPos = (Int32Point)openedOver.Tag;
                convMgr.ConvertImage(lcdStr =>
                {
                    if (lcdStr != null)
                        SetClipboardDelayed(lcdStr, 150);
                    else
                        ConversionFailedDialog();
                });
            }
            else
            {
                ShowAcrylDialog("Could not find square menu was opened over!");
            }
        }

        private void PreviewContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewContainerSize();

            ImagePreviewBackground.Width = PreviewContainerGridSize;
            ImagePreviewBackground.Height = PreviewContainerGridSize;
        }
    }
}
