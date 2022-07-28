using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;
using System.Drawing.Imaging;
using System.Threading;
using System.Runtime.InteropServices;

namespace SEImageToLCD_15BitColor
{
    public static class ConvertUtils
    {
        [DllImport("Image Processor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ChangeBitDepthCPP(byte[] colorArr, int arrayLength, int colorDepth);

        public static void ChangeBitDepth(byte[] colorArr, byte colorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);

            for (int i = 0; i < colorArr.Length; i++)
            {
                colorArr[i] = (Math.Round(colorArr[i] / colorStepInterval) * colorStepInterval).ToByte();
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
        public enum BitDepth
        {
            Invalid = -1,
            Color3 = 3,
            Color5 = 5,
        }
        public enum DitherMode
        {
            NoDither = 0,
            FloydSteinberg = 1,
        }

        private Bitmap image;
        private readonly DitherMode ditherMode;
        private readonly BitDepth colorDepth;
        private readonly Size lcdSize;
        private readonly Size imageSplitSize;
        private readonly int[] splitPos;
        private readonly InterpolationMode interpolationMode;
        private MainWindow.ConvertCallback callback;
        private readonly float xOffset;
        private readonly float yOffset;
        private readonly System.Diagnostics.Stopwatch sw;
        private bool taskCancelled;

        public ConvertThread(
            Bitmap image, 
            DitherMode ditherMode, 
            BitDepth colorDepth, 
            Size lcdSize, 
            Size imageSplitSize,
            int[] splitPos,
            InterpolationMode interpolationMode, 
            MainWindow.ConvertCallback callback,
            float xOffset,
            float yOffset)
        {
            this.image = new Bitmap(image);
            this.ditherMode = ditherMode;
            this.colorDepth = colorDepth;
            this.lcdSize = lcdSize;
            this.imageSplitSize = imageSplitSize;
            this.splitPos = splitPos;
            this.interpolationMode = interpolationMode;
            this.callback = callback;
            this.xOffset = xOffset;
            this.yOffset = yOffset;

            sw = new System.Diagnostics.Stopwatch();
            taskCancelled = false;
        }

        public void CancelTask()
        {
            callback = null;
            taskCancelled = true;
        }

        public void ConvertThreadedFast()
        {
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);
            MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Started conversion {colorDepth.ToString()} {interpolationMode.ToString()} {image.Size.ToShortString()} to {lcdSize.ToShortString()} {ditherMode.ToString()} {MainWindow.ImageCache.FileNameOrImageSource}");

            double zoom = Math.Min((double)lcdSize.Width * imageSplitSize.Width / image.Width, (double)lcdSize.Height * imageSplitSize.Height / image.Height);
            zoom *= MainWindow.imagePreviewScale;
            //zoom *= Math.Max(imageSplitSize.Width, imageSplitSize.Height);
            image = Scaling.ScaleAndOffset(image, zoom, xOffset * imageSplitSize.Width - lcdSize.Width * splitPos[0], yOffset * imageSplitSize.Height - lcdSize.Height * splitPos[1], interpolationMode, lcdSize);

            if (!taskCancelled)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Bitmap scaled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                image.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bitmapData = image.LockBits(rectangle, ImageLockMode.ReadWrite, image.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            //24bppRgb format means theres always 3 channels
            //NOTE: 24bppRgb has red and blue channels swapped!
            const int imgColorChannels = 3;//Image.GetPixelFormatSize(imageBitmap.PixelFormat) / 8;
            int imgByteWidth = image.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * image.Height;
            byte[] rawImgBytes = new byte[imgByteSize];

            Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            if (!taskCancelled)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Bitmap locked, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                image.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            switch (ditherMode)
            {
                case DitherMode.NoDither:
                    ConvertUtils.ChangeBitDepthCPP(rawImgBytes, imgByteSize, (int)colorDepth);
                    break;
                case DitherMode.FloydSteinberg:
                    Dithering.ChangeBitDepthAndDitherFastThreadedCPP(rawImgBytes, imgByteSize, image.Width, bitmapData.Stride, (int)colorDepth);
                    break;
            }

            if (!taskCancelled)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Image processing done, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                image.Dispose();
                sw.Stop();
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion cancelled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                return;
            }

            //convert to char
            StringBuilder convertedStrB = new StringBuilder();
            byte[] buffer = new byte[3];
            int bytePos = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int c = 0; c < imgColorChannels; c++)
                    {
                        buffer[c] = rawImgBytes[bytePos];
                        bytePos++;
                    }

                    //NOTE: 24bppRgb has R and B channels swapped!
                    switch (colorDepth)
                    {
                        case BitDepth.Color3:
                            convertedStrB.Append(ConvertUtils.ColorTo9BitChar(buffer[2], buffer[1], buffer[0]));
                            break;
                        case BitDepth.Color5:
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
            image.Dispose();

            if (!taskCancelled)
            {
                callback(convertedStrB.ToString());
                MainWindow.Logging.Log($"[Thread:{threadId}] Convert: Conversion & callback complete, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
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
        private Bitmap image;
        private readonly ConvertThread.DitherMode ditherMode;
        private readonly ConvertThread.BitDepth colorDepth;
        private readonly InterpolationMode interpolationMode;
        private MainWindow.PreviewConvertCallback callback;
        private readonly double zoom;
        private readonly System.Diagnostics.Stopwatch sw;
        private bool taskCancelled;

        private const bool debug = true;

        public PreviewConvertThread(
            Bitmap image,
            ConvertThread.DitherMode ditherMode,
            ConvertThread.BitDepth colorDepth,
            InterpolationMode interpolationMode,
            MainWindow.PreviewConvertCallback callback,
            double scale)
        {
            this.image = new Bitmap(image);
            this.ditherMode = ditherMode;
            this.colorDepth = colorDepth;
            this.interpolationMode = interpolationMode;
            this.callback = callback;
            this.zoom = scale;

            sw = new System.Diagnostics.Stopwatch();
            taskCancelled = false;
        }

        public void CancelTask()
        {
            callback = null;
            taskCancelled = true;
        }

        public void ConvertPreviewThreadedFast()
        {
            sw.Start();
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(3);

            image = Scaling.Scale(image, zoom, interpolationMode);

            if (taskCancelled)
            {
                image.Dispose();
                sw.Stop();
                return;
            }
            else if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap scaled, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bitmapData = image.LockBits(rectangle, ImageLockMode.ReadWrite, image.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            const int imgColorChannels = 3;
            int imgByteWidth = image.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * image.Height;
            byte[] rawImgBytes = new byte[imgByteSize];
            Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap locked, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            switch (ditherMode)
            {
                case ConvertThread.DitherMode.NoDither:
                    ConvertUtils.ChangeBitDepthCPP(rawImgBytes, imgByteSize, (int)colorDepth);
                    break;
                case ConvertThread.DitherMode.FloydSteinberg:
                    Dithering.ChangeBitDepthAndDitherFastThreadedCPP(rawImgBytes, imgByteSize, image.Width, bitmapData.Stride, (int)colorDepth);
                    break;
            }

            if (taskCancelled)
            {
                image.Dispose();
                sw.Stop();
                return;
            }
            else if (debug)
            {
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Bitmap processed, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }

            //System.Runtime.InteropServices.Marshal.Copy(rawImgBytes, 0, ptr, imgByteSize);
            //image.UnlockBits(bitmapData);

            if (!taskCancelled)
            {
                if (debug)
                {
                    MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Finished conversion, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
                }

                //callback(Utils.BitmapToBitmapImage(image));
                callback(Utils.ByteArrToBitmapImage(rawImgBytes, image.Width, image.Height/*, bitmapData.Stride, imgColorChannels*/));
                image.Dispose();
                MainWindow.Logging.Log($"[Thread:{threadId}] Preview: Callback completed, {sw.Elapsed.TotalMilliseconds.ToString("0.000")} ms elapsed.");
            }
            else
            {
                image.Dispose();
                sw.Stop();
                return;
            }
        }
    }

    public static class MathExt
    {
        public static byte ToByte(this double num)
        {
            return (byte)num.Clamp(byte.MinValue, byte.MaxValue);
        }

        public static int ToRoundedInt(this float num)
        {
            return (int)Math.Round(num);
        }

        public static int ToRoundedInt(this double num)
        {
            return (int)Math.Round(num);
        }
    }
}
