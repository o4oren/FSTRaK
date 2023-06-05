using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FSTRaK.Utils
{
    internal static class ResourceUtils
    {
        public static System.Drawing.Color GetColorFromResource(string name)
        {
            var mColor = (System.Windows.Media.Color)Application.Current.Resources[name];
            return System.Drawing.Color.FromArgb(mColor.A, mColor.R, mColor.G, mColor.B);
            
        }
    }
}
