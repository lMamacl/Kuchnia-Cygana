namespace KuchniaUCygana.Application.Configuration;

public sealed class PackingResourcesOptions
{
    public int? TransportBagStockItemId { get; set; }

    public decimal TransportBagWasteQuantity { get; set; } = 1m;

    public int? BoxContainerStockItemId { get; set; }

    public decimal BoxContainerWasteQuantity { get; set; } = 1m;
}
