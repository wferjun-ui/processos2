using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;

        [ObservableProperty] private ProcessoModel _processo;
        [ObservableProperty] private string _statusTexto;
        [ObservableProperty] private SolidColorBrush _statusCorBrush;
        
        // Campos de Verificação
        [ObservableProperty] private string _obsFixa;
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc;
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc;
        
        // Listas
        public ObservableCollection<dynamic> Historico { get; set; } = new();

        public ProcessoDetalhesViewModel(string processoId)
        {
            _db = new DatabaseService();
            _processoId = processoId;
            CarregarDados();
        }

        private void CarregarDados()
        {
            using var conn = _db.GetConnection();
            
            // Carrega Processo
            var pDto = conn.QueryFirstOrDefault<dynamic>("SELECT * FROM processos WHERE id = @id", new { id = _processoId });
            Processo = new ProcessoModel 
            { 
                Id = pDto.id, 
                Numero = pDto.numero, 
                Paciente = pDto.paciente, 
                DataPrazo = pDto.cache_proximo_prazo 
            };
            ObsFixa = pDto.observacao_fixa;

            // Calcula Status
            var (txt, cor) = CalcularStatus(Processo.DataPrazo); // Reutilizar lógica do MainViewModel ou criar helper estático
            StatusTexto = txt;
            StatusCorBrush = cor;

            // Carrega Histórico
            Historico.Clear();
            var hist = conn.Query("SELECT * FROM verificacoes WHERE processo_id = @id ORDER BY data_hora DESC", new { id = _processoId });
            foreach (var h in hist) Historico.Add(h);
        }
        
        [RelayCommand]
        public void SalvarVerificacao()
        {
            try
            {
                using var conn = _db.GetConnection();
                var usuario = Application.Current.Properties["UsuarioLogado"]?.ToString() ?? "Sistema";
                
                // Lógica simplificada de atualização de prazo (+14 dias)
                var novoPrazo = DateTime.Now.AddDays(14).ToString("dd/MM/yyyy");

                var sql = @"INSERT INTO verificacoes (id, processo_id, data_hora, responsavel, diligencia_realizada, diligencia_descricao, proximo_prazo_padrao, alteracoes_texto)
                            VALUES (@id, @pid, @dh, @resp, @dr, @dd, @prz, 'Nova Verificação')";
                
                conn.Execute(sql, new { 
                    id = Guid.NewGuid().ToString(), 
                    pid = _processoId, 
                    dh = DateTime.Now.ToString("s"), 
                    resp = usuario,
                    dr = DiligenciaRealizada ? 1 : 0,
                    dd = DiligenciaDesc,
                    prz = novoPrazo
                });

                // Atualiza Cache no Processo
                conn.Execute("UPDATE processos SET cache_proximo_prazo = @p, observacao_fixa = @o WHERE id = @id", 
                    new { p = novoPrazo, o = ObsFixa, id = _processoId });

                MessageBox.Show("Salvo com sucesso!");
                CarregarDados();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro: " + ex.Message);
            }
        }

        [RelayCommand]
        public void Voltar()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.ProcessoDetalhesWindow)?.Close();
        }

        // Helper rápido de status (idealmente ficaria numa classe estática Shared)
        private (string, SolidColorBrush) CalcularStatus(string? dataStr)
        {
             if (DateTime.TryParse(dataStr, out DateTime d))
             {
                 var dias = (d - DateTime.Now).TotalDays;
                 if (dias < 0) return ("ATRASADO", Brushes.Red);
                 return ("No Prazo", Brushes.Green);
             }
             return ("Indefinido", Brushes.Gray);
        }
    }
}
