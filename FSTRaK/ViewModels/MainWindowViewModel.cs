using FSTRaK.Views;

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
