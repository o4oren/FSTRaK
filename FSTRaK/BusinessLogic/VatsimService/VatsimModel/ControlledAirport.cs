using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    internal class ControlledAirport
    {
        public VatsimStaticData.Airport Airport { get; private set; }
        public List<Controller> Controllers { get; private set; }
        public List<Atis> Atis { get; private set; }

        public ControlledAirport(VatsimStaticData.Airport airport)
        {
            this.Airport = airport;
            Controllers = new List<Controller>();
            Atis = new List<Atis>();
        }
    }
}
