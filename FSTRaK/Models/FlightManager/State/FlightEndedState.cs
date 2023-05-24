using FSTRaK.DataTypes;
using FSTRaK.Models.Entity;
using Serilog;
using System;
using System.Runtime.InteropServices;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightEndedState : AbstractState
    {

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }

        private Boolean _isEnded = false;
        public FlightEndedState(FlightManager Context) : base(Context)
        {
            this.Name = "Flight Ended";
            this.IsMovementState = false;
            Log.Information($"Flight ended at {DateTime.Now}");
        }


        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if(!_isEnded)
            {
                FlightEndedEvent fe = new FlightEndedEvent();
                AddFlightEvent(Data, fe);

                // Flight ended because it was exited in the sim
                if (!Context.SimConnectInFlight)
                {
                    if (!Properties.Settings.Default.IsSaveOnlyCompleteFlights)
                    {
                        // TODO get last flight data params

                        // TODO persist data
                        return;
                    }

                }

                using (var context = new LogbookContext())
                {
                    context.Flights.Add(Context.ActiveFlight);
                    context.SaveChanges();
                }

                _isEnded = true;
            }
        }

        public override void HandleFlightExitEvent()
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState(Context);
            }
        }
    }
}
