using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SistemaJuridico.Services
{
    public static class ProcessLogic
    {
        public static string FormatCNJ(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string v = Regex.Replace(value, @"\D", "");
            if (v.Length > 20) v = v.Substring(0, 20);
            if (v.Length > 16) return $"{v[..7]}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14, 2)}.{v[16..]}";
            return v; 
        }

        // Lógica Exata do Python: 14 dias + Ajuste para Segunda-feira
        public static (string proximoPrazo, string dataNotificacao) CalculateDueDates(string? dataBaseStr, string? manualDateStr = null)
        {
            if (!string.IsNullOrWhiteSpace(manualDateStr) && DateTime.TryParseExact(manualDateStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime manual))
                return (manual.ToString("dd/MM/yyyy"), manual.AddDays(-7).ToString("dd/MM/yyyy"));

            DateTime baseDate = DateTime.Now;
            if (DateTime.TryParseExact(dataBaseStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime parsed)) baseDate = parsed;

            DateTime futureDate = baseDate.AddDays(14);
            int pythonWeekday = (int)futureDate.DayOfWeek == 0 ? 6 : (int)futureDate.DayOfWeek - 1; // Dom=6 no Python
            int daysAhead = (7 - pythonWeekday) % 7;
            if (daysAhead == 0) daysAhead = 7; 

            DateTime proximoPrazo = futureDate.AddDays(daysAhead);
            return (proximoPrazo.ToString("dd/MM/yyyy"), proximoPrazo.AddDays(-7).ToString("dd/MM/yyyy"));
        }

        public static (string texto, SolidColorBrush cor) CheckPrazoStatus(string proximoPrazoStr)
        {
            if (string.IsNullOrEmpty(proximoPrazoStr)) return ("Sem Prazo", new SolidColorBrush(Colors.Gray));
            if (DateTime.TryParseExact(proximoPrazoStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime prazo))
            {
                int diff = (prazo.Date - DateTime.Now.Date).Days;
                if (diff < 0) return ("ATRASADO", new SolidColorBrush(Colors.Red));
                if (diff == 0) return ("VENCE HOJE", new SolidColorBrush(Colors.Orange));
                if (diff <= 7) return ($"Vence em {diff} dias", new SolidColorBrush(Colors.Orange));
                return ("No Prazo", new SolidColorBrush(Colors.Green));
            }
            return ("Data Inválida", new SolidColorBrush(Colors.Gray));
        }

        public static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string clean = Regex.Replace(value, @"[^\d,]", "");
            if (decimal.TryParse(clean, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal result)) return result;
            return 0;
        }

        // Geração de PDF (Replicando o layout de tabela do Python com QuestPDF)
        public static void GeneratePdfReport(string processoNum, string paciente, IEnumerable<dynamic> contas, string path)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            Document.Create(container => {
                container.Page(page => {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.Header().Row(row => {
                        row.RelativeItem().Column(col => {
                            col.Item().Text("RELATÓRIO DE PRESTAÇÃO DE CONTAS").FontSize(18).Bold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Processo: {processoNum} | Paciente: {paciente}").FontSize(12);
                        });
                    });
                    
                    page.Content().PaddingVertical(10).Table(table => {
                        table.ColumnsDefinition(columns => {
                            columns.ConstantColumn(70); // Data
                            columns.ConstantColumn(80); // Mov
                            columns.RelativeColumn();   // Histórico
                            columns.ConstantColumn(80); // Crédito
                            columns.ConstantColumn(80); // Débito
                            columns.ConstantColumn(80); // Saldo
                        });

                        table.Header(header => {
                            header.Cell().Element(HeaderStyle).Text("Data");
                            header.Cell().Element(HeaderStyle).Text("Mov.");
                            header.Cell().Element(HeaderStyle).Text("Histórico Detalhado");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Alvará");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Contas");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Saldo");
                        });

                        decimal saldo = 0, totalCred = 0, totalDeb = 0;
                        foreach (var c in contas) {
                            decimal cred = (decimal)c.valor_alvara;
                            decimal deb = (decimal)c.valor_conta;
                            saldo += (cred - deb); totalCred += cred; totalDeb += deb;

                            table.Cell().Element(CellStyle).Text(c.data_movimentacao);
                            table.Cell().Element(CellStyle).Text(c.mov_processo ?? "-");
                            table.Cell().Element(CellStyle).Text(c.historico);
                            table.Cell().Element(CellStyle).AlignRight().Text(cred > 0 ? $"{cred:N2}" : "-").FontColor(Colors.Green.Medium);
                            table.Cell().Element(CellStyle).AlignRight().Text(deb > 0 ? $"{deb:N2}" : "-").FontColor(Colors.Red.Medium);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{saldo:N2}").Bold();
                        }
                        
                        // Linha de Totais
                        table.Cell().ColumnSpan(3).Element(HeaderStyle).AlignRight().Text("TOTAIS:").Bold();
                        table.Cell().Element(HeaderStyle).AlignRight().Text($"{totalCred:N2}").FontColor(Colors.Green.Medium);
                        table.Cell().Element(HeaderStyle).AlignRight().Text($"{totalDeb:N2}").FontColor(Colors.Red.Medium);
                        table.Cell().Element(HeaderStyle).AlignRight().Text($"{saldo:N2}").Bold();
                    });
                });
            }).GeneratePdf(path);
        }

        static IContainer HeaderStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Light).Padding(5).DefaultTextStyle(x => x.Bold());
        static IContainer CellStyle(IContainer container) => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).DefaultTextStyle(x => x.FontSize(10));
    }
}
