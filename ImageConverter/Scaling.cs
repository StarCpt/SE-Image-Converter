using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SEImageToLCD_15BitColor
{
    public class Scaling
    {
        public static Bitmap Scale(Bitmap image, float scale, InterpolationMode mode)
        {
            Bitmap newImage = new((int)(image.Width * scale), (int)(image.Height * scale), PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                //ImageAttributes att = new ImageAttributes();
                g.DrawImage(image, new Rectangle(Point.Empty, newImage.Size));
            }

            return newImage;
        }
        public static Bitmap ScaleAndPad(Bitmap image, float scale, InterpolationMode mode, Size lcdSize)
        {
            Bitmap newImage = new Bitmap(lcdSize.Width, lcdSize.Height, PixelFormat.Format24bppRgb);

            int xOffset = (int)Math.Round((lcdSize.Width - (image.Width * scale)) / 2f);
            int yOffset = (int)Math.Round((lcdSize.Height - (image.Height * scale)) / 2f);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, xOffset, yOffset, image.Width * scale, image.Height * scale);
            }

            return newImage;
        }

        public static Bitmap ScaleFast(Bitmap image, float scale, InterpolationMode mode)
        {
            Bitmap newImage = new((int)(image.Width * scale), (int)(image.Height * scale), PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                g.DrawImage(image, new Rectangle(Point.Empty, newImage.Size));
            }

            return newImage;
        }
    }
}
