using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace FSTRaK.Utils
{
    internal class ResourceUtil
    {
        private static readonly Dictionary<string, double[]> FontSizes = new Dictionary<string, double[]>
        {
            //                         Nav   Title Label Ctrl  Text  List  Hdr   Small
            ["Slopes"]             = { 34,   30,   28,   24,   22,   20,   18,   16 },
            ["Arial"]              = { 24,   22,   20,   18,   17,   15,   14,   12 },
            ["Segoe UI"]           = { 24,   22,   20,   18,   17,   15,   14,   12 },
            ["Georgia"]            = { 24,   22,   20,   18,   17,   15,   14,   12 },
            ["Consolas"]           = { 22,   20,   18,   16,   15,   14,   13,   11 },
            ["Comic Sans MS"]     = { 24,   22,   20,   18,   16,   15,   14,   12 },
            ["Palatino Linotype"] = { 24,   22,   20,   18,   17,   15,   14,   12 },
            ["Bahnschrift"]        = { 24,   22,   20,   18,   17,   15,   14,   12 },
            ["Ink Free"]           = { 28,   26,   24,   20,   19,   17,   16,   14 },
        };

        public static readonly string[] AvailableFonts = {
            "Slopes", "Arial", "Segoe UI", "Georgia", "Consolas",
            "Comic Sans MS", "Palatino Linotype", "Bahnschrift", "Ink Free"
        };

        public static void SetFont(string fontName)
        {
            if (!FontSizes.ContainsKey(fontName)) return;

            var res = Application.Current.Resources;
            var sizes = FontSizes[fontName];

            res["CurrentFont"] = fontName == "Slopes"
                ? res["Slopes"] as FontFamily
                : new FontFamily(fontName);

            res["NavFontSize"] = sizes[0];
            res["TitleFontSize"] = sizes[1];
            res["LabelFontSize"] = sizes[2];
            res["ControlFontSize"] = sizes[3];
            res["TextFontSize"] = sizes[4];
            res["ListFontSize"] = sizes[5];
            res["HeaderFontSize"] = sizes[6];
            res["SmallFontSize"] = sizes[7];
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
