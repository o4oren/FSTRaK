

using System.Collections;
using System.Windows;
using System.Collections.ObjectModel;
using MapControl;
using System.Linq;
using FSTRaK.DataTypes;
using FSTRaK.Utils;
using Microsoft.Win32;
using System;
using Serilog;

namespace FSTRaK.ViewModels
{

    internal class SettingsViewModel : BaseViewModel
    {
        public ObservableCollection<string> MapProviders { get; set; }
        private string _selectedMapProvider = "OpenStreetMap";
        public string SelectedMapProvider
        {
            get => _selectedMapProvider;
            set
            {
                if (value != null && value != _selectedMapProvider)
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
            get => _bingApiKey;
            set
            {
                _bingApiKey = value;
                Properties.Settings.Default.BingApiKey = _bingApiKey;
                BingMapsTileLayer.ApiKey = _bingApiKey;
                OnPropertyChanged();
            }
        }

        private Units _units;
        public Units Units
        {
            get => _units;
            set
            {
                _units = value;
                Properties.Settings.Default.Units = (int)_units;
                OnPropertyChanged();
            }
        }

        private bool _isShowBingApiKeyField = false;
        public bool IsShowBingApiKeyField
        {
            get => _isShowBingApiKeyField;
            private set
            {
                _isShowBingApiKeyField = value;
                OnPropertyChanged();
            }
        }

        private bool _isAlwaysOnTop;
        public bool IsAlwaysOnTop
        {
            get => _isAlwaysOnTop;
            set
            {
                _isAlwaysOnTop = value;
                Properties.Settings.Default.IsAlwaysOnTop = _isAlwaysOnTop;
                OnPropertyChanged();
            }
        }

        private bool _isSaveOnlyCompleteFlights;

        public bool IsSaveOnlyCompleteFlights
        {
            get => _isSaveOnlyCompleteFlights;
            set
            {
                _isSaveOnlyCompleteFlights = value;
                Properties.Settings.Default.IsSaveOnlyCompleteFlights = _isSaveOnlyCompleteFlights;
                OnPropertyChanged();
            }
        }

        private bool _isStartMinimized;

        public bool IsStartMinimized
        {
            get => _isStartMinimized;
            set
            {
                _isStartMinimized = value;
                Properties.Settings.Default.IsStartMinimized = _isStartMinimized;
                OnPropertyChanged();
            }
        }

        private bool _isMinimizeToTray;

        public bool IsMinimizeToTray
        {
            get => _isMinimizeToTray;
            set
            {
                _isMinimizeToTray = value;
                Properties.Settings.Default.IsMinimizeToTray = _isMinimizeToTray;
                OnPropertyChanged();
            }
        }

        private bool _isStartAutomatically;
        public bool IsStartAutomatically
        {
            get => _isStartAutomatically;
            set
            {
                _isStartAutomatically = value;
                Properties.Settings.Default.IsStartAutomatically = _isStartAutomatically;



#if DEBUG
                return; // prevent debug builds from registering for auto startup
#endif
                // Set startup as needed
                RegistryKey rkStartUp = Registry.CurrentUser;
                var applicationLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

                var startupPathSubKey = rkStartUp.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);


                AppDomain.CurrentDomain.SetData("DataDirectory", PathUtil.GetApplicationLocalDataPath());

                if (_isStartAutomatically && startupPathSubKey?.GetValue("FSTrAk") == null)
                {
                    startupPathSubKey?.SetValue("FSTrAk", applicationLocation, RegistryValueKind.ExpandString);
                }

                if (!_isStartAutomatically && startupPathSubKey?.GetValue("FSTrAk") != null)
                {
                    startupPathSubKey?.DeleteValue("FSTrAk");
                }

                OnPropertyChanged();
            }
        }



        public ObservableCollection<string> Fonts { get; set; } = new ObservableCollection<string>(new []{"Slopes", "Arial"});
        private string _fontName = "Slopes";
        public string FontName
        {
            get => _fontName;
            set
            {
                if (value != null && value != _fontName)
                {
                    _fontName = value;
                    FontUtil.SetFont(_fontName);
                    Properties.Settings.Default.FontName = _fontName;
                    OnPropertyChanged();
                }
            }
        }

        public SettingsViewModel() : base()
        {
            var mapProviders = new ResourceDictionary
            {
                Source = new System.Uri("pack://application:,,,/Resources/MapProvidersDictionary.xaml", uriKind: System.UriKind.Absolute)
            };
            var layers = new ObservableCollection<string>();

            foreach (DictionaryEntry provider in mapProviders)
            {
                if (provider.Value is MapTileLayerBase
                    || provider.Value is WmsImageLayer)
                {
                    layers.Add(provider.Key.ToString());
                }
            }
            MapProviders = new ObservableCollection<string>(layers.OrderBy(l => l));
        }

        public void SettingsView_OnLoaded()
        {
            SelectedMapProvider = Properties.Settings.Default.MapTileProvider;
            BingApiKey = Properties.Settings.Default.BingApiKey;
            IsAlwaysOnTop = Properties.Settings.Default.IsAlwaysOnTop;
            IsSaveOnlyCompleteFlights = Properties.Settings.Default.IsSaveOnlyCompleteFlights;
            Units = (Units)Properties.Settings.Default.Units;
            IsStartMinimized = Properties.Settings.Default.IsStartMinimized;
            IsMinimizeToTray = Properties.Settings.Default.IsMinimizeToTray;
            IsStartAutomatically = Properties.Settings.Default.IsStartAutomatically;
            FontName = Properties.Settings.Default.FontName;
        }

        public void SaveSettings()
        {
            Properties.Settings.Default.Save();
        }
    }
}
