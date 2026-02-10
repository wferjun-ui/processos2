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
    public class ProcessoModel {
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

        public MainViewModel()
        {
            _db = new DatabaseService();
            CarregarDashboard();
        }

        [RelayCommand]
        public void NovoProcesso()
        {
            var win = new Views.CadastroWindow();
            if (Application.Current.MainWindow != null) 
                win.Owner = Application.Current.MainWindow;
            
            win.ShowDialog();
            CarregarDashboard(); // Atualiza a lista ao voltar
        }

        [RelayCommand]
        public void Sair()
        {
            var login = new Views.LoginWindow();
            login.Show();
            Application.Current.MainWindow.Close();
        }

        [RelayCommand]
        public void CarregarDashboard()
        {
            Processos.Clear();
            using var conn = _db.GetConnection();
            
            var sql = "SELECT id, numero, paciente, cache_proximo_prazo FROM processos";
            if (!string.IsNullOrEmpty(SearchText)) sql += " WHERE numero LIKE @q OR paciente LIKE @q";
            
            var dados = conn.Query<dynamic>(sql, new { q = $"%{SearchText}%" });

            foreach (var item in dados)
            {
                string prazo = item.cache_proximo_prazo;
                var (txt, cor) = CalcularStatus(prazo);
                Processos.Add(new ProcessoModel {
                    Id = item.id, Numero = item.numero, Paciente = item.paciente,
                    DataPrazo = prazo ?? "--", StatusTexto = txt, StatusCor = cor
                });
            }
        }

        private (string, SolidColorBrush) CalcularStatus(string? dt)
        {
            if (DateTime.TryParse(dt, out DateTime d)) {
                var dias = (d - DateTime.Now).TotalDays;
                if (dias < 0) return ("ATRASADO", Brushes.Red);
                if (dias <= 7) return ($"Vence em {dias:F0} dias", Brushes.Orange);
                return ("No Prazo", Brushes.Green);
            }
            return ("--", Brushes.Gray);
        }
    }
}
