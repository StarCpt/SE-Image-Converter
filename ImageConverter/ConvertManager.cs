using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageConverterPlus.ImageConverter
{
    public class ConvertManager : NotifyPropertyChangedBase
    {
        public BitDepth BitDepth { get => bitDepth; set => SetValue(ref bitDepth, value); }
        public bool EnableDithering { get => enableDithering; set => SetValue(ref enableDithering, value); }
        public InterpolationMode Interpolation { get => interpolation; set => SetValue(ref interpolation, value); }
        public Size ConvertedSize { get => convertedSize; set => SetValue(ref convertedSize, value); }
        /// <summary>zoom</summary>
        public double Scale { get => scale; set => SetValue(ref scale, value); }
        public Point TopLeft { get => topLeft; set => SetValue(ref topLeft, value); }

        private BitDepth bitDepth;
        private bool enableDithering;
        private InterpolationMode interpolation;
        private Size convertedSize;
        private double scale = 1.0;
        private Point topLeft;

        public ConvertManager()
        {
            BitDepth = BitDepth.Color3;
            EnableDithering = true;
            Interpolation = InterpolationMode.HighQualityBicubic;
            ConvertedSize = new Size(178, 178);
            Scale = 1.0;
            TopLeft = new Point(0, 0);
        }

        public static void ConvertToString(Image image, ConvertOptions options, Action<string> callback, CancellationToken token)
        {
            callback?.Invoke(new Converter(options).ConvertSafe(image, token));
        }

        public void ConvertToString(Image image, Action<string> callback, CancellationToken token)
        {
            callback?.Invoke(new Converter(GetOptions()).ConvertSafe(image, token));
        }

        public static void ConvertToBitmap(Image image, ConvertOptions options, Action<Bitmap> callback, CancellationToken token)
        {
            callback?.Invoke(new Converter(options).ConvertToBitmapSafe(image, token));
        }

        public void ConvertToBitmap(Image image, Action<Bitmap> callback, CancellationToken token)
        {
            callback?.Invoke(new Converter(GetOptions()).ConvertToBitmapSafe(image, token));
        }

        private ConvertOptions GetOptions() =>
            new ConvertOptions
            {
                BitsPerChannel = (int)this.BitDepth,
                Dithering = this.EnableDithering,
                Interpolation = this.Interpolation,
                ConvertedSize = this.ConvertedSize,
                Scale = this.Scale,
                TopLeft = this.TopLeft,
            };
}

    public struct SplitInfo
    {
        public int Width { readonly get; set; }
        public int Height { readonly get; set; }
        public int XIndex { readonly get; set; }
        public int YIndex { readonly get; set; }

        public SplitInfo(int width, int height, int xIndex, int yIndex)
        {
            Width = width;
            Height = height;
            XIndex = xIndex;
            YIndex = yIndex;
        }
    }
}
