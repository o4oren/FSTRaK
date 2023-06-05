using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSTRaK.DataTypes;
using FSTRaK.Models.Entity;
using Serilog;

namespace FSTRaK.Models.FlightManager.State
{
    internal class FlightStartedState : AbstractState
    {
        private Boolean _isStarted = false;
        private double _prevFuelQuantity = 0;
        private readonly Stopwatch _fuelingStopwatch;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private readonly FlightStartedEvent _flightStartedEvent;

        public FlightStartedState(FlightManager context) : base(context)
        {
            this.Name = "Flight Started";
            this.IsMovementState = false;
            _flightStartedEvent = new FlightStartedEvent();
            _fuelingStopwatch = new Stopwatch();            
        }

        public override void ProcessFlightData(AircraftFlightData data)
        {
            // Only once in actual plane and not paused
            // This should only happen once per flight
            if (!_isStarted)
            {
                var flight = new Flight();
                Context.ActiveFlight = flight;

                SetAircraftAsynchronously(flight, data);
                
                Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                _isStarted = true;
                Log.Information("Flight started!");
            }
            
            if (data.CameraState == (int)CameraState.Cockpit && Context.ActiveFlight.FlightEvents.Count == 0)
            {
                _prevFuelQuantity = data.FuelWeightLbs;
                _flightStartedEvent.FuelWeightLbs = data.FuelWeightLbs;
                _fuelingStopwatch.Start();
                AddFlightEvent(data, _flightStartedEvent);
                Context.ActiveFlight.StartTime = _flightStartedEvent.Time;
            }

            // Update start event's fuel quantity if it was a large change (because that indicates the user set up fuel). Small changes can be attributed to APU running.
            if(_fuelingStopwatch.ElapsedMilliseconds > 1000)
            {
                if(_prevFuelQuantity + 3 < data.FuelWeightLbs || _prevFuelQuantity - 3 > data.FuelWeightLbs)
                {
                    _flightStartedEvent.FuelWeightLbs = data.FuelWeightLbs;
                    _prevFuelQuantity = data.FuelWeightLbs;
                    Log.Information($"Fuel Quantity updated to {_flightStartedEvent.FuelWeightLbs}");
                }
            }

            // Compare the location to determine movement ONLY after out of the "ready to fly" screen
            if (data.CameraState == (int)CameraState.Cockpit && 
                (data.Latitude != Context.CurrentFlightParams.Latitude || data.Longitude != Context.CurrentFlightParams.Longitude) && data.GroundVelocity > 1)
            {
                var to = new TaxiOutEvent
                {
                    FuelWeightLbs = data.FuelWeightLbs
                };
                AddFlightEvent(data, to);
                Context.State = new TaxiOutState(Context);
            }
        }

        private void SetAircraftAsynchronously(Flight flight, AircraftFlightData data)
        {
            _ = Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    Aircraft aircraft;
                    try
                    {
                        // If aircraft is already in the db, let's use the existing records instead.
                        aircraft = logbookContext.Aircraft.Where(a => a.Title == data.title).FirstOrDefault();
                        if (aircraft != null)
                        {
                            Context.ActiveFlight.Aircraft = aircraft;
                        }
                        else
                        {
                            aircraft = new Aircraft
                            {
                                Title = data.title,
                                Manufacturer = data.atcType,
                                Model = data.model,
                                AircraftType = data.model,
                                Airline = data.airline,
                                TailNumber = data.AtcId,
                                NumberOfEngines = data.NumberOfEngines,
                                EngineType = data.EngineType
                            };

                            aircraft = logbookContext.Aircraft.Add(aircraft);
                            flight.Aircraft = aircraft;

                            // The data returned in simconnect simvars in not consistent, so we will try to fill data from the loaded aircraft file.
                            var filename = Context.GetLoadedAircraftFileName();

                            using (var fileStream = File.OpenRead(filename))
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 128))
                            {
                                String line;
                                while ((line = streamReader.ReadLine()) != null)
                                {
                                    var parts = line.Split('=');
                                    if (parts.Length > 1)
                                    {
                                        if (parts[0].Trim() == "icao_type_designator")
                                        {
                                            aircraft.AircraftType = parts[1].Trim('"', ' ', '\t');
                                        }

                                        if (parts[0].Trim() == "icao_manufacturer")
                                        {
                                            aircraft.Manufacturer = parts[1].Trim('"', ' ', '\t');
                                        }

                                        if (parts[0].Trim() == "icao_model")
                                        {
                                            aircraft.Model = parts[1].Trim('"', ' ', '\t');
                                        }
                                    }
                                }
                                // Capitalize manufacturer name correctly.
                                CultureInfo cultureInfo = new CultureInfo("en-US");
                                TextInfo textInfo = cultureInfo.TextInfo;
                                aircraft.Manufacturer = textInfo.ToTitleCase(aircraft.Manufacturer.ToLower());
                                Log.Information(aircraft.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occured!");
                    }
                }

            });
        }
    }
}

