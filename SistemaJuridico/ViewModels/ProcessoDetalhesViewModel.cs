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
    public class ContaGridDto 
    {
        public string Id { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string Hist { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
        public string Cred { get; set; } = string.Empty;
        public string Deb { get; set; } = string.Empty;
        public string Saldo { get; set; } = string.Empty;
        public SolidColorBrush Cor { get; set; } = Brushes.Black;
        public bool IsRascunho { get; set; }
        public dynamic Raw { get; set; } = default!; 
    }

    public partial class ItemSaudeDto : ObservableObject 
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
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

        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "";
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCor = Brushes.Gray;
        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

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
        public ObservableCollection<string> Fases { get; } = new() { 
            "Conhecimento", "Cumprimento de Sentença", "Recurso", "Arquivado", 
            "Suspenso em recurso", "Julgado e aguardando trânsito", 
            "Cumprimento provisório", "Agravo de instrumento" 
        };

        [ObservableProperty] private string _finTipo = "Alvará";
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finNf = "";
        [ObservableProperty] private string _finMov = "";
        [ObservableProperty] private string _finValor = "";
        [ObservableProperty] private bool _isAnexo = false;
        
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

        [ObservableProperty] private string _btnSalvarContaTexto = "ADICIONAR AO RASCUNHO";
        [ObservableProperty] private SolidColorBrush _btnSalvarContaCor = Brushes.Blue; 
        [ObservableProperty] private Visibility _btnCancelarEditVisibility = Visibility.Collapsed;
        private string? _editingContaId = null;

        public ObservableCollection<ContaGridDto> Rascunhos { get; set; } = new();
        public ObservableCollection<ContaGridDto> HistoricoFin { get; set; } = new();
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
        
        partial void OnFinItemComboChanged(string value) {
            if (value == "Outro...") { ComboVisivel = Visibility.Collapsed; TextoVisivel = Visibility.Visible; }
            else { ComboVisivel = Visibility.Visible; TextoVisivel = Visibility.Collapsed; }
        }

        private void CarregarTudo()
        {
            using var conn = _db.GetConnection();
            
            var p = conn.QueryFirstOrDefault("SELECT * FROM processos WHERE id = @id", new { id = _processoId });
            if (p != null) {
                Numero = p.numero; Paciente = p.paciente; Juiz = p.juiz; Classificacao = p.classificacao;
                ObsFixa = p.observacao_fixa ?? ""; FaseProcessual = p.status_fase ?? "Conhecimento";
                
                string? prazoShow = p.cache_proximo_prazo;
                if (string.IsNullOrEmpty(prazoShow)) {
                    var last = conn.QueryFirstOrDefault("SELECT proximo_prazo_padrao FROM verificacoes WHERE processo_id=@id ORDER BY data_hora DESC", new { id=_processoId });
                    prazoShow = last?.proximo_prazo_padrao;
                }

                var (txt, cor) = ProcessLogic.CheckPrazoStatus(prazoShow);
                StatusTexto = txt; StatusCor = cor;
                SaudeVisibility = Classificacao == "Saúde" ? Visibility.Visible : Visibility.Collapsed;
            }

            ItensSaude.Clear(); ListaItensCombo.Clear();
            var itens = conn.Query<ItemSaudeDto>("SELECT * FROM itens_saude WHERE processo_id=@id", new { id = _processoId });
            foreach (var i in itens) { 
                if (i != null) {
                    ItensSaude.Add(i); 
                    ListaItensCombo.Add(i.Nome); 
                }
            }
            ListaItensCombo.Add("Outro...");

            Rascunhos.Clear(); HistoricoFin.Clear();
            var contas = conn.Query("SELECT * FROM contas WHERE processo_id=@id ORDER BY data_movimentacao", new { id = _processoId });
            decimal saldo = 0;

            foreach (var c in contas) {
                decimal valAlv = (decimal)(c.valor_alvara ?? 0.0);
                decimal valCon = (decimal)(c.valor_conta ?? 0.0);
                decimal valDisplay = valAlv > 0 ? valAlv : valCon;

                var dto = new ContaGridDto {
                    Id = c.id, Data = c.data_movimentacao, Hist = c.historico,
                    Valor = ProcessLogic.FormatMoney(valDisplay),
                    Raw = c 
                };

                if (c.status_conta == "rascunho") {
                    dto.IsRascunho = true;
                    Rascunhos.Add(dto);
                } else {
                    saldo += (valAlv - valCon);
                    dto.IsRascunho = false;
                    dto.Cred = valAlv > 0 ? ProcessLogic.FormatMoney(valAlv) : "";
                    dto.Deb = valCon > 0 ? ProcessLogic.FormatMoney(valCon) : "";
                    dto.Saldo = ProcessLogic.FormatMoney(saldo);
                    dto.Cor = valAlv > 0 ? Brushes.Green : Brushes.Red;
                    HistoricoFin.Add(dto);
                }
            }
            SaldoDisplay = ProcessLogic.FormatMoney(saldo);
            SaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;

            HistoricoLogs.Clear();
            var logs = conn.Query("SELECT * FROM verificacoes WHERE processo_id=@id ORDER BY data_hora DESC LIMIT 100", new { id = _processoId });
            foreach(var l in logs) {
                string det = "";
                if (l.diligencia_realizada == 1) det += $"[Dil] {l.diligencia_descricao} ";
                if (!string.IsNullOrEmpty(l.alteracoes_texto) && !l.alteracoes_texto.Contains("Nenhuma")) det += $"[Mod] {l.alteracoes_texto}";
                
                DateTime dh = DateTime.Parse(l.data_hora);
                HistoricoLogs.Add(new { Data=dh.ToString("dd/MM/yyyy HH:mm"), Resp=l.responsavel, Status=l.status_processo, Detalhes=det });
            }
        }

        [RelayCommand]
        public void SalvarVerificacao()
        {
            if (DiligenciaPendente && string.IsNullOrWhiteSpace(PendenciaDesc)) { MessageBox.Show("Descreva a pendência."); return; }
            if (string.IsNullOrWhiteSpace(Responsavel)) { MessageBox.Show("Informe o responsável."); return; }

            try {
                using var conn = _db.GetConnection(); conn.Open(); using var trans = conn.BeginTransaction();
                
                foreach (var item in ItensSaude)
                    conn.Execute("UPDATE itens_saude SET qtd=@Qtd, local=@Local, data_prescricao=@DataPrescricao, is_desnecessario=@IsDesnecessario, tem_bloqueio=@TemBloqueio WHERE id=@Id", item, trans);

                var (prz, notif) = ProcessLogic.CalculateDueDates(null, ProxData);
                if (string.IsNullOrEmpty(prz)) { 
                    (prz, notif) = ProcessLogic.CalculateDueDates(DateTime.Now.ToString("dd/MM/yyyy"));
                }

                string resumo = DiligenciaRealizada ? $"[Dil] {DiligenciaDesc}" : "Verificação de Rotina";
                
                conn.Execute("UPDATE processos SET status_fase=@f, observacao_fixa=@o, cache_proximo_prazo=@p, ultima_atualizacao=@u WHERE id=@id",
                    new { f = FaseProcessual, o = ObsFixa, p = prz, u = DateTime.Now.ToString("dd/MM/yyyy"), id = _processoId }, trans);

                string jsonSnapshot = JsonSerializer.Serialize(ItensSaude);

                conn.Execute(@"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, diligencia_realizada, diligencia_descricao, diligencia_pendente, pendencias_descricao, proximo_prazo_padrao, data_notificacao, alteracoes_texto, itens_snapshot_json)
                             VALUES (@id, @pid, @dh, @st, @resp, @dr, @dd, @dp, @pd, @pp, @dn, @at, @js)",
                             new { id = Guid.NewGuid().ToString(), pid = _processoId, dh = DateTime.Now.ToString("s"), st = FaseProcessual, resp = Responsavel, dr = DiligenciaRealizada?1:0, dd = DiligenciaDesc, dp = DiligenciaPendente?1:0, pd = PendenciaDesc, pp = prz, dn = notif, at = resumo, js = jsonSnapshot }, trans);

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
            if (!string.IsNullOrEmpty(FinMes) || !string.IsNullOrEmpty(FinAno)) hist += $" Ref: {FinMes}/{FinAno}";
            if (!string.IsNullOrEmpty(FinObs)) hist += $" - {FinObs}";

            string mov = IsAnexo ? "Anexo" : FinMov;
            decimal vAlv = FinTipo == "Alvará" ? val : 0;
            decimal vCon = FinTipo != "Alvará" ? val : 0;

            using var conn = _db.GetConnection();

            if (_editingContaId != null)
            {
                conn.Execute(@"UPDATE contas SET data_movimentacao=@dt, tipo_lancamento=@tp, historico=@hist, num_nf_alvara=@nf, 
                               valor_alvara=@va, valor_conta=@vc, mov_processo=@mov, terapia_medicamento_nome=@nm, quantidade=@qt, 
                               mes_referencia=@mr, ano_referencia=@ar, observacoes=@ob WHERE id=@id",
                               new { dt=FinData, tp=FinTipo, hist, nf=FinNf, va=vAlv, vc=vCon, mov, nm=nomeItem, qt=FinQtd, mr=FinMes, ar=FinAno, ob=FinObs, id=_editingContaId });
                MessageBox.Show("Lançamento atualizado!");
                CancelarEdicao();
            }
            else
            {
                conn.Execute(@"INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, num_nf_alvara, valor_alvara, valor_conta, mov_processo, status_conta, responsavel, terapia_medicamento_nome, quantidade, mes_referencia, ano_referencia, observacoes)
                               VALUES (@id, @pid, @dt, @tp, @hist, @nf, @va, @vc, @mov, 'rascunho', @resp, @nm, @qt, @mr, @ar, @ob)",
                               new { id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tp = FinTipo, hist, nf = FinNf, va = vAlv, vc = vCon, mov, resp = _userLogado, nm = nomeItem, qt = FinQtd, mr = FinMes, ar = FinAno, ob = FinObs });
                
                FinValor = ""; FinItemTexto = ""; FinObs = "";
            }
            CarregarTudo();
        }

        [RelayCommand]
        public void EditarConta(ContaGridDto dto)
        {
            if (dto == null) return;
            var r = dto.Raw; 

            _editingContaId = dto.Id;
            BtnSalvarContaTexto = "SALVAR ALTERAÇÃO";
            BtnSalvarContaCor = Brushes.Red;
            BtnCancelarEditVisibility = Visibility.Visible;

            FinTipo = r.tipo_lancamento;
            FinData = r.data_movimentacao;
            FinNf = r.num_nf_alvara ?? "";
            
            string mov = r.mov_processo ?? "";
            if (mov == "Anexo") { IsAnexo = true; FinMov = ""; }
            else { IsAnexo = false; FinMov = mov; }

            decimal v = (decimal)(r.valor_alvara > 0 ? r.valor_alvara : r.valor_conta);
            FinValor = ProcessLogic.FormatMoney(v);

            string nome = r.terapia_medicamento_nome;
            if (ListaItensCombo.Contains(nome)) { FinItemCombo = nome; }
            else { FinItemCombo = "Outro..."; FinItemTexto = nome ?? ""; }

            FinQtd = r.quantidade ?? "";
            FinMes = r.mes_referencia ?? "";
            FinAno = r.ano_referencia ?? "";
            FinObs = r.observacoes ?? "";
        }

        [RelayCommand]
        public void CancelarEdicao()
        {
            _editingContaId = null;
            BtnSalvarContaTexto = "ADICIONAR AO RASCUNHO";
            BtnSalvarContaCor = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)); 
            BtnCancelarEditVisibility = Visibility.Collapsed;
            FinValor = ""; FinObs = ""; FinNf = "";
        }

        [RelayCommand] public void LancarTudo() {
            if (MessageBox.Show("Lançar todos os rascunhos definitivamente?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                using var conn = _db.GetConnection();
                conn.Execute("UPDATE contas SET status_conta='lancado' WHERE processo_id=@id AND status_conta='rascunho'", new { id = _processoId });
                _db.PerformBackup();
                CarregarTudo();
            }
        }

        [RelayCommand] public void ExcluirConta(string id) {
            if (MessageBox.Show("Excluir este lançamento?", "Confirmação", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM contas WHERE id=@id", new { id });
                if (_editingContaId == id) CancelarEdicao();
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
