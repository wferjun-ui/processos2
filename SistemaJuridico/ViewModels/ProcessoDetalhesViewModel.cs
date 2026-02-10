using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // Modelo para Itens de Saúde (igual ao Python)
    public partial class ItemSaudeDto : ObservableObject {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        [ObservableProperty] private string _qtd = "";
        [ObservableProperty] private string _local = "";
        [ObservableProperty] private string _dataPrescricao = "";
        [ObservableProperty] private bool _isDesnecessario;
        [ObservableProperty] private bool _temBloqueio; // Python [cite: 329]
        [ObservableProperty] private string _frequencia = "";
    }

    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;
        private string _userLogado;

        // Dados Principais
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCor = Brushes.Gray;
        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

        // Aba Verificação
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private string _faseProcessual = "";
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";
        [ObservableProperty] private string _responsavel = "";
        [ObservableProperty] private string _proxData = "";
        
        public ObservableCollection<ItemSaudeDto> ItensSaude { get; set; } = new();
        public ObservableCollection<string> Fases { get; } = new() { "Conhecimento", "Cumprimento de Sentença", "Recurso", "Arquivado", "Suspenso" };

        // Aba Financeiro
        [ObservableProperty] private string _finTipo = "Alvará";
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finNf = "";
        [ObservableProperty] private string _finMov = "";
        [ObservableProperty] private string _finValor = "";
        [ObservableProperty] private bool _isAnexo = false;
        
        [cite_start]// Campos extras Financeiro (Box Extra do Python [cite: 345])
        [ObservableProperty] private Visibility _extrasVisivel = Visibility.Collapsed;
        [ObservableProperty] private string _finItem = "";
        [ObservableProperty] private string _finQtd = "";
        [ObservableProperty] private string _finMes = "";
        [ObservableProperty] private string _finAno = "";
        [ObservableProperty] private string _finObs = "";

        public ObservableCollection<dynamic> Rascunhos { get; set; } = new();
        public ObservableCollection<dynamic> HistoricoFin { get; set; } = new();
        public ObservableCollection<dynamic> HistoricoLogs { get; set; } = new();

        public ProcessoDetalhesViewModel(string id)
        {
            _db = App.DB!;
            _processoId = id;
            _userLogado = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";
            Responsavel = _userLogado;

            var (dt, _) = ProcessLogic.CalculateDueDates(DateTime.Now.ToString("dd/MM/yyyy"));
            ProxData = dt;

            CarregarTudo();
        }

        partial void OnFinTipoChanged(string value) => ExtrasVisivel = value == "Alvará" ? Visibility.Collapsed : Visibility.Visible;

        private void CarregarTudo()
        {
            using var conn = _db.GetConnection();
            
            // 1. Dados do Processo
            var p = conn.QueryFirstOrDefault("SELECT * FROM processos WHERE id = @id", new { id = _processoId });
            if (p != null)
            {
                Numero = p.numero; Paciente = p.paciente; Juiz = p.juiz;
                ObsFixa = p.observacao_fixa ?? "";
                FaseProcessual = p.status_fase ?? "Conhecimento";
                var (txt, cor) = ProcessLogic.CheckPrazoStatus(p.cache_proximo_prazo);
                StatusTexto = txt; StatusCor = cor;
            }

            // 2. Itens de Saúde
            ItensSaude.Clear();
            var itens = conn.Query<ItemSaudeDto>("SELECT id, tipo, nome, qtd, frequencia, local, data_prescricao as DataPrescricao, is_desnecessario as IsDesnecessario, tem_bloqueio as TemBloqueio FROM itens_saude WHERE processo_id=@id", new { id = _processoId });
            foreach (var i in itens) ItensSaude.Add(i);

            [cite_start]// 3. Financeiro (Lógica Python: Separar Rascunho de Lançado [cite: 388])
            Rascunhos.Clear(); HistoricoFin.Clear();
            var contas = conn.Query("SELECT * FROM contas WHERE processo_id=@id ORDER BY data_movimentacao", new { id = _processoId });
            decimal saldo = 0;

            foreach (var c in contas)
            {
                decimal valAlv = (decimal)(c.valor_alvara ?? 0.0);
                decimal valCon = (decimal)(c.valor_conta ?? 0.0);
                
                if (c.status_conta == "rascunho")
                {
                    Rascunhos.Add(new { Id=c.id, Data=c.data_movimentacao, Hist=c.historico, Valor=(valAlv > 0 ? valAlv : valCon).ToString("C2") });
                }
                else
                {
                    saldo += (valAlv - valCon);
                    HistoricoFin.Add(new { 
                        Id=c.id, Data=c.data_movimentacao, Hist=c.historico, 
                        Cred=valAlv > 0 ? valAlv.ToString("C2") : "", 
                        Deb=valCon > 0 ? valCon.ToString("C2") : "",
                        Saldo=saldo.ToString("C2"),
                        Cor=valAlv > 0 ? Brushes.Green : Brushes.Red
                    });
                }
            }
            SaldoDisplay = saldo.ToString("C2");
            SaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;
            
            // 4. Logs
            HistoricoLogs.Clear();
            var logs = conn.Query("SELECT * FROM verificacoes WHERE processo_id=@id ORDER BY data_hora DESC", new { id = _processoId });
            foreach(var l in logs) HistoricoLogs.Add(l);
        }

        [RelayCommand]
        public void SalvarVerificacao()
        {
            if (DiligenciaPendente && string.IsNullOrWhiteSpace(PendenciaDesc)) {
                MessageBox.Show("Descreva a pendência obrigatória."); return;
            }

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();

                [cite_start]// Salva Itens de Saúde Atualizados 
                foreach (var item in ItensSaude) {
                    conn.Execute(@"UPDATE itens_saude SET qtd=@Qtd, local=@Local, data_prescricao=@DataPrescricao, 
                                   is_desnecessario=@IsDesnecessario, tem_bloqueio=@TemBloqueio WHERE id=@Id", 
                                   item, trans);
                }

                // Calcula prazos
                var (prz, notif) = ProcessLogic.CalculateDueDates(ProxData); // Usa a data digitada ou hoje

                // Atualiza Processo (Cache)
                conn.Execute("UPDATE processos SET status_fase=@f, observacao_fixa=@o, cache_proximo_prazo=@p, ultima_atualizacao=@u WHERE id=@id",
                    new { f = FaseProcessual, o = ObsFixa, p = prz, u = DateTime.Now.ToString("dd/MM/yyyy"), id = _processoId }, trans);

                // Cria Log
                string resumo = DiligenciaRealizada ? $"[Dil] {DiligenciaDesc}" : "Verificação";
                string jsonSnap = JsonSerializer.Serialize(ItensSaude);
                
                conn.Execute(@"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, 
                             diligencia_realizada, diligencia_descricao, diligencia_pendente, pendencias_descricao,
                             proximo_prazo_padrao, data_notificacao, alteracoes_texto, itens_snapshot_json)
                             VALUES (@id, @pid, @dh, @st, @resp, @dr, @dd, @dp, @pd, @pp, @dn, @at, @js)",
                             new {
                                 id = Guid.NewGuid().ToString(), pid = _processoId, dh = DateTime.Now.ToString("s"),
                                 st = FaseProcessual, resp = Responsavel,
                                 dr = DiligenciaRealizada?1:0, dd = DiligenciaDesc, dp = DiligenciaPendente?1:0, pd = PendenciaDesc,
                                 pp = prz, dn = notif, at = resumo, js = jsonSnap
                             }, trans);

                trans.Commit();
                _db.PerformBackup();
                MessageBox.Show("Verificação Salva!");
                CarregarTudo();
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        [RelayCommand]
        public void AdicionarRascunho()
        {
            decimal val = ProcessLogic.ParseMoney(FinValor);
            if (val <= 0) return;

            string hist = FinTipo == "Alvará" ? $"Alvará - {FinNf}" : $"{FinTipo}: {FinItem} ({FinQtd}) Ref: {FinMes}/{FinAno} - {FinObs}";
            string mov = IsAnexo ? "Anexo" : FinMov;
            
            decimal vAlv = FinTipo == "Alvará" ? val : 0;
            decimal vCon = FinTipo != "Alvará" ? val : 0;

            using var conn = _db.GetConnection();
            conn.Execute(@"INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, 
                           num_nf_alvara, valor_alvara, valor_conta, mov_processo, status_conta, responsavel)
                           VALUES (@id, @pid, @dt, @tp, @hist, @nf, @va, @vc, @mov, 'rascunho', @resp)",
                           new {
                               id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tp = FinTipo, hist,
                               nf = FinNf, va = vAlv, vc = vCon, mov, resp = _userLogado
                           });
            
            FinValor = ""; FinItem = ""; FinObs = "";
            CarregarTudo();
        }

        [RelayCommand]
        public void LancarTudo()
        {
            if (MessageBox.Show("Confirmar lançamento definitivo?", "Financeiro", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("UPDATE contas SET status_conta='lancado' WHERE processo_id=@id AND status_conta='rascunho'", new { id = _processoId });
                _db.PerformBackup();
                CarregarTudo();
            }
        }
        
        [RelayCommand]
        public void ExcluirConta(string id) {
            using var conn = _db.GetConnection();
            conn.Execute("DELETE FROM contas WHERE id=@id", new { id });
            CarregarTudo();
        }
    }
}
