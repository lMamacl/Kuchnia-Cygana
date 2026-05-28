using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Application.Mappings;

public sealed class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<Order, OrderSummaryDto>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<DeliveryCalendar, DeliveryCalendarDto>()
            .ForMember(d => d.AddressFullLine, o => o.Ignore())
            .ForMember(d => d.DeliveryWindowName, o => o.Ignore());
        CreateMap<Address, AddressDto>()
            .ForMember(d => d.FullAddress, o => o.MapFrom(s => s.FullAddress));
        CreateMap<CustomerProfile, CustomerProfileDto>();
        CreateMap<DeliveryWindow, DeliveryWindowDto>();
        CreateMap<CreateAddressRequest, Address>();
        CreateMap<UpdateAddressRequest, Address>()
            .ForMember(d => d.IsDefault, o => o.Ignore())
            .ForMember(d => d.UserId, o => o.Ignore());
        CreateMap<CreateOrderItemRequest, OrderItem>();
    }
}
