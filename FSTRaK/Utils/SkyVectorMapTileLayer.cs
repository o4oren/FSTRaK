using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MapControl;
using Serilog;

namespace FSTRaK.Utils
{
    internal class SkyVectorMapTileLayer : MapTileLayer
    {
        public SkyVectorMapTileLayer() : base()
        {
            
            TileSource = new SkyVectorTileSource();
        }
    }
}
