using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace SEImageToLCD_15BitColor
{
    public class Dithering
    {
        //public static Pixel[,] ChangeBitDepthAndDither(Pixel[,] pixelArr, byte colorDepth)
        //{
        //    //Pixel[,] newPixelArr = new Pixel[pixelArr.GetLength(0), pixelArr.GetLength(1)];

        //    int[,] ditherMethod = FloydSteinbergOffsetAndWeight;//change when there are more

        //    int imageWidth = pixelArr.GetLength(0);
        //    int imageHeight = pixelArr.GetLength(1);
        //    int ditherMethodIterations = ditherMethod.GetLength(0);

        //    for (int y = 0; y < imageHeight; y++)
        //    {
        //        for (int x = 0; x < imageWidth; x++)
        //        {
        //            //Pixel oldColor = pixelArr[x, y];
        //            //Pixel newColor = Program.ChangeBitDepth(oldColor, colorDepth);
        //            //newPixelArr[x, y] = newColor;
        //            //Pixel error = oldColor - newColor;

        //            //this is about 20% faster
        //            Pixel oldColor = pixelArr[x, y];
        //            pixelArr[x, y] = ConversionUtils.ChangeBitDepth(oldColor, colorDepth);
        //            Pixel error = oldColor - pixelArr[x, y];

        //            for (int i = 0; i < ditherMethodIterations; i++)
        //            {
        //                //if (IsValidPixelPos(ref pixelArr, x + ditherMethod[i, 0], y + ditherMethod[i, 1]))
        //                if (x + ditherMethod[i, 0] >= 0 && x + ditherMethod[i, 0] < imageWidth && y + ditherMethod[i, 1] < imageHeight)
        //                {
        //                    pixelArr[x + ditherMethod[i, 0], y + ditherMethod[i, 1]] += (error * ditherMethod[i, 2]) / 16f;
        //                }
        //            }
        //        }
        //    }

        //    return pixelArr;
        //}

        public static byte[] ChangeBitDepthAndDitherFastThreaded(byte[] colorArr, int colorChannels, int width, byte colorDepth, int imgStride)
        {
            int[] bigColorArr = new int[colorArr.Length];
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);

            Task.WaitAll(new Task[3]
            {
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 0, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 1, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 2, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
            });

            return colorArr;
        }

        private static void ChangeBitDepthAndDitherThread(byte[] colorArr, int[] bigColorArr, int colorChannels, int channel, int width, int imgStride, int[,] ditherArr, double colorStepInterval)
        {
            int ditherIterations = ditherArr.GetLength(0);
            int realWidth = width * colorChannels;
            int strideDiff = imgStride - realWidth;

            for (int c = channel; c < colorArr.Length - strideDiff;)
            {
                int oldColor = bigColorArr[c] + colorArr[c];
                colorArr[c] = ChangeBitDepthFast(oldColor, colorStepInterval);
                int error = oldColor - colorArr[c];

                for (int i = 0; i < ditherIterations; i++)
                {
                    int offsetPos = c + (imgStride * ditherArr[i, 1]) + (ditherArr[i, 0] * colorChannels);
                    int offsetPosX = (c % imgStride / colorChannels) + ditherArr[i, 1];
                    bool isOutOfRange = offsetPos >= colorArr.Length - strideDiff || offsetPos < 0;
                    bool isBeforeWidth = offsetPosX < 0;
                    bool isAfterWidth = offsetPosX > width - 1;

                    if (!isOutOfRange && !isBeforeWidth && !isAfterWidth)
                    {
                        bigColorArr[offsetPos] += (int)Math.Round((error * ditherArr[i, 2]) / 16f);
                    }
                }

                c += colorChannels;
                if ((c - channel) % imgStride == realWidth)
                {
                    c += strideDiff;
                }
            }
        }

        private static byte ChangeBitDepthFast(int singleChannelColor, double colorStepInterval)
        {
            return (Math.Round(singleChannelColor / colorStepInterval) * colorStepInterval).ToByte();
        }

        //private static bool IsValidPixelPos(ref Pixel[,] image, int x, int y)
        //{
        //    return (x >= 0 && /*y >= 0 && */x < image.GetLength(0) && y < image.GetLength(1));
        //}

        //xOffset, yOffset, errorWeight
        private static readonly int[,] FloydSteinbergOffsetAndWeight =
            new int[4,3]
            {
                { 1, 0, 7 },
                {-1, 1, 3 },
                { 0, 1, 5 },
                { 1, 1, 1 },
            };
    }
}
