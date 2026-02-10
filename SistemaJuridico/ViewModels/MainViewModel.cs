using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    public class ProcessoModel
    {
        public string Id { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Paciente { get; set; } = string.Empty;
        public string DataPrazo { get; set; } = string.Empty;
        
        // Propriedades Visuais
        public string StatusTexto { get; set; } = string.Empty;
        public SolidColorBrush StatusCor { get; set; } = Brushes.Gray;
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _db;

        [ObservableProperty]
        private ObservableCollection<ProcessoModel> _processos = new();

        public MainViewModel()
        {
            _db = new DatabaseService();
            CarregarDashboard();
        }

        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();
            
            // Query Otimizada usando o Cache (source: 187)
            var dados = conn.Query("SELECT id, numero, paciente, cache_proximo_prazo FROM processos");

            foreach (var item in dados)
            {
                var (texto, cor) = CalcularStatus(item.cache_proximo_prazo);
                Processos.Add(new ProcessoModel
                {
                    Id = item.id,
                    Numero = item.numero,
                    Paciente = item.paciente,
                    DataPrazo = item.cache_proximo_prazo,
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }

        // Lógica de Prazos (Baseado em source: 53-54)
        private (string, SolidColorBrush) CalcularStatus(string? dataStr)
        {
            if (string.IsNullOrEmpty(dataStr)) return ("Sem Prazo", Brushes.Gray);

            if (DateTime.TryParseExact(dataStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime prazo))
            {
                var dias = (prazo.Date - DateTime.Now.Date).TotalDays;
                if (dias < 0) return ("ATRASADO", Brushes.Red);
                if (dias == 0) return ("VENCE HOJE", Brushes.Orange);
                if (dias <= 7) return ($"Vence em {dias} dias", Brushes.Goldenrod);
                return ("No Prazo", Brushes.Green);
            }
            return ("Data Inválida", Brushes.Gray);
        }
    }
}
