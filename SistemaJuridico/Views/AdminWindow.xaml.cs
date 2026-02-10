using System.Windows;
using SistemaJuridico.ViewModels;

namespace SistemaJuridico.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            DataContext = new AdminViewModel();
        }
    }
}
