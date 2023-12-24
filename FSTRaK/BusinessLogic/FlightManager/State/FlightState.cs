using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity.FlightEvent;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class FlightState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }
        public FlightState(FlightManager context) : base(context)
        {
            this.EventInterval = 10000;
            this.Name = "In flight";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(FlightData data)
        {
            if (data.SimOnGround == 1)
            {
                Context.State = new LandedState(Context, data);
                return;
            }

            if (data.IndicatedAirspeed < 150)
            {
                EventInterval = 6000;
            }
            else if (data.IndicatedAirspeed > 150 && data.IndicatedAirspeed < 250)
            {
                EventInterval = 10000;
            }
            else if (data.IndicatedAirspeed > 250 || data.Altitude > 10000)
            {
                EventInterval = 20000;
            }

            // TODO add code to handle specific flight events

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                var fe = CheckEnvelopeExceedingEvents(data);
                AddFlightEvent(data, fe);
                Stopwatch.Restart();
            }
        }

        private BaseFlightEvent CheckEnvelopeExceedingEvents(FlightData data)
        {
            if(data.OverSpeed == 1)
            {
                return new OverspeedEvent();
            }
            if (data.FlapSpeedExceeded == 1)
            {
                return new FlapsSpeedExceededEvent();
            }
            if (data.GearSpeedExceeded == 1)
            {
                return new GearsSpeedExceededEvent();
            }
            if (data.StallWarning == 1)
            {
                return new StallWarningEvent();
            }
            return new BaseFlightEvent();
        }
    }
}
