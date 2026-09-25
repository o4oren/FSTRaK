using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;
using Serilog;

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

        private readonly HashSet<string> _reportedUnresolvedLabels = new HashSet<string>();

        public SimTrafficViewModel()
        {
            _simConnectService.SimTrafficUpdated += OnTrafficUpdated;
        }

        /// <summary>
        /// Snapshots arrive on the SimConnect receive path, which runs inside the native
        /// WndProc -> ReceiveMessage() pump - so this always marshals via BeginInvoke rather
        /// than taking a same-thread shortcut. A full MapItemsControl container regeneration
        /// for potentially hundreds of aircraft must never run inside that pump, and any
        /// exception it throws must never escape across the native boundary. The tracker's
        /// poll timer can also publish an empty snapshot from a timer thread when a cycle
        /// goes silent, so marshalling is required on that path too.
        /// </summary>
        private void OnTrafficUpdated(IReadOnlyList<SimTrafficEntry> snapshot)
        {
            var items = snapshot.Select(entry => new SimTrafficAircraft(entry)).ToList();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // Design-time and test hosts have no Application.Current.
                Aircraft.ReplaceContent(items);
                return;
            }

            dispatcher.BeginInvoke(new System.Action(() =>
            {
                Aircraft.ReplaceContent(items);
                LogUnresolvedLabels(items);
            }));
        }

        /// <summary>
        /// Any label still carrying a localisation marker is a key shape SimVarText does not
        /// yet recognise. Logged once per distinct value so the log stays readable while
        /// still capturing the exact text needed to extend the parser.
        ///
        /// Runs on the dispatcher, so the set below needs no synchronisation.
        /// </summary>
        private void LogUnresolvedLabels(IReadOnlyList<SimTrafficAircraft> items)
        {
            foreach (var aircraft in items)
            {
                foreach (var label in new[] { aircraft.AircraftType, aircraft.Manufacturer, aircraft.Airline })
                {
                    if (string.IsNullOrEmpty(label) ||
                        label.IndexOf("ATCCOM", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (_reportedUnresolvedLabels.Add(label))
                    {
                        Log.Debug($"Sim traffic label not recognised as a localisation key: '{label}'");
                    }
                }
            }
        }
    }
}
