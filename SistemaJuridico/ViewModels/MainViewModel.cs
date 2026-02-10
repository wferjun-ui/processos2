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

        // --- CORREÇÃO AQUI ---
        // 1. Inicializamos com string.Empty para evitar avisos de não-nulo.
        // 2. cache_proximo_prazo virou string? porque no banco pode ser NULL.
        private class ProcessoDto
        {
            public string id { get; set; } = string.Empty;
            public string numero { get; set; } = string.Empty;
            public string paciente { get; set; } = string.Empty;
            public string? cache_proximo_prazo { get; set; }
        }
        // ---------------------

        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();
            
            // Dapper preencherá as propriedades automaticamente
            var dados = conn.Query<ProcessoDto>("SELECT id, numero, paciente, cache_proximo_prazo FROM processos");

            foreach (var item in dados)
            {
                var (texto, cor) = CalcularStatus(item.cache_proximo_prazo);

                Processos.Add(new ProcessoModel
                {
                    Id = item.id,
                    Numero = item.numero,
                    Paciente = item.paciente,
                    // Garante que não passamos null para a View, usando string vazia se for nulo
                    DataPrazo = item.cache_proximo_prazo ?? "", 
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }

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
