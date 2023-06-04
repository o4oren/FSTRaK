using FSTRaK.DataTypes;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media.Media3D;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        public int ID { get; set; }
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
            get { return TimeSpan.FromTicks(FlightTimeMilis); }
            set { FlightTimeMilis = value.Ticks; }
        }

        public double FlightDistanceInMeters { get; set; }

        public double TotalFuelUsed { get; set; }

        public FlightOutcome FlightOutcome { get; set; }
        public double Score { get; set; }

        public string ScoreDetails { get; set; }

        public ObservableCollection<BaseFlightEvent> FlightEvents { get; private set; }

        [NotMapped] public Airport DepartureAirportDetails 
        {
            get
            {
                try
                {
                    var airport = AirportResolver.Instance.AirportsDictionary[DepartureAirport];
                    return airport;
                }
                catch (Exception)
                {
                    return new Airport
                    {
                        icao = DepartureAirport
                    };
                }

            }
        }

        [NotMapped]
        public Airport ArrivalAirportDetails
        {
            get
            {
                var airport = AirportResolver.Instance.AirportsDictionary[ArrivalAirport];
                if (airport == null)
                    airport = new Airport
                    {
                        icao = ArrivalAirport
                    };
                return airport;
            }
        }

        public Flight()
        {
            this.FlightEvents = new ObservableCollection<BaseFlightEvent>();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Departed From: {this.DepartureAirportDetails}");

            if (this.FlightOutcome == FlightOutcome.Crashed)
            {
                sb.AppendLine(($"Crashed Near {this.ArrivalAirportDetails}"));
            }
            else
            {
                sb.AppendLine(($"Arrived At: {this.ArrivalAirportDetails}"));
            }

            sb.AppendLine($"Start Time: {this.StartTime}")
            .AppendLine($"End Time: {this.EndTime}")
            .AppendLine($"Block Time: {this.FlightTime}")
            .AppendLine($"Fuel Used: {TotalFuelUsed}")
            .AppendLine($"Flown Distance VSI: {FlightDistanceInMeters * Consts.MetersToNauticalMiles} NM");

            var landingEvent = (LandingEvent)this.FlightEvents.FirstOrDefault(e => e is LandingEvent);
            if (landingEvent != null)
            {
                sb.AppendLine($"{landingEvent.VerticalSpeed:F0} ft/m");
            }

            
            sb.AppendLine($"Score: {this.Score}")
            .ToString();

            return sb.ToString();
        }

        /// <summary>
        /// To be called after the flight is concluded. This method calculates the total score of the flight so it can be persisted.
        /// </summary>
        public void UpdateScore()
        {
            var scoringEvents = GetScoringEvents();
            Score = scoringEvents.Sum(e => e.ScoreDelta);
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
                    if(se is LandingEvent)
                    {
                        builder.AppendLine($"{((LandingEvent)se).LandingRate} {se.EventName} {se.ScoreDelta} Points");
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
