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
                page.Margin(24);
                page.Content().Column(column =>
                {
                    column.Item().Text(title).FontSize(20).SemiBold();
                    column.Item().PaddingTop(8).Text(content);
                });
            });
        }).GeneratePdf();
    }
}
