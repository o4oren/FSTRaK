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

        public double Score { get; set; }

        public string Comment { get; set; }


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
            Score = flight.Score;
            ArrivedOrCrashedText = flight.FlightOutcome == FlightOutcome.Crashed ? "Crashed near: " : "Arrived at: ";
            FuelUnit = Properties.Settings.Default.Units == (int)Units.Imperial ? "Lbs" : "Kg";
            PayloadUnit = Payload != null ? FuelUnit : "Unknown";
            Comment = flight.Comment;

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
                    sb.Append($", {airport.iso_region}");
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
