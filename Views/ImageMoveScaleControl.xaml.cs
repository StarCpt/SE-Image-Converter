using ImageConverterPlus.Controls;
using ImageConverterPlus.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public static readonly RoutedEvent ScaleChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ScaleChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>),
                typeof(ImageMoveScaleControl));

        public static readonly RoutedEvent OffsetChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(OffsetChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<Point>),
                typeof(ImageMoveScaleControl));

        public event RoutedPropertyChangedEventHandler<double> ScaleChanged
        {
            add => AddHandler(ScaleChangedEvent, value);
            remove => RemoveHandler(ScaleChangedEvent, value);
        }

        public event RoutedPropertyChangedEventHandler<Point> OffsetChanged
        {
            add => AddHandler(OffsetChangedEvent, value);
            remove => RemoveHandler(OffsetChangedEvent, value);
        }

        public double Scale
        {
            get => scaleTo;
            set
            {
                if (ScaleAnim != value)
                {
                    this.BeginAnimation(ScaleProperty, null);
                    animatingScale = false;
                    scaleTo = value;
                    SetValue(ScaleProperty, value);

                    var clamped = ClampOffset(offsetTo, scaleTo);
                    if (clamped != offsetTo)
                        Offset = clamped;
                }
            }
        }
        public double ScaleAnim => (double)GetValue(ScaleProperty);
        public Point Offset
        {
            get => offsetTo;
            set
            {
                var clamped = ClampOffset(value, scaleTo);
                if (clamped != value)
                    value = clamped;

                if (OffsetAnim != value)
                {
                    this.BeginAnimation(OffsetProperty, null);
                    animatingOffset = false;
                    offsetTo = value;
                    SetValue(OffsetProperty, value);
                }
            }
        }
        public Point OffsetAnim => (Point)GetValue(OffsetProperty);
        public double ImageActualWidth => image.ActualWidth;
        public double ImageActualHeight => image.ActualHeight;

        private double scaleTo = (double)ScaleProperty.DefaultMetadata.DefaultValue;
        private Point offsetTo = (Point)OffsetProperty.DefaultMetadata.DefaultValue;

        private bool animatingScale = false;
        private bool animatingOffset = false;

        private Point moveOrigin;
        private Point imageMoveOrigin;

        public  TimeSpan animationDuration => TimeSpan.FromMilliseconds(100);

        public ImageMoveScaleControl()
        {
            InitializeComponent();
        }

        private static void ScalePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageMoveScaleControl source = (ImageMoveScaleControl)d;

            source.SetMoveOrigin();
            double newScale = (double)e.NewValue;
            var mt = GetMatrixTransform(source.image);
            var mat = mt.Matrix;
            mat.M11 = newScale;
            mat.M22 = newScale;
            mt.Matrix = mat;

            MainWindow.Static.debug1.Text = newScale.ToString("0.0000");

            RoutedPropertyChangedEventArgs<double> args = new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, newScale)
            {
                RoutedEvent = ScaleChangedEvent,
                Source = source,
            };

            source.RaiseEvent(args);
        }

        protected virtual void OnScaleChanged(RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private static void OffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageMoveScaleControl source = (ImageMoveScaleControl)d;

            Point newOffset = (Point)e.NewValue;
            //newOffset = newOffset.Round();
            var mt = GetMatrixTransform(source.image);
            var mat = mt.Matrix;
            mat.OffsetX = newOffset.X;
            mat.OffsetY = newOffset.Y;
            mt.Matrix = mat;

            if (MainWindow.Static?.debug2?.Text != null)
                MainWindow.Static.debug2.Text = $"{newOffset.X:0.0000}, {newOffset.Y:0.0000}";

            RoutedPropertyChangedEventArgs<Point> args = new RoutedPropertyChangedEventArgs<Point>((Point)e.OldValue, newOffset)
            {
                RoutedEvent = OffsetChangedEvent,
                Source = source,
            };

            source.RaiseEvent(args);
        }

        protected virtual void OnOffsetChanged(RoutedPropertyChangedEventArgs<Point> e)
        {

        }

        public Point ClampOffset(Point offset, double scale)
        {
            double scaledWidth = image.ActualWidth * scale;
            double scaledHeight = image.ActualHeight * scale;

            if (scaledWidth < this.ActualWidth)
                offset.X = Math.Clamp(offset.X, 0, this.ActualWidth - scaledWidth);
            else
                offset.X = Math.Clamp(offset.X, -scaledWidth + this.ActualWidth, 0);

            if (scaledHeight < this.ActualHeight)
                offset.Y = Math.Clamp(offset.Y, 0, this.ActualHeight - scaledHeight);
            else
                offset.Y = Math.Clamp(offset.Y, -scaledHeight + this.ActualHeight, 0);

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
                //MainWindow.Static.debug3.Text = $"{diff.X:0.0000}, {diff.Y:0.0000}";
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

        public void SetScaleAnimated(double scaleTo, TimeSpan duration)
        {
            this.scaleTo = scaleTo;
            DoubleAnimation scaleAni = new DoubleAnimation(ScaleAnim, scaleTo, duration);
            scaleAni.AccelerationRatio = 0.5;
            scaleAni.DecelerationRatio = 0.5;
            this.BeginAnimation(ScaleProperty, scaleAni);
            animatingScale = true;

            var clamped = ClampOffset(offsetTo, scaleTo);
            if (clamped != offsetTo)
            {
                SetOffsetAnimated(clamped, duration);
            }
        }

        private void SkipScaleAnimation()
        {
            Scale = scaleTo;
            animatingScale = false;
        }

        public void SetOffsetAnimated(Point offsetTo, TimeSpan duration)
        {
            //if (offsetTo.X != double.NaN && offsetTo.Y != double.NaN)
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
            animatingOffset = false;
        }

        private static MatrixTransform GetMatrixTransform(UIElement element)
        {
            return (MatrixTransform)element.RenderTransform;
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Scale != 1.0)
                return;

            Point offsetNew = new Point(
                (this.ActualWidth - this.ImageActualWidth) / 2,
                (this.ActualHeight - this.ImageActualHeight) / 2);
            Offset = ClampOffset(offsetNew, Scale);
        }

        private void image_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //SetOffsetAnimated(ClampOffset(Offset, Scale), animationDuration);
            Offset = ClampOffset(Offset, Scale);
            MainWindow.Static.debug3.Text = image.ActualHeight.ToString("0.0000");
            MainWindowViewModel vm = (MainWindowViewModel)DataContext;
            if (vm.LCDWidth * vm.ImageSplitWidth <= this.ActualWidth && vm.LCDHeight * vm.ImageSplitHeight <= this.ActualHeight)
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
