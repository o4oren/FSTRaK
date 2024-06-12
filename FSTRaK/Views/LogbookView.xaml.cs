using System;
using System.Windows;
using System.Windows.Controls;
using FSTRaK.ViewModels;


namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for LogbookView.xaml
    /// </summary>
    public partial class LogbookView : UserControl
    {
        public LogbookView()
        {
            InitializeComponent();
        }

        private void LogbookView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LogbookViewModel viewModel)
            {
                viewModel.OnLoad();
            }
        }
    }
}
