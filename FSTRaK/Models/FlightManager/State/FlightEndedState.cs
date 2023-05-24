using FSTRaK.DataTypes;
using Serilog;
using System;

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

                // TODO persist data

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
