using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace SistemaJuridico.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        [ObservableProperty] 
        private string _username = "";

        public LoginViewModel()
        {
            _db = App.DB!;
        }

        [RelayCommand]
        public void Login(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Preencha usuário e senha.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _db.Login(Username, password);

            if (result.Success)
            {
                Application.Current.Properties["Usuario"] = result.Username;
                Application.Current.Properties["IsAdmin"] = result.IsAdmin;

                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();

                foreach (Window win in Application.Current.Windows.OfType<Views.LoginWindow>().ToList())
                {
                    win.Close();
                }
            }
            else
            {
                // Mensagem explícita para garantir que você saiba o que tentar
                MessageBox.Show($"Login falhou para o usuário '{Username}'.\n\nCertifique-se de que a instalação foi concluída.\nTente reiniciar o aplicativo para forçar o reset da senha.", 
                    "Credenciais Inválidas", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Fechar()
        {
            Application.Current.Shutdown();
        }
    }
}
