using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KuchniaUCygana.Infrastructure.Pdf;

public sealed class QuestPdfGenerator : IPdfGenerator
{
    public byte[] Generate(string title, string content)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(12);
                page.Content().Column(column =>
                {
                    column.Item().Text(title).FontSize(14).SemiBold().FontFamily("Courier New");
                    column.Item().PaddingTop(6).Text(content).FontSize(9).FontFamily("Courier New");
                });
            });
        }).GeneratePdf();
    }
}
