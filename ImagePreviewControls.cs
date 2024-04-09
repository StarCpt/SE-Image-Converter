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
using Bitmap = System.Drawing.Bitmap;
using System.Windows.Controls.Primitives;
using ImageConverterPlus.Data;
using ImageConverterPlus.ViewModels;
using System.Windows.Data;
using ImageConverterPlus.Services;
using ImageConverterPlus.Converters;

namespace ImageConverterPlus
{
    partial class MainWindow
    {
        private double PreviewContainerGridSize => PreviewContainerGrid != null ? Math.Min(PreviewContainerGrid.ActualWidth, PreviewContainerGrid.ActualHeight) : 0d;

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

            for (int x = 0; x < convMgr.ImageSplitSize.Width; x++)
            {
                for (int y = 0; y < convMgr.ImageSplitSize.Height; y++)
                {
                    ToggleButton btn = new ToggleButton
                    {
                        Style = (Style)FindResource("PreviewSplitBtn"),
                        Tag = new Int32Point(x, y),
                        IsChecked = new Int32Point(x, y) == convMgr.SelectedSplitPos, // not strictly necessary since the property is databound
                        ContextMenu = (ContextMenu)FindResource("SplitGridMenu"),
                    };

                    Binding binding = new Binding(nameof(ConvertManagerService.SelectedSplitPos))
                    {
                        Source = convMgr,
                        Mode = BindingMode.OneWay,
                        Converter = new EqualityConverter(),
                        ConverterParameter = btn.Tag,
                    };
                    btn.SetBinding(ToggleButton.IsCheckedProperty, binding);

                    Grid.SetColumn(btn, x);
                    Grid.SetRow(btn, y);
                    btn.Click += (sender, e) => convMgr.SelectedSplitPos = (Int32Point)btn.Tag;
                    PreviewGrid.Children.Add(btn);
                }
            }
        }

        private void PreviewContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewContainerSize();
        }
    }
}
