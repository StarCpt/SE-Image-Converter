using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ImageConverterPlus
{
    public static class Extensions
    {
        public static double ClampDoubleExt(this double val, double min, double max)
        {
            if (min > max)
            {
                if (val > min) return min;
                else if (val < max) return max;
                else return val;
            }
            else
            {
                if (val < min) return min;
                else if (val > max) return max;
                else return val;
            }
        }

        public static string ToShortString(this Size val)
        {
            return $"{val.Width}x{val.Height}";
        }

        public static System.Windows.Point Round(this System.Windows.Point point)
        {
            point.X = Math.Round(point.X);
            point.Y = Math.Round(point.Y);
            return point;
        }

        public static System.Windows.Size Round(this System.Windows.Size size)
        {
            size.Width = Math.Round(size.Width);
            size.Height = Math.Round(size.Height);
            return size;
        }

        /// <summary>
        /// Checks if either width or height is zero
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static bool IsAnyZero(this System.Windows.Size size)
        {
            return size.Width == 0 || size.Height == 0;
        }
    }
}
