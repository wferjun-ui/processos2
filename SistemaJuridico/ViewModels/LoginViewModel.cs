using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System.Windows;
using System.Windows.Controls;

namespace SistemaJuridico.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        [ObservableProperty] public string _username = "";

        public LoginViewModel() { _db = new DatabaseService(); }

        [RelayCommand]
        public void Login(object parameter)
        {
            var passBox = parameter as PasswordBox;
            var senha = passBox?.Password ?? "";

            var result = _db.Login(Username, senha);
            if (result.Success)
            {
                Application.Current.Properties["Usuario"] = result.Username;
                Application.Current.Properties["IsAdmin"] = result.IsAdmin;

                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();

                // Fecha a janela de login atual
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is Views.LoginWindow) win.Close();
                }
            }
            else
            {
                MessageBox.Show("Usuário ou senha incorretos.\n(Padrão: admin / admin)", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Fechar() { Application.Current.Shutdown(); }
    }
}
