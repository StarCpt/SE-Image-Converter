using ImageConverterPlus.Data;
using ImageConverterPlus.ImageConverter;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Bitmap = System.Drawing.Bitmap;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;

namespace ImageConverterPlus.Services
{
    public class ConvertManagerService : ReactiveObject
    {
        [Reactive]
        public BitDepth BitDepth { get; set; } = BitDepth.Color3;
        [Reactive]
        public bool EnableDithering { get; set; } = true;
        [Reactive]
        public InterpolationMode Interpolation { get; set; } = InterpolationMode.HighQualityBicubic;
        [Reactive]
        public Int32Size ConvertedSize { get; set; } = new Int32Size(178, 178);
        [Reactive]
        public Int32Size ImageSplitSize
        {
            get => _imageSplitSize;
            set => this.RaiseAndSetIfChanged(ref _imageSplitSize, new Int32Size(Math.Max(value.Width, 1), Math.Max(value.Height, 1)));
        }
        [Reactive]
        public Int32Point SelectedSplitPos { get; set; } = new Int32Point(0, 0);
        /// <summary>zoom</summary>
        [Reactive]
        public double Scale { get; set; } = 1.0;
        [Reactive]
        public System.Windows.Point TopLeftRatio { get; set; } = new System.Windows.Point(0, 0);
        [Reactive]
        public BitmapSource? SourceImage { get; set; }
        public Int32Size? SourceImageSize
        {
            get
            {
                if (SourceImage is BitmapSource img)
                {
                    lock (img)
                    {
                        return new Int32Size(img.PixelWidth, img.PixelHeight);
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// Non-cropped version of the converted (dithered, rescaled, etc) image
        /// </summary>
        [Reactive]
        public BitmapSource? ProcessedImageFull { get; set; }
        public Int32Size? ProcessedImageFullSize
        {
            get
            {
                if (ProcessedImageFull is BitmapSource img)
                {
                    lock (img)
                    {
                        return new Int32Size(img.PixelWidth, img.PixelHeight);
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// Converted and cropped lcd image string
        /// </summary>
        [Reactive]
        public string? ConvertedImageString { get; set; }
        [Reactive]
        public double Delay { get; set; } = 0;

        private Int32Size _imageSplitSize = new Int32Size(1, 1);

        private CancellationTokenSource? processImageTaskTokenSource;
        private CancellationTokenSource? convertImageTaskTokenSource;

        private Queue<Action<BitmapSource>> processImageCallbackQueue = new Queue<Action<BitmapSource>>();
        private Queue<Action<string>> convertImageCallbackQueue = new Queue<Action<string>>();

        private bool _processImageNextInterval = false;
        private bool _convertImageNextInterval = false;

        public ConvertManagerService()
        {
            Observable.Timer(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(i => PeriodicTimer_Tick());

            this.WhenAnyValue(
                    x => x.BitDepth,
                    x => x.EnableDithering,
                    x => x.Interpolation,
                    x => x.ConvertedSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                });
            this.WhenAnyValue(x => x.ImageSplitSize)
                .Skip(1)
                .Subscribe(i =>
                {
                    SelectedSplitPos = new Int32Point(Math.Min(SelectedSplitPos.X, i.Width - 1), Math.Min(SelectedSplitPos.Y, i.Height - 1));
                    ProcessedImageFull = null;
                    ProcessImageNextInterval();
                });
            this.WhenAnyValue(
                x => x.SelectedSplitPos,
                x => x.TopLeftRatio)
                .Skip(1)
                .Subscribe(i => ConvertedImageString = null);
            this.WhenAnyValue(x => x.Scale)
                .Skip(1)
                .Subscribe(i =>
                {
                    ProcessedImageFull = null;
                    ProcessImage();
                });
            this.WhenAnyValue(x => x.SourceImage)
                .Skip(1)
                .Subscribe(i =>
                {
                    this.RaisePropertyChanged(nameof(SourceImageSize));
                    ProcessedImageFull = null;
                    if (i != null)
                        ProcessImageNextInterval();
                });
            this.WhenAnyValue(x => x.ProcessedImageFull)
                .Skip(1)
                .Subscribe(i =>
                {
                    this.RaisePropertyChanged(nameof(ProcessedImageFullSize));
                    ConvertedImageString = null;
                });
        }

        private void PeriodicTimer_Tick()
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

        public void ProcessImage(bool noDelay = false) => ProcessImage(null, noDelay);

        public void ProcessImage(Action<BitmapSource>? callback, bool noDelay = false)
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
                Observable.Timer(TimeSpan.FromMilliseconds(Delay))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(i => _ = ProcessImageDelayedInternal(token), token);
            }
        }

        private async Task<bool> ProcessImageDelayedInternal(CancellationToken token)
        {
            if (SourceImage == null || SourceImageSize == null)
            {
                return false;
            }

            Int32Size sourceSize = SourceImageSize.Value;

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
                ConvertedSize = new Int32Size(
                    Convert.ToInt32(scaledImageWidth),
                    Convert.ToInt32(scaledImageHeight)),
                Scale = 1.0,
                TopLeft = new Int32Point(0, 0),
            };
            Converter converter = new Converter(options);
#pragma warning disable CS8600, CS8602, CS8604
            Bitmap bitmap = Helpers.BitmapSourceToBitmap(SourceImage);
            Task<Bitmap> converterTask = Task.Run(() => converter.ConvertToBitmapSafe(bitmap, token), token);
            Bitmap result = await converterTask;
            bitmap.Dispose();
#pragma warning restore CS8600, CS8602, CS8604

            ProcessedImageFull = Helpers.BitmapToBitmapSourceFast(result, true);

            while (processImageCallbackQueue.Count > 0 && !token.IsCancellationRequested)
            {
#pragma warning disable CS8604
                processImageCallbackQueue.Dequeue().Invoke(ProcessedImageFull);
#pragma warning restore CS8604
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
                Observable.Timer(TimeSpan.FromMilliseconds(Delay))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(i => _ = ConvertImageDelayedInternal(token), token);
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
                TopLeft = new Int32Point(
                    Convert.ToInt32(ConvertedSize.Width * TopLeftRatio.X + ConvertedSize.Width * SelectedSplitPos.X),
                    Convert.ToInt32(ConvertedSize.Height * TopLeftRatio.Y + ConvertedSize.Height * SelectedSplitPos.Y)),
            };

            Converter converter = new Converter(options);
#pragma warning disable CS8600, CS8604, CS8602
            Bitmap bitmap = Helpers.BitmapSourceToBitmap(ProcessedImageFull);
            Task<string> convertTask = Task.Run(() => converter.ConvertSafe(bitmap, token), token);
            ConvertedImageString = await convertTask;
            bitmap.Dispose();
#pragma warning restore CS8600, CS8604, CS8602

            while (convertImageCallbackQueue.Count > 0 && !token.IsCancellationRequested)
            {
                convertImageCallbackQueue.Dequeue().Invoke(ConvertedImageString);
            }

            return convertTask.IsCompletedSuccessfully;
        }
    }
}
