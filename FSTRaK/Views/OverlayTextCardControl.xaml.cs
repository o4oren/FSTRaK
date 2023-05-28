using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for OverlayTextCardControl.xaml
    /// </summary>
    public partial class OverlayTextCardControl : UserControl
    {
        public OverlayTextCardControl()
        {
            InitializeComponent();
        }

        

    public string Header
    {
        get { return (string)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(string), typeof(OverlayTextCardControl), new PropertyMetadata(default(string)));


    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(OverlayTextCardControl), new PropertyMetadata(default(string)));

    }
}
