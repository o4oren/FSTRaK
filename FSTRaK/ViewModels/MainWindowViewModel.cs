using FSTRaK.Views;

namespace FSTRaK.ViewModels
{
    internal class MainWindowViewModel : BaseViewModel
    {
        public RelayCommand MapViewCommand { get; set; }
        public RelayCommand LogBookCommand { get; set; }
        public RelayCommand SettingsCommand { get; set; }



        private FlightDataViewModel _flightDataViewModel;
        private SettingsViewModel _settingsViewModel;
        private LogbookViewModel _logbookViewModel;

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
            _settingsViewModel = new SettingsViewModel();
            _logbookViewModel = new LogbookViewModel();

            ActiveView = _flightDataViewModel;

            MapViewCommand = new RelayCommand(o =>
            {
                ActiveView = _flightDataViewModel;
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
