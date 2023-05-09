﻿using System;
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
    }
}