using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;

namespace SEImageToLCD_15BitColor
{
    public class Program
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

        public static Tuple<string, BitmapImage> ConvertImage(Bitmap imageBitmap, DitherMode ditherModeEnum, BitDepth bitDepthEnum, Size lcdSize, InterpolationMode interpolationEnum)
        {
            StringBuilder convertedStrB = new StringBuilder();
            float scale = Math.Min((float)lcdSize.Width / imageBitmap.Width, (float)lcdSize.Height / imageBitmap.Height);
            //double scale = 178d / Math.Max(img.Width, img.Height);
            imageBitmap = Scaling.Scale(imageBitmap, scale, interpolationEnum);
            Pixel[,] imagePixelArr = Pad(imageBitmap, lcdSize);

            switch (ditherModeEnum)
            {
                case DitherMode.NoDither:
                    imagePixelArr = ChangeBitDepth(imagePixelArr, (byte)bitDepthEnum);
                    break;
                case DitherMode.FloydSteinberg:
                    imagePixelArr = Dithering.ChangeBitDepthAndDither(imagePixelArr, (byte)bitDepthEnum);
                    break;
            }

            for (int x = 0; x < imagePixelArr.GetLength(0); x++)
            {
                for (int y = 0; y < imagePixelArr.GetLength(1); y++)
                {
                    Pixel pixel = imagePixelArr[x, y];
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

            return new Tuple<string, BitmapImage>(convertedStrB.ToString(), MainWindowUtils.BitmapToBitmapImage(GetBitmap(imagePixelArr)));
        }

        static Pixel[,] Pad(Bitmap image, Size targetSize)
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
            public short R { get; private set; }
            public short G { get; private set; }
            public short B { get; private set; }

            public Pixel(int R, int G, int B, bool clamp = true)
            {
                if (clamp)
                {
                    this.R = (byte)R;
                    this.G = (byte)G;
                    this.B = (byte)B;
                }
                else
                {
                    this.R = (short)R;
                    this.G = (short)G;
                    this.B = (short)B;
                }
            }

            public void Update(int newR, int newG, int newB, bool clamp = true)
            {
                if (clamp)
                {
                    this.R = (byte)newR;
                    this.G = (byte)newG;
                    this.B = (byte)newB;
                }
                else
                {
                    this.R = (short)newR;
                    this.G = (short)newG;
                    this.B = (short)newB;
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
            Bitmap map = new(image.GetLength(0), image.GetLength(1));
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

        private static Pixel[,] ChangeBitDepth(Pixel[,] imagePixelArray, byte perChannelColorDepth)
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

        public static Program.Pixel ToPixel(this Color color)
        {
            return new Program.Pixel(color.R, color.G, color.B);
        }
    }
}
