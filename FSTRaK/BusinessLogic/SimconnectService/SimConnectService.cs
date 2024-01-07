using System;
using System.ComponentModel;
using System.Device.Location;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Interop;
using FSTRaK.DataTypes;
using Microsoft.FlightSimulator.SimConnect;
using Serilog;

namespace FSTRaK.BusinessLogic.SimconnectService;

/// <summary>
///    This class is a facade over simconnect and simplifies communication with the simulator for the consumer's 
///    interaction with the sim.
///    It hides the simconnect details, handles connection to the sim and exposes data.
/// </summary>
internal sealed class SimConnectService : INotifyPropertyChanged
{
    private const int ConnectionInterval = 10000;
    private const int WmUserSimconnect = 0x0402;
    private const string MainMenuFlt = "flights\\other\\MainMenu.FLT";
    private SimConnect _simconnect = null;

    private HwndSource _gHs;
    private Timer _connectionTimer;
    private IntPtr _lHwnd;

    private bool _isConnected = false;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (value != _isConnected)
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
            }
        }
    }

    private bool _isInFlight = false;

    public bool IsInFlight
    {
        get => _isInFlight;
        private set
        {
            if (value != _isInFlight)
            {
                _isInFlight = value;
                OnPropertyChanged(nameof(IsInFlight));
                IsCrashed = false; // Remove Crashed flag
            }
        }
    }

    // PAUSE_STATE_FLAG_OFF 0 
    // PAUSE_STATE_FLAG_PAUSE 1 // "full" Pause (sim + traffic + etc...) 
    // PAUSE_STATE_FLAG_PAUSE_WITH_SOUND 2 // FSX Legacy Pause (not used anymore) 
    // PAUSE_STATE_FLAG_ACTIVE_PAUSE 4 // Pause was activated using the "Active Pause" Button 
    // PAUSE_STATE_FLAG_SIM_PAUSE 8 // Pause the player sim but traffic, multi, etc... will still run
    private uint _pauseState = 1;

    public uint PauseState
    {
        get => _pauseState;
        private set
        {
            if (value != _pauseState)
            {
                _pauseState = value;
                OnPropertyChanged(nameof(PauseState));
            }
        }
    }

    private bool _isCrashed = false;

    public bool IsCrashed
    {
        get => _isCrashed;
        private set
        {
            if (value != _isCrashed)
            {
                _isCrashed = value;
                OnPropertyChanged(nameof(IsCrashed));
            }
        }
    }

    private string _loadedFlight = string.Empty;

    public string LoadedFlight
    {
        get => _loadedFlight;
        private set
        {
            if (value != _loadedFlight)
            {
                _loadedFlight = value;
                OnPropertyChanged(nameof(LoadedFlight));
            }
        }
    }

    private string _loadedAircraft = string.Empty;

    public string LoadedAircraft
    {
        get => _loadedAircraft;
        private set
        {
            if (value != _loadedAircraft)
            {
                _loadedAircraft = value;
                OnPropertyChanged();
            }
        }
    }


    private FlightData _flightData;

    public FlightData FlightData
    {
        get => _flightData;
        private set
        {
            _flightData = value;
            OnPropertyChanged();
        }
    }

    private AircraftData _aircraftData;

    public AircraftData AircraftData
    {
        get => _aircraftData;
        private set
        {
            _aircraftData = value;
            OnPropertyChanged();
        }
    }

    public double NearestAirportDistance { get; set; } = double.MaxValue;
    private string _nearestAirport = string.Empty;

    public string NearestAirport
    {
        get => _nearestAirport;
        private set
        {
            _nearestAirport = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private SimConnectService()
    {
    }

    private static readonly object Lock = new();
    private static SimConnectService _instance = null;

    public static SimConnectService Instance
    {
        get
        {
            lock (Lock)
            {
                return _instance ??= new SimConnectService();
            }
        }
    }

    /// <summary>
    /// Initialize should only be called after a main window is loaded, as it relies on it's existance for recieving system events in a wpf application.
    /// </summary>
    internal void Initialize()
    {
        //  Create a handle and hook to receive windows messages
        if (System.Windows.Application.Current.MainWindow != null)
        {
            var lWih = new WindowInteropHelper(System.Windows.Application.Current.MainWindow);
            _lHwnd = lWih.Handle;
        }

        _gHs = HwndSource.FromHwnd(_lHwnd);
        _gHs?.AddHook(new HwndSourceHook(WndProc));

        SetConnectionTimer();
        WaitForSimConnection();
    }

    private void WaitForSimConnection()
    {
        ConnectToSimulator();
        if (_simconnect == null) _connectionTimer.Start();
    }

    private void SetConnectionTimer()
    {
        _connectionTimer = new Timer(ConnectionInterval);
        _connectionTimer.Elapsed += (sender, e) => ConnectToSimulator();
        _connectionTimer.AutoReset = true;
    }

    private void ConnectToSimulator()
    {
        try
        {
            Log.Debug("Trying to connect to the simulator...");
            _simconnect = new SimConnect("FSTrAk", _lHwnd, WmUserSimconnect, null, 0);
            if (_simconnect != null) ConfigureSimconnect();
        }
        catch (COMException ex)
        {
            Log.Debug(ex, ex.Message);
            // Do nothing
        }
    }

    private void ConfigureSimconnect()
    {
        // Management events
        _simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
        _simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
        _simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

        // Configure and register data DataDefinitions for requests


        // AIRCRAFT
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f,
            SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "ATC Airline", null, SIMCONNECT_DATATYPE.STRING256,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "ATC Model", null, SIMCONNECT_DATATYPE.STRING32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "ATC Type", null, SIMCONNECT_DATATYPE.STRING256,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "ATC ID", null, SIMCONNECT_DATATYPE.STRING32, 0.0f,
            SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "Category", null, SIMCONNECT_DATATYPE.STRING128,
            0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "ENGINE TYPE", "number", SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "NUMBER OF ENGINES", "number",
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "EMPTY WEIGHT", "pounds",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);




        // Flight data
        //Time
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Year", "number", SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Month of Year", "number",
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Day of Month", "number",
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Time", "seconds", SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Sim On Ground", "Bool", SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Latitude", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Longitude", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Heading Degrees True", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Altitude", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Airspeed True", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Airspeed Indicated", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Ground Velocity", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Ground Altitude", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Alt Above Ground", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Alt Above Ground Minus CG", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Vertical Speed", "ft/min",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Camera State", null, SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Flap Speed Exceeded", null,
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Gear Speed Exceeded", null,
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Overspeed Warning", null,
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Stall Warning", null, SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "TRAILING EDGE FLAPS LEFT ANGLE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "FUEL TOTAL QUANTITY WEIGHT", "pounds",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "TOTAL WEIGHT", "pounds",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "BRAKE PARKING POSITION", "Bool",
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG PCT MAX RPM:1", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG PCT MAX RPM:2", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG PCT MAX RPM:3", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG PCT MAX RPM:4", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG THROTTLE LEVER POSITION:1", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG THROTTLE LEVER POSITION:2", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG THROTTLE LEVER POSITION:3", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "GENERAL ENG THROTTLE LEVER POSITION:4", "percent",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.RegisterDataDefineStruct<AircraftData>(DataDefinitions.AircraftData);
        _simconnect.RegisterDataDefineStruct<FlightData>(DataDefinitions.FlightData);

        // Subscribe to System events
        _simconnect.SubscribeToSystemEvent(Events.FlightLoaded, "FlightLoaded");
        _simconnect.SubscribeToSystemEvent(Events.Pause, "Pause_EX1");
        _simconnect.SubscribeToSystemEvent(Events.Crashed, "Crashed");
        _simconnect.SubscribeToSystemEvent(Events.AircraftLoaded, "AircraftLoaded");


        // Register listeners on simconnect events
        _simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(Simconnect_OnRecvSimobjectData);
        _simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(Simconnect_OnRecvAirportList);
        _simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(Simconnect_OnRecvEvent);
        _simconnect.OnRecvEventFilename += new SimConnect.RecvEventFilenameEventHandler(Simconnect_OnRecvFilename);
        _simconnect.OnRecvSystemState += new SimConnect.RecvSystemStateEventHandler(Simconnect_OnRecvSystemState);

        // Start getting data
        _simconnect.RequestDataOnSimObject(Requests.FlightDataRequest, DataDefinitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0u, 0u, 0u);
    }

    private void Simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        if (data.dwRequestID == (uint)Requests.FlightLoaded)
        {
            LoadedFlight = data.szString;
            Log.Debug(LoadedFlight);
        } 
// Disabled because this is returning with a partial path (starting from 'Simbojects')
//        if (data.dwRequestID == (uint)Requests.AircraftLoaded)
//        {
//            LoadedAircraft = data.szString;
//           Log.Debug(LoadedAircraft);
//        }
    }

    private void Simconnect_OnRecvFilename(SimConnect sender, SIMCONNECT_RECV_EVENT_FILENAME data)
    {
        if (data.uEventID == (uint)Events.FlightLoaded)
        {
            LoadedFlight = data.szFileName;
            Log.Debug(LoadedFlight);
        }

        ;
        if (data.uEventID == (uint)Events.AircraftLoaded)
        {
            LoadedAircraft = data.szFileName;
            Log.Debug(data.szFileName);
        }
    }

    private void Simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        switch (data.uEventID)
        {
            case (int)Events.FlightLoaded:
                // Do nothing, this is handled in OnRecvFileName
                break;
            case (int)Events.Pause:
                PauseState = data.dwData;
                break;
            case (int)Events.Crashed:
                IsCrashed = true;
                break;
            case (int)Events.AircraftLoaded:
                // Do nothing, this is handled in OnRecvFileName;
                break;
        }
    }

    private void UpdateInFlightState()
    {
        Log.Information($"Flight state updated : {LoadedFlight.Equals(MainMenuFlt)}, Pause state: {PauseState}");
        if (!LoadedFlight.Equals(MainMenuFlt) && PauseState != 1)
        {
            IsInFlight = true;
        }
        else
        {
            IsInFlight = false;
        }
    }

    private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log.Information("Connection to the simulator is closed!");
        Close();
        IsConnected = false;
        _connectionTimer.Start();
    }

    private void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Log.Information("Connected to flight simulator!");
        _connectionTimer.Stop();
        IsConnected = true;
    }

    private void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        Log.Error($"Simconnect exception {data.dwException}");

        // Due to previous hanging after System.Runtime.InteropServices.COMException (0xC000014B) we will try to set IsConnected to false - and let it try to connect again.
        IsConnected = false;
        _connectionTimer.Start();
    }

    private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (int)Requests.FlightDataRequest)
        {
            FlightData = (FlightData)data.dwData[0];
            // OnPropertyChanged(nameof(FlightData));
        }
        else if (data.dwRequestID == (int)Requests.AircraftDataRequest)
        {
            AircraftData = (AircraftData)data.dwData[0];
        }
    }

    public void RequestNearestAirport()
    {
        NearestAirportDistance = double.MaxValue;
        _simconnect.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, Requests.NearbyAirportsRequest);
    }

    /// <summary>
    /// Gets aircraft data from simconnect and the file path to the loaded aircraft
    /// </summary>
    public void RequestLoadedAircraft()
    {
        _simconnect.RequestDataOnSimObject(Requests.AircraftDataRequest, DataDefinitions.AircraftData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0u, 0u, 0u);
        _simconnect.RequestSystemState(Requests.AircraftLoaded, "AircraftLoaded");
    }

    private void Simconnect_OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
    {
        try
        {
            var myCoordinates = new GeoCoordinate(FlightData.Latitude, FlightData.Longitude);

            foreach (var a in data.rgData.Cast<SIMCONNECT_DATA_FACILITY_AIRPORT>())
            {
                if(a.Icao.Length < 3 || a.Icao.Length > 4)
                    continue;
                var airportCoord = new GeoCoordinate(a.Latitude, a.Longitude);
                var distance = airportCoord.GetDistanceTo(myCoordinates);
                if (distance < NearestAirportDistance)
                {
                    NearestAirport = a.Icao;
                        NearestAirportDistance = distance;
                        Log.Information(
                            $"Closest found airport is {NearestAirport} at {NearestAirportDistance} meters!");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string name = null)
    {
        if (name.Equals(nameof(LoadedFlight)) || name.Equals(nameof(PauseState))) UpdateInFlightState();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = false;
        // If message is coming from simconnect and the connection is not null;
        // Continue and receive message.
        if (msg == WmUserSimconnect && _simconnect != null)
        {
            _simconnect.ReceiveMessage();
            handled = true;
        }

        return (IntPtr)0;
    }

    public void Close()
    {
        if (_simconnect != null)
        {
            _simconnect.Dispose();
            _simconnect = null;
        }

        Log.Debug("SimConnect Disposed!");
    }
}