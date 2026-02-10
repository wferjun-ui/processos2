using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using Microsoft.Win32;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // DTO para representar o item de saúde na tela
    public partial class ItemSaudeDto : ObservableObject {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        [ObservableProperty] private string _qtd = "";
        [ObservableProperty] private string _local = "";
        [ObservableProperty] private string _dataPrescricao = "";
        [ObservableProperty] private bool _isDesnecessario;
        [ObservableProperty] private bool _temBloqueio;
        [ObservableProperty] private string _frequencia = "";
    }

    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;
        private string _userLogado;

        // DADOS DO CABEÇALHO
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "";
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCor = Brushes.Gray;
        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

        // ABA 1: VERIFICAÇÃO
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private string _faseProcessual = "";
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";
        [ObservableProperty] private string _responsavel = "";
        [ObservableProperty] private string _proxData = "";
        [ObservableProperty] private Visibility _saudeVisibility = Visibility.Collapsed;
        
        public ObservableCollection<ItemSaudeDto> ItensSaude { get; set; } = new();
        public ObservableCollection<string> Fases { get; } = new() { "Conhecimento", "Cumprimento de Sentença", "Recurso", "Arquivado", "Suspenso em recurso", "Julgado e aguardando trânsito", "Cumprimento provisório", "Agravo de instrumento" };

        // ABA 2: HISTÓRICO
        public ObservableCollection<dynamic> HistoricoLogs { get; set; } = new();

        // ABA 3: FINANCEIRO (CONTAS)
        [ObservableProperty] private string _finTipo = "Alvará";
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finNf = "";
        [ObservableProperty] private string _finMov = "";
        [ObservableProperty] private string _finValor = "";
        [ObservableProperty] private bool _isAnexo = false;
        
        // Box Extra (Campos do Python)
        [ObservableProperty] private Visibility _extrasVisivel = Visibility.Collapsed;
        [ObservableProperty] private ObservableCollection<string> _listaItensCombo = new();
        [ObservableProperty] private string _finItemCombo = "";
        [ObservableProperty] private string _finItemTexto = "";
        [ObservableProperty] private Visibility _comboVisivel = Visibility.Visible;
        [ObservableProperty] private Visibility _textoVisivel = Visibility.Collapsed;
        [ObservableProperty] private string _finQtd = "";
        [ObservableProperty] private string _finMes = "";
        [ObservableProperty] private string _finAno = "";
        [ObservableProperty] private string _finObs = "";

        public ObservableCollection<dynamic> Rascunhos { get; set; } = new();
        public ObservableCollection<dynamic> HistoricoFin { get; set; } = new();

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
        
        partial void OnFinItemComboChanged(string value) {
            if (value == "Outro...") { ComboVisivel = Visibility.Collapsed; TextoVisivel = Visibility.Visible; }
            else { ComboVisivel = Visibility.Visible; TextoVisivel = Visibility.Collapsed; }
        }

        private void CarregarTudo()
        {
            using var conn = _db.GetConnection();
            
            // 1. Processo e Saldos
            var p = conn.QueryFirstOrDefault("SELECT * FROM processos WHERE id = @id", new { id = _processoId });
            if (p != null) {
                Numero = p.numero; Paciente = p.paciente; Juiz = p.juiz; Classificacao = p.classificacao;
                ObsFixa = p.observacao_fixa ?? ""; FaseProcessual = p.status_fase ?? "Conhecimento";
                var (txt, cor) = ProcessLogic.CheckPrazoStatus(p.cache_proximo_prazo);
                StatusTexto = txt; StatusCor = cor;
                SaudeVisibility = Classificacao == "Saúde" ? Visibility.Visible : Visibility.Collapsed;
            }

            // 2. Itens Saúde
            ItensSaude.Clear(); ListaItensCombo.Clear();
            var itens = conn.Query<ItemSaudeDto>("SELECT * FROM itens_saude WHERE processo_id=@id", new { id = _processoId });
            foreach (var i in itens) { ItensSaude.Add(i); ListaItensCombo.Add(i.Nome); }
            ListaItensCombo.Add("Outro...");

            // 3. Financeiro
            Rascunhos.Clear(); HistoricoFin.Clear();
            var contas = conn.Query("SELECT * FROM contas WHERE processo_id=@id ORDER BY data_movimentacao", new { id = _processoId });
            decimal saldo = 0;

            foreach (var c in contas) {
                decimal valAlv = (decimal)(c.valor_alvara ?? 0.0);
                decimal valCon = (decimal)(c.valor_conta ?? 0.0);
                
                if (c.status_conta == "rascunho") {
                    Rascunhos.Add(new { Id=c.id, Data=c.data_movimentacao, Hist=c.historico, Valor=(valAlv > 0 ? valAlv : valCon).ToString("C2") });
                } else {
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
            foreach(var l in logs) {
                string det = "";
                if (l.diligencia_realizada == 1) det += $"[Dil] {l.diligencia_descricao} ";
                if (!string.IsNullOrEmpty(l.alteracoes_texto) && !l.alteracoes_texto.Contains("Nenhuma")) det += $"[Mod] {l.alteracoes_texto}";
                HistoricoLogs.Add(new { Data=DateTime.Parse(l.data_hora).ToString("dd/MM/yyyy HH:mm"), Resp=l.responsavel, Status=l.status_processo, Detalhes=det });
            }
        }

        [RelayCommand]
        public void SalvarVerificacao()
        {
            if (DiligenciaPendente && string.IsNullOrWhiteSpace(PendenciaDesc)) { MessageBox.Show("Descreva a pendência."); return; }
            if (string.IsNullOrWhiteSpace(Responsavel)) { MessageBox.Show("Informe o responsável."); return; }

            try {
                using var conn = _db.GetConnection(); conn.Open(); using var trans = conn.BeginTransaction();
                
                // Atualiza Itens de Saúde
                foreach (var item in ItensSaude)
                    conn.Execute("UPDATE itens_saude SET qtd=@Qtd, local=@Local, data_prescricao=@DataPrescricao, is_desnecessario=@IsDesnecessario, tem_bloqueio=@TemBloqueio WHERE id=@Id", item, trans);

                var (prz, notif) = ProcessLogic.CalculateDueDates(ProxData);
                string resumo = DiligenciaRealizada ? $"[Dil] {DiligenciaDesc}" : "Verificação de Rotina";
                
                // Atualiza Processo e Log
                conn.Execute("UPDATE processos SET status_fase=@f, observacao_fixa=@o, cache_proximo_prazo=@p, ultima_atualizacao=@u WHERE id=@id",
                    new { f = FaseProcessual, o = ObsFixa, p = prz, u = DateTime.Now.ToString("dd/MM/yyyy"), id = _processoId }, trans);

                conn.Execute(@"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, diligencia_realizada, diligencia_descricao, diligencia_pendente, pendencias_descricao, proximo_prazo_padrao, data_notificacao, alteracoes_texto, itens_snapshot_json)
                             VALUES (@id, @pid, @dh, @st, @resp, @dr, @dd, @dp, @pd, @pp, @dn, @at, @js)",
                             new { id = Guid.NewGuid().ToString(), pid = _processoId, dh = DateTime.Now.ToString("s"), st = FaseProcessual, resp = Responsavel, dr = DiligenciaRealizada?1:0, dd = DiligenciaDesc, dp = DiligenciaPendente?1:0, pd = PendenciaDesc, pp = prz, dn = notif, at = resumo, js = JsonSerializer.Serialize(ItensSaude) }, trans);

                trans.Commit();
                _db.PerformBackup();
                MessageBox.Show("Verificação Salva!");
                CarregarTudo();
            } catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        [RelayCommand]
        public void AdicionarRascunho()
        {
            decimal val = ProcessLogic.ParseMoney(FinValor);
            if (val <= 0) { MessageBox.Show("Valor inválido."); return; }

            string nomeItem = (FinItemCombo == "Outro..." || string.IsNullOrEmpty(FinItemCombo)) ? FinItemTexto : FinItemCombo;
            if (string.IsNullOrEmpty(nomeItem)) nomeItem = "Despesa Diversa";

            string hist = FinTipo == "Alvará" ? $"Alvará - {FinNf}" : $"{FinTipo}: {nomeItem}";
            if (!string.IsNullOrEmpty(FinQtd)) hist += $" ({FinQtd})";
            if (!string.IsNullOrEmpty(FinMes)) hist += $" Ref: {FinMes}/{FinAno}";
            if (!string.IsNullOrEmpty(FinObs)) hist += $" - {FinObs}";

            string mov = IsAnexo ? "Anexo" : FinMov;
            decimal vAlv = FinTipo == "Alvará" ? val : 0;
            decimal vCon = FinTipo != "Alvará" ? val : 0;

            using var conn = _db.GetConnection();
            conn.Execute(@"INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, num_nf_alvara, valor_alvara, valor_conta, mov_processo, status_conta, responsavel, terapia_medicamento_nome, quantidade, mes_referencia, ano_referencia, observacoes)
                           VALUES (@id, @pid, @dt, @tp, @hist, @nf, @va, @vc, @mov, 'rascunho', @resp, @nm, @qt, @mr, @ar, @ob)",
                           new { id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tp = FinTipo, hist, nf = FinNf, va = vAlv, vc = vCon, mov, resp = _userLogado, nm = nomeItem, qt = FinQtd, mr = FinMes, ar = FinAno, ob = FinObs });
            
            FinValor = ""; FinItemTexto = ""; FinObs = "";
            CarregarTudo();
        }

        [RelayCommand] public void LancarTudo() {
            if (MessageBox.Show("Lançar todos os rascunhos?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                using var conn = _db.GetConnection();
                conn.Execute("UPDATE contas SET status_conta='lancado' WHERE processo_id=@id AND status_conta='rascunho'", new { id = _processoId });
                _db.PerformBackup();
                CarregarTudo();
            }
        }

        [RelayCommand] public void ExcluirConta(string id) {
            if (MessageBox.Show("Excluir item?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM contas WHERE id=@id", new { id });
                CarregarTudo();
            }
        }

        [RelayCommand] public void GerarPdf() {
            var sfd = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Prestacao_{Numero}_{DateTime.Now:yyyyMMdd}.pdf" };
            if (sfd.ShowDialog() == true) {
                using var conn = _db.GetConnection();
                var contas = conn.Query("SELECT * FROM contas WHERE processo_id=@id AND status_conta='lancado' ORDER BY data_movimentacao", new { id = _processoId });
                ProcessLogic.GeneratePdfReport(Numero, Paciente, contas, sfd.FileName);
                MessageBox.Show("PDF Gerado!");
            }
        }
    }
}
