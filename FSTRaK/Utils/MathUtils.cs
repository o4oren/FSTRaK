using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class MathUtils
    {
        public static double Clamp(double value, double min, double max) 
        { 
            return (value < min) ? min : (value > max) ? max : value; 
        }
    }
}
