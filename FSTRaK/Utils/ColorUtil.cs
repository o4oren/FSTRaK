using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace FSTRaK.Utils
{
    public  class ColorUtil
    {
        public static System.Drawing.Color GetDrawingColorFromResource(string resourceName)
        {
            var mediaColor = (System.Windows.Media.Color)Application.Current.Resources[resourceName];
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }
    }
}
