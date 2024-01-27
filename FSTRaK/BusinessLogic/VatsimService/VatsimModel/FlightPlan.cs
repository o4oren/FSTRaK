using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    internal class FlightPlan
    {

        public string aircraft { get; set; }

        public string aircraft_faa { get; set; }

        public string aircraft_short { get; set; }

        public string alternate { get; set; }

        public string altitude { get; set; }

        public string arrival { get; set; }

        public string assigned_transponder { get; set; }

        public string cruise_tas { get; set; }

        public string departure { get; set; }

        public string deptime { get; set; }

        public string enroute_time { get; set; }
        public string flight_rules { get; set; }

        public string fuel_time { get; set; }

        public string remarks { get; set; }

        public string revision_id { get; set; }
        public string route { get; set; }


    }
}
