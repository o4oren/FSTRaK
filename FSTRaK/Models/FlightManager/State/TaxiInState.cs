
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class TaxiInState : AbstractState
    {
        public override string Name { get; set; } = "Taxi";
        public override bool IsMovementState { get; set; }


        public TaxiInState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Taxi In";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if (Data.groundVelocity > 35)
            {
                Context.State = new TakeoffRollState(Context);
                return;
            }

            //Parking brakes, engines off - end flight
            if (Data.groundVelocity > 2)
            {
                Context.State = new FlightEndedState(Context);
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
