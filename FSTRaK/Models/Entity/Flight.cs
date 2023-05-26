using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

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

        public int FlightDistance { get; set; }

        public double TotalFuelUsed { get; set; }

        public double Score { get; set; }

        public ObservableCollection<FlightEvent> FlightEvents { get; private set; }

        public Flight()
        {
            this.FlightEvents = new ObservableCollection<FlightEvent>();
        }

        public override string ToString()
        {
            return ID.ToString();
        }

    }
}
