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

        public bool HasFlightPlan { get; }
        public string FlightPlanText { get; }

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

            HasFlightPlan = flight.FlightPlan != null;
            FlightPlanText = HasFlightPlan ? BuildFlightPlanText(flight) : string.Empty;
        }

        private static string BuildFlightPlanText(Flight flight)
        {
            var plan = flight.FlightPlan;
            var isImperial = Properties.Settings.Default.Units == (int)Units.Imperial;
            var weightUnit = isImperial ? "Lbs" : "Kg";
            string Weight(double? lbs) =>
                lbs == null ? "N/A" : $"{(isImperial ? lbs.Value : lbs.Value * Consts.LbsToKgs):N0} {weightUnit}";
            string Duration(int? seconds) =>
                seconds == null ? "N/A" : TimeSpan.FromSeconds(seconds.Value).ToString(@"hh\:mm");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(plan.ComposedFlightNumber))
                sb.AppendLine($"Flight: {plan.ComposedFlightNumber}");
            sb.AppendLine($"Planned: {plan.DepartureAirport} -> {plan.ArrivalAirport} (Altn: {plan.AlternateAirports ?? "N/A"})");
            if (!string.Equals(plan.ArrivalAirport, flight.ArrivalAirport, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"DIVERTED to {flight.ArrivalAirport}");
            sb.AppendLine($"Aircraft: {plan.AircraftType} {plan.AircraftReg}");
            sb.AppendLine($"Route: {plan.Route}");
            if (plan.CruiseAltitude != null)
                sb.AppendLine($"Cruise altitude: {plan.CruiseAltitude} ft");
            sb.AppendLine($"Distance: planned {plan.RouteDistanceNm:N0} NM / flown {flight.FlightDistanceNm:N0} NM");
            sb.AppendLine($"Block time: planned {Duration(plan.EstBlockSec)} / actual {flight.FlightTime:hh\\:mm}");
            if (plan.ScheduledOut != null)
                sb.AppendLine($"Sched out: {plan.ScheduledOut:yyyy-MM-dd HH:mm}Z / actual start {flight.StartTime:yyyy-MM-dd HH:mm}");
            if (plan.ScheduledIn != null)
                sb.AppendLine($"Sched in: {plan.ScheduledIn:yyyy-MM-dd HH:mm}Z / actual end {flight.EndTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Fuel: ramp {Weight(plan.PlanRampFuel)}, planned burn {Weight(plan.EnrouteBurn)} / used {Weight(flight.TotalFuelUsed)}");
            sb.AppendLine($"Payload: planned {Weight(plan.PayloadLbs)} / actual {Weight(flight.TotalPayloadLbs)}");
            sb.Append($"Pax: {plan.PaxCount?.ToString() ?? "N/A"}, Bags: {plan.BagCount?.ToString() ?? "N/A"}, Cargo: {Weight(plan.CargoLbs)}");
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
