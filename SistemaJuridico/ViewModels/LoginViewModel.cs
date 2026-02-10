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

        [ObservableProperty] 
        private string _username = "";

        public LoginViewModel()
        {
            _db = new DatabaseService();
        }

        [RelayCommand]
        public void Login(object parameter)
        {
            // O PasswordBox é passado como parâmetro porque a propriedade Password não é bindable por segurança
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

            var result = _db.Login(Username, password);

            if (result.Success)
            {
                // Armazena sessão globalmente
                if (Application.Current != null)
                {
                    Application.Current.Properties["UsuarioLogado"] = result.Username;
                    Application.Current.Properties["IsAdmin"] = result.IsAdmin;

                    // Abre a janela principal
                    var main = new MainWindow();
                    Application.Current.MainWindow = main;
                    main.Show();

                    // Fecha a janela de login
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is Views.LoginWindow)
                        {
                            win.Close();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Usuário ou senha incorretos.", "Erro de Acesso", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Fechar()
        {
            Application.Current.Shutdown();
        }
    }
}
