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
            ArrivedOrCrashedText = flight.FlightOutcome == FlightOutcome.Crashed ? "Crashed near: " : "To: ";
            FuelUnit = Properties.Settings.Default.Units == (int)Units.Imperial ? "Lbs" : "Kg";
            PayloadUnit = Payload != null ? FuelUnit : "Unknown";
            Comment = flight.Comment;

            FlightDataText = BuildFlightDataText(flight);
        }

        /// <summary>
        /// Builds the single Flight Data card body, kept deliberately compact. When a SimBrief
        /// plan exists, planned values are folded into the corresponding rows as "(planned ...)"
        /// suffixes; the route is not repeated here (it is drawn on the map with tooltips) and
        /// alternates appear only on a diversion.
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
            sb.AppendLine($"From: {DepartureAirportText}");
            sb.AppendLine($"{ArrivedOrCrashedText}{ArrivalAirportText}");
            if (plan != null && !string.Equals(plan.ArrivalAirport, flight.ArrivalAirport, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(string.IsNullOrEmpty(plan.AlternateAirports)
                    ? $"DIVERTED - planned arrival: {plan.ArrivalAirport}"
                    : $"DIVERTED - planned arrival: {plan.ArrivalAirport} (alternates: {plan.AlternateAirports})");
            sb.AppendLine(plan?.ScheduledOut != null
                ? $"Started: {StartTime:g} (sched {plan.ScheduledOut:HH:mm}Z)"
                : $"Started: {StartTime:g}");
            sb.AppendLine(plan?.ScheduledIn != null
                ? $"Ended: {EndTime:g} (sched {plan.ScheduledIn:HH:mm}Z)"
                : $"Ended: {EndTime:g}");
            sb.AppendLine(plan?.EstBlockSec != null
                ? $"Block time: {BlockTime:hh\\:mm} (planned {Duration(plan.EstBlockSec)})"
                : $"Block time: {BlockTime:hh\\:mm}");
            sb.AppendLine(plan?.EnrouteBurn != null
                ? $"Fuel used: {FuelUsed:N0} {FuelUnit} (planned {Weight(plan.EnrouteBurn)})"
                : $"Fuel used: {FuelUsed:N0} {FuelUnit}");
            sb.AppendLine(plan?.PayloadLbs != null
                ? $"Payload: {Payload:N0} {PayloadUnit} (planned {Weight(plan.PayloadLbs)})"
                : $"Payload: {Payload:N0} {PayloadUnit}");
            if (plan?.PaxCount != null || plan?.CargoLbs != null)
                sb.AppendLine($"Pax: {plan.PaxCount?.ToString() ?? "N/A"}, Cargo: {Weight(plan.CargoLbs)}");
            sb.AppendLine(plan?.RouteDistanceNm != null
                ? $"Distance: {Distance:N0} NM (planned {plan.RouteDistanceNm:N0} NM)"
                : $"Distance: {Distance:N0} NM");
            sb.AppendLine($"Landing: {LandingVerticalSpeed:N0} ft/m, {TouchdownGForce}");
            sb.Append($"Score: {Score}");
            if (!string.IsNullOrWhiteSpace(Comment))
                sb.Append($"\nComment: {Comment}");
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

        // Compact identity: "LLBG (Ben Gurion Intl)" — abbreviated, no city/country; a long
        // name simply wraps in the card. Full details stay available in the airports data.
        private string GetAirportText(Airport airport)
        {
            var ident = string.IsNullOrWhiteSpace(airport.icao_code)
                ? $"{airport.ident}/{airport.iata_code}"
                : airport.ident;
            if (string.IsNullOrWhiteSpace(airport.name))
                return ident;
            var name = airport.name
                .Replace("International", "Intl")
                .Replace(" Airport", "")
                .Trim();
            return $"{ident} ({name})";
        }
    }
}
