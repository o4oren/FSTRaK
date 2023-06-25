using FSTRaK.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.ViewModels
{
    internal class EditAircraftViewModel : BaseViewModel
    {
        private Aircraft _aircraft;
        public Aircraft Aircraft
        {
            get => _aircraft;
            set
            {
                _aircraft = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand UpdateAircraft { get; }

        public EditAircraftViewModel(Aircraft aircraft) : base()
        {
            Aircraft = aircraft;
            UpdateAircraft = new RelayCommand(o =>
            {
                Log.Debug(Aircraft.ToString());
                Log.Debug("Clicked!!!!");
            });
        }

    }
}

