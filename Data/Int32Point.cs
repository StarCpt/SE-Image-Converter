using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.Data
{
    public struct Int32Point
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Int32Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static bool Equals(Int32Point point, Int32Point point2)
        {
            return point.X == point2.X && point.Y == point2.Y;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is not Int32Point point)
                return false;
            return Equals(this, point);
        }

        public static bool operator ==(Int32Point point1, Int32Point point2)
        {
            return point1.X == point2.X && point1.Y == point2.Y;
        }

        public static bool operator !=(Int32Point point1, Int32Point point2)
        {
            return !(point1 == point2);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static explicit operator System.Drawing.Point(Int32Point point)
        {
            return new System.Drawing.Point(point.X, point.Y);
        }

        public static explicit operator Int32Point(System.Drawing.Point point)
        {
            return new Int32Point(point.X, point.Y);
        }
    }
}
