using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    internal class Atis
    {
        public string atis_code;
        public string callsign;

        public int cid;
        public int facility;
        public string frequency;
        public string last_updated;
        public string logon_time;
        public string name;
        public int rating;
        public string server;
        public string[] text_atis;
        public int visual_range;
    }
}
