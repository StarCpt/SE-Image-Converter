using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Pixel = SEImageToLCD_15BitColor.Program.Pixel;

namespace SEImageToLCD_15BitColor
{
    public class Dithering
    {
        public static Pixel[,] ChangeBitDepthAndDither(Pixel[,] pixelArr, byte colorDepth)
        {
            //Pixel[,] newPixelArr = new Pixel[pixelArr.GetLength(0), pixelArr.GetLength(1)];

            int[,] ditherMethod = FloydSteinbergOffsetAndWeight;//change when there are more

            int imageWidth = pixelArr.GetLength(0);
            int imageHeight = pixelArr.GetLength(1);
            int ditherMethodIterations = ditherMethod.GetLength(0);

            for (int y = 0; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    //Pixel oldColor = pixelArr[x, y];
                    //Pixel newColor = Program.ChangeBitDepth(oldColor, colorDepth);
                    //newPixelArr[x, y] = newColor;
                    //Pixel error = oldColor - newColor;

                    //this is about 20% faster
                    Pixel oldColor = pixelArr[x, y];
                    pixelArr[x, y] = Program.ChangeBitDepth(oldColor, colorDepth);
                    Pixel error = oldColor - pixelArr[x, y];

                    for (int i = 0; i < ditherMethodIterations; i++)
                    {
                        //if (IsValidPixelPos(ref pixelArr, x + ditherMethod[i, 0], y + ditherMethod[i, 1]))
                        if (x + ditherMethod[i, 0] >= 0 && x + ditherMethod[i, 0] < imageWidth && y + ditherMethod[i, 1] < imageHeight)
                        {
                            pixelArr[x + ditherMethod[i, 0], y + ditherMethod[i, 1]] += (error * ditherMethod[i, 2]) / 16f;
                        }
                    }
                }
            }

            return pixelArr;
        }

        public static byte[] ChangeBitDepthAndDitherFast(byte[] colorArr, int imgChannels, int imgWidth, int imgHeight, byte colorDepth)
        {
            //imgChannels = 3;
            int[] bigColorArray = new int[colorArr.Length];

            int[,] ditherMethod = FloydSteinbergOffsetAndWeight;
            int ditherMethodIterations = ditherMethod.GetLength(0);

            for (int b = 0; b < bigColorArray.Length; b++)
            {
                bigColorArray[b] = colorArr[b];
            }

            for (int p = 0; p < bigColorArray.Length; p++)
            {
                //color value of a single channel
                int oldColor = bigColorArray[p];
                byte newColor = ChangeBitDepth(oldColor, colorDepth);
                colorArr[p] = newColor;
                int error = oldColor - newColor;

                for (int i = 0; i < ditherMethodIterations; i++)
                {
                    int pos = p + (ditherMethod[i, 0] + (imgWidth * ditherMethod[i, 1])) * imgChannels;
                    bool isAfterWidth = ((p / colorDepth) % imgWidth == (imgWidth - 1)) && ((pos / colorDepth) % imgWidth == 0);
                    bool isBeforeWidth = ((p / colorDepth) % imgWidth == 0) && ((pos / colorDepth) % imgWidth == (imgWidth - 1));

                    if (pos < bigColorArray.Length && !isAfterWidth && !isBeforeWidth)
                    {
                        bigColorArray[pos] += (int)Math.Round((error * (float)ditherMethod[i, 2]) / 16f);
                    }
                }
            }

            //for (int b = 0; b < bigColorArray.Length; b++)
            //{
            //    colorArr[b] = (byte)bigColorArray[b];
            //}

            return colorArr;
        }

        private static byte ChangeBitDepth(int singleChannelColor, byte colorDepth)
        {
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);

            return (byte)Math.Round(Math.Round(singleChannelColor / colorStepInterval) * colorStepInterval);
        }

        private static bool IsValidPixelPos(ref Pixel[,] image, int x, int y)
        {
            return (x >= 0 && /*y >= 0 && */x < image.GetLength(0) && y < image.GetLength(1));
        }

        //xOffset, yOffset, errorWeight
        private static readonly int[,] FloydSteinbergOffsetAndWeight =
            new int[,]
            {
                { 1, 0, 7 },
                {-1, 1, 3 },
                { 0, 1, 5 },
                { 1, 1, 1 },
            };
    }
}
