using FSTRaK.DataTypes;
using FSTRaK.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Windows.Documents;

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

        public Flight()
        {
            this.FlightEvents = new ObservableCollection<BaseFlightEvent>();
        }

        public override string ToString()
        {
            return ID.ToString();
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
            StringBuilder builder = new StringBuilder();
            foreach(ScoringEvent se in scoringEvents)
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
