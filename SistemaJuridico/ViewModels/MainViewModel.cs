using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // CORREÇÃO AQUI: Adicionados Juiz e Classificacao
    public class ProcessoModel
    {
        public string Id { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Paciente { get; set; } = "";
        public string Juiz { get; set; } = "";          // <--- Adicionado
        public string Classificacao { get; set; } = ""; // <--- Adicionado
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

        // DTO interno para leitura do banco (mapeia colunas do SQLite)
        private class ProcessoDto 
        { 
            public string? id { get; set; } 
            public string? numero { get; set; } 
            public string? paciente { get; set; } 
            public string? juiz { get; set; }
            public string? classificacao { get; set; }
            public string? cache_proximo_prazo { get; set; } 
        }

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
            
            // Query ajustada para trazer mais dados se necessário no futuro
            var sql = "SELECT id, numero, paciente, juiz, classificacao, cache_proximo_prazo FROM processos";
            
            if (!string.IsNullOrEmpty(SearchText)) 
                sql += " WHERE numero LIKE @q OR paciente LIKE @q";
            
            sql += " ORDER BY cache_proximo_prazo ASC"; // Ordenação básica por string de data
            
            var dados = conn.Query<ProcessoDto>(sql, new { q = $"%{SearchText}%" });

            foreach (var item in dados)
            {
                var (texto, cor) = ProcessLogic.CheckPrazoStatus(item.cache_proximo_prazo ?? "");
                
                Processos.Add(new ProcessoModel
                {
                    Id = item.id ?? "",
                    Numero = item.numero ?? "",
                    Paciente = item.paciente ?? "",
                    Juiz = item.juiz ?? "",
                    Classificacao = item.classificacao ?? "",
                    DataPrazo = item.cache_proximo_prazo ?? "--",
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }
    }
}
