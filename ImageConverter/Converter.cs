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

    internal class ConvertBlock
    {
        internal Point StartPosition { get; }
        internal Size BlockSize { get; }
        internal Point BlockPosition { get; }
        internal volatile bool Started;
        internal volatile bool Completed;
        internal ManualResetEventSlim ResumeCheckEvent { get; }

        internal ConvertBlock(Point startPosition, Size blockSize, Point blockPosition, ManualResetEventSlim resumeCheckEvent)
        {
            StartPosition = startPosition;
            BlockSize = blockSize;
            BlockPosition = blockPosition;
            ResumeCheckEvent = resumeCheckEvent;
        }
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

                double c1 = sw.Elapsed.TotalMilliseconds;
                MainWindow.Logging.Log($"ChangeBitDepth {c1} ms");
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

                double c2 = sw.Elapsed.TotalMilliseconds;
                MainWindow.Logging.Log($"ChangeBitDepth {c2} ms");
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
                Stopwatch sw = new Stopwatch();
                sw.Start();

                int stride = data.Stride;
                int height = data.Height;
                int channels = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
                int padding = stride - (data.Width * channels);
                int bitmapWidth = data.Width;
                int bitmapByteWidth = bitmapWidth * channels;
                int ditherIterations = DitherOffsets.GetLength(0);

                //Precalculate offsets for each dithering iteration for pixel format
                int[] ditherOffsets = new int[ditherIterations];
                for (int i = 0; i < ditherIterations; i++)
                {
                    ditherOffsets[i] = (DitherOffsets[i, 0] * channels) + (DitherOffsets[i, 1] * data.Stride);
                }

                ParallelUsingBlockWithManualResetEvent();
                //ParallelUsingManualResetEvent();

                void ParallelUsingBlockWithManualResetEvent()
                {
                    sw.Start();
                    int workers = Environment.ProcessorCount;

                    int blockHeight = (int)Math.Ceiling(Math.Sqrt(2 * height));
                    int blockWidth = (int)Math.Ceiling(bitmapWidth / (blockHeight * (workers - 0.5) + (workers + 0.5)));

                    blockHeight = height;
                    blockWidth = bitmapWidth / 2;

                    int yBlocks = (height - 1) / blockHeight + 1;
                    int xBlocks = (bitmapWidth - 1) / blockWidth + 1;

                    ConvertBlock[,] blocks = new ConvertBlock[xBlocks, yBlocks];
                    BlockingCollection<ConvertBlock> blockQueue = new BlockingCollection<ConvertBlock>(new ConcurrentQueue<ConvertBlock>());
                    object queueLock = new object();

                    for (int y = 0; y < yBlocks; y++)
                    {
                        for (int x = 0; x < xBlocks; x++)
                        {
                            Point startPos = new Point(x * blockWidth, y * blockHeight);

                            Size blockSize = new Size(
                                x == xBlocks - 1 ? (bitmapWidth - 1) % blockWidth + 1 : blockWidth,
                                y == yBlocks - 1 ? (height - 1) % blockHeight + 1 : blockHeight);

                            Point blockPosition = new Point(x, y);

                            blocks[x, y] = new ConvertBlock(startPos, blockSize, blockPosition, new ManualResetEventSlim(false));
                        }
                    }

                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    ManualResetEvent completedEvent = new ManualResetEvent(false);

                    for (int t = 0; t < workers; t++)
                    {
                        //Task.Run(() => BlockQueueChecker(tokenSource.Token));
                    }

                    lock (queueLock)
                    {
                        //ProcessBlock(blocks[0, 0]);
                    }

                    for (int y = 0; y < yBlocks; y++)
                    {
                        for (int x = 0; x < xBlocks; x++)
                        {
                            ProcessPixelBlock(blocks[x, y]);
                        }
                    }

                    //completedEvent.WaitOne();
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds;
                    return;

                    void BlockQueueChecker(CancellationToken token)
                    {
                        try
                        {
                            while (true)
                            {
                                ProcessPixelBlock(blockQueue.Take(token));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }

                    void ProcessPixelBlock(ConvertBlock block)
                    {
                        block.Started = true;

                        int startWidth = block.StartPosition.X;
                        int startHeight = block.StartPosition.Y;

                        int endWidth = block.StartPosition.X + block.BlockSize.Width;
                        int endHeight = block.StartPosition.Y + block.BlockSize.Height;

                        for (int y = startHeight; y < endHeight; y++)
                        {
                            for (int x = startWidth; x < endWidth; x++)
                            {
                                int bytePos = y * stride + x;

                                byte oldColor = bytes[bytePos];
                                bytes[bytePos] = Precalc[bytes[bytePos]];
                                int error = oldColor - (bytes[bytePos]);

                                if (error != 0)
                                {
                                    ApplyErrorIntFast(error, bytePos, x, y);
                                }

                                bytePos++;

                                oldColor = bytes[bytePos];
                                bytes[bytePos] = Precalc[bytes[bytePos]];
                                error = oldColor - (bytes[bytePos]);

                                if (error != 0)
                                {
                                    ApplyErrorIntFast(error, bytePos, x, y);
                                }

                                bytePos++;

                                oldColor = bytes[bytePos];
                                bytes[bytePos] = Precalc[bytes[bytePos]];
                                error = oldColor - (bytes[bytePos]);

                                if (error != 0)
                                {
                                    ApplyErrorIntFast(error, bytePos, x, y);
                                }
                            }
                        }

                        block.Completed = true;

                        lock (queueLock)
                        {
                            bool canRightBlockStart = 
                                (block.BlockPosition.Y == 0 || blocks[block.BlockPosition.X, block.BlockPosition.Y - 1].Completed)
                                && block.BlockPosition.X != xBlocks - 1
                                && !blocks[block.BlockPosition.X + 1, block.BlockPosition.Y].Started;
                            bool canBottomBlockStart =
                                (block.BlockPosition.X == 0 || blocks[block.BlockPosition.X - 1, block.BlockPosition.Y].Completed)
                                && block.BlockPosition.Y != yBlocks - 1
                                && !blocks[block.BlockPosition.X, block.BlockPosition.Y + 1].Started;

                            if (canRightBlockStart)
                            {
                                blockQueue.Add(blocks[block.BlockPosition.X + 1, block.BlockPosition.Y]);
                            }

                            if (canBottomBlockStart)
                            {
                                blockQueue.Add(blocks[block.BlockPosition.X, block.BlockPosition.Y + 1]);
                            }
                        }

                        if (block.BlockPosition.Y == yBlocks - 1 && block.BlockPosition.X == xBlocks - 1)
                        {
                            completedEvent.Set();
                        }
                    }
                }

                void DitherPixelBlock(int startWidth, int startHeight, int endWidth, int endHeight)
                {
                    for (int y = startHeight; y < endHeight; y++)
                    {
                        for (int x = startWidth; x < endWidth; x++)
                        {
                            DitherPixel(x, y);
                        }
                    }
                }

                void DitherPixel(int x, int y)
                {
                    int bytePos = (y * stride) + (x * channels);

                    byte oldColor = bytes[bytePos];
                    bytes[bytePos] = Precalc[bytes[bytePos]];
                    int error = oldColor - (bytes[bytePos]);

                    if (error != 0)
                    {
                        ApplyErrorIntFast(error, bytePos, x, y);
                    }

                    bytePos++;

                    oldColor = bytes[bytePos];
                    bytes[bytePos] = Precalc[bytes[bytePos]];
                    error = oldColor - (bytes[bytePos]);

                    if (error != 0)
                    {
                        ApplyErrorIntFast(error, bytePos, x, y);
                    }

                    bytePos++;

                    oldColor = bytes[bytePos];
                    bytes[bytePos] = Precalc[bytes[bytePos]];
                    error = oldColor - (bytes[bytePos]);

                    if (error != 0)
                    {
                        ApplyErrorIntFast(error, bytePos, x, y);
                    }
                }

                void ParallelUsingManualResetEvent() // thread per row
                {
                    double init = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();

                    //unchanging
                    int separation = channels * 2;
                    int workerCount = Math.Min(Environment.ProcessorCount, height);

                    //Task[] workers = new Task[workerCount];
                    Queue<Task> workerQueue = new Queue<Task>();
                    int[] rowProgress = new int[height];
                    ManualResetEventSlim[] rowLocks = new ManualResetEventSlim[height];

                    SemaphoreSlim maxWorkerLock = new SemaphoreSlim(workerCount);

                    int r = 0;

                    //for (int w = 0; w < workerCount; w++)
                    //{
                    //    int row = r;
                    //    rowLocks[row] = new ManualResetEventSlim(false);
                    //    workers[w] = Task.Run(() => DitherRowChannel(row));
                    //}

                    for (; r < height; r++)
                    {
                        maxWorkerLock.Wait();
                        //int w = Task.WaitAny(workers);

                        int row = r;
                        rowLocks[row] = new ManualResetEventSlim(false);
                        workerQueue.Enqueue(Task.Run(() => DitherRowChannel(row)));
                        //workers[w] = Task.Run(() => DitherRowChannel(row));
                    }

                    //Task.WaitAll(workers);

                    while (workerQueue.TryDequeue(out Task? result))
                    {
                        result.Wait();
                    }

                    foreach (ManualResetEventSlim resetEvent in rowLocks)
                    {
                        resetEvent.Dispose();
                    }

                    double time = sw.Elapsed.TotalMilliseconds;
                    sw.Stop();

                    void DitherRowChannel(int row)
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
                        for (int i = rowStartIndex; i < rowEndIndex; i += channels)
                        {
                            //check the progress of the row above
                            if (row > 0)
                            {
                                while (rowProgress[row - 1] - separation <= rowPositionIndex)
                                {
                                    prevRowLock.Wait(Timeout.Infinite, CancellationToken.None);
                                    prevRowLock.Reset();
                                }
                            }

                            byte oldColor = bytes[i];
                            bytes[i] = Precalc[bytes[i]];
                            int error = oldColor - bytes[i];

                            if (error != 0)
                            {
                                ApplyErrorIntFast(error, i, rowPositionIndex, row);
                            }

                            oldColor = bytes[i + 1];
                            bytes[i + 1] = Precalc[bytes[i + 1]];
                            error = oldColor - bytes[i + 1];

                            if (error != 0)
                            {
                                ApplyErrorIntFast(error, i + 1, rowPositionIndex, row);
                            }

                            oldColor = bytes[i + 2];
                            bytes[i + 2] = Precalc[bytes[i + 2]];
                            error = oldColor - bytes[i + 2];

                            if (error != 0)
                            {
                                ApplyErrorIntFast(error, i + 2, rowPositionIndex, row);
                            }

                            rowPositionIndex += channels;
                            rowProgress[row] = rowPositionIndex;
                            rowLock.Set();
                        }

                        rowProgress[row] += separation;
                        rowLock.Set();

                        maxWorkerLock.Release();
                    }
                }

                void ParallelUsingManualResetEventPerChannel() // per row and per channel
                {
                    double init = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();

                    //unchanging
                    int separation = channels * 2;
                    int workerCount = Math.Min(Environment.ProcessorCount, data.Height);

                    //Task[] workers = new Task[workerCount];
                    Queue<Task> workerQueue = new Queue<Task>();
                    int[,] rowProgress = new int[data.Height, channels];
                    ManualResetEventSlim[,] rowLocks = new ManualResetEventSlim[data.Height, channels];
                    //ConcurrentDictionary<int, ManualResetEventSlim> rowLocks = new ConcurrentDictionary<int, ManualResetEventSlim>();

                    SemaphoreSlim maxWorkerLock = new SemaphoreSlim(workerCount);

                    for (int r = 0; r < data.Height; r++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            maxWorkerLock.Wait();

                            int row = r;
                            int channel = c;
                            rowLocks[r, c] = new ManualResetEventSlim(false);
                            workerQueue.Enqueue(Task.Run(() => DitherRowChannel(row, channel)));
                        }
                    }

                    //Task.WaitAll(workers);
                    while (workerQueue.TryDequeue(out Task? result))
                    {
                        result.Wait();
                    }

                    foreach (ManualResetEventSlim resetEvent in rowLocks)
                    {
                        resetEvent.Dispose();
                    }

                    double time = sw.Elapsed.TotalMilliseconds;
                    sw.Stop();

                    void DitherRowChannel(int row, int channel)
                    {
                        int rowStartIndex = row * data.Stride;
                        int rowEndIndex = rowStartIndex + bitmapByteWidth;

                        ManualResetEventSlim rowLock = rowLocks[row, channel];
                        ManualResetEventSlim? prevRowLock = null;
                        if (row > 0)
                        {
                            prevRowLock = rowLocks[row - 1, channel];
                        }

                        int rowPositionIndex = channel; //bytes not pixels
                        for (int i = rowStartIndex + channel; i < rowEndIndex; i += channels)
                        {
                            //check the progress of the row above
                            if (row > 0)
                            {
                                while (rowProgress[row - 1, channel] - separation <= rowPositionIndex)
                                {
                                    prevRowLock.Wait();
                                    prevRowLock.Reset();
                                }
                            }

                            byte oldColor = bytes[i];
                            bytes[i] = Precalc[bytes[i]];
                            int error = oldColor - bytes[i];

                            if (error != 0)
                            {
                                ApplyErrorIntFast(error, i, rowPositionIndex, row);
                            }

                            rowPositionIndex += channels;
                            rowProgress[row, channel] = rowPositionIndex;
                            rowLock.Set();
                        }

                        rowProgress[row, channel] += separation;
                        rowLock.Set();

                        maxWorkerLock.Release();
                    }
                }

                void ParallelUsingSpinWaitPerChannel() // per row and per channel
                {
                    double init = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();

                    //unchanging
                    int separation = channels;
                    int workerCount = Math.Min(Environment.ProcessorCount, data.Height);

                    //Task[] workers = new Task[workerCount];
                    Queue<Task> workerQueue = new Queue<Task>();
                    int[,] rowProgress = new int[data.Height, channels];
                    SemaphoreSlim maxWorkerLock = new SemaphoreSlim(workerCount);

                    for (int r = 0; r < data.Height; r++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            maxWorkerLock.Wait();
                            //int workerIndex = Task.WaitAny(workers);

                            int row = r;
                            int channel = c;
                            //workers[workerIndex] = Task.Run(() => DitherRow(row));
                            workerQueue.Enqueue(Task.Run(() => DitherRowChannel(row, channel)));
                        }
                    }

                    //Task.WaitAll(workers);
                    while (workerQueue.TryDequeue(out Task? result))
                    {
                        result.Wait();
                    }

                    double time = sw.Elapsed.TotalMilliseconds;
                    sw.Stop();

                    void DitherRowChannel(int row, int channel)
                    {
                        SpinWait spinWait = new SpinWait();
                        int rowStartIndex = row * data.Stride;
                        int rowEndIndex = rowStartIndex + bitmapByteWidth;

                        int rowPositionIndex = channel; //bytes not pixels
                        for (int i = rowStartIndex + channel; i < rowEndIndex; i += channels)
                        {
                            //check the progress of the row above
                            if (row > 0)
                            {
                                while (rowProgress[row - 1, channel] - separation <= rowPositionIndex)
                                {
                                    spinWait.SpinOnce();
                                }
                                spinWait.Reset();
                            }

                            byte oldColor = bytes[i];
                            bytes[i] = Precalc[bytes[i]];
                            int error = oldColor - bytes[i];

                            if (error != 0)
                            {
                                ApplyErrorDoubleFloat(error, i, rowPositionIndex, row);
                            }

                            rowPositionIndex += channels;
                            rowProgress[row, channel] = rowPositionIndex;
                        }

                        rowProgress[row, channel] += separation;
                        maxWorkerLock.Release();
                    }
                }

                void ParallelUsingSpinWait() // per row and not per channel
                {
                    double init = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();

                    //unchanging
                    int separation = channels;
                    int workerCount = Math.Min(Environment.ProcessorCount, data.Height);

                    //Task[] workers = new Task[workerCount];
                    Queue<Task> workerQueue = new Queue<Task>();
                    int[] rowProgress = new int[data.Height];
                    SemaphoreSlim maxWorkerLock = new SemaphoreSlim(workerCount);

                    int r = 0;
                    for (; r < workerCount; r++)
                    {
                        int row = r;
                        //workers[r] = Task.Run(() => DitherRow(row));
                        workerQueue.Enqueue(Task.Run(() => DitherRow(row)));
                    }

                    for (; r < data.Height; r++)
                    {
                        maxWorkerLock.Wait();
                        //int workerIndex = Task.WaitAny(workers);

                        int row = r;
                        //workers[workerIndex] = Task.Run(() => DitherRow(row));
                        workerQueue.Enqueue(Task.Run(() => DitherRow(row)));
                    }

                    //Task.WaitAll(workers);
                    while (workerQueue.TryDequeue(out Task? result))
                    {
                        result.Wait();
                    }

                    double time = sw.Elapsed.TotalMilliseconds;
                    sw.Stop();

                    void DitherRow(int row)
                    {
                        SpinWait spinWait = new SpinWait();
                        int rowStartIndex = row * data.Stride;
                        int rowEndIndex = rowStartIndex + bitmapByteWidth;

                        int rowPositionIndex = 0; //bytes not pixels
                        for (int i = rowStartIndex; i < rowEndIndex; i++)
                        {
                            //check the progress of the row above
                            if (row > 0)
                            {
                                while (rowProgress[row - 1] - separation <= rowPositionIndex)
                                {
                                    spinWait.SpinOnce();
                                }
                                spinWait.Reset();
                            }

                            byte oldColor = bytes[i];
                            bytes[i] = Precalc[bytes[i]];
                            int error = oldColor - bytes[i];

                            if (error != 0)
                            {
                                ApplyErrorDoubleFloat(error, i, rowPositionIndex, row);
                            }

                            rowPositionIndex++;
                            rowProgress[row] = rowPositionIndex;
                        }

                        rowProgress[row] += separation;
                        maxWorkerLock.Release();
                    }
                }

                void ParallelUsingSemaphoreSlim()
                {
                    int separation = channels * 2;

                    int workerCount = Environment.ProcessorCount * 2;

                    SemaphoreSlim maxThreadLock = new SemaphoreSlim(workerCount);
                    int[,] rowProgress = new int[data.Height, channels];
                    //SemaphoreSlim[,] rowLocks = new SemaphoreSlim[data.Height,channels];
                    Task[,] tasks = new Task[data.Height, channels];

                    ConcurrentDictionary<(int, int), SemaphoreSlim> rowLockDict = new ConcurrentDictionary<(int, int), SemaphoreSlim>();

                    double init = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();

                    for (int r = 0; r < data.Height; r++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            maxThreadLock.Wait();
                            (int row, int channel) rc = (r, c);
                            SemaphoreSlim rowLock = new SemaphoreSlim(1);
                            rowLockDict[rc] = rowLock;
                            rowLockDict.TryGetValue((r - 1, c), out SemaphoreSlim? prevRowLock);
                            tasks[r, c] = Task.Run(() => DitherRowChannel(rc.row, rc.channel, rowLock, prevRowLock));
                        }
                    }

                    foreach (var task in tasks)
                    {
                        task.Wait();
                    }

                    double ms = sw.Elapsed.TotalMilliseconds;

                    for (int c = 0; c < channels; c++)
                    {
                        rowLockDict[(data.Height - 1, c)].Dispose();
                    }

                    maxThreadLock.Dispose();

                    void DitherRowChannel(int row, int channel, SemaphoreSlim rowLock, SemaphoreSlim? prevRowLock)
                    {
                        int rowStartIndex = row * data.Stride;
                        int rowEndIndex = rowStartIndex + bitmapByteWidth;

                        int rowBytePositionIndex = channel; // how far along it is in bytes not pixels
                        for (int i = rowStartIndex + channel; i < rowEndIndex; i += channels)
                        {
                            //check the progress of the row above
                            if (prevRowLock != null)
                            {
                                while (rowProgress[row - 1, channel] - separation <= rowBytePositionIndex)
                                {
                                    prevRowLock.Wait();
                                }
                            }

                            byte oldColor = bytes[i];
                            bytes[i] = Precalc[bytes[i]];
                            int error = oldColor - bytes[i];

                            if (error != 0)
                            {
                                ApplyErrorDoubleFloat(error, i, rowBytePositionIndex, row);
                            }

                            rowBytePositionIndex += channels;
                            rowProgress[row, channel] = rowBytePositionIndex;
                            rowLock.Release();
                        }

                        rowProgress[row, channel] += separation;
                        rowLock.Release();

                        if (rowLockDict.TryRemove((row - 1, channel), out SemaphoreSlim? ss))
                        {
                            ss.Dispose();
                        }

                        maxThreadLock.Release();
                    }
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
