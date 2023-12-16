using FSTRaK.DataTypes;

namespace FSTRaK.Utils;

public class UnitsUtil
{
    public static string GetWeightString(double? weight)
    {
        if (Properties.Settings.Default.Units.Equals((int)Units.Imperial)) return $"{weight:N1} Lbs"; 
        
        weight *= DataTypes.Consts.LbsToKgs;
        return $"{weight:N1} Kg";
    }
}