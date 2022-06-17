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
    public class ConversionUtils
    {
        public static Pixel[,] Pad(Bitmap image, Size targetSize)
        {
            Pixel[,] paddedImage = new Pixel[Math.Max(targetSize.Width, image.Width), Math.Max(targetSize.Height, image.Height)];
            int xPadding = ((targetSize.Width - image.Width) / 2).Clamp(0, int.MaxValue);
            int yPadding = ((targetSize.Height - image.Height) / 2).Clamp(0, int.MaxValue);
            
            for (int x = 0; x < paddedImage.GetLength(0); x++)
            {
                for (int y = 0; y < paddedImage.GetLength(1); y++)
                {
                    paddedImage[x, y] = new Pixel(0, 0, 0);
                }
            }

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    paddedImage[x + xPadding, y + yPadding] = image.GetPixel(x, y).ToPixel();
                }
            }

            return paddedImage;
        }

        //basically a copy of whip's https://github.com/Whiplash141/Whips-Image-Converter/blob/993281e9499a6744b91bd86be7c1711593a58754/WhipsImageConverter/Color3.cs
        public struct Pixel
        {
            public int R { get; private set; }
            public int G { get; private set; }
            public int B { get; private set; }

            public Pixel(int R, int G, int B, bool clamp = true)
            {
                if (clamp)
                {
                    this.R = R.Clamp(0, 255);
                    this.G = G.Clamp(0, 255);
                    this.B = B.Clamp(0, 255);
                }
                else
                {
                    this.R = R;
                    this.G = G;
                    this.B = B;
                }
            }

            public void Update(int newR, int newG, int newB, bool clamp = true)
            {
                if (clamp)
                {
                    this.R = newR.Clamp(0, 255);
                    this.G = newG.Clamp(0, 255);
                    this.B = newB.Clamp(0, 255);
                }
                else
                {
                    this.R = newR;
                    this.G = newG;
                    this.B = newB;
                }
            }

            public static Pixel operator -(Pixel pixel1, Pixel pixel2)
            {
                return pixel1 + (-1 * pixel2);
            }

            public static Pixel operator +(Pixel color1, Pixel color2)
            {
                return new Pixel(color1.R + color2.R, color1.G + color2.G, color1.B + color2.B, false);
            }

            public static Pixel operator *(Pixel color, float multiplier)
            {
                return new Pixel((int)Math.Round(color.R * multiplier), (int)Math.Round(color.G * multiplier), (int)Math.Round(color.B * multiplier), false);
            }

            public static Pixel operator *(float multiplier, Pixel color)
            {
                return new Pixel((int)Math.Round(color.R * multiplier), (int)Math.Round(color.G * multiplier), (int)Math.Round(color.B * multiplier), false);
            }

            public static Pixel operator /(float dividend, Pixel color)
            {
                return new Pixel((int)Math.Round(dividend / color.R), (int)Math.Round(dividend / color.G), (int)Math.Round(dividend / color.B), false);
            }

            public static Pixel operator /(Pixel color, float dividend)
            {
                return new Pixel((int)Math.Round(color.R / dividend), (int)Math.Round(color.G / dividend), (int)Math.Round(color.B / dividend), false);
            }
        }

        public static Bitmap GetBitmap(Pixel[,] image)
        {
            Bitmap map = new(image.GetLength(0), image.GetLength(1), PixelFormat.Format24bppRgb);
            for (int x = 0; x < image.GetLength(0); x++)
            {
                for (int y = 0; y < image.GetLength(1); y++)
                {
                    Pixel p = image[x, y];
                    map.SetPixel(x, y, Color.FromArgb(p.R, p.G, p.B));
                }
            }
            return map;
        }

        public static Pixel ChangeBitDepth(Pixel pixel, byte colorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);//I dont' understand this. I'm just copying whip's code

            pixel.Update(
                (int)(Math.Round(pixel.R / colorStepInterval) * colorStepInterval),
                (int)(Math.Round(pixel.G / colorStepInterval) * colorStepInterval),
                (int)(Math.Round(pixel.B / colorStepInterval) * colorStepInterval));

            return pixel;
        }

        public static Pixel[,] ChangeBitDepth(Pixel[,] imagePixelArray, byte perChannelColorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, perChannelColorDepth) - 1);

            for (int x = 0; x < imagePixelArray.GetLength(0); x++)
            {
                for (int y = 0; y < imagePixelArray.GetLength(1); y++)
                {
                    Pixel pixel = imagePixelArray[x, y];
                    imagePixelArray[x, y].Update(
                        (int)(Math.Round(pixel.R / colorStepInterval) * colorStepInterval),
                        (int)(Math.Round(pixel.G / colorStepInterval) * colorStepInterval),
                        (int)(Math.Round(pixel.B / colorStepInterval) * colorStepInterval));
                }
            }

            return imagePixelArray;
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

        private Bitmap imageBitmap;
        private DitherMode ditherModeEnum;
        private BitDepth bitDepthEnum;
        private Size lcdSize;
        private InterpolationMode interpolationEnum;
        private MainWindow.ConvertCallback callback;
        private CancellationToken cancellationToken;

        public ConvertThread(
            Bitmap imageBitmap, 
            DitherMode ditherModeEnum, 
            BitDepth bitDepthEnum, 
            Size lcdSize, 
            InterpolationMode interpolationEnum, 
            MainWindow.ConvertCallback callback,
            CancellationToken cancellationToken)
        {
            this.imageBitmap = imageBitmap;
            this.ditherModeEnum = ditherModeEnum;
            this.bitDepthEnum = bitDepthEnum;
            this.lcdSize = lcdSize;
            this.interpolationEnum = interpolationEnum;
            this.callback = callback;
            this.cancellationToken = cancellationToken;
        }

        public void CancelCallback() => callback = null;

        public void ConvertImageThreaded()
        {
            StringBuilder convertedStrB = new StringBuilder();
            float scale = Math.Min((float)lcdSize.Width / imageBitmap.Width, (float)lcdSize.Height / imageBitmap.Height);
            //double scale = 178d / Math.Max(img.Width, img.Height);
            imageBitmap = Scaling.Scale(imageBitmap, scale, interpolationEnum);
            MainWindow.Logging.Log($"scaled. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            ConversionUtils.Pixel[,] imagePixelArr = ConversionUtils.Pad(imageBitmap, lcdSize);
            MainWindow.Logging.Log($"converted to pixel array & padded. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            switch (ditherModeEnum)
            {
                case DitherMode.NoDither:
                    imagePixelArr = ConversionUtils.ChangeBitDepth(imagePixelArr, (byte)bitDepthEnum);
                    break;
                case DitherMode.FloydSteinberg:
                    imagePixelArr = Dithering.ChangeBitDepthAndDither(imagePixelArr, (byte)bitDepthEnum);
                    break;
            }

            MainWindow.Logging.Log($"bitdepth change and dithering done. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            for (int y = 0; y < imagePixelArr.GetLength(1); y++)
            {
                for (int x = 0; x < imagePixelArr.GetLength(0); x++)
                {
                    ConversionUtils.Pixel pixel = imagePixelArr[x, y];
                    char colorChar;
                    switch (bitDepthEnum)
                    {
                        case BitDepth.Color3:
                            colorChar = ColorTo9BitChar((byte)pixel.R, (byte)pixel.G, (byte)pixel.B);
                            convertedStrB.Append(colorChar);
                            break;
                        case BitDepth.Color5:
                            colorChar = ColorTo15BitChar((byte)pixel.R, (byte)pixel.G, (byte)pixel.B);
                            convertedStrB.Append(colorChar);
                            break;
                    }
                }
                convertedStrB.AppendLine();
            }

            MainWindow.Logging.Log($"finished processing. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            if (callback != null)
            {
                callback(convertedStrB.ToString(), MainWindowUtils.BitmapToBitmapImage(ConversionUtils.GetBitmap(imagePixelArr)));
                MainWindow.Logging.Log($"callback done. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");
            }
        }

        public void ConvertImageThreadedFast()
        {
            StringBuilder convertedStrB = new StringBuilder();
            float scale = Math.Min((float)lcdSize.Width / imageBitmap.Width, (float)lcdSize.Height / imageBitmap.Height);
            imageBitmap = Scaling.ScaleAndPad(imageBitmap, scale, interpolationEnum, lcdSize);

            MainWindow.Logging.Log($"bitmap scaled and padded. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

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

            MainWindow.Logging.Log($"bitmap locked and converted to byte array. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            switch (ditherModeEnum)
            {
                case DitherMode.NoDither:
                    rawImgBytes = ChangeBitDepth(rawImgBytes, (byte)bitDepthEnum);
                    break;
                case DitherMode.FloydSteinberg:
                    rawImgBytes = Dithering.ChangeBitDepthAndDitherFast(rawImgBytes, imgColorChannels, imageBitmap.Width, (byte)bitDepthEnum, bitmapData.Stride);
                    break;
            }

            MainWindow.Logging.Log($"bitdepth change and dithering done. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

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

            MainWindow.Logging.Log($"bitmap unlocked & finished processing. {MainWindow.Main.sw.ElapsedMilliseconds} ms elapsed.");

            if (callback != null)
            {
                callback(convertedStrB.ToString(), MainWindowUtils.BitmapToBitmapImage(imageBitmap));
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

    public static class MathExt
    {
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static ConversionUtils.Pixel ToPixel(this Color color)
        {
            return new ConversionUtils.Pixel(color.R, color.G, color.B);
        }

        public static byte ToByte(this double num)
        {
            return (byte)num.Clamp(byte.MinValue, byte.MaxValue);
        }
    }
}
