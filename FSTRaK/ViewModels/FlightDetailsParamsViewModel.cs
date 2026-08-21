using System;
using System.Linq;
using System.Text;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity;

namespace FSTRaK.ViewModels
{
    internal class FlightDetailsParamsViewModel : BaseViewModel
    {
        public Aircraft Aircraft { get; set; }
        public Airport DepartureAirport { get; set; }

        public string DepartureAirportText { get; set; }
        public string ArrivalAirportText { get; set; }
        public string ArrivedOrCrashedText { get; set; }

        public Airport ArrivalAirport { get; set; }
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public TimeSpan BlockTime { get; set; }

        private double _fuelUsed;
        public double FuelUsed
        {
            get => Properties.Settings.Default.Units == (int)Units.Imperial ? _fuelUsed : _fuelUsed * Consts.LbsToKgs;
            set => _fuelUsed = value;
        }

        public string FuelUnit { get; set; }

        public double Distance { get; set; }

        private double? _payload;

        public double? Payload
        {
            get => _payload != null ? Properties.Settings.Default.Units == (int)Units.Imperial ? _payload : _payload * Consts.LbsToKgs : null;
            set => _payload = value;
        }

        public string PayloadUnit { get; set; }


        public double LandingVerticalSpeed { get; set; }

        public string TouchdownGForce { get; set; }

        public double Score { get; set; }

        public string Comment { get; set; }

        public string FlightDataText { get; }

        public FlightDetailsParamsViewModel(Flight flight) : base()
        {
            Aircraft = flight.Aircraft;
            DepartureAirport = AirportResolver.Instance.GetAirportByIdentCode(flight.DepartureAirport);
            DepartureAirportText = GetAirportText(DepartureAirport);
            ArrivalAirport = AirportResolver.Instance.GetAirportByIdentCode(flight.ArrivalAirport);
            ArrivalAirportText = GetAirportText(ArrivalAirport);
            StartTime = flight.StartTime;
            EndTime = flight.EndTime;
            FuelUsed = flight.TotalFuelUsed;
            Distance = flight.FlightDistanceNm;
            Payload = flight.TotalPayloadLbs;
            BlockTime = flight.FlightTime;
            LandingVerticalSpeed = CalculateLandingVs(flight);
            TouchdownGForce = CalculateTouchdownGForce(flight);
            Score = flight.Score;
            ArrivedOrCrashedText = flight.FlightOutcome == FlightOutcome.Crashed ? "Crashed near: " : "Arrived at: ";
            FuelUnit = Properties.Settings.Default.Units == (int)Units.Imperial ? "Lbs" : "Kg";
            PayloadUnit = Payload != null ? FuelUnit : "Unknown";
            Comment = flight.Comment;

            FlightDataText = BuildFlightDataText(flight);
        }

        /// <summary>
        /// Builds the single Flight Data card body. When a SimBrief plan exists, planned values
        /// are merged into the corresponding rows and plan-only rows (flight number, route,
        /// alternates, pax/cargo) are added — there is no separate plan card.
        /// </summary>
        private string BuildFlightDataText(Flight flight)
        {
            var plan = flight.FlightPlan;
            var isImperial = Properties.Settings.Default.Units == (int)Units.Imperial;
            var weightUnit = isImperial ? "Lbs" : "Kg";
            string Weight(double? lbs) =>
                lbs == null ? "N/A" : $"{(isImperial ? lbs.Value : lbs.Value * Consts.LbsToKgs):N0} {weightUnit}";
            string Duration(int? seconds) =>
                seconds == null ? "N/A" : TimeSpan.FromSeconds(seconds.Value).ToString(@"hh\:mm");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(plan?.ComposedFlightNumber))
                sb.AppendLine($"Flight: {plan.ComposedFlightNumber}");
            sb.AppendLine($"Departed from: {DepartureAirportText}");
            sb.AppendLine($"{ArrivedOrCrashedText}{ArrivalAirportText}");
            if (plan != null)
            {
                if (!string.Equals(plan.ArrivalAirport, flight.ArrivalAirport, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"DIVERTED - planned arrival: {plan.ArrivalAirport}");
                if (!string.IsNullOrEmpty(plan.AlternateAirports))
                    sb.AppendLine($"Alternates: {plan.AlternateAirports}");
                if (!string.IsNullOrEmpty(plan.Route))
                    sb.AppendLine(plan.CruiseAltitude != null
                        ? $"Route: {plan.Route} @ {plan.CruiseAltitude} ft"
                        : $"Route: {plan.Route}");
            }
            sb.AppendLine(plan?.ScheduledOut != null
                ? $"Started at: {StartTime} (sched {plan.ScheduledOut:yyyy-MM-dd HH:mm}Z)"
                : $"Started at: {StartTime}");
            sb.AppendLine(plan?.ScheduledIn != null
                ? $"Ended at: {EndTime} (sched {plan.ScheduledIn:yyyy-MM-dd HH:mm}Z)"
                : $"Ended at: {EndTime}");
            sb.AppendLine(plan?.EstBlockSec != null
                ? $"Block time: {BlockTime} (planned {Duration(plan.EstBlockSec)})"
                : $"Block time: {BlockTime}");
            sb.AppendLine(plan?.EnrouteBurn != null
                ? $"Total fuel used: {FuelUsed:N2} {FuelUnit} (planned burn {Weight(plan.EnrouteBurn)}, ramp {Weight(plan.PlanRampFuel)})"
                : $"Total fuel used: {FuelUsed:N2} {FuelUnit}");
            sb.AppendLine(plan?.PayloadLbs != null
                ? $"Payload: {Payload:N2} {PayloadUnit} (planned {Weight(plan.PayloadLbs)})"
                : $"Payload: {Payload:N2} {PayloadUnit}");
            if (plan != null)
                sb.AppendLine($"Pax: {plan.PaxCount?.ToString() ?? "N/A"}, Bags: {plan.BagCount?.ToString() ?? "N/A"}, Cargo: {Weight(plan.CargoLbs)}");
            sb.AppendLine(plan?.RouteDistanceNm != null
                ? $"Distance flown: {Distance:N2} NM (planned {plan.RouteDistanceNm:N0} NM)"
                : $"Distance flown: {Distance:N2} NM");
            sb.AppendLine($"Landing VS: {LandingVerticalSpeed:N0} ft/m");
            sb.AppendLine($"Touchdown G: {TouchdownGForce}");
            sb.AppendLine($"Score: {Score}");
            sb.AppendLine("Comment: ");
            sb.Append(Comment);
            return sb.ToString();
        }

        private double CalculateLandingVs(Flight flight)
        {
            var landingEvent = (LandingEvent)flight.FlightEvents.FirstOrDefault(e => e is LandingEvent);
            if (landingEvent != null)
            {
                return landingEvent.VerticalSpeed;
            }

            return 0;
        }

        private string CalculateTouchdownGForce(Flight flight)
        {
            var landingEvent = (LandingEvent)flight.FlightEvents.FirstOrDefault(e => e is LandingEvent);
            if (landingEvent?.TouchdownGForce != null)
            {
                return $"{landingEvent.TouchdownGForce:F2} G";
            }

            return "—";
        }

        private string GetAirportText(Airport airport)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(airport.ident);
            if (string.IsNullOrWhiteSpace(airport.icao_code))
                sb.AppendLine($"/{airport.iata_code}");
            else sb.Append("\n");

            if (!string.IsNullOrEmpty(airport.name))
                sb.Append($"{airport.name}, ");
            if (!string.IsNullOrEmpty(airport.municipality))
            {
                sb.Append(airport.municipality);
                if (airport.iso_country == "US")
                {
                    sb.Append($", {airport.iso_region.Replace("US-", "")}");
                    sb.Append(", USA ");
                }
                else
                {
                    sb.Append($", {airport.CountryName}");
                }
            }
            return sb.ToString();
        }
    }
}
