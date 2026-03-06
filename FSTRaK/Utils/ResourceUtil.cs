using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FSTRaK.Utils
{
    internal class ResourceUtil
    {
        public static void SetFont(string fontName)
        {
            var themeDictionary = Application.Current.Resources.MergedDictionaries[0];
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

        public static void SetTheme(string themeName)
        {
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            ResourceDictionary newTheme;
            if (themeName == "Normal")
            {
                newTheme = new ResourceDictionary() { Source = new Uri("/Resources/Theme.xaml", UriKind.Relative) };
            }
            else if (themeName == "Dark")
            {
                newTheme = new ResourceDictionary() { Source = new Uri("/Resources/DarkTheme.xaml", UriKind.Relative) };
            }
            else return;

            mergedDicts.RemoveAt(0);
            mergedDicts.Insert(0, newTheme);
        }
    }
}
