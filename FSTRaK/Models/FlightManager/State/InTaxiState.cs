
using FSTRaK.DataTypes;
using Serilog;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.Models.FlightManager
{
    internal class InTaxiState : AbstractState
    {
        public override string Name { get; set; } = "Taxi";
        public override bool IsMovementState { get; set; }

        
        public InTaxiState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Taxi";
            this.IsMovementState = true;
        }
        public override void processFlightData(AircraftFlightData Data)
        {
            if(!Data.simOnGround)
            {
                TakeoffEvent To = new TakeoffEvent() { FlapsPosition = Data.FlapPosition };
                AddFlightEvent(Data, To);
                Context.State = new InFlightState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if(!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data, new FlightEvent());
                _stopwatch.Restart();
            }
            
            HandleFlightExit(Context);
        }
    }
}
