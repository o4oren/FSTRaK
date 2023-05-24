

using FSTRaK.DataTypes;
using System;

namespace FSTRaK.Models.FlightManager
{
    internal class LandedState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public LandedState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if (!Convert.ToBoolean(Data.SimOnGround))
            {
                Context.State = new FlightState(Context);
                return;
            }

            if (Data.GroundVelocity < 35)
            {
                Context.State = new TaxiInState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data, new FlightEvent());
                _stopwatch.Restart();
            }

            HandleFlightExit(Context);
        }
    }
}
