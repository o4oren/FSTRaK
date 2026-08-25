using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Camera state is polled on a wall clock rather than delivered with the flight data
    /// subscription, because it must keep arriving while the simulator is paused or in a
    /// menu. A quarter second is well inside human reaction time for a menu transition and
    /// costs a fraction of the former 50ms full-struct poll.
    /// </summary>
    private const int CameraPollInterval = 250;

    /// <summary>
    /// How long an in-progress flight survives a dropped pipe before it is ended. Long
    /// enough to ride out a transient blip, short enough that a flight does not sit open
    /// after the user closes the simulator and walks away.
    /// </summary>
    private const int GracePeriodInterval = 60000;

    public const string MSFS2020 = "MSFS2020";
    public const string MSFS2024 = "MSFS2024";
    private SimConnect _simconnect = null;

    /// <summary>
    /// Guards every access to <see cref="_simconnect"/>. Three threads reach the handle:
    /// the UI thread via WndProc, the camera timer, and the connection timer. Critical
    /// sections must stay narrow - never hold this across a property change, because
    /// those reach the FlightManager state machine, which calls back into this service.
    /// </summary>
    private readonly object _simConnectLock = new object();

    private HwndSource _gHs;
    private Timer _connectionTimer;
    private Timer _cameraTimer;
    private Timer _gracePeriodTimer;

    private bool _pendingIdentityCheck;

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
                OnPropertyChanged();
            }
        }
    }

    private bool _isAwaitingReconnect;

    /// <summary>
    /// True between a pipe failure and either a successful reconnect or the grace period
    /// expiring. The active flight is held in limbo while this is set.
    /// </summary>
    public bool IsAwaitingReconnect
    {
        get => _isAwaitingReconnect;
        private set
        {
            if (value != _isAwaitingReconnect)
            {
                _isAwaitingReconnect = value;
                OnPropertyChanged();
            }
        }
    }

    private string _simVersion = null;
    public string SimVersion
    {
        get => _simVersion;
        private set
        {
            if (value != _simVersion)
            {
                _simVersion = value;
                OnPropertyChanged();
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
                OnPropertyChanged();
                IsCrashed = false; // Remove Crashed flag
            }
        }
    }

    private CameraState _cameraState;

    public CameraState CameraState
    {
        get => _cameraState;
        private set
        {
            if (value != _cameraState)
            {
                PreviousCameraState = _cameraState;
                _cameraState = value;
                OnPropertyChanged();
            }
        }
    }

    private CameraState _previousCameraState;

    public CameraState PreviousCameraState
    {
        get => _previousCameraState;
        private set
        {
            if (value != _previousCameraState)
            {
                _previousCameraState = value;
                OnPropertyChanged();
            }
        }
    }

    // PAUSE_STATE_FLAG_OFF 0 
    // PAUSE_STATE_FLAG_PAUSE 1 // "full" Pause (sim + traffic + etc...) 
    // PAUSE_STATE_FLAG_PAUSE_WITH_SOUND 2 // FSX Legacy Pause (not used anymore) 
    // PAUSE_STATE_FLAG_ACTIVE_PAUSE 4 // Pause was activated using the "Active Pause" Button 
    // PAUSE_STATE_FLAG_SIM_PAUSE 8 // Pause the player sim but traffic, multi, etc... will still run
    // PAUSE_STATE_FLAG_SIM_PAUSE 9 // Fired by MSFS 2024 on back to main menu
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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

    private bool _simStarted = false;

    public bool SimStarted
    {
        get => _simStarted;
        private set
        {
            if (value != _simStarted)
            {
                _simStarted = value;
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
        SetCameraTimer();
        SetConnectionTimer();
        SetGracePeriodTimer();
        WaitForSimConnection();
    }

    private void WaitForSimConnection()
    {
        // ConnectToSimulator restarts the connection timer itself when it fails to
        // produce a handle, so there is nothing left to do here.
        ConnectToSimulator();
    }

    private void SetConnectionTimer()
    {
        _connectionTimer = new Timer(ConnectionInterval);
        _connectionTimer.Elapsed += (sender, e) => ConnectToSimulator();
        _connectionTimer.AutoReset = true;
    }

    private void SetCameraTimer()
    {
        _cameraTimer = new Timer(CameraPollInterval);
        _cameraTimer.Elapsed += (sender, e) => RequestCameraData();
        _cameraTimer.AutoReset = true;
    }

    private void SetGracePeriodTimer()
    {
        _gracePeriodTimer = new Timer(GracePeriodInterval);
        _gracePeriodTimer.AutoReset = false;
        _gracePeriodTimer.Elapsed += (sender, e) => OnGracePeriodExpired();
    }

    private void OnGracePeriodExpired()
    {
        // A normal flight end, a clean quit, or a completed identity check all supersede
        // the grace period. Stop() cannot cancel an already-queued callback, so re-check.
        if (!IsAwaitingReconnect)
        {
            return;
        }

        // Two different expiries share this timer. If the identity check is still pending
        // the pipe did come back, but no data sample followed it within a second window -
        // the sim is hung, or sitting in a menu with nothing changing. Either way the
        // limbo state must not outlive the window, and with no sample there is nothing to
        // identify the flight by, so it ends.
        if (_pendingIdentityCheck)
        {
            _pendingIdentityCheck = false;
            Log.Information($"Reconnected, but no flight data arrived within {GracePeriodInterval / 1000}s - ending the flight.");
        }
        else
        {
            Log.Information($"No reconnection within {GracePeriodInterval / 1000}s - ending the flight.");
        }

        IsAwaitingReconnect = false;
        IsInFlight = false;
    }

    /// <summary>
    /// Decides whether the reconnected session is the same flight. Runs on the first data
    /// sample after a reconnect, because aircraft data and position are not available at
    /// the moment the connection opens.
    ///
    /// <paramref name="before"/> is captured by the caller ahead of assigning
    /// <see cref="FlightData"/>, since that assignment refreshes the very snapshot this
    /// compares against.
    /// </summary>
    private void VerifyFlightIdentity(FlightIdentitySnapshot? before, FlightData data)
    {
        // The fallback expiry may have already ended the flight and cleared the flag; in
        // that case this sample is the first of whatever comes next, not a resumption.
        if (!IsAwaitingReconnect)
        {
            Log.Debug("Identity check skipped - the flight was already resolved.");
            return;
        }

        _gracePeriodTimer.Stop();

        var after = new FlightIdentitySnapshot
        {
            Title = AircraftData.title,
            LiveryName = AircraftData.liveryName,
            Latitude = data.Latitude,
            Longitude = data.Longitude,
            OnGround = Convert.ToBoolean(data.SimOnGround)
        };

        // No snapshot means no flight was ever sampled in the air, so there is nothing to
        // resume - treated as a mismatch rather than compared against a default at 0,0.
        var canResume = before.HasValue
                        && FlightIdentity.CanResume(before.Value, after, IsInFlight, SimVersion == MSFS2024);

        IsAwaitingReconnect = false;

        if (canResume)
        {
            Log.Information($"Resuming the flight after reconnection - {after.Title} near the last known position.");
        }
        else
        {
            Log.Information($"Not the same flight after reconnection (was {before?.Title}, now {after.Title}); ending it.");
            IsInFlight = false;
        }
    }

    /// <summary>
    /// Tears down a failed connection and starts reconnecting. An active flight is kept
    /// alive for the grace period so a transient pipe failure does not discard it.
    /// </summary>
    private void HandleConnectionLost()
    {
        var hadFlight = IsInFlight;

        StopGettingData();
        Close();
        IsConnected = false;

        // SimVersion is deliberately NOT cleared here. The simulator cannot change version
        // without restarting, which arrives as OnRecvQuit (where it IS cleared). Clearing it
        // on a pipe drop would race the post-reconnect facilities reply and silently disable
        // the livery half of the identity check on MSFS 2024.

        // Any check still pending belongs to the connection that just died; the next
        // OnRecvOpen arms a new one if there is still a flight to verify.
        _pendingIdentityCheck = false;

        if (hadFlight)
        {
            IsAwaitingReconnect = true;
            _gracePeriodTimer.Stop();
            _gracePeriodTimer.Start();
            Log.Information($"Connection lost mid-flight; holding the flight for {GracePeriodInterval / 1000}s.");
        }
        else
        {
            IsInFlight = false;
        }

        _connectionTimer.Start();
    }

    /// <summary>
    /// Starts the camera poll. Flight data itself arrives on a standing SIM_FRAME
    /// subscription that dies with the connection, so there is nothing else to start.
    /// The name is kept because the connection-recovery paths call it.
    /// </summary>
    private void StartGettingData()
    {
        _cameraTimer.Start();
    }

    private void StopGettingData()
    {
        _cameraTimer?.Stop();
    }

    private void ConnectToSimulator()
    {
        bool connected;

        try
        {
            Log.Debug("Trying to connect to the simulator...");

            lock (_simConnectLock)
            {
                _simconnect = new SimConnect("FSTrAk", _lHwnd, WmUserSimconnect, null, 0);

                try
                {
                    ConfigureSimconnect();
                }
                catch (Exception ex)
                {
                    // A half-configured handle would otherwise be retained forever, and
                    // WaitForSimConnection only restarts the timer when the field is null.
                    Log.Error(ex, "SimConnect configuration failed; discarding the handle.");
                    _simconnect.Dispose();
                    _simconnect = null;
                    throw;
                }
            }
        }
        catch (COMException ex)
        {
            Log.Debug(ex, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected failure while connecting to the simulator.");
        }

        lock (_simConnectLock)
        {
            connected = _simconnect != null;
        }

        if (!connected && _connectionTimer != null && !_connectionTimer.Enabled)
        {
            _connectionTimer.Start();
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
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "Livery Name", null, SIMCONNECT_DATATYPE.STRING128, 0.0f,
            SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.AircraftData, "Livery Folder", null, SIMCONNECT_DATATYPE.STRING128, 0.0f,
            SimConnect.SIMCONNECT_UNUSED);




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

        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "G FORCE", "GForce",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "PLANE PITCH DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "PLANE BANK DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        // CAMERA - deliberately its own definition, polled on a wall clock rather than
        // carried with the flight data, so it keeps arriving while the sim is paused.
        _simconnect.AddToDataDefinition(DataDefinitions.CameraData, "Camera State", null,
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simconnect.RegisterDataDefineStruct<AircraftData>(DataDefinitions.AircraftData);
        _simconnect.RegisterDataDefineStruct<FlightData>(DataDefinitions.FlightData);
        _simconnect.RegisterDataDefineStruct<CameraData>(DataDefinitions.CameraData);

        // Subscribe to System events
        _simconnect.SubscribeToSystemEvent(Events.FlightLoaded, "FlightLoaded");
        _simconnect.SubscribeToSystemEvent(Events.Pause, "Pause_EX1");
        _simconnect.SubscribeToSystemEvent(Events.Crashed, "Crashed");
        _simconnect.SubscribeToSystemEvent(Events.AircraftLoaded, "AircraftLoaded");
        _simconnect.SubscribeToSystemEvent(Events.Sim, "Sim");
        _simconnect.SubscribeToSystemEvent(Events.View, "View");



        // Register listeners on simconnect events
        _simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(Simconnect_OnRecvSimobjectData);
        _simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(Simconnect_OnRecvAirportList);
        _simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(Simconnect_OnRecvEvent);
        _simconnect.OnRecvEventFilename += new SimConnect.RecvEventFilenameEventHandler(Simconnect_OnRecvFilename);
        _simconnect.OnRecvSystemState += new SimConnect.RecvSystemStateEventHandler(Simconnect_OnRecvSystemState);

        // One standing subscription replaces the former 50ms request loop. SIM_FRAME is
        // tied to the physics loop rather than the render loop, so it does not fluctuate
        // with GPU load - and it stops while the simulator is paused, which is why camera
        // state is polled separately.
        _simconnect.RequestDataOnSimObject(Requests.FlightDataRequest, DataDefinitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SIM_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);

        StartGettingData();
    }

    private void Simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        if (data.dwRequestID == (uint)Requests.FlightLoaded)
        {
            LoadedFlight = data.szString;
            Log.Debug(LoadedFlight);
        } 
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
            case (int)Events.Sim:
                Log.Debug($"Sim: {data.dwData}");
                SimStarted = data.dwData == 1;
                break;
        }
    }

    private void UpdateInFlightState()
    {
        var inputs = new FlightStateInputs
        {
            Camera = CameraState,
            PreviousCamera = PreviousCameraState,
            PauseState = PauseState,
            LoadedFlight = LoadedFlight,
            IsConnected = IsConnected,
            WasInFlight = IsInFlight
        };

        var isInFlight = FlightStateEvaluator.IsInFlight(inputs);

        Log.Debug($"Flight state: pause {PauseState}, sim started {SimStarted}, camera {CameraState} -> in flight {isInFlight}");

        // While awaiting reconnection the flight is held; the grace timer or the identity
        // check decides its fate, not the stale camera state left behind by the drop.
        if (IsAwaitingReconnect && !isInFlight)
        {
            return;
        }

        IsInFlight = isInFlight;
    }

    private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log.Information("Connection to the simulator is closed!");

        StopGettingData();
        Close();
        IsConnected = false;
        SimVersion = null;
        IsAwaitingReconnect = false;
        _pendingIdentityCheck = false;
        _gracePeriodTimer.Stop();
        IsInFlight = false;   // was missing: a flight in progress was orphaned, never saved
        _connectionTimer.Start();
    }

    private void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Log.Information("Connected to flight simulator!");
        _connectionTimer.Stop();
        IsConnected = true;

        _gracePeriodTimer.Stop();

        if (IsAwaitingReconnect)
        {
            // The original window is cancelled by reconnection rather than by a completed
            // identity check: the first data sample may arrive well after it would have
            // closed, and a flight we successfully reconnected to must not be lost waiting
            // for it. A fresh single-shot window is armed in its place so that the held
            // flight cannot sit in limbo forever if that sample never comes at all - the
            // data subscription only reports changed values, so a hung or parked sim can
            // stay silent indefinitely.
            Log.Information("Reconnected inside the grace window; verifying flight identity.");
            _pendingIdentityCheck = true;
            _gracePeriodTimer.Start();
            RequestLoadedAircraft();
        }

        SafeSimConnectCall(
            sc => sc.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, Requests.SimVersionRequest),
            "RequestFacilitiesList_EX1 (sim version)");
    }

    private void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        var exception = (SIMCONNECT_EXCEPTION)data.dwException;

        // SIMCONNECT_RECV_EXCEPTION is a protocol-level report - a bad SimVar or an unknown
        // request - not a pipe failure. Unlike a COMException, which always reconnects, it
        // must not trigger a teardown.
        Log.Error($"SimConnect exception {exception} on send id {data.dwSendID}, index {data.dwIndex}");
    }

    private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        try
        {
            if (data.dwRequestID == (int)Requests.FlightDataRequest)
            {
                // The snapshot has to be read before FlightData is assigned: that assignment
                // raises a property change the FlightManager answers by overwriting
                // LastKnownSnapshot with this very sample, which would make the comparison
                // below trivially true.
                var runIdentityCheck = _pendingIdentityCheck;
                FlightIdentitySnapshot? before = runIdentityCheck
                    ? FlightManager.FlightManager.Instance.LastKnownSnapshot
                    : null;

                FlightData = (FlightData)data.dwData[0];

                if (runIdentityCheck)
                {
                    _pendingIdentityCheck = false;
                    VerifyFlightIdentity(before, FlightData);
                }
            }
            else if (data.dwRequestID == (int)Requests.CameraDataRequest)
            {
                var cameraData = (CameraData)data.dwData[0];

                // Assigned before the tick so that a camera change has already been folded
                // into IsInFlight by the time the state machine is asked to act on it.
                CameraState = cameraData.CameraState;
                FlightManager.FlightManager.Instance.HandleCameraTick();
            }
            else if (data.dwRequestID == (int)Requests.AircraftDataRequest)
            {
                AircraftData = (AircraftData)data.dwData[0];
            }
        }
        catch (COMException ex)
        {
            HandleCOMException(ex);
        }
    }

    /// <summary>
    /// Runs a SimConnect call under the handle lock, routing any failure through the
    /// recovery path. NullReference and ObjectDisposed are caught alongside COMException
    /// because a teardown can still race a caller that captured the handle.
    ///
    /// The lock is released before any handler runs: recovery sets properties, which reach
    /// the FlightManager state machine, which calls back into this service. Holding the
    /// lock across that path would deadlock, so the catch blocks sit outside the lock
    /// statement rather than inside it.
    /// </summary>
    private void SafeSimConnectCall(Action<SimConnect> call, string description)
    {
        try
        {
            lock (_simConnectLock)
            {
                if (_simconnect == null)
                {
                    Log.Debug($"Skipping {description} - no SimConnect handle.");
                    return;
                }

                call(_simconnect);
            }
        }
        catch (COMException ex)
        {
            HandleCOMException(ex);
        }
        catch (Exception ex) when (ex is NullReferenceException || ex is ObjectDisposedException)
        {
            Log.Warning(ex, $"SimConnect handle disposed during {description}; reconnecting.");
            HandleConnectionLost();
        }
    }

    public void RequestNearestAirport()
    {
        NearestAirportDistance = double.MaxValue;
        SafeSimConnectCall(
            sc => sc.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, Requests.NearbyAirportsRequest),
            nameof(RequestNearestAirport));
    }

    /// <summary>
    /// Gets aircraft data from simconnect and the file path to the loaded aircraft
    /// </summary>
    public void RequestLoadedAircraft()
    {
        SafeSimConnectCall(sc =>
        {
            sc.RequestDataOnSimObject(Requests.AircraftDataRequest, DataDefinitions.AircraftData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);
            sc.RequestSystemState(Requests.AircraftLoaded, "AircraftLoaded");
        }, nameof(RequestLoadedAircraft));
    }

    /// <summary>
    /// Polls camera state on a wall clock. Deliberately not part of the FlightData
    /// subscription: this must keep arriving while the simulator is paused or in a menu.
    /// </summary>
    public void RequestCameraData()
    {
        SafeSimConnectCall(sc =>
            sc.RequestDataOnSimObject(Requests.CameraDataRequest, DataDefinitions.CameraData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u),
            nameof(RequestCameraData));
    }

    private void Simconnect_OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
    {

        if ((Requests)data.dwRequestID == Requests.SimVersionRequest)
        {
            if(string.IsNullOrEmpty(SimVersion))
            {
                SimVersion = data.dwVersion >= 6 ?
                    MSFS2024 :
                    MSFS2020;
                Console.WriteLine($"MSFS version = {SimVersion}");
            }
        }
        
        else if((Requests)data.dwRequestID == Requests.NearbyAirportsRequest)
        {
            ProcessAirports(data.rgData.Cast<SIMCONNECT_DATA_FACILITY_AIRPORT>());
        }
    }

    private void ProcessAirports(IEnumerable<SIMCONNECT_DATA_FACILITY_AIRPORT> airports)
    {
        var myCoordinates = new GeoCoordinate(FlightData.Latitude, FlightData.Longitude);

        foreach (var a in airports)
        {
            if (a.Ident.Length is >= 3 and <= 4)
            {
                var airportCoord = new GeoCoordinate(a.Latitude, a.Longitude);
                var distance = airportCoord.GetDistanceTo(myCoordinates);
                if (distance < NearestAirportDistance)
                {
                    NearestAirport = a.Ident;
                    NearestAirportDistance = distance;
                    Log.Information($"Closest found airport is {NearestAirport} at {NearestAirportDistance} meters!");
                }
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string name = null)
    {
        if (name.Equals(nameof(LoadedFlight)) || name.Equals(nameof(PauseState)) || name.Equals(nameof(SimStarted)) || name.Equals(nameof(CameraState)))
        {
            UpdateInFlightState();
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = false;
        // If message is coming from simconnect and the connection is not null;
        // Continue and receive message.
        if (msg == WmUserSimconnect)
        {
            // The _simconnect != null test now lives inside the helper, where it is read
            // under the lock instead of racing a concurrent teardown.
            SafeSimConnectCall(sc => sc.ReceiveMessage(), "ReceiveMessage");
            handled = true;
        }

        return (IntPtr)0;
    }

    private void HandleCOMException(COMException ex)
    {
        var hresult = (uint)ex.ErrorCode;
        Log.Error(ex, $"COMException: {ConnectionRecovery.DescribeFor(hresult)} (HRESULT: 0x{hresult:X8})");

        if (ConnectionRecovery.ActionFor(hresult) == RecoveryAction.Reconnect)
        {
            HandleConnectionLost();
        }
    }

    public void Close()
    {
        _cameraTimer?.Stop();
        _gracePeriodTimer?.Stop();

        lock (_simConnectLock)
        {
            if (_simconnect != null)
            {
                _simconnect.Dispose();
                _simconnect = null;
            }
        }

        Log.Debug("SimConnect Disposed!");
    }
}