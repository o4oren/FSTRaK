using FSTRaK.DataTypes;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Serilog;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        [Column("ID")]
        public int Id { get; set; }
        public virtual Aircraft Aircraft { get; set; }

        [Index(nameof(DepartureAirport))]
        public String DepartureAirport { get; set; }

        [Index(nameof(ArrivalAirport))]
        public String ArrivalAirport { get; set; }

        [Index(nameof(StartTime))]
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public Int64 FlightTimeMilis { get; set; }
        [NotMapped]
        public TimeSpan FlightTime
        {
            get => TimeSpan.FromTicks(FlightTimeMilis);
            set => FlightTimeMilis = value.Ticks;
        }

        public double FlightDistanceNm { get; set; }

        public double TotalFuelUsed { get; set; }

        public FlightOutcome FlightOutcome { get; set; }
        public double Score { get; set; }

        public ObservableCollection<BaseFlightEvent> FlightEvents { get; }

        private Airport _departureAirportDetails;

        [NotMapped] public Airport DepartureAirportDetails 
        {
            get
            {
                try
                {
                    if (_departureAirportDetails != null)
                        return _departureAirportDetails;

                    var airport = AirportResolver.Instance.AirportsDictionary[DepartureAirport];
                    _departureAirportDetails = airport;
                }
                catch (Exception)
                {
                    Log.Error($"Can't resolve {DepartureAirport}");

                    _departureAirportDetails = new Airport
                    {
                        icao = DepartureAirport
                    };
                }
                return _departureAirportDetails;
            }
        }


        private Airport _arrivalAirportDetails;
        [NotMapped]
        public Airport ArrivalAirportDetails
        {
            get
            {
                try
                {
                    if (_arrivalAirportDetails != null)
                        return _arrivalAirportDetails;

                    var airport = AirportResolver.Instance.AirportsDictionary[ArrivalAirport];
                    _arrivalAirportDetails = airport;
                }
                catch (Exception)
                {
                    Log.Error($"Can't resolve {ArrivalAirport}");
                    _arrivalAirportDetails = new Airport
                    {
                        icao = ArrivalAirport
                    };
                }
                return _arrivalAirportDetails;
            }
        }

        public Flight()
        {
            this.FlightEvents = new ObservableCollection<BaseFlightEvent>();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{this.FlightOutcome}")
                .AppendLine($"Departed From: {this.DepartureAirportDetails}")
                .AppendLine(($"Arrived At: {this.ArrivalAirportDetails}"))
                .AppendLine($"Start Time: {this.StartTime}")
                .AppendLine($"End Time: {this.EndTime}")
                .AppendLine($"Block Time: {this.FlightTime}")
                .AppendLine($"Fuel Used: {TotalFuelUsed:F1} Lbs")
                .AppendLine($"Flown Distance: {FlightDistanceNm:F0} NM")
                .Append($"Score: {this.Score}");
            return sb.ToString();
        }

        /// <summary>
        /// To be called after the flight is concluded. This method calculates the total score of the flight so it can be persisted.
        /// </summary>
        public void UpdateScore()
        {
            var scoringEvents = GetScoringEvents();
            Score = MathUtils.Clamp(100 + scoringEvents.Sum(e => e.ScoreDelta), 0, 110);
        }

        private List<ScoringEvent> GetScoringEvents()
        {
            return this.FlightEvents
            .OfType<ScoringEvent>()
            .GroupBy(e => e.GetType())
            .Select(e => (ScoringEvent)e.First())
            .ToList<ScoringEvent>();
        }

        public string GetScoreDetails()
        {
            var scoringEvents = GetScoringEvents();
            var builder = new StringBuilder();
            foreach(var se in scoringEvents)
            {
                if(se.ScoreDelta != 0)
                {
                    if(se is LandingEvent @event)
                    {
                        builder.AppendLine($"{@event.LandingRate} {se.EventName} {se.ScoreDelta} Points");
                    }
                    else
                    {
                        builder.AppendLine($"{se.EventName} {se.ScoreDelta} Points");
                    }
                }
            }
            return builder.ToString();
        }
    }
}
