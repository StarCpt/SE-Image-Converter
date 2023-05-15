using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageConverterPlus.ImageConverter
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

    public struct ConvertOptions
    {
        public int BitsPerChannel { readonly get; set; }
        public bool Dithering { readonly get; set; }
        public InterpolationMode Interpolation { readonly get; set; }
        public Size ConvertedSize { readonly get; set; }
        public double Scale { readonly get; set; }
        public Point TopLeft { readonly get; set; }
    }

    internal class Converter
    {
        private readonly ConvertOptions Options;
        private readonly byte[] Precalc;
        //xOffset, yOffset
        private static readonly int[,] DitherOffsets =
            new int[4, 2]
            {
                { 1, 0 },
                {-1, 1 },
                { 0, 1 },
                { 1, 1 },
            };
        //errorWeight
        private static readonly double[] DitherWeights =
            new double[4]
            {
                7.0 / 16.0,
                3.0 / 16.0,
                5.0 / 16.0,
                1.0 / 16.0,
            };

        internal Converter(ConvertOptions options)
        {
            this.Options = options;
            Precalc = new byte[256];
            FillPrecalcArray();
        }

        private void FillPrecalcArray()
        {
            double colorStep = 255.0 / (Math.Pow(2, Options.BitsPerChannel) - 1);
            int shiftRight = 8 - Options.BitsPerChannel;

            for (int i = 0; i < 256; i++)
            {
                Precalc[i] = Convert.ToByte((i >> shiftRight) * colorStep);
            }
        }

        internal unsafe string ConvertUnsafe(Image image, CancellationToken token)
        {
            Bitmap bitmap = new Bitmap(image);

            try
            {
                ApplyScaleOffset(ref bitmap);

                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                byte* ptr = (byte*)data.Scan0.ToPointer();

                ChangeBitDepthUnsafe(ptr, data);

                token.ThrowIfCancellationRequested();

                string convertedString = GetConvertedStringUnsafe(ptr, data);

                bitmap.UnlockBits(data);

                token.ThrowIfCancellationRequested();

                return convertedString;
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        internal unsafe Bitmap ConvertToBitmapUnsafe(Image image, CancellationToken token)
        {
            Bitmap bitmap = new Bitmap(image);

            try
            {
                ApplyScaleOffset(ref bitmap);

                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                byte* ptr = (byte*)data.Scan0.ToPointer();

                ChangeBitDepthUnsafe(ptr, data);

                token.ThrowIfCancellationRequested();

                bitmap.UnlockBits(data);

                token.ThrowIfCancellationRequested();

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private unsafe void ChangeBitDepthUnsafe(byte* ptr, BitmapData data)
        {
            int sizeInBytes = data.Stride * data.Height;

            if (!Options.Dithering)
            {
                for (int i = 0; i < sizeInBytes; i++)
                {
                    ptr[i] = Precalc[ptr[i]];
                }
            }
            else
            {
                int channels = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
                int padding = data.Stride - (data.Width * channels);
                int bitmapWidth = data.Width;
                int ditherIterations = DitherOffsets.GetLength(0);

                //Precalculate dither offsets for pixel format
                int[] ditherOffsets = new int[ditherIterations];
                for (int i = 0; i < ditherIterations; i++)
                {
                    ditherOffsets[i] = (DitherOffsets[i, 0] * channels) + (DitherOffsets[i, 1] * data.Stride);
                }

                for (int c = 0; c < channels; c++)
                {
                    int rowPixelIndex = 0;

                    for (int i = c; i < sizeInBytes; i += channels)
                    {
                        byte oldColor = ptr[i];
                        ptr[i] = Precalc[ptr[i]];
                        int error = oldColor - ptr[i];

                        if (error != 0)
                        {
                            for (int d = 0; d < ditherIterations; d++)
                            {
                                int targetIndex = i + ditherOffsets[d];
                                int targetRowIndex = rowPixelIndex + DitherOffsets[d, 0];

                                bool outOfBounds =
                                    (targetIndex >= sizeInBytes) ||
                                    (targetRowIndex < 0) ||
                                    (targetRowIndex >= bitmapWidth);

                                if (!outOfBounds)
                                {
                                    ptr[targetIndex] = Convert.ToByte(Math.Clamp(ptr[targetIndex] + error * DitherWeights[d], 0.0, 255.0));
                                }
                            }
                        }

                        rowPixelIndex++;
                        if (rowPixelIndex >= bitmapWidth)
                        {
                            rowPixelIndex = 0;
                            i += padding;
                        }
                    }
                }
            }
        }

        private unsafe string GetConvertedStringUnsafe(byte* ptr, BitmapData data)
        {
            int channels = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            int sizeInBytes = data.Stride * data.Height;
            int bitmapWidth = data.Width;
            int padding = data.Stride - (data.Width * channels);
            //+1 to width because of the newline character
            char[] temp = new char[(data.Width + 1) * data.Height];
            int arrayIndex = 0;
            int rowPixelIndex = 0;

            for (int i = 0; i < sizeInBytes; i += channels)
            {
                //Format24bppRgb is [Blue, Green, Red] BGR
                temp[arrayIndex] = GetColorChar(ptr[i + 2], ptr[i + 1], ptr[i]);
                arrayIndex++;

                rowPixelIndex++;
                if (rowPixelIndex >= bitmapWidth)
                {
                    temp[arrayIndex] = '\n';
                    arrayIndex++;

                    rowPixelIndex = 0;
                    i += padding;
                }
            }

            return new string(temp);
        }

        internal string ConvertSafe(Image image, CancellationToken token)
        {
            Bitmap bitmap = new Bitmap(image);

            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                ApplyScaleOffset(ref bitmap);

                long c1 = sw.ElapsedMilliseconds;
                sw.Restart();

                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                IntPtr ptr = data.Scan0;

                int sizeInBytes = data.Stride * data.Height;
                byte[] bitmapBytes = new byte[sizeInBytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bitmapBytes, 0, sizeInBytes);

                ChangeBitDepth(bitmapBytes, data);

                long c2 = sw.ElapsedMilliseconds;
                sw.Restart();

                token.ThrowIfCancellationRequested();

                string convertedString = GetConvertedString(bitmapBytes, data);

                long c3 = sw.ElapsedMilliseconds;
                sw.Restart();

                System.Runtime.InteropServices.Marshal.Copy(bitmapBytes, 0, ptr, sizeInBytes);

                bitmap.UnlockBits(data);

                token.ThrowIfCancellationRequested();

                return convertedString;
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        internal Bitmap ConvertToBitmapSafe(Image image, CancellationToken token)
        {
            Bitmap bitmap = new Bitmap(image);

            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                ApplyScaleOffset(ref bitmap);

                long c1 = sw.ElapsedMilliseconds;
                sw.Restart();

                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                IntPtr ptr = data.Scan0;

                int sizeInBytes = data.Stride * data.Height;
                byte[] bitmapBytes = new byte[sizeInBytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bitmapBytes, 0, sizeInBytes);

                ChangeBitDepth(bitmapBytes, data);

                long c2 = sw.ElapsedMilliseconds;
                sw.Restart();

                token.ThrowIfCancellationRequested();

                System.Runtime.InteropServices.Marshal.Copy(bitmapBytes, 0, ptr, sizeInBytes);

                bitmap.UnlockBits(data);

                token.ThrowIfCancellationRequested();

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private void ChangeBitDepth(byte[] bytes, BitmapData data)
        {
            int sizeInBytes = data.Stride * data.Height;

            if (!Options.Dithering)
            {
                for (int i = 0; i < sizeInBytes; i++)
                {
                    bytes[i] = Precalc[bytes[i]];
                }
            }
            else
            {
                int channels = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
                int padding = data.Stride - (data.Width * channels);
                int bitmapWidth = data.Width;
                int bitmapByteWidth = bitmapWidth * channels;
                int ditherIterations = DitherOffsets.GetLength(0);

                //Precalculate offsets for each dithering iteration for pixel format
                int[] ditherOffsets = new int[ditherIterations];
                for (int i = 0; i < ditherIterations; i++)
                {
                    ditherOffsets[i] = (DitherOffsets[i, 0] * channels) + (DitherOffsets[i, 1] * data.Stride);
                }

                Stopwatch sw = new Stopwatch();

                int separation = channels;

                int workerCount = Environment.ProcessorCount * 2;

                SemaphoreSlim maxThreadLock = new SemaphoreSlim(workerCount);
                int[,] rowProgress = new int[data.Height,channels];
                SemaphoreSlim[,] rowLocks = new SemaphoreSlim[data.Height,channels];
                Task[,] tasks = new Task[data.Height,channels];

                sw.Start();
                for (int r = 0; r < data.Height; r++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        maxThreadLock.Wait();
                        rowLocks[r, c] = new SemaphoreSlim(1);
                        int row = r;
                        int channel = c;
                        tasks[r, c] = Task.Run(() => WorkRowChannel(row, channel));
                    }
                }

                foreach (var task in tasks)
                {
                    task.Wait();
                }
                double ms = sw.Elapsed.TotalMilliseconds;

                foreach (var l in rowLocks)
                {
                    l.Dispose();
                }

                return;

                void WorkRowChannel(int row, int channel)
                {
                    int rowStartIndex = row * data.Stride;
                    int rowEndIndex = rowStartIndex + bitmapByteWidth;

                    int rowPositionIndex = 0;
                    for (int i = rowStartIndex; i < rowEndIndex; i += channels)
                    {
                        //check the progress of the row above
                        if (row > 0)
                        {
                            while (rowProgress[row - 1, channel] - separation <= rowPositionIndex)
                            {
                                rowLocks[row - 1, channel]?.Wait();
                            }
                        }

                        byte oldColor = bytes[i];
                        bytes[i] = Precalc[bytes[i]];
                        int error = oldColor - bytes[i];

                        if (error != 0)
                        {
                            for (int d = 0; d < ditherIterations; d++)
                            {
                                int targetIndex = i + ditherOffsets[d];
                                int targetRowIndex = rowPositionIndex + (DitherOffsets[d, 0] * channels);

                                bool outOfBounds =
                                    (targetIndex >= sizeInBytes) ||
                                    (targetRowIndex < 0) ||
                                    (targetRowIndex >= bitmapByteWidth);

                                if (!outOfBounds)
                                {
                                    bytes[targetIndex] = Convert.ToByte(Math.Clamp(bytes[targetIndex] + error * DitherWeights[d], 0.0, 255.0));
                                }
                            }
                        }

                        rowPositionIndex++;
                        rowProgress[row, channel] = rowPositionIndex;
                        rowLocks[row, channel].Release();
                    }

                    Interlocked.Add(ref rowProgress[row, channel], separation);
                    rowLocks[row, channel].Release();
                    maxThreadLock.Release();
                }

            }
        }

        public static byte ToByte(double value)
        {
            if (value >= 0)
            {
                if (value < 255.5)
                {
                    byte result = (byte)value;
                    double dif = value - result;
                    if (dif > 0.5 || dif == 0.5 && (result & 1) != 0) result++;
                    return result;
                }
            }
            else
            {
                if (value >= -0.5)
                {
                    byte result = (byte)value;
                    double dif = value - result;
                    if (dif < -0.5 || dif == -0.5 && (result & 1) != 0) result--;
                    return result;
                }
            }
            throw new OverflowException();
        }

        private string GetConvertedString(byte[] bytes, BitmapData data)
        {
            int channels = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            int sizeInBytes = data.Stride * data.Height;
            int bitmapWidth = data.Width;
            int padding = data.Stride - (data.Width * channels);
            //+1 to width because of the newline character
            char[] temp = new char[(data.Width + 1) * data.Height];
            int arrayIndex = 0;
            int rowPixelIndex = 0;

            for (int i = 0; i < sizeInBytes; i += channels)
            {
                //Format24bppRgb is [Blue, Green, Red] BGR
                temp[arrayIndex] = GetColorChar(bytes[i + 2], bytes[i + 1], bytes[i]);
                arrayIndex++;

                rowPixelIndex++;
                if (rowPixelIndex >= bitmapWidth)
                {
                    temp[arrayIndex] = '\n';
                    arrayIndex++;

                    rowPixelIndex = 0;
                    i += padding;
                }
            }

            return new string(temp);
        }

        private char GetColorChar(byte r, byte g, byte b)
        {
            int shiftRight = 8 - Options.BitsPerChannel;
            int shiftLeft = Options.BitsPerChannel;

            uint add = Options.BitsPerChannel == 3 ? 0xe100 : 0x3000u;

            return (char)(add + ((r >> shiftRight) << (shiftLeft * 2)) + ((g >> shiftRight) << shiftLeft) + (b >> shiftRight));
        }

        private void ApplyScaleOffset(ref Bitmap bitmap)
        {
            double widthScale = (double)bitmap.Width / Options.ConvertedSize.Width;
            double heightScale = (double)bitmap.Height / Options.ConvertedSize.Height;
            double biggerScale = Math.Min(widthScale, heightScale); //Min = fill, Max = fit

            double scaledBitmapWidth = bitmap.Width * Options.Scale / biggerScale;
            double scaledBitmapHeight = bitmap.Height * Options.Scale / biggerScale;

            Size scaledSize = new Size
            {
                Width = Convert.ToInt32(scaledBitmapWidth),
                Height = Convert.ToInt32(scaledBitmapHeight),
            };
            Rectangle destREct = new Rectangle(Options.TopLeft, scaledSize);
            //Format24bppRgb is [Blue, Green, Red]
            Bitmap destImage = new Bitmap(Options.ConvertedSize.Width, Options.ConvertedSize.Height, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(destImage))
            {
                g.InterpolationMode = Options.Interpolation;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(bitmap, destREct, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
                }
            }

            bitmap.Dispose();
            bitmap = destImage;
        }
    }
}
