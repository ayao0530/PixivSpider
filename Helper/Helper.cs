using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Helper
{
    public static class Converter
    {
        public static bool ConvertToBool(this string str)
        {
            var res = false;
            var convert = false;
            if (Boolean.TryParse(str, out convert))
                res = convert;
            return res;
        }

        public static int ToInt(this object str)
        {
            var res = 0;
            var convert = 0;
            if (str != null)
            {
                if (int.TryParse(str.ToString(), out convert))
                    res = convert;
            }
            return res;
        }

        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static string keyWordVerify(this string str,string warn)
        {
            if (str.IsNullOrEmpty())
            {
                Console.Write(warn);
                str = Console.ReadLine();
            }

            if (str.IsNullOrEmpty())
            {
                keyWordVerify(str, warn);
            }

            return str;
        }
    }
}
