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
                using (var logbookContext = new LogbookContext())
                {
                    var flight = logbookContext.Flights.Create();
                    Context.ActiveFlight = flight;

                    SetAircraftAsynchronously(flight, data);

                    Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                    _isStarted = true;
                    Log.Information("Flight started!");
                }  

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
                (Math.Abs(data.Latitude - Context.CurrentFlightParams.Latitude) > 0.0000001 || Math.Abs(data.Longitude - Context.CurrentFlightParams.Longitude) > 0.0000001) && data.GroundVelocity > 1)
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
                    try
                    {
                        // If aircraft is already in the db, let's use the existing records instead.
                        var aircraft = logbookContext.Aircraft.FirstOrDefault(a => a.Title == data.title);
                        if (aircraft != null)
                        {
                            Context.ActiveFlight.Aircraft = aircraft;
                        }
                        else
                        {
                            aircraft = logbookContext.Aircraft.Create();

                            aircraft.Title = data.title;
                            aircraft.Manufacturer = data.atcType;
                            aircraft.Model = data.model;
                            aircraft.AircraftType = data.model;
                            aircraft.Airline = data.airline;
                            aircraft.TailNumber = data.AtcId;
                            aircraft.NumberOfEngines = data.NumberOfEngines;
                            aircraft.EngineType = data.EngineType;
                        

                            EnrichAircraftDataFromFile(aircraft);

                            // Capitalize manufacturer name correctly.
                            var cultureInfo = new CultureInfo("en-US");
                            var textInfo = cultureInfo.TextInfo;
                            aircraft.Manufacturer = textInfo.ToTitleCase(aircraft.Manufacturer.ToLower());

                            aircraft = logbookContext.Aircraft.Add(aircraft);
                            flight.Aircraft = aircraft;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occurred!");
                    }
                    finally
                    {
                        Log.Information(flight.Aircraft.ToString());
                    }
                }

            });
        }

        private void EnrichAircraftDataFromFile(Aircraft aircraft)
        {
            var filename = Context.GetLoadedAircraftFileName();
            if (String.IsNullOrEmpty(filename))
                return;

            try
            {
                using (var fileStream = File.OpenRead(filename))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 128))
                {
                    String line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        var parts = line.Split('=');
                        if (parts.Length <= 1) continue;
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
            }
            catch (Exception ex)
            {
                Log.Error("Could not enrich aircraft from file.", ex.Message);
            }

        }
    }
}

