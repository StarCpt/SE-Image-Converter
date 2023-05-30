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
        public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapImage)
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
        public static BitmapImage BitmapToBitmapImage(Image bitmap, bool disposeBitmap)
        {
            if (bitmap == null)
                return null;

            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                if (disposeBitmap)
                    bitmap.Dispose();

                return bitmapimage;
            }
        }

        public static BitmapSource BitmapToBitmapSourceFast(Bitmap bitmap, bool disposeBitmap)
        {
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

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hBitmap);
    }
}
