using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    public class StringUtil
    {
        public static void RemoveTrailingWhitespace(StringBuilder stringBuilder)
        {
            if (stringBuilder.Length > 0)
            {
                int lastIndex = stringBuilder.Length - 1;

                // Remove trailing whitespaces
                while (lastIndex >= 0 && char.IsWhiteSpace(stringBuilder[lastIndex]))
                {
                    stringBuilder.Remove(lastIndex, 1);
                    lastIndex--;
                }
            }
        }
    }
}
