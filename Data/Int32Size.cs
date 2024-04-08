using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.Data
{
    public struct Int32Size
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Int32Size(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static bool Equals(Int32Size size1, Int32Size size2)
        {
            return size1.Width == size2.Width && size1.Height == size2.Height;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is not Int32Size size)
                return false;
            return Equals(this, size);
        }

        public static bool operator ==(Int32Size size1, Int32Size size2)
        {
            return size1.Width == size2.Width && size1.Height == size2.Height;
        }

        public static bool operator !=(Int32Size size1, Int32Size size2)
        {
            return !(size1 == size2);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        public static explicit operator System.Drawing.Size(Int32Size size)
        {
            return new System.Drawing.Size(size.Width, size.Height);
        }

        public static explicit operator Int32Size(System.Drawing.Size size)
        {
            return new Int32Size(size.Width, size.Height);
        }
    }
}
