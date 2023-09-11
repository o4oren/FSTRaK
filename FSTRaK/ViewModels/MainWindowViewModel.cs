using FSTRaK.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Serilog;

namespace FSTRaK.ViewModels
{
    internal class MainWindowViewModel : BaseViewModel
    {
        public RelayCommand MapViewCommand { get; set; }
        public RelayCommand LogBookCommand { get; set; }
        public RelayCommand SettingsCommand { get; set; }



        private readonly LiveViewViewModel _liveViewViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly LogbookViewModel _logbookViewModel;

        private object _activeView;


        public object ActiveView
        {
            get => _activeView;
            set
            {
                if (_settingsViewModel != null && _activeView == _settingsViewModel && value != _settingsViewModel)
                {
                    _settingsViewModel.SaveSettings(); // Save settings when navigating away from the settings page
                }
                _activeView = value;
                OnPropertyChanged();
            }
        }

        public double Height { 
            get => Properties.Settings.Default.Height;
            set
            {
                if(value > 449)
                    Properties.Settings.Default.Height = value;
            }
        }


        public double Width
        {
            get => Properties.Settings.Default.Width;
            set
            {
                if(value > 599)
                    Properties.Settings.Default.Width = value;
            }
        }

        public double Left
        {
            get => Properties.Settings.Default.Left;
            set => Properties.Settings.Default.Left = value;
        }
        public double Top
        {
            get => Properties.Settings.Default.Top;
            set => Properties.Settings.Default.Top = value;
        }

        public MainWindowViewModel()
        {
            _liveViewViewModel = new LiveViewViewModel();
            _settingsViewModel = new SettingsViewModel();
            _logbookViewModel = new LogbookViewModel();

            ActiveView = _liveViewViewModel;

            MapViewCommand = new RelayCommand(o =>
            {
                ActiveView = _liveViewViewModel;
            });

            LogBookCommand = new RelayCommand(o =>
            {
                ActiveView = _logbookViewModel;
            });

            SettingsCommand = new RelayCommand(o =>
            {
                ActiveView = _settingsViewModel;
            });
        }
    }
}
