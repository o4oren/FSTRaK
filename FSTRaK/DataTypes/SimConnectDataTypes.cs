using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FSTRaK.DataTypes
{


    // This enum represents camera states as defined on https://docs.flightsimulator.com/html/Programming_Tools/SimVars/Camera_Variables.htm#CAMERA_STATE 
    public enum CameraState
    {
        Cockpit = 2,
        External = 3,
        Drone = 4,
        Fixed = 5,
        Environment = 6,
        SixDof = 7,
        GamePlay = 8,
        Showcase = 9,
        DroneAircraft = 10,
        Waiting = 11,
        WorldMap = 12,
        HangarRtc = 13, 
        HangarCustom = 14,
        MenuRtc = 15,
        InGameRtc = 16,
        Replay = 17,
        DroneTopDown = 19,
        Hangar = 21,
        Ground = 24,
        FollowTrafficAircraft = 25
    }
    internal class SimConnectDataTypes
    {
         
    }
}
