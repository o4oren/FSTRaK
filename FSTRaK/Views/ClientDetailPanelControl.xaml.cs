using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FSTRaK.Views
{
    public partial class ClientDetailPanelControl : UserControl
    {
        public ClientDetailPanelControl()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is FSTRaK.ViewModels.LiveViewViewModel vm)
                {
                    ((System.Windows.Input.ICommand)vm.ClearSelectionCommand).Execute(null);
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
    }
}
