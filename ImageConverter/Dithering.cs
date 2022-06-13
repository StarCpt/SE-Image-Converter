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
            Pixel[,] newPixelArr = new Pixel[pixelArr.GetLength(0), pixelArr.GetLength(1)];

            int[,] ditherMethod = FloydSteinbergOffsetAndWeight;//change when there are more

            for (int y = 0; y < pixelArr.GetLength(1); y++)
            {
                for (int x = 0; x < pixelArr.GetLength(0); x++)
                {
                    Pixel newColor = Program.ChangeBitDepth(pixelArr[x, y], colorDepth);
                    newPixelArr[x, y] = newColor;
                    Pixel error = pixelArr[x, y] - newColor;

                    for (int i = 0; i < ditherMethod.GetLength(0); i++)
                    {
                        if (IsValidPixelPos(ref pixelArr, x + ditherMethod[i, 0], y + ditherMethod[i, 1]))
                        {
                            pixelArr[x + ditherMethod[i, 0], y + ditherMethod[i, 1]] += (error * ditherMethod[i, 2]) / 16f;
                        }
                    }
                }
            }

            return newPixelArr;
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
