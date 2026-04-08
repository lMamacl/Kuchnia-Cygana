namespace KuchniaUCygana.Infrastructure.Pdf;

public interface IPdfGenerator
{
    byte[] Generate(string title, string content);
}
