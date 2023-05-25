using FSTRaK.Models;
using FSTRaK.Models.Entity;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FSTRaK.ViewModels
{
    internal class LogbookViewModel : BaseViewModel
    {
        LogbookContext _logbookContext = new LogbookContext();
        
        public ObservableCollection<Flight> Flights { get; set; }

        public RelayCommand LoadFlightsCommand { get; set; }

        public LogbookViewModel() 
        {
                _logbookContext.Flights.Load();
                Flights = _logbookContext.Flights.Local;


            Flight f = _logbookContext.Flights.Find(1);

        }

        


    }
}
