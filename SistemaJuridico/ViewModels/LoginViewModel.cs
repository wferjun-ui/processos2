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

        // Propriedade que recebe o texto digitado na tela
        [ObservableProperty] 
        private string _username = "";

        public LoginViewModel()
        {
            // Garante acesso ao banco de dados global
            _db = App.DB!;
        }

        [RelayCommand]
        public void Login(object? parameter)
        {
            // Pega a senha do componente visual de forma segura
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

            // Verificação Detalhada para Debug
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("O campo 'Usuário' está vazio.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("O campo 'Senha' está vazio.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tenta fazer o login no banco
            var result = _db.Login(Username, password);

            if (result.Success)
            {
                // Salva quem logou
                Application.Current.Properties["Usuario"] = result.Username;
                Application.Current.Properties["IsAdmin"] = result.IsAdmin;

                // Abre o Dashboard
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();

                // Fecha a tela de Login
                foreach (Window win in Application.Current.Windows.OfType<Views.LoginWindow>().ToList())
                {
                    win.Close();
                }
            }
            else
            {
                MessageBox.Show($"Login falhou.\nUsuário digitado: {Username}\nSenha digitada: (oculta)\n\nTente: admin / admin", 
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
