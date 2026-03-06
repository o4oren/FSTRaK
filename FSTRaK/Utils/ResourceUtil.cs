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
        public static void SetFont(string fontName)
        {
            var res = Application.Current.Resources;

            if (fontName == "Slopes")
            {
                res["CurrentFont"] = res["Slopes"] as FontFamily;
                res["NavFontSize"] = 34.0;
                res["TitleFontSize"] = 30.0;
                res["LabelFontSize"] = 28.0;
                res["ControlFontSize"] = 24.0;
                res["TextFontSize"] = 22.0;
                res["ListFontSize"] = 20.0;
                res["HeaderFontSize"] = 18.0;
                res["SmallFontSize"] = 16.0;
            }
            else if (fontName == "Arial")
            {
                res["CurrentFont"] = new FontFamily("Arial");
                res["NavFontSize"] = 24.0;
                res["TitleFontSize"] = 22.0;
                res["LabelFontSize"] = 20.0;
                res["ControlFontSize"] = 18.0;
                res["TextFontSize"] = 17.0;
                res["ListFontSize"] = 15.0;
                res["HeaderFontSize"] = 14.0;
                res["SmallFontSize"] = 12.0;
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
