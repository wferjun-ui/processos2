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
    // Modelo usado para exibição na DataGrid (UI)
    public class ProcessoModel
    {
        public string Id { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Paciente { get; set; } = string.Empty;
        public string DataPrazo { get; set; } = string.Empty;
        
        // Propriedades Visuais (Cores e Textos de Status)
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
            _db = new DatabaseService();
            CarregarDashboard();
        }

        // Classe auxiliar para mapear o retorno do SQLite (DTO)
        // Evita erros de tipagem com o Dapper e permite valores nulos do banco
        private class ProcessoDto
        {
            public string id { get; set; } = string.Empty;
            public string numero { get; set; } = string.Empty;
            public string paciente { get; set; } = string.Empty;
            public string? cache_proximo_prazo { get; set; }
        }

        // --- COMANDO: ABRIR JANELA DE CADASTRO ---
        [RelayCommand]
        public void NovoProcesso()
        {
            // Cria a janela de cadastro
            // Certifique-se de ter criado o arquivo CadastroWindow.xaml conforme instruções anteriores
            var janela = new CadastroWindow();
            
            // Define a janela principal como "dona" para centralizar corretamente
            if (Application.Current.MainWindow != null)
            {
                janela.Owner = Application.Current.MainWindow;
            }
            
            // Abre como Modal (bloqueia a janela de fundo até fechar)
            bool? resultado = janela.ShowDialog();

            // Ao fechar a janela, recarrega a lista para mostrar o novo processo
            CarregarDashboard();
        }

        // --- COMANDO: CARREGAR DASHBOARD ---
        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();

            // SQL Base
            var sql = "SELECT id, numero, paciente, cache_proximo_prazo FROM processos";
            
            // Filtro de Busca (Lógica portada do Python source: 181)
            if (!string.IsNullOrEmpty(SearchText))
            {
                sql += " WHERE numero LIKE @q OR paciente LIKE @q";
            }
            
            // Ordenação por prazo (opcional, mas recomendada para UX)
            sql += " ORDER BY cache_proximo_prazo ASC";

            // Executa a query mapeando para o DTO
            var dados = conn.Query<ProcessoDto>(sql, new { q = $"%{SearchText}%" });

            foreach (var item in dados)
            {
                var (texto, cor) = CalcularStatus(item.cache_proximo_prazo);

                Processos.Add(new ProcessoModel
                {
                    Id = item.id,
                    Numero = item.numero,
                    Paciente = item.paciente,
                    // Garante que não passamos null para a View
                    DataPrazo = item.cache_proximo_prazo ?? "Sem Data",
                    StatusTexto = texto,
                    StatusCor = cor
                });
            }
        }

        // --- LÓGICA DE NEGÓCIO: CÁLCULO DE PRAZOS ---
        // Portado de ProcessLogic.check_prazo_status (source: 53-54)
        private (string, SolidColorBrush) CalcularStatus(string? dataStr)
        {
            if (string.IsNullOrEmpty(dataStr)) return ("Sem Prazo", Brushes.Gray);

            // Tenta converter a string de data (dd/MM/yyyy) para DateTime
            if (DateTime.TryParseExact(dataStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime prazo))
            {
                var dias = (prazo.Date - DateTime.Now.Date).TotalDays;
                
                if (dias < 0) return ("ATRASADO", Brushes.Red);
                if (dias == 0) return ("VENCE HOJE", Brushes.OrangeRed);
                if (dias <= 7) return ($"Vence em {dias} dias", Brushes.Goldenrod); // Amarelo escuro para melhor leitura
                return ("No Prazo", Brushes.Green);
            }
            
            return ("Data Inválida", Brushes.Gray);
        }
    }
}
