using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    /// <summary>
    /// A SimBrief OFP captured for a flight. Weights and fuel are stored in lbs.
    /// Shares its primary key with the owning Flight (EF6 one-to-optional-one).
    /// </summary>
    public class FlightPlan
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("ID")]
        public int Id { get; set; }

        public virtual Flight Flight { get; set; }

        public string AirlineIcao { get; set; }
        public string FlightNumber { get; set; }

        public string AircraftType { get; set; }
        public string AircraftName { get; set; }
        public string AircraftReg { get; set; }

        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        // Comma-joined ICAO idents; SimBrief can plan multiple alternates.
        public string AlternateAirports { get; set; }

        public string Route { get; set; }
        public int? CruiseAltitude { get; set; }
        public double? RouteDistanceNm { get; set; }

        public double? TaxiFuel { get; set; }
        public double? EnrouteBurn { get; set; }
        public double? ContingencyFuel { get; set; }
        public double? AlternateFuel { get; set; }
        public double? ReserveFuel { get; set; }
        public double? ExtraFuel { get; set; }
        public double? PlanRampFuel { get; set; }
        public double? PlanTakeoffFuel { get; set; }
        public double? PlanLandingFuel { get; set; }

        public DateTime? ScheduledOut { get; set; }
        public DateTime? ScheduledIn { get; set; }
        public int? EstTimeEnrouteSec { get; set; }
        public int? EstBlockSec { get; set; }

        public int? PaxCount { get; set; }
        public int? BagCount { get; set; }
        public double? CargoLbs { get; set; }
        public double? PayloadLbs { get; set; }
        public double? EstZfw { get; set; }
        public double? EstTow { get; set; }
        public double? EstLdw { get; set; }

        public virtual ICollection<FlightPlanPoint> Points { get; set; } = new List<FlightPlanPoint>();

        [NotMapped]
        public string ComposedFlightNumber =>
            string.IsNullOrWhiteSpace(AirlineIcao) && string.IsNullOrWhiteSpace(FlightNumber)
                ? null
                : $"{AirlineIcao}{FlightNumber}";

        [NotMapped]
        public string[] AlternateList =>
            string.IsNullOrEmpty(AlternateAirports) ? Array.Empty<string>() : AlternateAirports.Split(',');
    }
}
