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
using System.Text.Json; // Necessário para snapshot
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // Modelos Locais
    public class ItemSaudeModel
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Qtd { get; set; } = string.Empty;
        public string Frequencia { get; set; } = string.Empty;
        public string Local { get; set; } = string.Empty;
        public string DataPrescricao { get; set; } = string.Empty;
        public bool IsDesnecessario { get; set; }
        public bool TemBloqueio { get; set; }
    }

    public class ContaModel
    {
        public string Id { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string Historico { get; set; } = string.Empty;
        public decimal ValorAlvara { get; set; }
        public decimal ValorConta { get; set; } 
        public string Tipo { get; set; } = string.Empty;
        public string Status { get; set; } = "lancado"; // rascunho ou lancado
        
        // Propriedade visual para destacar rascunhos na tabela
        public SolidColorBrush CorTexto => Status == "rascunho" ? Brushes.Orange : Brushes.Black;
        public string HistoricoVisual => Status == "rascunho" ? "[RASCUNHO] " + Historico : Historico;
    }

    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;

        [ObservableProperty] private ProcessoModel _processo = new();
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCorBrush = Brushes.Gray;
        
        // Verificação e Status
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private string _faseProcessual = ""; // Novo: Controle de Fase
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";
        
        // Financeiro Inputs
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finTipo = "Despesa"; 
        [ObservableProperty] private string _finHistorico = "";
        [ObservableProperty] private string _finValor = "";
        [ObservableProperty] private string _finSaldoTotal = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _finSaldoCor = Brushes.Black;

        // Saúde Inputs
        [ObservableProperty] private string _saudeTipo = "Medicamento";
        [ObservableProperty] private string _saudeNome = "";
        [ObservableProperty] private string _saudeQtd = "";
        [ObservableProperty] private string _saudeFreq = "";
        [ObservableProperty] private string _saudeLocal = "";

        // Listas
        public ObservableCollection<dynamic> Historico { get; set; } = new();
        public ObservableCollection<ContaModel> Contas { get; set; } = new();
        public ObservableCollection<ItemSaudeModel> ItensSaude { get; set; } = new();
        
        // Combobox de fases (Igual ao Python)
        public ObservableCollection<string> FasesPossiveis { get; } = new() 
        { 
            "Conhecimento", "Cumprimento de Sentença", "Recurso", "Arquivado", 
            "Suspenso em recurso", "Julgado e aguardando trânsito", "Cumprimento provisório" 
        };

        public ProcessoDetalhesViewModel(string processoId)
        {
            _db = App.DB!;
            _processoId = processoId;
            QuestPDF.Settings.License = LicenseType.Community;
            CarregarDados();
        }

        private void CarregarDados()
        {
            using var conn = _db.GetConnection();
            
            var pDto = conn.QueryFirstOrDefault<dynamic>("SELECT * FROM processos WHERE id = @id", new { id = _processoId });
            if (pDto != null)
            {
                Processo = new ProcessoModel 
                { 
                    Id = pDto.id ?? "", 
                    Numero = pDto.numero ?? "", 
                    Paciente = pDto.paciente ?? "", 
                    DataPrazo = pDto.cache_proximo_prazo ?? ""
                };
                ObsFixa = pDto.observacao_fixa ?? "";
                FaseProcessual = pDto.status_fase ?? "Conhecimento";

                var (txt, cor) = CalcularStatus(Processo.DataPrazo);
                StatusTexto = txt;
                StatusCorBrush = cor;
            }

            // Histórico de Verificações
            Historico.Clear();
            var hist = conn.Query("SELECT * FROM verificacoes WHERE processo_id = @id ORDER BY data_hora DESC", new { id = _processoId });
            foreach (var h in hist) Historico.Add(h);

            // Itens de Saúde
            ItensSaude.Clear();
            var itens = conn.Query<ItemSaudeModel>("SELECT * FROM itens_saude WHERE processo_id = @id", new { id = _processoId });
            foreach (var i in itens) ItensSaude.Add(i);

            // Financeiro
            CarregarFinanceiro(conn);
        }

        private void CarregarFinanceiro(System.Data.IDbConnection conn)
        {
            Contas.Clear();
            var lancamentos = conn.Query<dynamic>("SELECT * FROM contas WHERE processo_id = @id ORDER BY data_movimentacao", new { id = _processoId });
            
            decimal saldo = 0;
            foreach (var l in lancamentos)
            {
                string status = l.status_conta; // 'rascunho' ou 'lancado'
                
                decimal cred = (decimal)(l.valor_alvara ?? 0.0);
                decimal deb = (decimal)(l.valor_conta ?? 0.0);
                
                // Só soma no saldo visível se estiver lançado (regra Python)
                if (status == "lancado") 
                    saldo += (cred - deb);

                Contas.Add(new ContaModel
                {
                    Id = l.id,
                    Data = l.data_movimentacao,
                    Historico = l.historico,
                    ValorAlvara = cred,
                    ValorConta = deb,
                    Tipo = l.tipo_lancamento,
                    Status = status
                });
            }

            FinSaldoTotal = $"Saldo Disponível: {saldo:C2}";
            FinSaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;
        }

        // --- SAÚDE ---
        [RelayCommand]
        public void AdicionarSaude()
        {
            if (string.IsNullOrWhiteSpace(SaudeNome)) return;
            using var conn = _db.GetConnection();
            conn.Execute(@"INSERT INTO itens_saude (id, processo_id, tipo, nome, qtd, frequencia, local) 
                           VALUES (@id, @pid, @t, @n, @q, @f, @l)",
                new { id = Guid.NewGuid().ToString(), pid = _processoId, t = SaudeTipo, n = SaudeNome, q = SaudeQtd, f = SaudeFreq, l = SaudeLocal });
            
            SaudeNome = ""; SaudeQtd = ""; SaudeLocal = "";
            CarregarDados();
        }

        [RelayCommand]
        public void RemoverSaude(string id)
        {
            if (MessageBox.Show("Remover item?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM itens_saude WHERE id = @id", new { id });
                CarregarDados();
            }
        }

        // --- FINANCEIRO ---
        [RelayCommand]
        public void AdicionarConta()
        {
            if (string.IsNullOrWhiteSpace(FinValor) || string.IsNullOrWhiteSpace(FinHistorico)) return;
            
            decimal valor = 0;
            // Tenta limpar R$ e converter
            var valClean = FinValor.Replace("R$", "").Trim();
            if (!decimal.TryParse(valClean, out valor))
            {
                MessageBox.Show("Valor inválido"); return;
            }

            decimal alvara = FinTipo == "Alvará" ? valor : 0;
            decimal despesa = FinTipo != "Alvará" ? valor : 0;

            using var conn = _db.GetConnection();
            var user = Application.Current.Properties["Usuario"]?.ToString() ?? "Sistema";

            // INSERE COMO RASCUNHO (Regra Python)
            conn.Execute(@"INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, valor_alvara, valor_conta, responsavel, status_conta) 
                           VALUES (@id, @pid, @dt, @tipo, @hist, @alv, @desp, @resp, 'rascunho')",
                           new { 
                               id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tipo = FinTipo, hist = FinHistorico,
                               alv = alvara, desp = despesa, resp = user
                           });

            FinHistorico = ""; FinValor = "";
            CarregarDados();
        }

        [RelayCommand]
        public void LancarRascunhos()
        {
            if (MessageBox.Show("Confirmar todos os lançamentos de rascunho?", "Financeiro", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("UPDATE contas SET status_conta='lancado' WHERE processo_id=@pid AND status_conta='rascunho'", new { pid = _processoId });
                CarregarDados();
            }
        }

        [RelayCommand]
        public void RemoverConta(string id)
        {
            if (MessageBox.Show("Remover lançamento?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM contas WHERE id = @id", new { id });
                CarregarDados();
            }
        }

        [RelayCommand]
        public void GerarRelatorioPDF()
        {
            try
            {
                var arquivo = $"Relatorio_{Processo.Numero}_{DateTime.Now:yyyyMMdd}.pdf";
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), arquivo);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(50);
                        page.Header().Text($"Relatório Financeiro - Processo {Processo.Numero}")
                            .FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                        
                        page.Content().PaddingVertical(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.RelativeColumn();
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Data").Bold();
                                header.Cell().Text("Histórico").Bold();
                                header.Cell().AlignRight().Text("Crédito").Bold();
                                header.Cell().AlignRight().Text("Débito").Bold();
                            });

                            foreach (var item in Contas.Where(c => c.Status == "lancado"))
                            {
                                table.Cell().Text(item.Data);
                                table.Cell().Text(item.Historico);
                                table.Cell().AlignRight().Text(item.ValorAlvara > 0 ? $"{item.ValorAlvara:N2}" : "-")
                                    .FontColor(QuestPDF.Helpers.Colors.Green.Medium);
                                table.Cell().AlignRight().Text(item.ValorConta > 0 ? $"{item.ValorConta:N2}" : "-")
                                    .FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                            }
                        });
                        
                        page.Footer().AlignCenter().Text(x => { x.Span("Gerado em " + DateTime.Now); });
                    });
                }).GeneratePdf(path);

                MessageBox.Show($"PDF gerado na Área de Trabalho:\n{path}");
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        // --- VERIFICAÇÃO ---
        [RelayCommand]
        public void SalvarVerificacao()
        {
             try
            {
                using var conn = _db.GetConnection();
                var usuario = Application.Current.Properties["Usuario"]?.ToString() ?? "Sistema";
                
                // Cálculo de Prazo (14 dias)
                var dataBase = DateTime.Now.AddDays(14);
                if (dataBase.DayOfWeek == DayOfWeek.Saturday) dataBase = dataBase.AddDays(2);
                if (dataBase.DayOfWeek == DayOfWeek.Sunday) dataBase = dataBase.AddDays(1);
                var novoPrazo = dataBase.ToString("dd/MM/yyyy");

                // Snapshot dos Itens de Saúde (Regra Python)
                // Salva o estado atual dos itens como JSON para histórico
                string snapshot = JsonSerializer.Serialize(ItensSaude);

                var sql = @"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, 
                            diligencia_realizada, diligencia_descricao, diligencia_pendente, pendencias_descricao,
                            proximo_prazo_padrao, alteracoes_texto, itens_snapshot_json)
                            VALUES (@id, @pid, @dh, @st, @resp, @dr, @dd, @dp, @pd, @prz, 'Nova Verificação', @snap)";
                
                conn.Execute(sql, new { 
                    id = Guid.NewGuid().ToString(), 
                    pid = _processoId, 
                    dh = DateTime.Now.ToString("s"), 
                    st = FaseProcessual, // Salva a fase atual selecionada
                    resp = usuario,
                    dr = DiligenciaRealizada ? 1 : 0,
                    dd = DiligenciaDesc ?? "",
                    dp = DiligenciaPendente ? 1 : 0,
                    pd = PendenciaDesc ?? "",
                    prz = novoPrazo,
                    snap = snapshot
                });

                // Atualiza o processo (Cache e Fase)
                conn.Execute("UPDATE processos SET cache_proximo_prazo = @p, observacao_fixa = @o, status_fase = @f WHERE id = @id", 
                    new { p = novoPrazo, o = ObsFixa, f = FaseProcessual, id = _processoId });

                // Atualiza o banco (Backup Trigger)
                _db.PerformBackup();

                MessageBox.Show("Verificação salva com sucesso!");
                
                // Limpeza
                DiligenciaRealizada = false; DiligenciaPendente = false;
                DiligenciaDesc = ""; PendenciaDesc = "";
                
                CarregarDados();
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        [RelayCommand]
        public void Voltar()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        }

        private (string, SolidColorBrush) CalcularStatus(string dataStr)
        {
             if (DateTime.TryParseExact(dataStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime d))
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
