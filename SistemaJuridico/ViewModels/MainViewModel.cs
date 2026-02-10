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
    // Modelo de UI (blindado contra nulos)
    public class ProcessoModel
    {
        public string Id { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Paciente { get; set; } = string.Empty;
        public string DataPrazo { get; set; } = string.Empty;
        public string StatusTexto { get; set; } = string.Empty;
        public SolidColorBrush StatusCor { get; set; } = Brushes.Gray;
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        [ObservableProperty]
        private ObservableCollection<ProcessoModel> _processos = new();

        [ObservableProperty]
        private string _searchText = "";

        public MainViewModel()
        {
            _db = App.DB!;
            CarregarDashboard();
        }

        // Classe interna DTO para mapeamento do Banco (aceita nulos do SQLite)
        private class ProcessoDto
        {
            public string? id { get; set; }
            public string? numero { get; set; }
            public string? paciente { get; set; }
            public string? cache_proximo_prazo { get; set; }
        }

        [RelayCommand]
        public void NovoProcesso()
        {
            var janela = new Views.CadastroWindow();
            if (Application.Current.MainWindow != null)
            {
                janela.Owner = Application.Current.MainWindow;
            }
            
            janela.ShowDialog();
            CarregarDashboard(); // Recarrega lista ao voltar
        }

        [RelayCommand]
        public void AbrirDetalhes(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            var janela = new Views.ProcessoDetalhesWindow();
            janela.DataContext = new ProcessoDetalhesViewModel(id);
            
            if (Application.Current.MainWindow != null)
                janela.Owner = Application.Current.MainWindow;

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
            
            if (!string.IsNullOrEmpty(SearchText))
            {
                sql += " WHERE numero LIKE @q OR paciente LIKE @q";
            }
            
            sql += " ORDER BY cache_proximo_prazo ASC";

            var dados = conn.Query<ProcessoDto>(sql, new { q = $"%{SearchText}%" });

            foreach (var item in dados)
            {
                var prazo = item.cache_proximo_prazo ?? "";
                var (texto, cor) = CalcularStatus(prazo);

                Processos.Add(new ProcessoModel
                {
                    Id = item.id ?? "",
                    Numero = item.numero ?? "Sem Número",
                    Paciente = item.paciente ?? "Sem Nome",
                    DataPrazo = string.IsNullOrEmpty(prazo) ? "--" : prazo,
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }

        private (string, SolidColorBrush) CalcularStatus(string dataStr)
        {
            if (string.IsNullOrEmpty(dataStr)) return ("Sem Prazo", Brushes.Gray);

            if (DateTime.TryParseExact(dataStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime prazo))
            {
                var dias = (prazo.Date - DateTime.Now.Date).TotalDays;
                
                if (dias < 0) return ("ATRASADO", Brushes.Red);
                if (dias == 0) return ("VENCE HOJE", Brushes.OrangeRed);
                if (dias <= 7) return ($"Vence em {dias} dias", Brushes.Goldenrod);
                return ("No Prazo", Brushes.Green);
            }
            
            return ("Data Inválida", Brushes.Gray);
        }
    }
}
