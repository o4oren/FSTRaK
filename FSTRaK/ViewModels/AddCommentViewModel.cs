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
using System.Data.Entity;

namespace FSTRaK.ViewModels
{
    internal class AddCommentViewModel : BaseViewModel
    {
        private Flight _flight;
        public Flight Flight
        {
            get => _flight;
            set
            {
                _flight = value;
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

        public RelayCommand AddComment { get; }
        public RelayCommand ClosePopup { get; }


        public AddCommentViewModel(Flight flight) : base()
        {
            Flight = flight;

            AddComment = new RelayCommand(o =>
            {
                Task.Run(() =>
                {
                    using (var logbookContext = new LogbookContext())
                    {
                        try
                        {
                            logbookContext.Entry(flight).State = EntityState.Modified;
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

