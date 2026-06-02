/*
 * Plik: Entities/Admin/TicketAttachment.cs
 * Opis: Załączniki do zgłoszeń – pliki (np. screeny, dokumenty) powiązane z konkretnym ticketem.
 *       Przechowuje nazwę pliku i ścieżkę na serwerze.
 */

using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;


namespace KuchniaUCygana.Domain.Entities.Admin;


[Table("TicketAttachments")]
public sealed class TicketAttachment : BaseEntity
{

    public int TicketId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;


    public int UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
