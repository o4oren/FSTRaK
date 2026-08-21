using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using MapControl;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class FlightEndedState : AbstractState
    {

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private Boolean _isEnded = false;
        public FlightEndedState(FlightManager context) : base(context)
        {
            this.Name = "Flight Ended";
            this.IsMovementState = false;
            Log.Information($"Flight ended at {DateTime.Now}");
        }

        public override void ProcessFlightData(FlightData data)
        {
            if(!_isEnded)
            {

                var fe = new FlightEndedEvent();

                SetFlightOutcome();

                AddFlightEvent(data, fe);

                Context.ActiveFlight.EndTime = fe.Time;
                // Use taxi out event if available, otherwise, use flightstarted for fuel and time calc.
                if (
                    Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is FlightStartedEvent) is FlightStartedEvent
                    startEvent)
                {
                    if (Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is TaxiOutEvent) is TaxiOutEvent taxiOutEvent)
                    {
                        var flightTime = fe.Time - taxiOutEvent.Time;

                        if (fe.FuelWeightLbs > 0)
                        {
                            if (Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is ParkingEvent) is ParkingEvent
                                parkingEvent)
                            {
                                Context.ActiveFlight.TotalFuelUsed =
                                    (startEvent.FuelWeightLbs - parkingEvent.FuelWeightLbs) ?? 0;
                            }
                            else
                            {
                                Context.ActiveFlight.TotalFuelUsed = (taxiOutEvent.FuelWeightLbs - fe.FuelWeightLbs) ?? 0;
                            }
                        }
                        else
                        {
                            if (Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is ParkingEvent) is ParkingEvent parkingEvent)
                            {
                                Context.ActiveFlight.TotalFuelUsed = (startEvent.FuelWeightLbs - parkingEvent.FuelWeightLbs) ?? 0;
                            }
                        }
                        Context.ActiveFlight.FlightTime = flightTime;
                    }
                    else
                    {
                        var flightTime = fe.Time - startEvent.Time;

                        if (fe.FuelWeightLbs > 0)
                        {
                            Context.ActiveFlight.TotalFuelUsed = (startEvent.FuelWeightLbs - fe.FuelWeightLbs) ?? 0;
                        }
                        else
                        {
                            if (Context.ActiveFlight.FlightEvents.FirstOrDefault(e => e is ParkingEvent) is ParkingEvent parkingEvent)
                            {
                                Context.ActiveFlight.TotalFuelUsed = (startEvent.FuelWeightLbs - parkingEvent.FuelWeightLbs) ?? 0;
                            }
                        }
                        Context.ActiveFlight.FlightTime = flightTime;
                    }
                }

                Context.ActiveFlight.FlightDistanceNm = FlightPathLength(Context.ActiveFlight.FlightEvents) * Consts.MetersToNauticalMiles;

                Context.ActiveFlight.UpdateScore();

                if (Context.ActiveFlight.FlightOutcome == FlightOutcome.Completed || !Properties.Settings.Default.IsSaveOnlyCompleteFlights)
                {
                    _ = SaveFlight();
                }
                Log.Information($"Flight Ended!\n{Context.ActiveFlight.ToString()}");
                _isEnded = true;
            }

            if (_isEnded && data.MaxEngineRpmPct() > 5 && Context.ActiveFlight.FlightOutcome != FlightOutcome.Crashed)
            {
                Context.State = new FlightStartedState(Context);
            }
        }

        private void SetFlightOutcome()
        {
            if (Context.ActiveFlight.FlightEvents.LastOrDefault() is ParkingEvent)
            {
                Context.ActiveFlight.FlightOutcome = FlightOutcome.Completed;
            }
            else if (Context.ActiveFlight.FlightEvents.LastOrDefault() is CrashEvent)
            {
                Context.ActiveFlight.FlightOutcome = FlightOutcome.Crashed;
            }
            else
            {
                Context.ActiveFlight.FlightOutcome = FlightOutcome.Exited;
            }
        }

        private double FlightPathLength(ObservableCollection<BaseFlightEvent> flightEvents)
        {
            double length = 0;
            for ( var i = 1; i < flightEvents.Count; i++)
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

        private Task SaveFlight()
        {
            return Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        // Aircraft is potentially already in the db, so we attach it in this dbcontext. If the aircraft was pulled from the db in the flightstarted phase, it will have an ID.
                        if (Context.ActiveFlight.Aircraft.Id != 0)
                        {
                            logbookContext.Aircraft.Attach(Context.ActiveFlight.Aircraft);
                        }

                        AttachFlightPlanIfEligible();

                        logbookContext.Flights.Add(Context.ActiveFlight);
                        logbookContext.SaveChanges();
                        Context.OnFlightSaved(Context.ActiveFlight.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred while trying to persist the flight!");
                        // Plan persistence must never lose a flight — retry once without the plan graph.
                        if (Context.ActiveFlight.FlightPlan != null)
                        {
                            try
                            {
                                Context.ActiveFlight.FlightPlan = null;
                                logbookContext.SaveChanges();
                                Context.OnFlightSaved(Context.ActiveFlight.Id);
                                Log.Warning("Flight saved without its SimBrief plan after a save failure");
                            }
                            catch (Exception retryEx)
                            {
                                Log.Error(retryEx, "Retry without SimBrief plan also failed!");
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Attaches the matched SimBrief plan when the flight landed at the planned arrival or one
        /// of the planned alternates. Never throws — plan persistence must not endanger the flight save.
        /// Must be called AFTER the aircraft is attached to the context so the airline backfill is
        /// detected as a modification.
        /// </summary>
        private void AttachFlightPlanIfEligible()
        {
            try
            {
                var plan = BusinessLogic.SimBriefService.SimBriefService.Instance.MatchedFlightPlan;
                if (plan == null)
                    return;

                if (!BusinessLogic.SimBriefService.SimBriefOfpMapper.ShouldSavePlan(plan, Context.ActiveFlight.ArrivalAirport))
                {
                    Log.Information($"SimBrief: not saving plan - arrival {Context.ActiveFlight.ArrivalAirport} matches neither planned arrival {plan.ArrivalAirport} nor alternates {plan.AlternateAirports}");
                    return;
                }

                Context.ActiveFlight.FlightPlan = plan;
                Log.Information($"SimBrief: plan {plan.ComposedFlightNumber} {plan.DepartureAirport} -> {plan.ArrivalAirport} attached to flight");

                if (string.IsNullOrWhiteSpace(Context.ActiveFlight.Aircraft?.Airline)
                    && !string.IsNullOrWhiteSpace(plan.AirlineIcao))
                {
                    Context.ActiveFlight.Aircraft.Airline = plan.AirlineIcao;
                    Log.Information($"SimBrief: backfilled blank aircraft airline with {plan.AirlineIcao}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SimBrief: failed to attach flight plan - saving flight without it");
            }
        }
    }
}
