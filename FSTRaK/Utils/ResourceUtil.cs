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
        //                                                    Nav   Title Label Ctrl  Text  List  Hdr   Small
        private static readonly Dictionary<string, double[]> FontSizes = new Dictionary<string, double[]>
        {
            { "Slopes",            new double[] { 34,   30,   28,   24,   22,   20,   18,   16 } },
            { "Arial",             new double[] { 24,   22,   20,   18,   17,   15,   14,   12 } },
            { "Segoe UI",          new double[] { 24,   22,   20,   18,   17,   15,   14,   12 } },
            { "Georgia",           new double[] { 24,   22,   20,   18,   17,   15,   14,   12 } },
            { "Consolas",          new double[] { 22,   20,   18,   16,   15,   14,   13,   11 } },
            { "Comic Sans MS",    new double[] { 24,   22,   20,   18,   16,   15,   14,   12 } },
            { "Palatino Linotype", new double[] { 24,   22,   20,   18,   17,   15,   14,   12 } },
            { "Bahnschrift",       new double[] { 24,   22,   20,   18,   17,   15,   14,   12 } },
            { "Ink Free",          new double[] { 28,   26,   24,   20,   19,   17,   16,   14 } },
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
