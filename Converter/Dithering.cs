using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SEImageToLCD_15BitColor
{
    public class Dithering
    {
        [DllImport("Image Processor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DitherCPP([In, Out]byte[] colorArr, int imgByteSize, int width, int imgStride, double colorStepInterval);

        public static void ChangeBitDepthAndDitherFastThreaded(byte[] colorArr, int colorChannels, int width, byte colorDepth, int imgStride)
        {
            int[] bigColorArr = new int[colorArr.Length];
            double colorStepInterval = 255.0 / (Math.Pow(2, colorDepth) - 1);

            Task.WaitAll(new Task[3]
            {
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 0, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 1, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
                Task.Run(() => ChangeBitDepthAndDitherThread(colorArr, bigColorArr, colorChannels, 2, width, imgStride, FloydSteinbergOffsetAndWeight, colorStepInterval)),
            });
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
