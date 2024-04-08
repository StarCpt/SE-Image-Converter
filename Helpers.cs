using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using ImageConverterPlus.Data;
using System.Collections.Immutable;
using SixLabors.ImageSharp.Formats.Webp;
using System.Threading;
using System.Windows.Media;

namespace ImageConverterPlus
{
    public static class Helpers
    {
        public static bool IsNumeric(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (!IsNumeric(str[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsNumeric(char c)
        {
            return c >= '0' && c <= '9';
        }

        //https://stackoverflow.com/questions/6484357/converting-bitmapimage-to-bitmap-and-vice-versa
        public static Bitmap? BitmapSourceToBitmap([NotNullIfNotNull("bitmapImage")] BitmapSource bitmapImage)
        {
            if (bitmapImage == null)
                return null;

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new(outStream);

                return bitmap;
            }
        }

        //https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap
        public static BitmapImage? BitmapToBitmapImage([NotNullIfNotNull("image")] Image image, bool disposeBitmap)
        {
            if (image == null)
                return null;

            using (MemoryStream memory = new MemoryStream())
            {
                image.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                if (disposeBitmap)
                    image.Dispose();

                return bitmapimage;
            }
        }

        public static BitmapSource? BitmapToBitmapSourceFast([NotNullIfNotNull("bitmap")] Bitmap? bitmap, bool disposeBitmap)
        {
            if (bitmap is null)
                return null;

            IntPtr hBitmap = bitmap.GetHbitmap();

            try
            {
                BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                return bitmapSource;
            }
            finally
            {
                DeleteObject(hBitmap);
                
                if (disposeBitmap)
                    bitmap.Dispose();
            }
        }

        public static bool TryLoadImage(string filePath, out Bitmap? result)
        {
            result = null;

            if (!File.Exists(filePath))
                return false;

            try
            {
                IsFileSupportedEnum supEnum = IsImageFileSupported(filePath);
                if (supEnum == IsFileSupportedEnum.Supported)
                {
                    result = new Bitmap(filePath);
                    return true;
                }
                else if (supEnum == IsFileSupportedEnum.Webp)
                {
                    result = LoadWebpImage(filePath);
                    return true;
                }
            }
            catch (Exception e)
            {
                App.Log.Log(e.ToString());
            }

            return false;
        }

        public static Bitmap LoadWebpImage(string filePath)
        {
            WebpDecoder webpDecoder = new WebpDecoder();
            using SixLabors.ImageSharp.Image webpImg = webpDecoder.Decode(SixLabors.ImageSharp.Configuration.Default, new FileStream(filePath, FileMode.Open, FileAccess.Read), CancellationToken.None);

            SixLabors.ImageSharp.Formats.Bmp.BmpEncoder enc = new();

            using MemoryStream stream = new MemoryStream();
            webpImg.Save(stream, enc);
            return new Bitmap(stream);
        }

        public static readonly ImmutableArray<string> SupportedImageFileTypes =
            ImmutableArray.Create("png", "jpg", "jpeg", "jfif", "tiff", "bmp", "gif", "ico", "webp");

        public static IsFileSupportedEnum IsImageFileSupported(string file)
        {
            try
            {
                string fileExtension = file.Split('.').Last();

                if (SupportedImageFileTypes.Any(i => i.Equals(fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    if (fileExtension.Equals("webp", StringComparison.OrdinalIgnoreCase))
                    {
                        return IsFileSupportedEnum.Webp;
                    }
                    else
                    {
                        return IsFileSupportedEnum.Supported;
                    }
                }
                else
                {
                    return IsFileSupportedEnum.NotSupported;
                }
            }
            catch (Exception e)
            {
                App.Log.Log($"Caught exception at MainWindow.IsFileTypeSupported(string) ({file})");
                App.Log.Log(e.ToString());
                return IsFileSupportedEnum.NotSupported;
            }
        }

        public static BitmapSource TransformBitmap(RotateFlipType type, BitmapSource bitmap)
        {
            Transform transform;
            switch (type)
            {
                case RotateFlipType.Rotate90FlipNone:
                    transform = new RotateTransform(90);
                    return new TransformedBitmap(bitmap, transform);
                case RotateFlipType.RotateNoneFlipX:
                    transform = new ScaleTransform(-1, 1, 0, 0);
                    return new TransformedBitmap(bitmap, transform);
                case RotateFlipType.RotateNoneFlipY:
                    transform = new ScaleTransform(1, -1, 0, 0);
                    return new TransformedBitmap(bitmap, transform);
            }
            return bitmap;
        }

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hBitmap);
    }
}
