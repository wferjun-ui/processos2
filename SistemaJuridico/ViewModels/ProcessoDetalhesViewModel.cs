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

        // Inicializamos com 'new()' para evitar nulos
        [ObservableProperty] private ProcessoModel _processo = new();
        
        // Inicializamos textos com string.Empty ("")
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCorBrush = Brushes.Gray;
        
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";
        
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
            
            // Tenta buscar o processo
            var pDto = conn.QueryFirstOrDefault<dynamic>("SELECT * FROM processos WHERE id = @id", new { id = _processoId });

            // Proteção contra nulos (CS8602)
            if (pDto != null)
            {
                // Conversão segura lidando com possíveis nulos do banco
                string dataPrazo = pDto.cache_proximo_prazo ?? "";

                Processo = new ProcessoModel 
                { 
                    Id = pDto.id ?? "", 
                    Numero = pDto.numero ?? "", 
                    Paciente = pDto.paciente ?? "", 
                    DataPrazo = dataPrazo
                };
                
                // Conversão segura para string
                ObsFixa = (string?)pDto.observacao_fixa ?? "";

                // Calcula Status
                var (txt, cor) = CalcularStatus(Processo.DataPrazo);
                StatusTexto = txt;
                StatusCorBrush = cor;
            }
            else
            {
                // Se não achar o processo, define padrão para evitar crash
                Processo = new ProcessoModel { Paciente = "Processo não encontrado" };
                StatusTexto = "ERRO";
                StatusCorBrush = Brushes.Red;
            }

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
                var usuario = Application.Current.Properties["Usuario"]?.ToString() ?? "Sistema";
                
                // +14 dias
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
                
                // Limpa campos de diligência após salvar
                DiligenciaRealizada = false;
                DiligenciaPendente = false;
                DiligenciaDesc = "";
                PendenciaDesc = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro: " + ex.Message);
            }
        }

        [RelayCommand]
        public void Voltar()
        {
            // Fecha a janela atual
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        private (string, SolidColorBrush) CalcularStatus(string? dataStr)
        {
             if (DateTime.TryParse(dataStr, out DateTime d))
             {
                 var dias = (d - DateTime.Now.Date).TotalDays;
                 if (dias < 0) return ("ATRASADO", Brushes.Red);
                 if (dias == 0) return ("VENCE HOJE", Brushes.OrangeRed);
                 if (dias <= 7) return ($"Vence em {dias:F0} dias", Brushes.Orange);
                 return ("No Prazo", Brushes.Green);
             }
             return ("--", Brushes.Gray);
        }
    }
}
