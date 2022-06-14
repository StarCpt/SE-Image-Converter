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
            Bitmap newImage = new((int)(image.Width * scale), (int)(image.Height * scale));

            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.InterpolationMode = mode;
                ImageAttributes att = new ImageAttributes();
                g.DrawImage(image, new Rectangle(Point.Empty, newImage.Size));
            }

            return newImage;
        }
    }
}
