using System.Windows;
using SistemaJuridico.ViewModels;

namespace SistemaJuridico.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
        }
    }
}
