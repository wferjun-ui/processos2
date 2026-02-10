using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    public partial class AdminViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        // Lista de usuários para a tabela
        public ObservableCollection<UserModel> Users { get; set; } = new();

        // Campos para adicionar novo usuário
        [ObservableProperty] private string _novoUser = "";
        [ObservableProperty] private string _novaSenha = "";
        [ObservableProperty] private string _novoEmail = "";
        [ObservableProperty] private bool _novoIsAdmin = false;

        public AdminViewModel()
        {
            _db = App.DB!;
            CarregarUsuarios();
        }

        [RelayCommand]
        public void CarregarUsuarios()
        {
            Users.Clear();
            var lista = _db.GetAllUsers();
            foreach (var u in lista) Users.Add(u);
        }

        [RelayCommand]
        public void AdicionarUsuario()
        {
            if (string.IsNullOrWhiteSpace(NovoUser) || string.IsNullOrWhiteSpace(NovaSenha))
            {
                MessageBox.Show("Preencha usuário e senha.", "Erro");
                return;
            }

            try
            {
                _db.RegistrarUsuario(NovoUser, NovaSenha, NovoEmail, NovoIsAdmin);
                MessageBox.Show("Usuário criado com sucesso!");
                
                // Limpa campos
                NovoUser = ""; NovaSenha = ""; NovoEmail = ""; NovoIsAdmin = false;
                CarregarUsuarios();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Erro ao criar usuário (talvez o nome já exista): " + ex.Message);
            }
        }

        [RelayCommand]
        public void DeletarUsuario(string id)
        {
            if (MessageBox.Show("Tem certeza que deseja remover este usuário?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _db.DeleteUser(id);
                CarregarUsuarios();
            }
        }
    }
}
