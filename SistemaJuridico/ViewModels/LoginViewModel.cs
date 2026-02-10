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
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private Visibility _firstAccessVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _loginVisibility = Visibility.Visible;
        
        // Campos Primeiro Acesso
        [ObservableProperty] private string _faEmail = "";
        [ObservableProperty] private string _faUser = "";
        
        public LoginViewModel() => _db = App.DB!;

        [RelayCommand]
        public void Login(object parameter)
        {
            var password = (parameter as PasswordBox)?.Password ?? "";
            var result = _db.Login(Username, password);
            if (result.Success) {
                Application.Current.Properties["Usuario"] = result.Username;
                Application.Current.Properties["IsAdmin"] = result.IsAdmin;
                new MainWindow().Show();
                Application.Current.Windows[0].Close();
            } else {
                MessageBox.Show("Login inválido. Se for seu primeiro acesso, clique na opção abaixo.", "Erro");
            }
        }

        [RelayCommand] public void ToggleFirstAccess() {
            FirstAccessVisibility = Visibility.Visible;
            LoginVisibility = Visibility.Collapsed;
        }

        [RelayCommand] public void CancelFirstAccess() {
            FirstAccessVisibility = Visibility.Collapsed;
            LoginVisibility = Visibility.Visible;
        }

        [RelayCommand]
        public void RegisterFirstAccess(object parameter) {
            var pass = (parameter as PasswordBox)?.Password ?? "";
            string res = _db.CompleteRegistration(FaEmail, FaUser, pass);
            if (res == "OK") {
                MessageBox.Show("Cadastro concluído! Faça login.");
                CancelFirstAccess();
            } else MessageBox.Show(res);
        }
    }
}
