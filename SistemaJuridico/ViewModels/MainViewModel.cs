using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    public class ProcessoModel
    {
        public string Id { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Paciente { get; set; } = "";
        public string DataPrazo { get; set; } = "";
        public string StatusTexto { get; set; } = "";
        public SolidColorBrush StatusCor { get; set; } = Brushes.Gray;
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        [ObservableProperty] private ObservableCollection<ProcessoModel> _processos = new();
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private Visibility _adminVisibility = Visibility.Collapsed;

        public MainViewModel()
        {
            _db = App.DB!;
            VerificarPermissoes();
            CarregarDashboard();
        }

        private void VerificarPermissoes()
        {
            if (Application.Current.Properties.Contains("IsAdmin") && 
                Application.Current.Properties["IsAdmin"] is bool isAdmin && isAdmin)
            {
                AdminVisibility = Visibility.Visible;
            }
            else { AdminVisibility = Visibility.Collapsed; }
        }

        private class ProcessoDto { public string? id { get; set; } public string? numero { get; set; } public string? paciente { get; set; } public string? cache_proximo_prazo { get; set; } }

        [RelayCommand]
        public void NovoProcesso()
        {
            var janela = new Views.CadastroWindow();
            if (Application.Current.MainWindow != null) janela.Owner = Application.Current.MainWindow;
            janela.ShowDialog();
            CarregarDashboard();
        }
        
        [RelayCommand]
        public void AbrirAdmin()
        {
            var janela = new Views.AdminWindow();
            if (Application.Current.MainWindow != null) janela.Owner = Application.Current.MainWindow;
            janela.ShowDialog();
        }

        [RelayCommand]
        public void AbrirDetalhes(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var janela = new Views.ProcessoDetalhesWindow();
            janela.DataContext = new ProcessoDetalhesViewModel(id);
            if (Application.Current.MainWindow != null) janela.Owner = Application.Current.MainWindow;
            janela.ShowDialog();
            CarregarDashboard();
        }

        [RelayCommand]
        public void Sair()
        {
            var login = new Views.LoginWindow();
            login.Show();
            Application.Current.MainWindow?.Close();
        }

        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();
            var sql = "SELECT id, numero, paciente, cache_proximo_prazo FROM processos";
            if (!string.IsNullOrEmpty(SearchText)) sql += " WHERE numero LIKE @q OR paciente LIKE @q";
            
            // Ordenação por data (String dd/mm/yyyy no SQLite não ordena bem por padrão, mas para simplicidade manteremos a query)
            // Idealmente converter para YYYY-MM-DD no banco, mas a lógica Python usa dd/mm/yyyy.
            // Vamos ordenar em memória ou aceitar a ordem de inserção por enquanto.
            
            var dados = conn.Query<ProcessoDto>(sql, new { q = $"%{SearchText}%" });

            foreach (var item in dados)
            {
                // USA A LÓGICA CENTRALIZADA PARA CORES
                var (texto, cor) = ProcessLogic.CheckPrazoStatus(item.cache_proximo_prazo ?? "");
                
                Processos.Add(new ProcessoModel
                {
                    Id = item.id ?? "",
                    Numero = item.numero ?? "",
                    Paciente = item.paciente ?? "",
                    DataPrazo = item.cache_proximo_prazo ?? "--",
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }
    }
}
