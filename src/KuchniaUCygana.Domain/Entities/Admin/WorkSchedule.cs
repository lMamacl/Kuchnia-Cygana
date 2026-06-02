/*
 * Plik: Entities/Admin/WorkSchedule.cs
 * Opis: Grafik pracy pracowników – określa datę, rodzaj zmiany (Morning/Afternoon/Night) oraz opcjonalną rolę.
 *       Unikalność: jeden użytkownik może mieć tylko jeden wpis na daną datę i zmianę.
 */

using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Admin;

public sealed class WorkSchedule : AuditableEntity
{
    public int UserId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public WorkShift Shift { get; set; }
    public string? RoleAtShift { get; set; }
}