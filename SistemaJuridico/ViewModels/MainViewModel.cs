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
    // Modelo para exibição na Tabela
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
        
        // Controla se o botão roxo de "ADMINISTRAÇÃO" aparece ou não
        [ObservableProperty] private Visibility _adminVisibility = Visibility.Collapsed;

        public MainViewModel()
        {
            // O operador ! garante que o DB existe (foi criado no App.xaml.cs)
            _db = App.DB!;
            
            VerificarPermissoes();
            CarregarDashboard();
        }

        private void VerificarPermissoes()
        {
            // CORREÇÃO DO WARNING CS8605:
            // Verificamos de forma segura se a propriedade existe e se é verdadeira
            if (Application.Current.Properties.Contains("IsAdmin") && 
                Application.Current.Properties["IsAdmin"] is bool isAdmin && 
                isAdmin)
            {
                AdminVisibility = Visibility.Visible;
            }
            else
            {
                AdminVisibility = Visibility.Collapsed;
            }
        }

        // Classe auxiliar interna para ler do banco (DTO)
        private class ProcessoDto
        {
            public string? id { get; set; }
            public string? numero { get; set; }
            public string? paciente { get; set; }
            public string? cache_proximo_prazo { get; set; }
        }

        // --- COMANDOS ---

        [RelayCommand]
        public void NovoProcesso()
        {
            var janela = new Views.CadastroWindow();
            if (Application.Current.MainWindow != null) 
                janela.Owner = Application.Current.MainWindow;
            
            janela.ShowDialog();
            CarregarDashboard(); // Atualiza a lista ao fechar o cadastro
        }
        
        [RelayCommand]
        public void AbrirAdmin()
        {
            var janela = new Views.AdminWindow();
            if (Application.Current.MainWindow != null) 
                janela.Owner = Application.Current.MainWindow;
            
            janela.ShowDialog();
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
            
            // Recarrega o dashboard ao voltar, pois o prazo pode ter mudado
            CarregarDashboard();
        }

        [RelayCommand]
        public void Sair()
        {
            var login = new Views.LoginWindow();
            login.Show();
            
            // Fecha a janela principal atual
            Application.Current.MainWindow?.Close();
        }

        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();
            
            var sql = "SELECT id, numero, paciente, cache_proximo_prazo FROM processos";
            
            // Filtro de Busca
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

        // Lógica de Cores e Prazos (Igual ao Python)
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
