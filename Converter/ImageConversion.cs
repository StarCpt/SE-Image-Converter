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

namespace SEImageToLCD_15BitColor
{
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

        private Bitmap imageBitmap;
        private DitherMode ditherModeEnum;
        private BitDepth bitDepthEnum;
        private Size lcdSize;
        private InterpolationMode interpolationEnum;
        private MainWindow.ConvertCallback callback;
        private CancellationToken cancellationToken;
        private bool resetZoom;
        private float xOffset;
        private float yOffset;

        public ConvertThread(
            Bitmap imageBitmap, 
            DitherMode ditherModeEnum, 
            BitDepth bitDepthEnum, 
            Size lcdSize, 
            InterpolationMode interpolationEnum, 
            MainWindow.ConvertCallback callback,
            bool resetZoom,
            float xOffset,
            float yOffset)
        {
            this.imageBitmap = new Bitmap(imageBitmap);
            this.ditherModeEnum = ditherModeEnum;
            this.bitDepthEnum = bitDepthEnum;
            this.lcdSize = lcdSize;
            this.interpolationEnum = interpolationEnum;
            this.callback = callback;
            this.resetZoom = resetZoom;
            this.xOffset = xOffset;
            this.yOffset = yOffset;
        }

        public void CancelCallback() => callback = null;

        public void ConvertImageThreadedFast()
        {
            StringBuilder convertedStrB = new StringBuilder();
            float scaleX = (float)lcdSize.Width / imageBitmap.Width;
            float scaleY = (float)lcdSize.Height / imageBitmap.Height;
            float scale = Math.Min(scaleX, scaleY);
            scale *= MainWindow.imageZoom;
            Bitmap previewImage = Scaling.Scale(imageBitmap, scale, interpolationEnum);
            //imageBitmap = Scaling.ScaleAndPad(imageBitmap, scale, interpolationEnum, lcdSize);
            imageBitmap = Scaling.ScaleAndOffset(imageBitmap, scale, xOffset, yOffset, interpolationEnum, lcdSize);

            MainWindow.Logging.Log($"bitmap scaled and padded. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            Rectangle rectangle = new Rectangle(0, 0, imageBitmap.Width, imageBitmap.Height);
            BitmapData bitmapData = imageBitmap.LockBits(rectangle, ImageLockMode.ReadWrite, imageBitmap.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            //24bppRgb format means theres always 3 channels
            //NOTE: 24bppRgb has red and blue channels swapped!
            const int imgColorChannels = 3;//Image.GetPixelFormatSize(imageBitmap.PixelFormat) / 8;
            int imgByteWidth = imageBitmap.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * imageBitmap.Height;
            byte[] rawImgBytes = new byte[imgByteSize];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            MainWindow.Logging.Log($"bitmap locked. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            switch (ditherModeEnum)
            {
                case DitherMode.NoDither:
                    rawImgBytes = ChangeBitDepth(rawImgBytes, (byte)bitDepthEnum);
                    break;
                case DitherMode.FloydSteinberg:
                    rawImgBytes = Dithering.ChangeBitDepthAndDitherFastThreaded(rawImgBytes, imgColorChannels, imageBitmap.Width, (byte)bitDepthEnum, bitmapData.Stride);
                    break;
            }

            MainWindow.Logging.Log($"bitdepth change and dithering done. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            //convert to char
            byte[] pixelBuffer = new byte[3];
            int bytePos = 0;
            for (int y = 0; y < imageBitmap.Height; y++)
            {
                for (int x = 0; x < imageBitmap.Width; x++)
                {
                    for (int c = 0; c < imgColorChannels; c++)
                    {
                        pixelBuffer[c] = rawImgBytes[bytePos];
                        bytePos++;
                    }

                    //NOTE: 24bppRgb has R and B channels swapped!
                    switch (bitDepthEnum)
                    {
                        case BitDepth.Color3:
                            convertedStrB.Append(ColorTo9BitChar(pixelBuffer[2], pixelBuffer[1], pixelBuffer[0]));
                            break;
                        case BitDepth.Color5:
                            convertedStrB.Append(ColorTo15BitChar(pixelBuffer[2], pixelBuffer[1], pixelBuffer[0]));
                            break;
                    }

                    if (bytePos % bitmapData.Stride >= imgByteWidth)
                    {
                        bytePos += strideDiff;
                    }
                }
                convertedStrB.AppendLine();
            }

            System.Runtime.InteropServices.Marshal.Copy(rawImgBytes, 0, ptr, imgByteSize);

            imageBitmap.UnlockBits(bitmapData);

            MainWindow.Logging.Log($"bitmap unlocked & finished processing. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            //do same for preview
            //MainWindow.Logging.Log($"working on preview now. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            rectangle = new Rectangle(0, 0, previewImage.Width, previewImage.Height);
            bitmapData = previewImage.LockBits(rectangle, ImageLockMode.ReadWrite, previewImage.PixelFormat);
            ptr = bitmapData.Scan0;

            imgByteWidth = previewImage.Width * imgColorChannels;
            strideDiff = bitmapData.Stride - imgByteWidth;
            imgByteSize = Math.Abs(bitmapData.Stride) * previewImage.Height;
            rawImgBytes = new byte[imgByteSize];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            MainWindow.Logging.Log($"preview locked. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            switch (ditherModeEnum)
            {
                case DitherMode.NoDither:
                    rawImgBytes = ChangeBitDepth(rawImgBytes, (byte)bitDepthEnum);
                    break;
                case DitherMode.FloydSteinberg:
                    rawImgBytes = Dithering.ChangeBitDepthAndDitherFastThreaded(rawImgBytes, imgColorChannels, previewImage.Width, (byte)bitDepthEnum, bitmapData.Stride);
                    break;
            }

            MainWindow.Logging.Log($"preview bitdepth change and dithering done. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            System.Runtime.InteropServices.Marshal.Copy(rawImgBytes, 0, ptr, imgByteSize);
            previewImage.UnlockBits(bitmapData);

            MainWindow.Logging.Log($"preview unlocked & finished processing. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms elapsed.");

            if (callback != null)
            {
                callback(convertedStrB.ToString(), Utils.BitmapToBitmapImage(/*imageBitmap*/previewImage), lcdSize, resetZoom);
            }
        }

        public static byte[] ChangeBitDepth(byte[] colorArr, byte colorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);

            for (int i = 0; i < colorArr.Length; i++)
            {
                colorArr[i] = (Math.Round(colorArr[i] / colorStepInterval) * colorStepInterval).ToByte();
            }

            return colorArr;
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

    public class PreviewConvertThread
    {
        private Bitmap imagePreview;
        private ConvertThread.DitherMode ditherModeEnum;
        private ConvertThread.BitDepth bitDepthEnum;
        private InterpolationMode interpolationEnum;
        private MainWindow.PreviewConvertCallback callback;
        private float scale;

        public PreviewConvertThread(
            Bitmap imagePreview,
            ConvertThread.DitherMode ditherModeEnum,
            ConvertThread.BitDepth bitDepthEnum,
            InterpolationMode interpolationEnum,
            MainWindow.PreviewConvertCallback callback,
            float zoom)
        {
            this.imagePreview = new Bitmap(imagePreview);
            this.ditherModeEnum = ditherModeEnum;
            this.bitDepthEnum = bitDepthEnum;
            this.interpolationEnum = interpolationEnum;
            this.callback = callback;
            this.scale = zoom;
        }

        public void CancelCallback() => callback = null;

        public void ConvertPreviewThreadedFast()
        {
            imagePreview = Scaling.Scale(imagePreview, scale, interpolationEnum);

            MainWindow.Logging.Log($"preview scaled. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms");

            Rectangle rectangle = new Rectangle(0, 0, imagePreview.Width, imagePreview.Height);
            BitmapData bitmapData = imagePreview.LockBits(rectangle, ImageLockMode.ReadWrite, imagePreview.PixelFormat);
            IntPtr ptr = bitmapData.Scan0;

            const int imgColorChannels = 3;
            int imgByteWidth = imagePreview.Width * imgColorChannels;
            int strideDiff = bitmapData.Stride - imgByteWidth;
            int imgByteSize = Math.Abs(bitmapData.Stride) * imagePreview.Height;
            byte[] rawImgBytes = new byte[imgByteSize];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rawImgBytes, 0, imgByteSize);

            MainWindow.Logging.Log($"preview locked. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms");

            switch (ditherModeEnum)
            {
                case ConvertThread.DitherMode.NoDither:
                    rawImgBytes = ConvertThread.ChangeBitDepth(rawImgBytes, (byte)bitDepthEnum);
                    break;
                case ConvertThread.DitherMode.FloydSteinberg:
                    rawImgBytes = Dithering.ChangeBitDepthAndDitherFastThreaded(rawImgBytes, imgColorChannels, imagePreview.Width, (byte)bitDepthEnum, bitmapData.Stride);
                    break;
            }

            MainWindow.Logging.Log($"preview processed. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms");

            System.Runtime.InteropServices.Marshal.Copy(rawImgBytes, 0, ptr, imgByteSize);
            imagePreview.UnlockBits(bitmapData);

            MainWindow.Logging.Log($"preview unlocked. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms");

            if (callback != null)
            {
                callback(Utils.BitmapToBitmapImage(imagePreview));
                MainWindow.Logging.Log($"preview callback complete. {MainWindow.Main.sw.Elapsed.TotalMilliseconds} ms");
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
    }
}
