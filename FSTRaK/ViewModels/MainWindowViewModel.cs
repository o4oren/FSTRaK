﻿using FSTRaK.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace FSTRaK.ViewModels
{
    internal class MainWindowViewModel : BaseViewModel
    {
        public RelayCommand MapViewCommand { get; set; }
        public RelayCommand LogBookCommand { get; set; }


        private FlightDataViewModel _flightDataViewModel;

        private object _activeView;


        public object ActiveView
        {
            get { return _activeView; }
            set
            {
                _activeView = value;
                OnPropertyChanged();
            }
        }

        public MainWindowViewModel()
        {
            _flightDataViewModel = new FlightDataViewModel();
            ActiveView = _flightDataViewModel;

            MapViewCommand = new RelayCommand(o =>
            {
                ActiveView = _flightDataViewModel;
            });

            LogBookCommand = new RelayCommand(o =>
            {
                ActiveView = "Hahahaha!";
            });
        }
    }
}