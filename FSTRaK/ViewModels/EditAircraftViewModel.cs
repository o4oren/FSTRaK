using FSTRaK.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FSTRaK.Models;
using Serilog;
using FSTRaK.Models.Entity;
using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;
using System.Data.Entity;

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

        private bool _wasUpdated = false;
        public bool WasUpdated
        {
            get => _wasUpdated;
            set
            {
                _wasUpdated = value;
                OnPropertyChanged();
            }
        }

        private bool _isShow = true;
        public bool IsShow
        {
            get => _isShow;
            set
            {
                _isShow = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand UpdateAircraft { get; }
        public RelayCommand ClosePopup { get; }


        public EditAircraftViewModel(Aircraft aircraft) : base()
        {
            Aircraft = aircraft;

            UpdateAircraft = new RelayCommand(o =>
            {
                Task.Run(() =>
                {
                    using (var logbookContext = new LogbookContext())
                    {
                        try
                        {
                            logbookContext.Entry(aircraft).State = EntityState.Modified;
                            logbookContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, ex.Message);
                        }
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            WasUpdated = true;
                            IsShow = false;
                        });
                    }
                });
            });


            ClosePopup = new RelayCommand(o =>
            {
                IsShow = false;
            });
        }

    }
}

