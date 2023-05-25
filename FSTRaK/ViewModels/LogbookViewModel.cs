using FSTRaK.Models;
using FSTRaK.Models.Entity;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
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
            Flights = new ObservableCollection<Flight>();
            var flights = _logbookContext.Flights.Where(f => f.ID < 10).Include(f => f.Aircraft);
            flights.ToList().ForEach(Flights.Add);
        }
    }
}
