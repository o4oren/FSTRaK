using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class FontUtil
    {
        public static void SetFont(string fontName)
        {
            var themeDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[0];
            themeDictionary.Remove("ThemeFontName");
            themeDictionary.Remove("HeaderFontSize");
            themeDictionary.Remove("TextFontSize");

            if (fontName == "Slopes")
            {
                themeDictionary.Add("ThemeFontName", fontName);
                themeDictionary.Add("HeaderFontSize", 18.0);
                themeDictionary.Add("TextFontSize", 22.0);
            } 
            else if (fontName == "Arial")
            {
                themeDictionary.Add("ThemeFontName", fontName);
                themeDictionary.Add("HeaderFontSize", 14.0);
                themeDictionary.Add("TextFontSize", 17.0);
            }
        }
    }
}
