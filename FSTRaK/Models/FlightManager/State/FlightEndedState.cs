using FSTRaK.DataTypes;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using MapControl;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
                FlightEndedEvent fe = new FlightEndedEvent
                {
                    FuelWeightLbs = Data.FuelWeightLbs
                };

                if (Context.ActiveFlight.FlightEvents.Last() is ParkingEvent)
                {
                    Context.ActiveFlight.FlightOutcome = FlightOutcome.Completed;
                } 
                else if(Context.ActiveFlight.FlightEvents.Last() is CrashEvent)
                {
                    Context.ActiveFlight.FlightOutcome = FlightOutcome.Crashed;
                }
                else if (!Context.SimConnectInFlight)
                {
                    Context.ActiveFlight.FlightOutcome = FlightOutcome.Exited;
                }

                AddFlightEvent(Data, fe);

                Context.ActiveFlight.EndTime = fe.Time;

                var startEvent = Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is FlightStartedEvent) as FlightStartedEvent;

                var flightTime = fe.Time - startEvent.Time;

                if(fe.FuelWeightLbs > 0)
                {
                    Context.ActiveFlight.TotalFuelUsed = startEvent.FuelWeightLbs - fe.FuelWeightLbs;
                } else
                {
                    if (Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is ParkingEvent) is ParkingEvent parkingEvent)
                    {
                        Context.ActiveFlight.TotalFuelUsed = startEvent.FuelWeightLbs - parkingEvent.FuelWeightLbs;
                    }
                }

                Context.ActiveFlight.FlightTime = flightTime;
                Context.ActiveFlight.FlightDistanceInMeters = FlightPathLength(Context.ActiveFlight.FlightEvents);

                // Calcualate the score.
                var scoringDelta = Context.ActiveFlight.FlightEvents
                    .Where(e => e.GetType().IsSubclassOf(typeof(ScoringEvent)))
                    .GroupBy(e => e.GetType())
                    .Select(e => (ScoringEvent)e.First())
                    .Sum(e => e.ScoreDelta);

                Context.ActiveFlight.Score = MathUtils.Clamp(100 + scoringDelta, 0, 100);

                if (Context.ActiveFlight.FlightOutcome == FlightOutcome.Completed || !Properties.Settings.Default.IsSaveOnlyCompleteFlights)
                {
                    SavetFlight();
                }
                _isEnded = true;
            }

            if(_isEnded && Data.MaxEngineRpmPct() > 5 && Context.ActiveFlight.FlightOutcome != FlightOutcome.Crashed)
            {
                Context.State = new FlightStartedState(Context);
            }
        }

        private double FlightPathLength(ObservableCollection<FlightEvent> flightEvents)
        {
            double length = 0;
            for ( int i = 1; i < flightEvents.Count; i++)
            {
                var start = new Location(flightEvents[i - 1].Latitude, flightEvents[i - 1].Longitude);
                var end = new Location(flightEvents[i].Latitude, flightEvents[i].Longitude);

                length += end.GetDistance(start);
            }
            return length;
        }

        public override void HandleFlightExitEvent()
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState(Context);
            }
        }

        private Task SavetFlight()
        {
            return Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        // Check if the aircraft user is already in the db
                        var aircraft = logbookContext.Aircraft.Where(a => a.Title == Context.ActiveFlight.Aircraft.Title).FirstOrDefault();
                        if (aircraft != null)
                        {
                            Context.ActiveFlight.Aircraft = aircraft;
                        }
                        logbookContext.Flights.Add(Context.ActiveFlight);
                        logbookContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex.Message);

                        Log.Debug(ex.ToString());
                        Log.Debug(ex.InnerException.ToString());

                    }
                }
            });
        }
    }
}
