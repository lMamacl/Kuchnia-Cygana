using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class CheckoutService : ICheckoutService
{
    private readonly IOrderRepository orderRepository;
    private readonly IPaymentRepository paymentRepository;
    private readonly IPaymentService paymentService;
    private readonly IAddressRepository addressRepository;
    private readonly IMapper mapper;

    public CheckoutService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        IAddressRepository addressRepository,
        IMapper mapper)
    {
        this.orderRepository = orderRepository;
        this.paymentRepository = paymentRepository;
        this.paymentService = paymentService;
        this.addressRepository = addressRepository;
        this.mapper = mapper;
    }

    public async Task<CheckoutSummaryDto> BuildSummaryAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetWithItemsAndDeliveryAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            throw new KeyNotFoundException($"Zamowienie {orderId} nie istnieje.");

        var orderDto = mapper.Map<OrderDto>(order);

        // Uzupelnij adresy w dniach dostawy
        AddressDto? primaryAddress = null;
        if (order.DeliveryDays.Any())
        {
            var firstAddressId = order.DeliveryDays.First().AddressId;
            var addr = await addressRepository.GetByIdAsync(firstAddressId);
            if (addr is not null)
                primaryAddress = mapper.Map<AddressDto>(addr);

            var addressCache = new Dictionary<int, string>();
            foreach (var addrId in order.DeliveryDays.Select(d => d.AddressId).Distinct())
            {
                var a = await addressRepository.GetByIdAsync(addrId);
                addressCache[addrId] = a?.FullAddress ?? string.Empty;
            }
            foreach (var day in orderDto.DeliveryDays)
            {
                if (addressCache.TryGetValue(day.AddressId, out var fullAddr))
                    day.AddressFullLine = fullAddr;
            }
        }

        return new CheckoutSummaryDto
        {
            OrderId = orderId,
            Items = orderDto.Items,
            DeliveryDays = orderDto.DeliveryDays,
            SelectedAddress = primaryAddress,
            TotalPrice = order.TotalPrice,
            DiscountAmount = order.DiscountAmount,
            FinalPrice = order.FinalPrice,
        };
    }

    public async Task<string> InitiatePaymentAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            throw new ArgumentException("Zamowienie nie istnieje.");

        if (order.Status == OrderStatus.Paid)
            throw new InvalidOperationException("Zamowienie jest juz oplacone.");

        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Nie mozna oplacic anulowanego zamowienia.");

        if (order.FinalPrice <= 0m)
            throw new InvalidOperationException("Kwota platnosci musi byc wieksza od zera.");

        // Sprawdzamy, czy dla tego zamówienia istnieje już rozpoczęta sesja płatności
        var existingPayment = await paymentRepository.GetByOrderIdAsync(orderId);
        if (existingPayment is not null && !string.IsNullOrEmpty(existingPayment.StripeClientSecret))
        {
            if (existingPayment.Status == PaymentStatus.Succeeded)
                throw new InvalidOperationException("Platnosc dla tego zamowienia zostala juz zakonczona.");

            if (existingPayment.AttemptCount >= 3)
                throw new InvalidOperationException("Przekroczono limit nieudanych prob platnosci.");

            existingPayment.Status = PaymentStatus.Pending;
            existingPayment.ErrorMessage = null;
            existingPayment.LastAttemptAt = DateTimeOffset.UtcNow;
            await paymentRepository.UpdateAsync(existingPayment);

            order.Status = OrderStatus.PendingPayment;
            await orderRepository.UpdateAsync(order);

            return existingPayment.StripeClientSecret;
        }

        // Prawdziwa integracja Stripe SDK - kwota idzie do IPaymentService
        var (paymentIntentId, clientSecret) = await paymentService.CreatePaymentIntentAsync(order.FinalPrice);

        // Zapis Payment w bazie ze statusem Pending
        var payment = new Payment
        {
            OrderId = orderId,
            StripePaymentIntentId = paymentIntentId,
            StripeClientSecret = clientSecret,
            Amount = order.FinalPrice,
            Currency = "PLN",
            Status = PaymentStatus.Pending,
            AttemptCount = 0,
            LastAttemptAt = DateTimeOffset.UtcNow,
        };
        await paymentRepository.InsertAsync(payment);

        // Ustawienie Order.Status = PendingPayment
        order.Status = OrderStatus.PendingPayment;
        await orderRepository.UpdateAsync(order);

        return clientSecret;
    }

    public async Task<bool> ConfirmPaymentAsync(string paymentIntentId)
    {
        var payment = await paymentRepository.GetByStripeIntentIdAsync(paymentIntentId);
        if (payment is null) return false;

        if (payment.Status == PaymentStatus.Succeeded)
        {
            await MarkOrderAsPaidAsync(payment.OrderId);
            return true;
        }

        // Weryfikacja statusu w Stripe API
        var isVerified = await paymentService.VerifyPaymentIntentAsync(paymentIntentId);
        if (!isVerified) return false;

        // Aktualizacja statusu płatności
        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = DateTimeOffset.UtcNow;
        await paymentRepository.UpdateAsync(payment);

        await MarkOrderAsPaidAsync(payment.OrderId);

        return true;
    }

    public async Task<bool> HandlePaymentFailureAsync(string paymentIntentId, string errorMessage)
    {
        var payment = await paymentRepository.GetByStripeIntentIdAsync(paymentIntentId);
        if (payment is null) return false;

        payment.AttemptCount++;
        payment.Status = PaymentStatus.Failed;
        payment.ErrorMessage = errorMessage;
        payment.LastAttemptAt = DateTimeOffset.UtcNow;
        await paymentRepository.UpdateAsync(payment);

        // Jesli AttemptCount >= 3, anulujemy zamowienie definitywnie.
        if (payment.AttemptCount >= 3)
        {
            var order = await orderRepository.GetByIdAsync(payment.OrderId);
            if (order is not null)
            {
                order.Status = OrderStatus.Cancelled;
                await orderRepository.UpdateAsync(order);
            }
            return false;
        }

        return true;
    }

    private async Task MarkOrderAsPaidAsync(int orderId)
    {
        // Aktualizacja statusu zamowienia i przekazanie do M3 (InProduction)
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is not null)
        {
            // Przejscie z planu projektu: Order.Status = Paid, a system ERP/M3 widzi to jako gotowe do produkcji.
            order.Status = OrderStatus.Paid;
            await orderRepository.UpdateAsync(order);

            // Tutaj mozna opcjonalnie wywolac event domenowy lub serwis zewnetrzny przekazujacy do M3:
            // await _productionIntegrationService.NotifyNewOrderAsync(order.Id);
        }
    }
}
