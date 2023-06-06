using System.Windows;
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.Utils
{
    internal static class ResourceUtils
    {
        public static System.Drawing.Color GetColorFromResource(string name)
        {
            var mColor = (System.Windows.Media.Color)Application.Current.Resources[name];
            return System.Drawing.Color.FromArgb(mColor.A, mColor.R, mColor.G, mColor.B);
        }

        public static string GetAircraftIcon(Aircraft aircraft)
        {
            if (aircraft.NumberOfEngines == 1 && aircraft.EngineType == EngineType.Piston)
            {
                return "C172";
            }

            return "B737";
        }
    }
}
