using System.Windows;
using SmartTrafficDashboard.ViewModels;

namespace SmartTrafficDashboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
