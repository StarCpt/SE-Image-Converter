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
    [Obsolete]
    public class ConvertThread
    {
        private Bitmap bitmap;
        private Action<string> callback;
        private ConvertOptions options;
        private CancellationToken cancelToken;

        public ConvertThread(
            Image image,
            ConvertOptions options,
            Action<string> callback,
            CancellationToken token)
        {
            this.bitmap = new Bitmap(image);
            this.options = options;
            this.callback = callback;
            this.cancelToken = token;
        }

        public void ConvertNew()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);
            MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Started conversion {options.BitsPerChannel} bit color, {options.Interpolation} {bitmap.Size.ToShortString()} to {options.ConvertedSize.ToShortString()} dither: {options.Dithering} {MainWindow.ImageCache.FileNameOrImageSource}");

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
    }

    [Obsolete]
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
    }
}
