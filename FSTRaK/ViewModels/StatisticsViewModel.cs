using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSTRaK.Models;
using FSTRaK.Models.Entity;

namespace FSTRaK.ViewModels
{
    internal class StatisticsViewModel : BaseViewModel
    {
        private int _totalNumberOfFlights;

        public int TotalNumberOfFlights
        {
            get => _totalNumberOfFlights;
            set
            {
                _totalNumberOfFlights = value;
                OnPropertyChanged();
            }
        }

        public StatisticsViewModel()
        {
            // Flights = new ObservableCollection<Flight>();
            using var logbookContext = new LogbookContext();
            TotalNumberOfFlights = logbookContext.Flights.Count();

        }
    }
}
