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
using static System.Formats.Asn1.AsnWriter;

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
                    1.0,
                    ScalePropertyChanged));

        public static readonly DependencyProperty OffsetProperty =
            DependencyProperty.Register(
                nameof(Offset),
                typeof(Point),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    new Point(0, 0),
                    OffsetPropertyChanged));

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource),
                typeof(ImageSource),
                typeof(ImageMoveScaleControl),
                new PropertyMetadata(
                    null,
                    ImageSourcePropertyChanged));

        public double Scale
        {
            get => scaleTo;
            set
            {
                this.BeginAnimation(ScaleProperty, null);
                animatingScale = false;
                scaleTo = value;
                SetValue(ScaleProperty, value);
            }
        }

        public double ScaleAnim => (double)GetValue(ScaleProperty);

        public Point Offset
        {
            get => offsetTo;
            set
            {
                this.BeginAnimation(OffsetProperty, null);
                animatingOffset = false;
                offsetTo = value;
                SetValue(OffsetProperty, value);
            }
        }

        public Point OffsetAnim => (Point)GetValue(OffsetProperty);

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        private double scaleTo = (double)ScaleProperty.DefaultMetadata.DefaultValue;
        private Point offsetTo = (Point)OffsetProperty.DefaultMetadata.DefaultValue;

        private bool animatingScale = false;
        private bool animatingOffset = false;

        Point moveOrigin;
        Point imageMoveOrigin;

        TimeSpan animationDuration => TimeSpan.FromMilliseconds(100);

        public ImageMoveScaleControl()
        {
            InitializeComponent();
        }

        private static void ScalePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageMoveScaleControl)d).SetMoveOrigin();
            double newScale = (double)e.NewValue;
            var mt = GetMatrixTransform(((ImageMoveScaleControl)d).image);
            var mat = mt.Matrix;
            mat.M11 = newScale;
            mat.M22 = newScale;
            mt.Matrix = mat;
            MainWindow.Static.debug1.Text = newScale.ToString("0.0000");
        }

        private static void OffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Point newOffset = (Point)e.NewValue;
            newOffset = newOffset.Round();
            var mt = GetMatrixTransform(((ImageMoveScaleControl)d).image);
            var mat = mt.Matrix;
            mat.OffsetX = newOffset.X;
            mat.OffsetY = newOffset.Y;
            mt.Matrix = mat;
            MainWindow.Static.debug2.Text = $"{newOffset.X:0.0000}, {newOffset.Y:0.0000}";
        }

        private static void ImageSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageMoveScaleControl)d).image.Source = (ImageSource)e.NewValue;
        }

        private Point ClampOffset(Point offset, double scale)
        {
            double scaledWidth = Math.Truncate(Math.FusedMultiplyAdd(image.ActualWidth, scale, 0.01));
            double scaledHeight = Math.Truncate(Math.FusedMultiplyAdd(image.ActualHeight, scale, 0.01));

            if (scaledWidth < container.ActualWidth)
                offset.X = Math.Clamp(offset.X, 0, container.ActualWidth - scaledWidth);
            else
                offset.X = Math.Clamp(offset.X, -scaledWidth + container.ActualWidth, 0);

            if (scaledHeight < container.ActualHeight)
                offset.Y = Math.Clamp(offset.Y, 0, container.ActualHeight - scaledHeight);
            else
                offset.Y = Math.Clamp(offset.Y, -scaledHeight + container.ActualHeight, 0);

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
                    SkipScaleAnimation();
                    SetMoveOrigin();
                }
                if (animatingOffset)
                {
                    SkipOffsetAnimation();
                    SetMoveOrigin();
                }
                Offset = ClampOffset(new Point(diff.X + imageMoveOrigin.X, diff.Y + imageMoveOrigin.Y), Scale);
                MainWindow.Static.debug3.Text = $"{diff.X:0.0000}, {diff.Y:0.0000}";
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

            SetScaleAnimated(newScale, animationDuration);

            SetOffsetAnimated(ClampOffset(Offset - new Vector(mousePos.X * changeActual, mousePos.Y * changeActual), Scale), animationDuration);
        }

        private void SetMoveOrigin()
        {
            moveOrigin = Mouse.GetPosition(this);
            imageMoveOrigin = Offset;
        }

        private void SetScaleAnimated(double scaleTo, TimeSpan duration)
        {
            this.scaleTo = scaleTo;
            DoubleAnimation scaleAni = new DoubleAnimation(ScaleAnim, scaleTo, duration);
            scaleAni.AccelerationRatio = 0.5;
            scaleAni.DecelerationRatio = 0.5;
            this.BeginAnimation(ScaleProperty, scaleAni);
            animatingScale = true;
        }

        private void SkipScaleAnimation()
        {
            Scale = scaleTo;
        }

        private void SetOffsetAnimated(Point offsetTo, TimeSpan duration)
        {
            this.offsetTo = offsetTo;
            PointAnimation pointAni = new PointAnimation(OffsetAnim, offsetTo, duration);
            pointAni.AccelerationRatio = 0.5;
            pointAni.DecelerationRatio = 0.5;
            this.BeginAnimation(OffsetProperty, pointAni);
            animatingOffset = true;
        }

        private void SkipOffsetAnimation()
        {
            Offset = offsetTo;
        }

        private static MatrixTransform GetMatrixTransform(UIElement element)
        {
            return (MatrixTransform)element.RenderTransform;
        }

        private void image_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Offset = ClampOffset(Offset, Scale);
        }
    }
}
