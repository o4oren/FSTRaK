using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK
{
    internal sealed class FlightManager
    {
        private static readonly object _lock = new object();
        private static FlightManager instance = null;
        private FlightManager() { }
        private SimConnectService _simConnectService;
        public static FlightManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new FlightManager();
                    }
                    return instance;
                }
            }
        }

        public void Close()
        {
            _simConnectService?.Close();
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
        }
    }
}
