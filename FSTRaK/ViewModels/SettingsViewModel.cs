

using Serilog;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Collections.ObjectModel;
using MapControl;

namespace FSTRaK.ViewModels
{
    internal class SettingsViewModel : BaseViewModel
    {
        public ObservableCollection<string> MapProviders { get; set; } = new ObservableCollection<string>();
        private string _selectedMapProvider;
        public string SelectedMapProvider
        {
            get {
                return _selectedMapProvider;
            }
            set {
                _selectedMapProvider = value;
                Properties.Settings.Default.MapTileProvider = _selectedMapProvider;
                Properties.Settings.Default.Save();
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
                Properties.Settings.Default.Save();
                BingMapsTileLayer.ApiKey = _bingApiKey;
                BingMapsTileLayer l;
                Log.Debug(BingMapsTileLayer.ApiKey);
                OnPropertyChanged();
            }
        }

        public SettingsViewModel() : base()
        {

        }

        public void OnLoaded()
        {
            ResourceDictionary mapProviders = new ResourceDictionary();
            mapProviders.Source = new System.Uri("pack://application:,,,/Resources/MapProvidersDictionary.xaml", uriKind: System.UriKind.Absolute);
            MapProviders.Clear();
            foreach (DictionaryEntry provider in mapProviders)
            {
                if((typeof(MapControl.MapTileLayerBase).IsAssignableFrom(provider.Value.GetType())))
                {
                    MapProviders.Add(provider.Key.ToString());
                    WmtsTileLayer l;
                }
            }
            SelectedMapProvider = Properties.Settings.Default.MapTileProvider;
            BingApiKey = Properties.Settings.Default.BingApiKey;
        }
    }
}
