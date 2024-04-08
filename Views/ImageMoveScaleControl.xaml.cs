using CommunityToolkit.Mvvm.DependencyInjection;
using ImageConverterPlus.Services;
using System;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageConverterPlus.Views
{
    /// <summary>
    /// Interaction logic for ImageMoveScaleControl.xaml
    /// </summary>
    public partial class ImageMoveScaleControl : UserControl
    {
        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register(
                nameof(Scale),
                typeof(double),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    1.0));

        public static readonly DependencyProperty ScaleAnimProperty =
            DependencyProperty.Register(
                nameof(ScaleAnim),
                typeof(double),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    1.0,
                    ScaleAnimPropertyChanged));

        public static readonly DependencyProperty OffsetProperty =
            DependencyProperty.Register(
                nameof(Offset),
                typeof(Point),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    new Point(0, 0),
                    OffsetPropertyChanged));

        public static readonly DependencyProperty OffsetRatioProperty =
            DependencyProperty.Register(
                nameof(OffsetRatio),
                typeof(Point),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    new Point(0, 0)));

        public static readonly DependencyProperty OffsetAnimProperty =
            DependencyProperty.Register(
                nameof(OffsetAnim),
                typeof(Point),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    new Point(0, 0),
                    OffsetAnimPropertyChanged));

        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }
        public double ScaleAnim => (double)GetValue(ScaleAnimProperty);
        public Point Offset
        {
            get => (Point)GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }
        public Point OffsetAnim => (Point)GetValue(OffsetAnimProperty);
        /// <summary>
        /// offset amount as % of container width/height
        /// </summary>
        public Point OffsetRatio
        {
            get => (Point)GetValue(OffsetRatioProperty);
            set => SetValue(OffsetRatioProperty, value);
        }

        private ConvertManagerService convMgr => Ioc.Default.GetRequiredService<ConvertManagerService>(); // TEMP!!

        private bool animatingScale = false;
        private bool animatingOffset = false;

        private Point moveOrigin;
        private Point imageMoveOrigin;

        public readonly TimeSpan animationDuration = TimeSpan.FromMilliseconds(200);

        public ImageMoveScaleControl()
        {
            InitializeComponent();
        }

        private static void ScaleAnimPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageMoveScaleControl source = (ImageMoveScaleControl)d;

            source.SetMoveOrigin();
            double newScale = (double)e.NewValue;
            var mt = GetMatrixTransform(source.image);
            var mat = mt.Matrix;
            mat.M11 = newScale;
            mat.M22 = newScale;
            mt.Matrix = mat;
        }

        private static void OffsetAnimPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageMoveScaleControl source = (ImageMoveScaleControl)d;

            Point newOffset = (Point)e.NewValue;
            //newOffset = newOffset.Round();
            var mt = GetMatrixTransform(source.image);
            var mat = mt.Matrix;
            mat.OffsetX = newOffset.X;
            mat.OffsetY = newOffset.Y;
            mt.Matrix = mat;
        }

        private static void OffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageMoveScaleControl source = (ImageMoveScaleControl)d;
            source.OffsetRatio = new Point(source.Offset.X / source.ActualWidth, source.Offset.Y / source.ActualHeight);
        }

        public Point ClampOffset(Point offset, double scale)
        {
            double scaledWidth = image.ActualWidth * scale;
            double scaledHeight = image.ActualHeight * scale;
#pragma warning disable CS8509 //double.CompareTo(double) only returns -1, 0, and 1
            offset.X = scaledWidth.CompareTo(this.ActualWidth) switch
            {
                0 => 0,
                -1 => Math.Clamp(offset.X, 0, this.ActualWidth - scaledWidth),
                1 => Math.Clamp(offset.X, -scaledWidth + this.ActualWidth, 0),
            };

            offset.Y = scaledHeight.CompareTo(this.ActualHeight) switch
            {
                0 => 0,
                -1 => Math.Clamp(offset.Y, 0, this.ActualHeight - scaledHeight),
                1 => Math.Clamp(offset.Y, -scaledHeight + this.ActualHeight, 0),
            };
#pragma warning restore CS8509
            return offset;
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetMoveOrigin();

            this.CaptureMouse();
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.ReleaseMouseCapture();
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPos = e.GetPosition(this);

            if (this.IsMouseCaptured)
            {
                Vector diff = currentPos - moveOrigin;
                if (animatingScale)
                {
                    SetScaleNoAnim(Scale);
                    SetMoveOrigin();
                }
                if (animatingOffset)
                {
                    SetOffsetNoAnim(Offset);
                    SetMoveOrigin();
                }
                SetOffsetNoAnim(ClampOffset(new Point(diff.X + imageMoveOrigin.X, diff.Y + imageMoveOrigin.Y), Scale));
            }
        }

        private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(image);

            double change = 1.1;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                change = 1.01;

            double newScale = Scale * (e.Delta > 0 ? change : (1 / change));
            if (newScale is > 10 or < 0.1)
                return;

            double changeActual = newScale - Scale;

            Point oldoffset = Offset;

            SetScaleAnimated(newScale, animationDuration);
            //SetScaleNoAnim(newScale);
            var clamped = ClampOffset(oldoffset - new Vector(mousePos.X * changeActual, mousePos.Y * changeActual), newScale);
            SetOffsetAnimated(clamped, animationDuration);
            //SetOffsetNoAnim(clamped);
        }

        private void SetMoveOrigin()
        {
            moveOrigin = Mouse.GetPosition(this);
            imageMoveOrigin = Offset;
        }

        public void SetScaleAnimated(double scaleTo, TimeSpan duration)
        {
            this.Scale = scaleTo;
            DoubleAnimation scaleAni = new DoubleAnimation(ScaleAnim, scaleTo, duration);
            scaleAni.AccelerationRatio = 0.5;
            scaleAni.DecelerationRatio = 0.5;
            this.BeginAnimation(ScaleAnimProperty, scaleAni);
            animatingScale = true;

            var clamped = ClampOffset(Offset, scaleTo);
            if (clamped != Offset)
            {
                SetOffsetAnimated(clamped, duration);
            }
        }

        public void SetScaleNoAnim(double value)
        {
            animatingScale = false;
            if (ScaleAnim != value)
            {
                DoubleAnimation scaleAni = new DoubleAnimation(ScaleAnim, value, TimeSpan.Zero);
                this.BeginAnimation(ScaleAnimProperty, scaleAni);
                Scale = value;

                //SetValue(ScaleAnimProperty, value);

                var clamped = ClampOffset(Offset, Scale);
                if (clamped != Offset)
                    SetOffsetNoAnim(clamped);
            }
        }

        public void SetOffsetAnimated(Point offsetTo, TimeSpan duration)
        {
            this.Offset = offsetTo;
            PointAnimation pointAni = new PointAnimation(OffsetAnim, offsetTo, duration);
            pointAni.AccelerationRatio = 0.5;
            pointAni.DecelerationRatio = 0.5;
            this.BeginAnimation(OffsetAnimProperty, pointAni);
            animatingOffset = true;
        }

        public void SetOffsetNoAnim(Point value)
        {
            var clamped = ClampOffset(value, Scale);
            if (clamped != value)
                value = clamped;

            animatingOffset = false;
            if (OffsetAnim != value)
            {
                PointAnimation pointAni = new PointAnimation(OffsetAnim, value, TimeSpan.Zero);
                this.BeginAnimation(OffsetAnimProperty, pointAni);
                Offset = value;

                //SetValue(OffsetAnimProperty, value);
            }
        }

        private static MatrixTransform GetMatrixTransform(UIElement element)
        {
            return (MatrixTransform)element.RenderTransform;
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Point offsetNew;

            if (e.PreviousSize.IsAnyZero() || e.NewSize.IsAnyZero())
            {
                return;
                //offsetNew = new Point
                //{
                //    X = (this.ActualWidth - this.image.ActualWidth) / 2,
                //    Y = (this.ActualHeight - this.image.ActualHeight) / 2,
                //};
            }
            else
            {
                offsetNew = new Point
                {
                    X = Offset.X * (e.NewSize.Width / e.PreviousSize.Width),
                    Y = Offset.Y * (e.NewSize.Height / e.PreviousSize.Height),
                };
            }

            SetOffsetNoAnim(ClampOffset(offsetNew, Scale));
        }

        private void image_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetOffsetNoAnim(ClampOffset(Offset, Scale));
            if (convMgr.ConvertedSize.Width * convMgr.ImageSplitSize.Width <= this.ActualWidth &&
                convMgr.ConvertedSize.Height * convMgr.ImageSplitSize.Height <= this.ActualHeight)
            {
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            }
        }
    }
}
