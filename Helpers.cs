using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus
{
    public class Helpers
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
    }
}
