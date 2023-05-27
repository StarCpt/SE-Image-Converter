using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Documents;

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
                7.0 / 16.0, // 0.4375
                3.0 / 16.0, // 0.1875
                5.0 / 16.0, // 0.3125
                1.0 / 16.0, // 0.0625
            };

        internal Converter(ConvertOptions options)
        {
            this.Options = options;

            if (Options.ConvertedSize.Width > 10000)
            {
                Options.ConvertedSize = new Size(
                    Convert.ToInt32(Options.ConvertedSize.Width * 10000.0 / Options.ConvertedSize.Width),
                    Convert.ToInt32(Options.ConvertedSize.Height * 10000.0 / Options.ConvertedSize.Width));
                App.Instance.Log.Log($"Converter.ctor: Width too large. Resizing to 10000");
            }

            if (Options.ConvertedSize.Height > 10000)
            {
                Options.ConvertedSize = new Size(
                    Convert.ToInt32(Options.ConvertedSize.Width * 10000.0 / Options.ConvertedSize.Height),
                    Convert.ToInt32(Options.ConvertedSize.Height * 10000.0 / Options.ConvertedSize.Height));
                App.Instance.Log.Log($"Converter.ctor: Height too large. Resizing to 10000");
            }

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
            Monitor.Enter(image);
            Bitmap bitmap = new Bitmap(image);
            Monitor.Exit(image);

            try
            {
                ApplyScaleOffset(ref bitmap, false);

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
            Monitor.Enter(image);
            Bitmap bitmap = new Bitmap(image);
            Monitor.Exit(image);

            try
            {
                ApplyScaleOffset(ref bitmap, true);

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
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            Monitor.Enter(image);
            Bitmap bitmap = ApplyScaleOffset(image, false);
            Monitor.Exit(image);

            long c1 = sw.ElapsedMilliseconds;
            sw.Restart();

            try
            {
                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                IntPtr ptr = data.Scan0;

                int sizeInBytes = data.Stride * data.Height;
                byte[] bitmapBytes = new byte[sizeInBytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bitmapBytes, 0, sizeInBytes);

                ChangeBitDepth(bitmapBytes, data, token);

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

        internal string ConvertNoColorDepthChangeSafe(Image image, CancellationToken token)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Monitor.Enter(image);
            Bitmap bitmap = ApplyScaleOffset(image, false);
            Monitor.Exit(image);

            long c1 = sw.ElapsedMilliseconds;
            sw.Restart();

            try
            {
                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                IntPtr ptr = data.Scan0;

                int sizeInBytes = data.Stride * data.Height;
                byte[] bitmapBytes = new byte[sizeInBytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bitmapBytes, 0, sizeInBytes);

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
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            Monitor.Enter(image);
            Bitmap bitmap = ApplyScaleOffset(image, true);
            Monitor.Exit(image);

            double c1 = sw.Elapsed.TotalMilliseconds;
            App.Instance.Log.Log($"ConvertToBitmapSafe ApplyScaleOffset {c1} ms");
            sw.Restart();

            try
            {
                token.ThrowIfCancellationRequested();

                if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                    throw new Exception("Pixel format not supported");

                //Format24bppRgb is [Blue, Green, Red]
                BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                IntPtr ptr = data.Scan0;

                int sizeInBytes = data.Stride * data.Height;
                byte[] bitmapBytes = new byte[sizeInBytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bitmapBytes, 0, sizeInBytes);

                ChangeBitDepth(bitmapBytes, data, token);

                double c2 = sw.Elapsed.TotalMilliseconds;
                App.Instance.Log.Log($"ConvertToBitmapSafe ChangeBitDepth {c2} ms");
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

        private void ChangeBitDepth(byte[] bytes, BitmapData data, CancellationToken token)
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
                int stride = data.Stride;
                int height = data.Height;
                int channels = 3; // Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
                int padding = stride - (data.Width * channels);
                int bitmapWidth = data.Width;
                int bitmapByteWidth = bitmapWidth * channels;

                int separation = channels * 2;
                int workerCount = Math.Min(Environment.ProcessorCount, height);

                Queue<Task> workerQueue = new Queue<Task>();
                int[] rowProgress = new int[height];
                ManualResetEventSlim[] rowLocks = new ManualResetEventSlim[height];

                SemaphoreSlim maxWorkerLock = new SemaphoreSlim(workerCount);

                for (int r = 0; r < height; r++)
                {
                    maxWorkerLock.Wait();
                    token.ThrowIfCancellationRequested();

                    int row = r;
                    rowLocks[row] = new ManualResetEventSlim(false);
                    workerQueue.Enqueue(Task.Run(() => ProcessRow(row)));
                }

                while (workerQueue.TryDequeue(out Task? result))
                {
                    result.Wait();
                }

                foreach (ManualResetEventSlim resetEvent in rowLocks)
                {
                    resetEvent.Dispose();
                }

                maxWorkerLock.Dispose();

                void ProcessRow(int row)
                {
                    int rowStartIndex = row * stride;
                    int rowEndIndex = rowStartIndex + bitmapByteWidth;

                    ManualResetEventSlim rowLock = rowLocks[row];
                    ManualResetEventSlim? prevRowLock = null;
                    if (row > 0)
                    {
                        prevRowLock = rowLocks[row - 1];
                    }

                    int rowPositionIndex = 0; //bytes not pixels
                    for (int i = rowStartIndex; i < rowEndIndex; i++)
                    {
                        //check the progress of the row above
                        if (row > 0)
                        {
                            while (rowProgress[row - 1] - separation <= rowPositionIndex)
                            {
#pragma warning disable CS8602
                                prevRowLock.Wait(Timeout.Infinite, CancellationToken.None);
#pragma warning restore CS8602
                                prevRowLock.Reset();
                            }
                        }

                        byte oldColor = bytes[i];
                        int error = oldColor - (bytes[i] = Precalc[oldColor]);

                        if (error != 0)
                        {
                            ApplyErrorIntFast(error, i, rowPositionIndex, row);
                        }

                        i++;

                        oldColor = bytes[i];
                        error = oldColor - (bytes[i] = Precalc[oldColor]);

                        if (error != 0)
                        {
                            ApplyErrorIntFast(error, i, rowPositionIndex, row);
                        }

                        i++;

                        oldColor = bytes[i];
                        error = oldColor - (bytes[i] = Precalc[oldColor]);

                        if (error != 0)
                        {
                            ApplyErrorIntFast(error, i, rowPositionIndex, row);
                        }

                        rowPositionIndex += channels;
                        rowProgress[row] = rowPositionIndex;
                        rowLock.Set();
                    }

                    rowProgress[row] += separation;
                    rowLock.Set();

                    maxWorkerLock.Release();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void ApplyErrorDoubleFloat(int error, int byteIndex, int columnByteIndex, int row)
                {
                    if (columnByteIndex + channels < bitmapByteWidth)
                    {
                        // 1, 0
                        bytes[byteIndex + channels] = Convert.ToByte(Math.Clamp(bytes[byteIndex + channels] + error * 0.4375, 0.0, 255.0));
                    }

                    if (row + 1 < data.Height)
                    {
                        if (columnByteIndex - channels >= 0)
                        {
                            // -1, 1
                            bytes[byteIndex - channels + data.Stride] = Convert.ToByte(Math.Clamp(bytes[byteIndex - channels + data.Stride] + error * 0.1875, 0.0, 255.0));
                        }

                        // 0, 1
                        bytes[byteIndex + data.Stride] = Convert.ToByte(Math.Clamp(bytes[byteIndex + data.Stride] + error * 0.3125, 0.0, 255.0));

                        if (columnByteIndex + channels < bitmapByteWidth)
                        {
                            //1, 1
                            bytes[byteIndex + channels + data.Stride] = Convert.ToByte(Math.Clamp(bytes[byteIndex + channels + data.Stride] + error * 0.0625, 0.0, 255.0));
                        }
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void ApplyErrorIntFast(int error, int byteIndex, int columnByteIndex, int row)
                {
                    if (columnByteIndex + channels < bitmapByteWidth)
                    {
                        // 1, 0
                        bytes[byteIndex + channels] = (byte)Math.Clamp(bytes[byteIndex + channels] + ((error * 7) >> 4), 0, 255);
                    }

                    if (row + 1 < height)
                    {
                        if (columnByteIndex - channels >= 0)
                        {
                            // -1, 1
                            bytes[byteIndex - channels + stride] = (byte)Math.Clamp(bytes[byteIndex - channels + stride] + ((error * 3) >> 4), 0, 255);
                        }

                        // 0, 1
                        bytes[byteIndex + stride] = (byte)Math.Clamp(bytes[byteIndex + data.Stride] + ((error * 5) >> 4), 0, 255);

                        if (columnByteIndex + channels < bitmapByteWidth)
                        {
                            //1, 1
                            bytes[byteIndex + channels + stride] = (byte)Math.Clamp(bytes[byteIndex + channels + stride] + ((error) >> 4), 0, 255);
                        }
                    }
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

        private void ApplyScaleOffset(ref Bitmap bitmap, bool fillTarget)
        {
            Bitmap result = ApplyScaleOffset(bitmap, fillTarget);

            bitmap.Dispose();
            bitmap = result;
        }

        private Bitmap ApplyScaleOffset(Image bitmap, bool fillTarget)
        {
            double widthScale = (double)bitmap.Width / Options.ConvertedSize.Width;
            double heightScale = (double)bitmap.Height / Options.ConvertedSize.Height;
            //Min = scale source to fill the convertedSize rect
            //Max = scale source to fit the convertedSize rect
            double biggerScale = fillTarget ? Math.Min(widthScale, heightScale) : Math.Max(widthScale, heightScale);

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

            return destImage;
        }
    }
}
