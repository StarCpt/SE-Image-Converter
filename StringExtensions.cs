using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus
{
    public static class StringExtensions
    {
        public static bool EqualsAny(this string str, params string[] values) => values.Any(str.Equals);
        public static bool EqualsAny(this string str, IEnumerable<string> values) => values.Any(str.Equals);
        public static bool EqualsAny(this string str, StringComparison comparison, params string[] values) => values.Any(i => str.Equals(i, comparison));
        public static bool EqualsAny(this string str, StringComparison comparison, IEnumerable<string> values) => values.Any(i => str.Equals(i, comparison));

        public static bool StartsWithAny(this string str, params string[] values) => values.Any(str.StartsWith);
        public static bool StartsWithAny(this string str, IEnumerable<string> values) => values.Any(str.StartsWith);
        public static bool StartsWithAny(this string str, StringComparison comparison, params string[] values) => values.Any(i => str.StartsWith(i, comparison));
        public static bool StartsWithAny(this string str, StringComparison comparison, IEnumerable<string> values) => values.Any(i => str.StartsWith(i, comparison));

        public static bool EndsWithAny(this string str, params string[] values) => values.Any(str.EndsWith);
        public static bool EndsWithAny(this string str, IEnumerable<string> values) => values.Any(str.EndsWith);
        public static bool EndsWithAny(this string str, StringComparison comparison, params string[] values) => values.Any(i => str.EndsWith(i, comparison));
        public static bool EndsWithAny(this string str, StringComparison comparison, IEnumerable<string> values) => values.Any(i => str.EndsWith(i, comparison));
    }
}
