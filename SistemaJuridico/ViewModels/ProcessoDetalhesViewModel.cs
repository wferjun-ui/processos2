using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // Modelo Interno Completo
    public class ProcessoCompletoDto
    {
        public string Id { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Paciente { get; set; } = "";
        public string Juiz { get; set; } = "";
        public string GenitorRepNome { get; set; } = "";
        public string GenitorRepTipo { get; set; } = "";
        public string Classificacao { get; set; } = "";
        public string StatusFase { get; set; } = "";
        public string UltimaAtualizacao { get; set; } = "";
        public string ObservacaoFixa { get; set; } = "";
        public string CacheProximoPrazo { get; set; } = "";
    }

    public partial class ItemSaudeCheckModel : ObservableObject
    {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        [ObservableProperty] private string _qtd = "";
        [ObservableProperty] private string _local = "";
        [ObservableProperty] private string _dataPrescricao = "";
        [ObservableProperty] private bool _isDesnecessario;
        [ObservableProperty] private bool _temBloqueio;
    }

    public class ContaModel
    {
        public string Id { get; set; } = "";
        public string Data { get; set; } = "";
        public string Historico { get; set; } = "";
        public decimal ValorAlvara { get; set; }
        public decimal ValorConta { get; set; } 
        public string Tipo { get; set; } = "";
        public string Status { get; set; } = ""; 
        public bool IsRascunho => Status == "rascunho";
        public SolidColorBrush CorTexto => IsRascunho ? Brushes.Orange : Brushes.Black;
    }

    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;

        // --- DADOS DA TELA ---
        [ObservableProperty] private ProcessoCompletoDto _processo = new(); // Objeto principal
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCorBrush = Brushes.Gray;
        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

        // --- ABA VERIFICAÇÃO ---
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private string _faseProcessual = "";
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";
        
        [ObservableProperty] private string _proxData = "";
        [ObservableProperty] private string _responsavel = "";

        public ObservableCollection<ItemSaudeCheckModel> ItensVerificacao { get; set; } = new();
        [ObservableProperty] private Visibility _temSaudeVisibility = Visibility.Collapsed;

        // --- ABA HISTÓRICO ---
        public ObservableCollection<dynamic> HistoricoGeral { get; set; } = new();

        // --- ABA FINANCEIRO ---
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finTipo = "Alvará"; 
        [ObservableProperty] private string _finNF = "";
        [ObservableProperty] private string _finMov = "";
        [ObservableProperty] private string _finValor = "";
        
        [ObservableProperty] private string _finItemNome = "";
        [ObservableProperty] private string _finQtd = "";
        [ObservableProperty] private string _finMes = "";
        [ObservableProperty] private string _finAno = "";
        [ObservableProperty] private string _finObs = "";
        [ObservableProperty] private Visibility _boxExtraVisibility = Visibility.Collapsed;

        public ObservableCollection<ContaModel> ListaRascunhos { get; set; } = new();
        public ObservableCollection<ContaModel> ListaHistoricoFin { get; set; } = new();

        public ObservableCollection<string> FasesPossiveis { get; } = new() 
        { "Conhecimento", "Cumprimento de Sentença", "Recurso", "Arquivado", "Suspenso", "Aguardando Trânsito", "Cumprimento Provisório" };

        public ProcessoDetalhesViewModel(string processoId)
        {
            _db = App.DB!;
            _processoId = processoId;
            QuestPDF.Settings.License = LicenseType.Community;
            
            Responsavel = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";
            var (d, _) = ProcessLogic.CalculateDueDates(DateTime.Now.ToString("dd/MM/yyyy"));
            ProxData = d;

            CarregarDadosCompletos();
        }

        partial void OnFinTipoChanged(string value)
        {
            BoxExtraVisibility = value == "Alvará" ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CarregarDadosCompletos()
        {
            try
            {
                using var conn = _db.GetConnection();
                conn.Open();

                // 1. CARREGAR PROCESSO (Com Aliases para o C# entender)
                string sqlProc = @"
                    SELECT 
                        id, numero, paciente, juiz, 
                        genitor_rep_nome AS GenitorRepNome, 
                        genitor_rep_tipo AS GenitorRepTipo,
                        classificacao, 
                        status_fase AS StatusFase, 
                        ultima_atualizacao AS UltimaAtualizacao,
                        observacao_fixa AS ObservacaoFixa, 
                        cache_proximo_prazo AS CacheProximoPrazo
                    FROM processos 
                    WHERE id = @id";

                var p = conn.QueryFirstOrDefault<ProcessoCompletoDto>(sqlProc, new { id = _processoId });
                
                if (p != null)
                {
                    Processo = p;
                    ObsFixa = p.ObservacaoFixa ?? "";
                    FaseProcessual = p.StatusFase ?? "Conhecimento";
                    
                    var (txt, cor) = ProcessLogic.CheckPrazoStatus(p.CacheProximoPrazo);
                    StatusTexto = txt; 
                    StatusCorBrush = cor;

                    TemSaudeVisibility = Processo.Classificacao == "Saúde" ? Visibility.Visible : Visibility.Collapsed;
                }

                // 2. ITENS DE SAÚDE
                ItensVerificacao.Clear();
                string sqlItens = @"
                    SELECT id, tipo, nome, qtd, local, 
                           data_prescricao AS DataPrescricao, 
                           is_desnecessario AS IsDesnecessario, 
                           tem_bloqueio AS TemBloqueio 
                    FROM itens_saude 
                    WHERE processo_id = @id";
                
                var itens = conn.Query<ItemSaudeCheckModel>(sqlItens, new { id = _processoId });
                foreach (var i in itens) ItensVerificacao.Add(i);

                // 3. HISTÓRICO
                HistoricoGeral.Clear();
                var hists = conn.Query("SELECT * FROM verificacoes WHERE processo_id = @id ORDER BY data_hora DESC", new { id = _processoId });
                foreach (var h in hists) HistoricoGeral.Add(h);

                // 4. FINANCEIRO
                CarregarFinanceiro(conn);
            }
            catch (Exception ex) { MessageBox.Show("Erro ao carregar: " + ex.Message); }
        }

        private void CarregarFinanceiro(System.Data.IDbConnection conn)
        {
            ListaRascunhos.Clear();
            ListaHistoricoFin.Clear();
            
            var lancamentos = conn.Query<dynamic>("SELECT * FROM contas WHERE processo_id = @id ORDER BY data_movimentacao", new { id = _processoId });
            decimal saldo = 0;

            foreach (var l in lancamentos)
            {
                decimal cred = (decimal)(l.valor_alvara ?? 0.0);
                decimal deb = (decimal)(l.valor_conta ?? 0.0);
                string status = l.status_conta;

                var conta = new ContaModel {
                    Id = l.id, Data = l.data_movimentacao, Historico = l.historico,
                    ValorAlvara = cred, ValorConta = deb, Tipo = l.tipo_lancamento, Status = status
                };

                if (status == "rascunho")
                {
                    ListaRascunhos.Add(conta);
                }
                else
                {
                    saldo += (cred - deb);
                    ListaHistoricoFin.Add(conta);
                }
            }

            SaldoDisplay = saldo.ToString("C2");
            SaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;
        }

        [RelayCommand]
        public void SalvarVerificacao()
        {
            try
            {
                using var conn = _db.GetConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();

                // 1. Atualizar Processo
                conn.Execute("UPDATE processos SET status_fase=@f, observacao_fixa=@o, cache_proximo_prazo=@p, ultima_atualizacao=@u WHERE id=@id",
                    new { f = FaseProcessual, o = ObsFixa, p = ProxData, u = DateTime.Now.ToString("dd/MM/yyyy"), id = _processoId }, trans);

                // 2. Atualizar Itens de Saúde
                foreach(var item in ItensVerificacao)
                {
                    conn.Execute("UPDATE itens_saude SET qtd=@q, local=@l, data_prescricao=@d, is_desnecessario=@isd, tem_bloqueio=@tb WHERE id=@id",
                        new { q = item.Qtd, l = item.Local, d = item.DataPrescricao, isd = item.IsDesnecessario ? 1 : 0, tb = item.TemBloqueio ? 1 : 0, id = item.Id }, trans);
                }

                // 3. Gerar Log
                string snapshot = JsonSerializer.Serialize(ItensVerificacao);
                string resumo = DiligenciaRealizada ? $"[Dil] {DiligenciaDesc}" : "Verificação de Rotina";
                if(DiligenciaPendente) resumo += $" | [Pend] {PendenciaDesc}";
                
                var (_, notif) = ProcessLogic.CalculateDueDates(ProxData);

                conn.Execute(@"
                    INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, 
                    diligencia_realizada, diligencia_descricao, diligencia_pendente, pendencias_descricao,
                    proximo_prazo_padrao, data_notificacao, alteracoes_texto, itens_snapshot_json)
                    VALUES (@id, @pid, @dh, @st, @resp, @dr, @dd, @dp, @pd, @prz, @notif, @alt, @snap)",
                    new { 
                        id = Guid.NewGuid().ToString(), pid = _processoId, dh = DateTime.Now.ToString("s"),
                        st = FaseProcessual, resp = Responsavel, 
                        dr = DiligenciaRealizada ? 1 : 0, dd = DiligenciaDesc ?? "",
                        dp = DiligenciaPendente ? 1 : 0, pd = PendenciaDesc ?? "",
                        prz = ProxData, notif = notif, alt = resumo, snap = snapshot
                    }, trans);

                trans.Commit();
                _db.PerformBackup();
                
                MessageBox.Show("Verificação Salva!");
                DiligenciaRealizada = false; DiligenciaPendente = false; DiligenciaDesc = ""; PendenciaDesc = "";
                CarregarDadosCompletos();
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        [RelayCommand]
        public void AdicionarRascunho()
        {
            if (string.IsNullOrWhiteSpace(FinValor)) { MessageBox.Show("Informe o Valor."); return; }
            
            decimal val = ProcessLogic.ParseMoney(FinValor);
            decimal valAlvara = FinTipo == "Alvará" ? val : 0;
            decimal valConta = FinTipo != "Alvará" ? val : 0;

            string historico = FinTipo == "Alvará" 
                ? $"Alvará - {FinNF} - {FinMov}" 
                : $"{FinTipo}: {FinItemNome} (Ref: {FinMes}/{FinAno}) - {FinObs}";

            using var conn = _db.GetConnection();
            conn.Execute(@"
                INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, num_nf_alvara, mov_processo, 
                valor_alvara, valor_conta, status_conta, responsavel)
                VALUES (@id, @pid, @dt, @tp, @hist, @nf, @mov, @va, @vc, 'rascunho', @resp)",
                new { 
                    id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tp = FinTipo, hist = historico,
                    nf = FinNF, mov = FinMov, va = valAlvara, vc = valConta, resp = Responsavel 
                });

            FinValor = ""; FinObs = ""; FinItemNome = "";
            CarregarDadosCompletos();
        }

        [RelayCommand]
        public void LancarTudo()
        {
            if (MessageBox.Show("Confirmar o lançamento definitivo de todos os rascunhos?", "Financeiro", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("UPDATE contas SET status_conta='lancado' WHERE processo_id=@pid AND status_conta='rascunho'", new { pid = _processoId });
                CarregarDadosCompletos();
            }
        }

        [RelayCommand]
        public void ExcluirConta(string id)
        {
            if (MessageBox.Show("Excluir lançamento?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM contas WHERE id=@id", new { id });
                CarregarDadosCompletos();
            }
        }

        [RelayCommand]
        public void Voltar() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        
        [RelayCommand]
        public void GerarPDF() 
        {
            try
            {
                var arquivo = $"Relatorio_{Processo.Numero}_{DateTime.Now:yyyyMMdd}.pdf";
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), arquivo);
                Document.Create(container => {
                    container.Page(page => {
                        page.Margin(50);
                        page.Header().Text($"Relatório Processo {Processo.Numero}").FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                        page.Content().PaddingVertical(10).Table(table => {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(80); columns.RelativeColumn(); columns.ConstantColumn(80); columns.ConstantColumn(80); });
                            table.Header(header => { header.Cell().Text("Data").Bold(); header.Cell().Text("Histórico").Bold(); header.Cell().AlignRight().Text("Crédito").Bold(); header.Cell().AlignRight().Text("Débito").Bold(); });
                            foreach (var item in ListaHistoricoFin) {
                                table.Cell().Text(item.Data); table.Cell().Text(item.Historico);
                                table.Cell().AlignRight().Text(item.ValorAlvara > 0 ? $"{item.ValorAlvara:N2}" : "-").FontColor(QuestPDF.Helpers.Colors.Green.Medium);
                                table.Cell().AlignRight().Text(item.ValorConta > 0 ? $"{item.ValorConta:N2}" : "-").FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                            }
                        });
                    });
                }).GeneratePdf(path);
                MessageBox.Show($"PDF gerado:\n{path}");
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }
    }
}
