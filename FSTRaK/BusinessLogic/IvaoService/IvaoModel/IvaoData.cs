using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoData
    {
        public List<IvaoPilot> pilots { get; set; }
        public List<IvaoAtcEntry> atcEntries { get; set; }
    }
}
