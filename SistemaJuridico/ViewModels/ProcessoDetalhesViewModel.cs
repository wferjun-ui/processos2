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

        // Propriedades devidamente inicializadas
        [ObservableProperty] private ProcessoModel _processo = new();
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
            _db = App.DB!;
            _processoId = processoId;
            CarregarDados();
        }

        private void CarregarDados()
        {
            using var conn = _db.GetConnection();
            
            // DTO dinâmico para leitura segura
            var pDto = conn.QueryFirstOrDefault<dynamic>("SELECT * FROM processos WHERE id = @id", new { id = _processoId });

            if (pDto != null)
            {
                // Conversão segura de nulos
                string dataPrazo = pDto.cache_proximo_prazo ?? "";
                
                Processo = new ProcessoModel 
                { 
                    Id = pDto.id ?? "", 
                    Numero = pDto.numero ?? "", 
                    Paciente = pDto.paciente ?? "", 
                    DataPrazo = dataPrazo 
                };
                
                ObsFixa = pDto.observacao_fixa ?? "";

                var (txt, cor) = CalcularStatus(Processo.DataPrazo);
                StatusTexto = txt;
                StatusCorBrush = cor;
            }
            else
            {
                Processo = new ProcessoModel { Paciente = "Não encontrado" };
            }

            // Histórico
            Historico.Clear();
            var hist = conn.Query("SELECT * FROM verificacoes WHERE processo_id = @id ORDER BY data_hora DESC", new { id = _processoId });
            foreach (var h in hist)
            {
                Historico.Add(h);
            }
        }
        
        [RelayCommand]
        public void SalvarVerificacao()
        {
            try
            {
                using var conn = _db.GetConnection();
                var usuario = Application.Current.Properties["Usuario"]?.ToString() ?? "Sistema";
                
                // Regra de prazo (+14 dias)
                var dataBase = DateTime.Now.AddDays(14);
                // Ajuste fim de semana
                if (dataBase.DayOfWeek == DayOfWeek.Saturday) dataBase = dataBase.AddDays(2);
                if (dataBase.DayOfWeek == DayOfWeek.Sunday) dataBase = dataBase.AddDays(1);
                
                var novoPrazo = dataBase.ToString("dd/MM/yyyy");

                var sql = @"INSERT INTO verificacoes (id, processo_id, data_hora, responsavel, diligencia_realizada, diligencia_descricao, proximo_prazo_padrao, alteracoes_texto)
                            VALUES (@id, @pid, @dh, @resp, @dr, @dd, @prz, 'Nova Verificação')";
                
                conn.Execute(sql, new { 
                    id = Guid.NewGuid().ToString(), 
                    pid = _processoId, 
                    dh = DateTime.Now.ToString("s"), 
                    resp = usuario,
                    dr = DiligenciaRealizada ? 1 : 0,
                    dd = DiligenciaDesc ?? "",
                    prz = novoPrazo
                });

                // Atualiza o processo principal
                conn.Execute("UPDATE processos SET cache_proximo_prazo = @p, observacao_fixa = @o WHERE id = @id", 
                    new { p = novoPrazo, o = ObsFixa, id = _processoId });

                MessageBox.Show("Verificação salva e prazo atualizado!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Limpa campos e recarrega
                DiligenciaRealizada = false;
                DiligenciaPendente = false;
                DiligenciaDesc = "";
                PendenciaDesc = "";
                
                CarregarDados();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Voltar()
        {
            // Encontra a janela que está usando este ViewModel e fecha
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        private (string, SolidColorBrush) CalcularStatus(string dataStr)
        {
             if (DateTime.TryParse(dataStr, out DateTime d))
             {
                 var dias = (d.Date - DateTime.Now.Date).TotalDays;
                 if (dias < 0) return ("ATRASADO", Brushes.Red);
                 if (dias == 0) return ("VENCE HOJE", Brushes.OrangeRed);
                 if (dias <= 7) return ($"Vence em {dias} dias", Brushes.Goldenrod);
                 return ("No Prazo", Brushes.Green);
             }
             return ("--", Brushes.Gray);
        }
    }
}
