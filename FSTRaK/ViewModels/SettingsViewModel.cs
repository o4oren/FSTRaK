

using System.Collections;
using System.Windows;
using System.Collections.ObjectModel;
using MapControl;
using System.Linq;
using Serilog;

namespace FSTRaK.ViewModels
{
    internal class SettingsViewModel : BaseViewModel
    {
        public ObservableCollection<string> MapProviders { get; set; } = new ObservableCollection<string>();
        private string _selectedMapProvider = "OpenStreetMap";
        public string SelectedMapProvider
        {
            get {
                return _selectedMapProvider;
            }
            set {
                if(value != null && value != _selectedMapProvider)
                {
                    _selectedMapProvider = value;
                    Properties.Settings.Default.MapTileProvider = _selectedMapProvider;

                    var mapProvider = Application.Current.TryFindResource(_selectedMapProvider);
                    if (mapProvider is BingMapsTileLayer)
                        IsShowBingApiKeyField = true;
                    else
                        IsShowBingApiKeyField = false;

                }
                OnPropertyChanged();

            }
        }

        private string _bingApiKey = "";
        public string BingApiKey
        {
            get
            {
                return _bingApiKey;
            }
            set
            {
                _bingApiKey = value;
                Properties.Settings.Default.BingApiKey = _bingApiKey;
                BingMapsTileLayer.ApiKey = _bingApiKey;
                OnPropertyChanged();
            }
        }

        

        private bool _isShowBingApiKeyField = false;
        public bool IsShowBingApiKeyField
        {
            get
            {
                return _isShowBingApiKeyField;
            }
            private set
            {
                _isShowBingApiKeyField = value;
                OnPropertyChanged();
            }
        }

        private bool _isAlwaysOnTop = false;
        public bool IsAlwaysOnTop
        {
            get
            {
                return _isAlwaysOnTop;
            }
            set
            {
                _isAlwaysOnTop = value;
                Properties.Settings.Default.IsAlwaysOnTop = _isAlwaysOnTop;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel() : base()
        {
            ResourceDictionary mapProviders = new ResourceDictionary();
            mapProviders.Source = new System.Uri("pack://application:,,,/Resources/MapProvidersDictionary.xaml", uriKind: System.UriKind.Absolute);
            ObservableCollection<string> layers = new ObservableCollection<string>();

            foreach (DictionaryEntry provider in mapProviders)
            {
                if ((typeof(MapTileLayerBase).IsAssignableFrom(provider.Value.GetType())) 
                    || (typeof(WmsImageLayer).IsAssignableFrom(provider.Value.GetType())))
                {
                    layers.Add(provider.Key.ToString());
                }
            }
            MapProviders = new ObservableCollection<string>(layers.OrderBy(l => l));
        }

        public void OnLoaded()
        {
            SelectedMapProvider = Properties.Settings.Default.MapTileProvider;
            BingApiKey = Properties.Settings.Default.BingApiKey;
            IsAlwaysOnTop = Properties.Settings.Default.IsAlwaysOnTop;
        }
    }
}
