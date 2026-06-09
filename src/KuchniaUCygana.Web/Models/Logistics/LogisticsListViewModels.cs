using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class VehicleListFilterViewModel : StaffListFilterViewModel
{
    public string? Status { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        !string.IsNullOrWhiteSpace(this.Status);

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(this.Status), this.Status);
        return values;
    }
}

public sealed class VehicleListViewModel
{
    public VehicleListFilterViewModel Filter { get; init; } = new();

    public VehiclePageDto Page { get; init; } = new();

    public IReadOnlyList<VehicleDto> Vehicles => this.Page.Items;
}

public sealed class DriverListFilterViewModel : StaffListFilterViewModel
{
    public bool? IsActive { get; set; }

    public bool? HasVehicleAssignment { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        this.IsActive.HasValue ||
        this.HasVehicleAssignment.HasValue;

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(this.IsActive), this.IsActive);
        AddIfSet(values, nameof(this.HasVehicleAssignment), this.HasVehicleAssignment);
        return values;
    }
}

public sealed class DriverListViewModel
{
    public DriverListFilterViewModel Filter { get; init; } = new();

    public DriverPageDto Page { get; init; } = new();

    public IReadOnlyList<DriverDto> Drivers => this.Page.Items;
}
