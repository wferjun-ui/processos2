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
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    // Modelos Locais
    public class ItemSaudeModel
    {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Qtd { get; set; } = "";
        public string Frequencia { get; set; } = "";
    }

    public class ContaModel
    {
        public string Id { get; set; } = "";
        public string Data { get; set; } = "";
        public string Historico { get; set; } = "";
        public decimal ValorAlvara { get; set; } // Crédito
        public decimal ValorConta { get; set; }  // Débito
        public string Tipo { get; set; } = "";
    }

    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly string _processoId;

        [ObservableProperty] private ProcessoModel _processo = new();
        [ObservableProperty] private string _statusTexto = "";
        [ObservableProperty] private SolidColorBrush _statusCorBrush = Brushes.Gray;
        
        // Verificação
        [ObservableProperty] private string _obsFixa = "";
        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";
        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";

        // Financeiro - Inputs
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finTipo = "Despesa"; // Alvara ou Despesa
        [ObservableProperty] private string _finHistorico = "";
        [ObservableProperty] private string _finValor = "";
        [ObservableProperty] private string _finSaldoTotal = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _finSaldoCor = Brushes.Black;

        // Saúde - Inputs
        [ObservableProperty] private string _saudeTipo = "Medicamento";
        [ObservableProperty] private string _saudeNome = "";
        [ObservableProperty] private string _saudeQtd = "";
        [ObservableProperty] private string _saudeFreq = "";

        // Coleções
        public ObservableCollection<dynamic> Historico { get; set; } = new();
        public ObservableCollection<ContaModel> Contas { get; set; } = new();
        public ObservableCollection<ItemSaudeModel> ItensSaude { get; set; } = new();

        public ProcessoDetalhesViewModel(string processoId)
        {
            _db = App.DB!;
            _processoId = processoId;
            QuestPDF.Settings.License = LicenseType.Community; // Licença Community
            CarregarDados();
        }

        private void CarregarDados()
        {
            using var conn = _db.GetConnection();
            
            // 1. Processo Principal
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
                
                var (txt, cor) = CalcularStatus(Processo.DataPrazo);
                StatusTexto = txt;
                StatusCorBrush = cor;
            }

            // 2. Histórico
            Historico.Clear();
            var hist = conn.Query("SELECT * FROM verificacoes WHERE processo_id = @id ORDER BY data_hora DESC", new { id = _processoId });
            foreach (var h in hist) Historico.Add(h);

            // 3. Itens Saúde
            CarregarSaude(conn);

            // 4. Financeiro
            CarregarFinanceiro(conn);
        }

        private void CarregarSaude(System.Data.IDbConnection conn)
        {
            ItensSaude.Clear();
            var itens = conn.Query<ItemSaudeModel>("SELECT id, tipo, nome, qtd, frequencia FROM itens_saude WHERE processo_id = @id", new { id = _processoId });
            foreach (var i in itens) ItensSaude.Add(i);
        }

        private void CarregarFinanceiro(System.Data.IDbConnection conn)
        {
            Contas.Clear();
            var lancamentos = conn.Query<dynamic>("SELECT * FROM contas WHERE processo_id = @id ORDER BY data_movimentacao", new { id = _processoId });
            
            decimal saldo = 0;
            foreach (var l in lancamentos)
            {
                decimal cred = (decimal)(l.valor_alvara ?? 0.0);
                decimal deb = (decimal)(l.valor_conta ?? 0.0);
                saldo += (cred - deb);

                Contas.Add(new ContaModel
                {
                    Id = l.id,
                    Data = l.data_movimentacao,
                    Historico = l.historico,
                    ValorAlvara = cred,
                    ValorConta = deb,
                    Tipo = l.tipo_lancamento
                });
            }

            FinSaldoTotal = $"Saldo: {saldo:C2}";
            FinSaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;
        }

        // --- COMANDOS SAÚDE ---
        [RelayCommand]
        public void AdicionarSaude()
        {
            if (string.IsNullOrWhiteSpace(SaudeNome)) return;
            using var conn = _db.GetConnection();
            conn.Execute("INSERT INTO itens_saude (id, processo_id, tipo, nome, qtd, frequencia) VALUES (@id, @pid, @t, @n, @q, @f)",
                new { id = Guid.NewGuid().ToString(), pid = _processoId, t = SaudeTipo, n = SaudeNome, q = SaudeQtd, f = SaudeFreq });
            
            SaudeNome = ""; SaudeQtd = ""; // Limpa
            CarregarSaude(conn);
        }

        [RelayCommand]
        public void RemoverSaude(string id)
        {
            if (MessageBox.Show("Remover item?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM itens_saude WHERE id = @id", new { id });
                CarregarSaude(conn);
            }
        }

        // --- COMANDOS FINANCEIRO ---
        [RelayCommand]
        public void AdicionarConta()
        {
            if (string.IsNullOrWhiteSpace(FinValor) || string.IsNullOrWhiteSpace(FinHistorico)) return;
            
            decimal valor = 0;
            if (!decimal.TryParse(FinValor.Replace("R$", "").Trim(), out valor))
            {
                MessageBox.Show("Valor inválido"); return;
            }

            decimal alvara = FinTipo == "Alvará" ? valor : 0;
            decimal despesa = FinTipo == "Despesa" ? valor : 0;

            using var conn = _db.GetConnection();
            conn.Execute(@"INSERT INTO contas (id, processo_id, data_movimentacao, tipo_lancamento, historico, valor_alvara, valor_conta, responsavel) 
                           VALUES (@id, @pid, @dt, @tipo, @hist, @alv, @desp, @resp)",
                           new { 
                               id = Guid.NewGuid().ToString(), pid = _processoId, dt = FinData, tipo = FinTipo, hist = FinHistorico,
                               alv = alvara, desp = despesa, resp = Application.Current.Properties["Usuario"]
                           });

            FinHistorico = ""; FinValor = "";
            CarregarFinanceiro(conn);
        }

        [RelayCommand]
        public void RemoverConta(string id)
        {
            if (MessageBox.Show("Remover lançamento?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM contas WHERE id = @id", new { id });
                CarregarFinanceiro(conn);
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
                        page.Header().Text($"Relatório Financeiro - Processo {Processo.Numero}").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                        
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

                            foreach (var item in Contas)
                            {
                                table.Cell().Text(item.Data);
                                table.Cell().Text(item.Historico);
                                table.Cell().AlignRight().Text(item.ValorAlvara > 0 ? $"{item.ValorAlvara:N2}" : "-").FontColor(Colors.Green.Medium);
                                table.Cell().AlignRight().Text(item.ValorConta > 0 ? $"{item.ValorConta:N2}" : "-").FontColor(Colors.Red.Medium);
                            }
                        });

                        page.Footer().AlignCenter().Text(x => {
                            x.Span("Gerado em ");
                            x.Span(DateTime.Now.ToString("g"));
                        });
                    });
                })
                .GeneratePdf(path);

                MessageBox.Show($"PDF gerado na Área de Trabalho:\n{path}", "Sucesso");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar PDF: {ex.Message}\nVerifique se o arquivo não está aberto.");
            }
        }

        // --- COMANDOS GERAIS ---
        [RelayCommand]
        public void SalvarVerificacao()
        {
            // (Mesma lógica do código anterior...)
            // Devido ao limite de tamanho, mantenha a lógica de SalvarVerificacao que já forneci antes, 
            // apenas certifique-se de usar _db = App.DB! no construtor.
        }

        [RelayCommand]
        public void Voltar()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        }

        private (string, SolidColorBrush) CalcularStatus(string? dataStr)
        {
             if (DateTime.TryParse(dataStr, out DateTime d))
             {
                 var dias = (d.Date - DateTime.Now.Date).TotalDays;
                 if (dias < 0) return ("ATRASADO", Brushes.Red);
                 if (dias == 0) return ("VENCE HOJE", Brushes.OrangeRed);
                 return ("No Prazo", Brushes.Green);
             }
             return ("--", Brushes.Gray);
        }
    }
}
