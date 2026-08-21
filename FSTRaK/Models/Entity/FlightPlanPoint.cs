using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FSTRaK.Utils;

namespace FSTRaK.Models
{
    /// <summary>
    /// One navlog fix of a SimBrief flight plan. Sequence 0 is the departure airport.
    /// FuelOnboardLbs is stored in lbs.
    /// </summary>
    public class FlightPlanPoint
    {
        [Column("ID")]
        public int Id { get; set; }

        public int FlightPlanId { get; set; }
        public virtual FlightPlan FlightPlan { get; set; }

        public int Sequence { get; set; }
        public string Ident { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string ViaAirway { get; set; }
        public string Stage { get; set; }
        public bool IsSidStar { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? AltitudeFt { get; set; }
        public int? IndicatedAirspeed { get; set; }
        public double? FuelOnboardLbs { get; set; }
        public int? TimeTotalSec { get; set; }
        public double? DistanceNm { get; set; }

        [NotMapped]
        public string TooltipText
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Ident);
                if (!string.IsNullOrEmpty(Name) && Name != Ident)
                    sb.Append($" ({Name})");
                if (!string.IsNullOrEmpty(ViaAirway) && ViaAirway != "DCT")
                    sb.Append($" via {ViaAirway}");
                if (AltitudeFt != null)
                    sb.Append($"\nPlanned altitude: {AltitudeFt} ft");
                if (IndicatedAirspeed != null)
                    sb.Append($"\nPlanned IAS: {IndicatedAirspeed} kts");
                if (FuelOnboardLbs != null)
                    sb.Append($"\nPlanned fuel onboard: {UnitsUtil.GetWeightString(FuelOnboardLbs)}");
                if (TimeTotalSec != null)
                    sb.Append($"\nElapsed: {TimeSpan.FromSeconds(TimeTotalSec.Value):hh\\:mm}");
                return sb.ToString();
            }
        }
    }
}
