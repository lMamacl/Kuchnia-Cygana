/*
 * Plik: Entities/HR/Department.cs
 * Opis: Departament w firmie – nazwa, opis oraz referencja do szefa (HeadEmployeeId).
 */
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.HR;

public sealed class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
        public int? HeadEmployeeId { get; set; }
}