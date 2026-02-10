using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;

namespace SistemaJuridico.ViewModels
{
    public partial class AdminViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public ObservableCollection<UserModel> Users { get; set; } = new();

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
                MessageBox.Show("Preencha Usuário e Senha.", "Campos Obrigatórios");
                return;
            }

            try
            {
                _db.RegistrarUsuario(NovoUser, NovaSenha, NovoEmail, NovoIsAdmin);
                MessageBox.Show("Usuário cadastrado com sucesso!");
                NovoUser = ""; NovaSenha = ""; NovoEmail = ""; NovoIsAdmin = false;
                CarregarUsuarios();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Erro ao cadastrar (usuário já existe?): " + ex.Message);
            }
        }

        [RelayCommand]
        public void DeletarUsuario(string id)
        {
            if (MessageBox.Show("Tem certeza que deseja remover este usuário?", "Confirmação", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Proteção para não se deletar
                var currentUser = Application.Current.Properties["Usuario"]?.ToString();
                var userToDelete = Users.FirstOrDefault(u => u.Id == id);
                
                if (userToDelete != null && userToDelete.Username == currentUser)
                {
                    MessageBox.Show("Você não pode deletar a si mesmo!");
                    return;
                }

                _db.DeleteUser(id);
                CarregarUsuarios();
            }
        }

        [RelayCommand]
        public void Fechar()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }
}
