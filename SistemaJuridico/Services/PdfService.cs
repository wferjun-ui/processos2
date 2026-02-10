using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaJuridico.Models;

namespace SistemaJuridico.Services
{
    public class PdfService
    {
        public void GerarRelatorioProcesso(
            string caminhoArquivo,
            string numeroProcesso,
            string paciente,
            string fase,
            string observacao,
            List<ItemSaudeModel> itens,
            List<ContaModel> contas)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header().Text($"Processo: {numeroProcesso}")
                        .FontSize(20).Bold();

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Paciente: {paciente}");
                        col.Item().Text($"Fase: {fase}");
                        col.Item().Text($"Observação: {observacao}");

                        col.Item().PaddingTop(15).Text("Itens de Saúde")
                            .FontSize(16).Bold();

                        foreach (var i in itens)
                        {
                            col.Item().Text(
                                $"{i.Tipo} - {i.Nome} | Qtd: {i.Qtd} | Freq: {i.Frequencia}");
                        }

                        col.Item().PaddingTop(15).Text("Financeiro")
                            .FontSize(16).Bold();

                        foreach (var c in contas)
                        {
                            col.Item().Text(
                                $"{c.Data} - {c.Tipo} | Alvará: {c.ValorAlvara} | Conta: {c.ValorConta}");
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Gerado em ");
                            x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                        });
                });
            })
            .GeneratePdf(caminhoArquivo);
        }
    }
}
