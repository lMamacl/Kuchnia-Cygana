using System;
using KuchniaUCygana.Domain.Common.Interfaces;

namespace KuchniaUCygana.Domain.Common;

public abstract class AuditableEntity<TId> : BaseEntity<TId>, ISoftDeletable
{
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public abstract class AuditableEntity : AuditableEntity<int>
{
}
