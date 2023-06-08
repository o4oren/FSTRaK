
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager.State
{
    internal class TaxiInState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }


        public TaxiInState(FlightManager context) : base(context)
        {
            this.EventInterval = 10000;
            this.Name = "Taxi In";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData data)
        {
            if ((data.GroundVelocity > 40 && data.MinThrottlePosition() > 75) || data.SimOnGround != 1)
            {
                Context.State = new TakeoffRollState(Context);
                return;
            }

            //Parking brakes, engines off - end flight
            if (data.GroundVelocity < 2 && data.ParkingBrakesSet == 1 && data.MaxEngineRpmPct() < 5)
            {
                var pe = new ParkingEvent
                {
                    FlapsPosition = data.FlapPosition,
                    FuelWeightLbs = data.FuelWeightLbs
                };

                AddFlightEvent(data, pe);
                Context.State = new FlightEndedState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                AddFlightEvent(data, new BaseFlightEvent());
                Stopwatch.Restart();
            }
        }
    }
}
