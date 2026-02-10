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

        // Inicialização para evitar aviso de nulo
        [ObservableProperty] 
        private string _username = "";

        public LoginViewModel()
        {
            // O operador ! garante ao compilador que App.DB não é nulo aqui
            // (pois foi iniciado no App.xaml.cs)
            _db = App.DB!;
        }

        [RelayCommand]
        public void Login(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

            var result = _db.Login(Username, password);

            if (result.Success)
            {
                // Armazena sessão globalmente
                Application.Current.Properties["Usuario"] = result.Username;
                Application.Current.Properties["IsAdmin"] = result.IsAdmin;

                // Abre a janela principal
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();

                // Fecha a janela de login
                // Usamos ToList() para evitar erro de modificação da coleção enquanto itera
                foreach (Window win in Application.Current.Windows.OfType<Views.LoginWindow>().ToList())
                {
                    win.Close();
                }
            }
            else
            {
                MessageBox.Show("Usuário ou senha incorretos.\n(Padrão: admin / admin)", "Erro de Acesso", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Fechar()
        {
            Application.Current.Shutdown();
        }
    }
}
