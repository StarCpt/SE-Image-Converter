using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageConverterPlus
{
    public class Scaling
    {
        public static Bitmap Scale(Bitmap image, double zoom, InterpolationMode mode)
        {
            Bitmap newImage = new Bitmap((image.Width * zoom).ToRoundedInt(), (image.Height * zoom).ToRoundedInt(), PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, 0, 0, newImage.Width, newImage.Height);
            }

            return newImage;
        }

        public static Bitmap ScaleAndOffset(Bitmap image, double zoom, float xOff, float yOff, InterpolationMode mode, Size size)
        {
            Bitmap newImage = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, xOff.ToRoundedInt(), yOff.ToRoundedInt(), (image.Width * zoom).ToRoundedInt(), (image.Height * zoom).ToRoundedInt());
            }

            return newImage;
        }
    }
}
