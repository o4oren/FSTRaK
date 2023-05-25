using FSTRaK.Models;
using FSTRaK.Models.Entity;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.ViewModels
{
    internal class LogbookViewModel : BaseViewModel
    {
        LogbookContext _logbookContext = new LogbookContext();
        
        public ObservableCollection<Flight> Flights { get; set; }

        public RelayCommand LoadFlightsCommand { get; set; }

        public LogbookViewModel() 
        {
            var flight = _logbookContext.Flights
                                .Where(f => f.ID == 1)
                                .FirstOrDefault<Flight>();

            _logbookContext.Entry(flight).Reference(f => f.Aircraft).Load(); // loads StudentAddress
            _logbookContext.Entry(flight).Collection(f => f.FlightEvents).Load(); // loads Courses collection 

            Flights = new ObservableCollection<Flight>();
            Flights.Add(flight);

        }

        


    }
}
