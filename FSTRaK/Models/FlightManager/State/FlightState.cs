

using FSTRaK.DataTypes;
using FSTRaK.Models.Entity.FlightEvent;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public FlightState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 10000;
            this.Name = "In flight";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if (Data.SimOnGround == 1)
            {
                Context.State = new LandedState(Context, Data);
                return;
            }

            if (Data.IndicatedAirpeed < 150)
            {
                _eventInterval = 10000;
            }
            else if (Data.IndicatedAirpeed > 150 && Data.IndicatedAirpeed < 250)
            {
                _eventInterval = 12000;
            }
            else if (Data.IndicatedAirpeed > 250 || Data.Altitude > 10000)
            {
                _eventInterval = 20000;
            }

            // TODO add code to handle specific flight events

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                var fe = CheckEnvelopeExceedingEvents(Data);
                AddFlightEvent(Data, fe);
                _stopwatch.Restart();
            }
        }

        private BaseFlightEvent CheckEnvelopeExceedingEvents(AircraftFlightData data)
        {
            if(data.Overspeed == 1)
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
