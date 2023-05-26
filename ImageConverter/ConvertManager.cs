using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ImageConverterPlus.ImageConverter
{
    public class ConvertManager : NotifyPropertyChangedBase
    {
        public static ConvertManager Instance { get; } = new ConvertManager();

        public BitDepth BitDepth
        {
            get => bitDepth;
            set
            {
                if (SetValue(ref bitDepth, value))
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                }
            }
        }
        public bool EnableDithering
        {
            get => enableDithering;
            set
            {
                if (SetValue(ref enableDithering, value))
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                }
            }
        }
        public InterpolationMode Interpolation
        {
            get => interpolation;
            set
            {
                if (SetValue(ref interpolation, value))
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                }
            }
        }
        public Size ConvertedSize
        {
            get => convertedSize;
            set
            {
                if (SetValue(ref convertedSize, value))
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                }
            }
        }
        public Size ImageSplitSize
        {
            get => imageSplitSize;
            set
            {
                if (SetValue(ref imageSplitSize, value))
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                }
            }
        }
        public Point SelectedSplitPos
        {
            get => selectedSplitPos;
            set
            {
                if (SetValue(ref selectedSplitPos, value))
                {
                    ConvertedImageString = null;
                }
            }
        }
        /// <summary>zoom</summary>
        public double Scale
        {
            get => scale;
            set
            {
                if (SetValue(ref scale, value))
                {
                    ProcessedImageFull = null;
                    ProcessImage();
                }
            }
        }
        public System.Windows.Point TopLeftRatio
        {
            get => topLeftRatio;
            set
            {
                if (SetValue(ref topLeftRatio, value))
                {
                    ConvertedImageString = null;
                }
            }
        }

        public Image? SourceImage
        {
            get => _sourceImage;
            set
            {
                Image? old = _sourceImage;
                if (SetValue(ref _sourceImage, value))
                {
                    if (value == null)
                        old?.Dispose();
                    OnSourceImageChanged();
                }
            }
        }
        public Size? SourceImageSize
        {
            get
            {
                if (_sourceImage != null)
                    lock (_sourceImage)
                        return _sourceImage.Size;
                else
                    return null;
            }
        }
        public Bitmap? ProcessedImageFull
        {
            get => _processedImageFull;
            private set
            {
                Bitmap? old = _processedImageFull;
                if (SetValue(ref _processedImageFull, value))
                {
                    if (value == null)
                        old?.Dispose();
                    ProcessedImageFullChanged.Invoke(value);
                    ConvertedImageString = null;
                }
            }
        }
        public Size? ProcessedImageFullSize
        {
            get
            {
                if (_processedImageFull != null)
                    lock (_processedImageFull)
                        return _processedImageFull.Size;
                else
                    return null;
            }
        }
        public string? ConvertedImageString
        {
            get => _convertedImageString;
            private set
            {
                if (SetValue(ref _convertedImageString, value))
                {
                    ConvertedImageStringChanged.Invoke(value);
                }
            }
        }

        public double Delay
        {
            get => _delay;
            set => SetValue(ref _delay, value);
        }

        public event Action<Image?> SourceImageChanged = delegate { };
        public event Action<Bitmap?> ProcessedImageFullChanged = delegate { };
        public event Action<string?> ConvertedImageStringChanged = delegate { };

        private BitDepth bitDepth;
        private bool enableDithering;
        private InterpolationMode interpolation;
        private Size convertedSize;
        private Size imageSplitSize;
        private Point selectedSplitPos;
        private double scale;
        private System.Windows.Point topLeftRatio;

        private Image? _sourceImage;
        /// <summary>
        /// Non-cropped version of the converted (dithered, rescaled, etc) image
        /// </summary>
        private Bitmap? _processedImageFull;
        /// <summary>
        /// Converted and cropped lcd image string
        /// </summary>
        private string? _convertedImageString;

        private double _delay = 0;

        private CancellationTokenSource? processImageTaskTokenSource;
        private CancellationTokenSource? convertImageTaskTokenSource;

        private Queue<Action<Bitmap>> processImageCallbackQueue = new Queue<Action<Bitmap>>();
        private Queue<Action<string>> convertImageCallbackQueue = new Queue<Action<string>>();

        private readonly DispatcherTimer _periodicTimer;
        private bool _processImageNextInterval = false;
        private bool _convertImageNextInterval = false;

        public ConvertManager()
        {
            bitDepth = BitDepth.Color3;
            enableDithering = true;
            interpolation = InterpolationMode.HighQualityBicubic;
            convertedSize = new Size(178, 178);
            scale = 1.0;
            topLeftRatio = new System.Windows.Point(0, 0);
            
            _periodicTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(5),
                IsEnabled = true,
            };
            _periodicTimer.Tick += PeriodicTimer_Tick;
            _periodicTimer.Start();
        }

        private void PeriodicTimer_Tick(object? sender, EventArgs e)
        {
            if (_processImageNextInterval)
            {
                _processImageNextInterval = false;
                ProcessImage(true);
            }
            if (_convertImageNextInterval)
            {
                _convertImageNextInterval = false;
                ConvertImage(true);
            }
        }

        private void ProcessImageNextInterval()
        {
            _processImageNextInterval = true;

            processImageTaskTokenSource?.Cancel();
        }

        public void OnSourceImageChanged()
        {
            RaisePropertyChanged(nameof(SourceImage));
            SourceImageChanged.Invoke(SourceImage);
            ProcessedImageFull = null;
            if (SourceImage != null)
                ProcessImageNextInterval();
        }

        public void ProcessImage(bool noDelay = false) => ProcessImage(null, noDelay);

        public void ProcessImage(Action<Bitmap>? callback, bool noDelay = false)
        {
            if (ProcessedImageFull != null)
            {
                callback?.Invoke(ProcessedImageFull);
                return;
            }
            else if (callback != null)
            {
                processImageCallbackQueue.Enqueue(callback);
            }

            processImageTaskTokenSource?.Cancel();
            processImageTaskTokenSource?.Dispose();
            processImageTaskTokenSource = new CancellationTokenSource();
            var token = processImageTaskTokenSource.Token;

            if (noDelay)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(ProcessImageDelayedInternal, DispatcherPriority.Normal, token);
            }
            else
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Tick += delegate
                {
                    timer.Stop();
                    if (token.IsCancellationRequested)
                        return;

                    Dispatcher.CurrentDispatcher.BeginInvoke(ProcessImageDelayedInternal, DispatcherPriority.Normal, token);

                };
                timer.Interval = TimeSpan.FromMilliseconds(Delay);
                timer.Start();
            }
        }

        private async Task<bool> ProcessImageDelayedInternal(CancellationToken token)
        {
            if (SourceImage == null || SourceImageSize == null)
            {
                return false;
            }

            Size sourceSize = SourceImageSize.Value;

            double imageToLcdWidthRatio = (double)sourceSize.Width / ConvertedSize.Width;
            double imageToLcdHeightRatio = (double)sourceSize.Height / ConvertedSize.Height;

            //get the bigger ratio taking into account the image split
            double biggerImageToLcdRatio = Math.Max(imageToLcdWidthRatio / ImageSplitSize.Width, imageToLcdHeightRatio / ImageSplitSize.Height);

            double scaledImageWidth = sourceSize.Width / biggerImageToLcdRatio;
            double scaledImageHeight = sourceSize.Height / biggerImageToLcdRatio;

            scaledImageWidth *= Scale;
            scaledImageHeight *= Scale;

            ConvertOptions options = new ConvertOptions
            {
                BitsPerChannel = (int)BitDepth,
                Dithering = EnableDithering,
                Interpolation = Interpolation,
                ConvertedSize = new Size(
                    Convert.ToInt32(scaledImageWidth),
                    Convert.ToInt32(scaledImageHeight)),
                Scale = 1.0,
                TopLeft = Point.Empty,
            };
            Converter converter = new Converter(options);
            Task<Bitmap> converterTask = Task.Run(() => converter.ConvertToBitmapSafe(SourceImage, token), token);
            Bitmap result = await converterTask;

            ProcessedImageFull = result;

            while (processImageCallbackQueue.Count > 0 && !token.IsCancellationRequested)
            {
                processImageCallbackQueue.Dequeue().Invoke(ProcessedImageFull);
            }

            return converterTask.IsCompletedSuccessfully;
        }

        private void ConvertImageNextInterval()
        {
            _convertImageNextInterval = true;

            convertImageTaskTokenSource?.Cancel();
        }

        public void ConvertImage(bool noDelay = false) => ConvertImage(null, noDelay);

        public void ConvertImage(Action<string>? callback, bool noDelay = false)
        {
            if (ConvertedImageString != null)
            {
                callback?.Invoke(ConvertedImageString);
                return;
            }
            else if (callback != null)
            {
                convertImageCallbackQueue.Enqueue(callback);
            }

            convertImageTaskTokenSource?.Cancel();
            convertImageTaskTokenSource?.Dispose();
            convertImageTaskTokenSource = new CancellationTokenSource();
            var token = convertImageTaskTokenSource.Token;

            if (noDelay)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(ConvertImageDelayedInternal, DispatcherPriority.Normal, token);
            }
            else
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Tick += delegate
                {
                    timer.Stop();
                    if (token.IsCancellationRequested)
                        return;

                    Dispatcher.CurrentDispatcher.BeginInvoke(ConvertImageDelayedInternal, DispatcherPriority.Normal, token);
                };
                timer.Interval = TimeSpan.FromMilliseconds(Delay);
                timer.Start();
            }
        }
        
        private async Task<bool> ConvertImageDelayedInternal(CancellationToken token)
        {
            if (ProcessedImageFull == null && !await ProcessImageDelayedInternal(token))
            {
                return false;
            }

            var options = new ConvertOptions
            {
                Dithering = EnableDithering,
                BitsPerChannel = (int)BitDepth,
                ConvertedSize = ConvertedSize,
                Interpolation = Interpolation,
                Scale = Scale,
                TopLeft = new Point(
                    Convert.ToInt32(ConvertedSize.Width * TopLeftRatio.X + ConvertedSize.Width * SelectedSplitPos.X),
                    Convert.ToInt32(ConvertedSize.Height * TopLeftRatio.Y + ConvertedSize.Height * SelectedSplitPos.Y)),
            };

            Converter converter = new Converter(options);
#pragma warning disable CS8604
            Task<string> convertTask = Task.Run(() => converter.ConvertSafe(ProcessedImageFull, token), token);
#pragma warning restore CS8604
            ConvertedImageString = await convertTask;

            while (convertImageCallbackQueue.Count > 0 && !token.IsCancellationRequested)
            {
                convertImageCallbackQueue.Dequeue().Invoke(ConvertedImageString);
            }

            return convertTask.IsCompletedSuccessfully;
        }
    }
}
