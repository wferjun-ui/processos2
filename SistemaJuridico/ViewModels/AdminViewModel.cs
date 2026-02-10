using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using Microsoft.Win32;
using SistemaJuridico.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    public class UserDto 
    { 
        public string Id { get; set; } = string.Empty; 
        public string Email { get; set; } = string.Empty; 
        public string Username { get; set; } = string.Empty; 
        public bool IsAdmin { get; set; } 
    }

    public partial class AdminViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        public ObservableCollection<UserDto> Users { get; set; } = new();
        [ObservableProperty] private string _novoEmail = "";
        [ObservableProperty] private bool _novoIsAdmin;

        public AdminViewModel() { _db = App.DB!; LoadUsers(); }

        private void LoadUsers() {
            Users.Clear();
            using var conn = _db.GetConnection();
            var us = conn.Query<UserDto>("SELECT id, email, username, is_admin as IsAdmin FROM usuarios");
            foreach(var u in us) Users.Add(u);
        }

        [RelayCommand]
        public void AutorizarEmail() {
            if (_db.AuthorizeEmail(NovoEmail, NovoIsAdmin)) {
                MessageBox.Show("E-mail autorizado! O usuário pode definir a senha no primeiro acesso.");
                LoadUsers();
                NovoEmail = "";
            } else MessageBox.Show("Erro ao autorizar (e-mail já existe?).");
        }

        [RelayCommand]
        public void DeletarUser(string id) {
            if (MessageBox.Show("Remover usuário?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                _db.DeleteUser(id);
                LoadUsers();
            }
        }

        [RelayCommand]
        public void ImportarJson() {
            var ofd = new OpenFileDialog { Filter = "JSON files (*.json)|*.json", Title = "Importar Backup Legado" };
            if (ofd.ShowDialog() == true) {
                if (MessageBox.Show("Isso irá mesclar dados do JSON no banco atual. Continuar?", "Importar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                    var (success, msg) = _db.ImportFromJson(ofd.FileName);
                    MessageBox.Show(msg, success ? "Sucesso" : "Erro");
                }
            }
        }
    }
}
