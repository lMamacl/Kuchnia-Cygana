namespace KuchniaUCygana.Domain.Enums;

[Flags]
public enum PackingIncidentReasonFlag
{
    None = 0,
    BoxDamaged = 1,
    BoxLeaking = 2,
    BoxMissing = 4,
    LabelUnreadable = 8,
    WrongMeal = 16,
    BagTorn = 32,
    BagDirty = 64,
    BagClosureDamaged = 128,
    TransportLabelDamaged = 256,
    BagMissing = 512,
    Other = 1024,
}
