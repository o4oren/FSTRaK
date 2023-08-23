
using FSTRaK.DataTypes;
using Serilog;

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
        public override void ProcessFlightData(FlightData data)
        {
            if ((data.GroundVelocity > 40 && data.MinThrottlePosition(Context.ActiveFlight.Aircraft.NumberOfEngines) > 75) || data.SimOnGround != 1)
            {
                Context.State = new TakeoffRollState(Context);
                return;
            }

            //engines off and no movement or parking brakes - or helicopter and a higher max rpm (because it takes long to spool down) - end flight
            if ((data.GroundVelocity < 2 && data.ParkingBrakesSet == 1 && data.MaxEngineRpmPct() < 5)
                || (data.GroundVelocity < 2 && data.MaxEngineRpmPct() < 2)
                || Context.ActiveFlight.Aircraft.Category.Equals("Helicopter") && data.MaxEngineRpmPct() < 15)
            {
                var pe = new ParkingEvent
                {
                    FlapsPosition = data.FlapPosition,
                    FuelWeightLbs = data.FuelWeightLbs
                };
                Log.Information($"Parked! Flaps: {pe.FlapsPosition}, with {pe.FuelWeightLbs} Lbs of fuel.");

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
