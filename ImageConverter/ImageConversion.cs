using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;

namespace ImageConverterPlus.ImageConverter
{
    public static class ConvertUtils
    {
        public static void ChangeBitDepth(byte[] colorArr, int colorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);
            int shiftRight = 8 - colorDepth;

            for (int i = 0; i < colorArr.Length; i++)
            {
                colorArr[i] = Convert.ToByte((colorArr[i] >> shiftRight) * colorStepInterval);
            }
        }

        public static char ColorTo9BitChar(byte r, byte g, byte b)
        {
            return (char)(0xe100 + ((r >> 5) << 6) + ((g >> 5) << 3) + (b >> 5));
        }

        public static char ColorTo15BitChar(byte r, byte g, byte b)
        {
            return (char)((uint)0x3000 + ((r >> 3) << 10) + ((g >> 3) << 5) + (b >> 3));
        }
    }

    public class ConvertThread
    {
        private Bitmap bitmap;
        private readonly Size imageSplitSize;
        private readonly Point splitIndex;
        private Action<string> callback;
        private readonly double xOffset;
        private readonly double yOffset;
        private ConvertOptions options;
        private CancellationToken cancelToken;

        public ConvertThread(
            Image image, 
            bool dither, 
            int colorDepth,
            Size lcdSize, 
            Size imageSplitSize,
            Point splitPos,
            InterpolationMode interpolationMode, 
            Action<string> callback,
            double xOffset,
            double yOffset,
            CancellationToken token)
        {
            this.bitmap = new Bitmap(image);
            this.imageSplitSize = imageSplitSize;
            this.splitIndex = splitPos;
            this.callback = callback;
            this.xOffset = xOffset;
            this.yOffset = yOffset;

            double widthScale = (double)bitmap.Width / lcdSize.Width / imageSplitSize.Width;
            double heightScale = (double)bitmap.Height / lcdSize.Height / imageSplitSize.Height;
            double biggerScale = Math.Max(widthScale, heightScale);

            widthScale /= biggerScale;
            heightScale /= biggerScale;

            double bitmapWidthToWidthRatio = widthScale * imageSplitSize.Width;
            double bitmapHeightToHeightRatio = heightScale * imageSplitSize.Height;

            this.options = new ConvertOptions
            {
                Dithering = dither,
                BitsPerChannel = colorDepth,
                Interpolation = interpolationMode,
                ConvertedSize = lcdSize,
                //Scale = MainWindow.imagePreviewScale * Math.Max(imageSplitSize.Width, imageSplitSize.Height * lcdSize.Height),
                Scale = Math.Max(bitmapWidthToWidthRatio, bitmapHeightToHeightRatio) * MainWindow.imagePreviewScale,
                TopLeft = new Point(
                    (int)xOffset * imageSplitSize.Width + (lcdSize.Width * splitIndex.X),
                    (int)yOffset * imageSplitSize.Height - (lcdSize.Height * splitIndex.Y)),
            };

            this.cancelToken = token;
        }

        public void ConvertNew()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);
            MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Started conversion {options.BitsPerChannel.ToString()} bit color, {options.Interpolation.ToString()} {bitmap.Size.ToShortString()} to {options.ConvertedSize.ToShortString()} dither: {options.Dithering.ToString()} {MainWindow.ImageCache.FileNameOrImageSource}");

            Converter converter = new Converter(options);
            string result = converter.ConvertSafe(bitmap, cancelToken);

            if (!cancelToken.IsCancellationRequested)
            {
                callback(result);
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion complete, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            bitmap.Dispose();
            sw.Stop();
            return;
        }

        public void ConvertThreadedFast()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);
            MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Started conversion {options.BitsPerChannel.ToString()} bit color, {options.Interpolation.ToString()} {bitmap.Size.ToShortString()} to {options.ConvertedSize.ToShortString()} dither: {options.Dithering.ToString()} {MainWindow.ImageCache.FileNameOrImageSource}");

            double zoom = Math.Min((double)options.ConvertedSize.Width * imageSplitSize.Width / bitmap.Width, (double)options.ConvertedSize.Height * imageSplitSize.Height / bitmap.Height);
            zoom *= MainWindow.imagePreviewScale;
            //zoom *= Math.Max(imageSplitSize.Width, imageSplitSize.Height);
            bitmap = Scaling.ScaleAndOffset(bitmap, zoom, xOffset * imageSplitSize.Width - options.ConvertedSize.Width * splitIndex.X, yOffset * imageSplitSize.Height - options.ConvertedSize.Height * splitIndex.Y, options.Interpolation, options.ConvertedSize);

            if (!cancelToken.IsCancellationRequested)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Bitmap scaled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                bitmap.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            //24bppRgb format means theres always 3 channels
            //NOTE: 24bppRgb has red and blue channels swapped!
            const int imgColorChannels = 3;//Image.GetPixelFormatSize(imageBitmap.PixelFormat) / 8;
            int imgByteWidth = bitmap.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * bitmap.Height;
            byte[] rawImgBytes = new byte[imgByteSize];

            System.Runtime.InteropServices.Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            if (!cancelToken.IsCancellationRequested)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Bitmap locked, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                bitmap.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            switch (options.Dithering)
            {
                case false:
                    ConvertUtils.ChangeBitDepth(rawImgBytes, options.BitsPerChannel);
                    break;
                case true:
                    Dithering.ChangeBitDepthAndDitherFastThreaded(rawImgBytes, imgColorChannels, bitmap.Width, options.BitsPerChannel, bitmapData.Stride);
                    break;
            }

            if (!cancelToken.IsCancellationRequested)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Image processing done, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                bitmap.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            //convert to char
            StringBuilder convertedStrB = new StringBuilder();
            byte[] buffer = new byte[3];
            int bytePos = 0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int c = 0; c < imgColorChannels; c++)
                    {
                        buffer[c] = rawImgBytes[bytePos];
                        bytePos++;
                    }

                    //NOTE: 24bppRgb has R and B channels swapped!
                    switch (options.BitsPerChannel)
                    {
                        case 3:
                            convertedStrB.Append(ConvertUtils.ColorTo9BitChar(buffer[2], buffer[1], buffer[0]));
                            break;
                        case 5:
                            convertedStrB.Append(ConvertUtils.ColorTo15BitChar(buffer[2], buffer[1], buffer[0]));
                            break;
                    }

                    if (bytePos % bitmapData.Stride >= imgByteWidth)
                    {
                        bytePos += strideDiff;
                    }
                }
                convertedStrB.AppendLine();
            }

            //dispose things I don't need anymore before returning
            bitmap.Dispose();

            if (!cancelToken.IsCancellationRequested)
            {
                callback(convertedStrB.ToString());
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion complete, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }
        }
    }

    public class PreviewConvertThread
    {
        private Bitmap bitmap;
        private ConvertOptions options;
        private Action<System.Windows.Media.Imaging.BitmapImage> callback;
        private CancellationToken cancelToken;

        private const bool debug = true;

        public PreviewConvertThread(Image image, ConvertOptions options, Action<System.Windows.Media.Imaging.BitmapImage> callback, CancellationToken token)
        {
            this.bitmap = new Bitmap(image);
            this.options = options;
            this.callback = callback;
            this.cancelToken = token;
        }

        public void ConvertPreviewNew()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);

            Converter converter = new Converter(options);
            Bitmap result = converter.ConvertToBitmapSafe(bitmap, cancelToken);

            if (!cancelToken.IsCancellationRequested)
            {
                if (debug)
                {
                    MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Finished conversion, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                }

                callback(Helpers.BitmapToBitmapImage(result));
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Finished processing, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            bitmap.Dispose();
            result.Dispose();
            sw.Stop();
        }

        [Obsolete]
        public void ConvertPreviewThreadedFast()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);

            bitmap = Scaling.Scale(bitmap, (double)options.ConvertedSize.Width / (double)bitmap.Width, options.Interpolation);

            if (cancelToken.IsCancellationRequested)
            {
                bitmap.Dispose();
                sw.Stop();
                return;
            }
            else if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap scaled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            const int imgColorChannels = 3;
            int imgByteWidth = bitmap.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * bitmap.Height;
            byte[] rawImgBytes = new byte[imgByteSize];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap locked, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            switch (options.Dithering)
            {
                case false:
                    ConvertUtils.ChangeBitDepth(rawImgBytes, (byte)options.BitsPerChannel);
                    break;
                case true:
                    Dithering.ChangeBitDepthAndDitherFastThreaded(rawImgBytes, imgColorChannels, bitmap.Width, (byte)options.BitsPerChannel, bitmapData.Stride);
                    break;
            }

            if (cancelToken.IsCancellationRequested)
            {
                bitmap.Dispose();
                sw.Stop();
                return;
            }
            else if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap processed, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            System.Runtime.InteropServices.Marshal.Copy(rawImgBytes, 0, ptr, imgByteSize);
            bitmap.UnlockBits(bitmapData);

            if (!cancelToken.IsCancellationRequested)
            {
                if (debug)
                {
                    MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Finished conversion, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                }

                callback(Helpers.BitmapToBitmapImage(bitmap));
                bitmap.Dispose();
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Finished processing, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                bitmap.Dispose();
                sw.Stop();
                return;
            }
        }
    }

    public static class MathExt
    {
        public static byte ToByte(this double num)
        {
            return (byte)Math.Clamp(num, byte.MinValue, byte.MaxValue);
        }
    }
}
