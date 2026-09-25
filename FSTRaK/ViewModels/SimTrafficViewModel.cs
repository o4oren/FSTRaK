using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// The simulator traffic layer. Kept apart from LiveViewViewModel deliberately: that
    /// file already carries the VATSIM and IVAO layers, and this is a third source with its
    /// own lifecycle.
    /// </summary>
    internal sealed class SimTrafficViewModel : BaseViewModel
    {
        private readonly SimConnectService _simConnectService = SimConnectService.Instance;

        public BindingList<SimTrafficAircraft> Aircraft { get; } = new BindingList<SimTrafficAircraft>();

        private bool _isShowSimTraffic;

        /// <summary>
        /// Off on every launch, and deliberately not persisted. Switching it off stops the
        /// polling outright rather than merely hiding the layer.
        /// </summary>
        public bool IsShowSimTraffic
        {
            get => _isShowSimTraffic;
            set
            {
                if (value == _isShowSimTraffic)
                {
                    return;
                }

                _isShowSimTraffic = value;
                OnPropertyChanged();
                _simConnectService.SetSimTrafficEnabled(value);
            }
        }

        public SimTrafficViewModel()
        {
            _simConnectService.SimTrafficUpdated += OnTrafficUpdated;
        }

        /// <summary>
        /// Snapshots arrive on the SimConnect receive path, which is the UI thread - but the
        /// tracker's poll timer can also publish an empty snapshot from a timer thread when a
        /// cycle goes silent, so the marshalling below is not optional.
        /// </summary>
        private void OnTrafficUpdated(IReadOnlyList<SimTrafficEntry> snapshot)
        {
            var items = snapshot.Select(entry => new SimTrafficAircraft(entry)).ToList();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Aircraft.ReplaceContent(items);
                return;
            }

            dispatcher.BeginInvoke(new System.Action(() => Aircraft.ReplaceContent(items)));
        }
    }
}
