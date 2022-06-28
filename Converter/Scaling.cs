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
            Bitmap newImage = new Bitmap((image.Width * scale).ToRoundedInt(), (image.Height * scale).ToRoundedInt(), PixelFormat.Format24bppRgb);

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

        public static Bitmap ScaleAndOffset(Bitmap image, float scale, float xOff, float yOff, InterpolationMode mode, Size size)
        {
            Bitmap newImage = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, xOff, yOff, image.Width * scale, image.Height * scale);
            }

            return newImage;
        }
    }
}
