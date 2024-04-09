using System;
using System.Runtime.InteropServices;

namespace ImageConverterPlus
{
    public static partial class ExternalMethods
    {
        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteObject(IntPtr hBitmap);
    }
}