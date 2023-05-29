using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;


namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        public void OnLoaded(object s, RoutedEventArgs e)
        {
            ((SettingsViewModel)DataContext).OnLoaded();
        }
    }
}
