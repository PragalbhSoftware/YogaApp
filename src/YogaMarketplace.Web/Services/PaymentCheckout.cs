using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

public interface IPaymentCheckout
{
    bool UseFakeCheckout { get; }

    Task<ApiResult<CheckoutDraft>> BeginAsync(BeginCheckout begin, CancellationToken cancellationToken);

    Task<ApiResult<BookingDto>> CaptureLocalAsync(CheckoutDraft draft, CancellationToken cancellationToken);

    Task<ApiResult<BookingDto>> ConfirmGatewayAsync(
        string? orderId,
        string? paymentId,
        string? signature,
        CancellationToken cancellationToken);
}

public sealed class PaymentCheckout : IPaymentCheckout
{
    private readonly IBookingApi _bookings;
    private readonly ILocalRazorpayCheckout _local;
    private readonly ILogger<PaymentCheckout> _logger;

    public PaymentCheckout(IBookingApi bookings, ILocalRazorpayCheckout local, ILogger<PaymentCheckout> logger)
    {
        _bookings = bookings;
        _local = local;
        _logger = logger;
    }

    public bool UseFakeCheckout => _local.Enabled;

    public async Task<ApiResult<CheckoutDraft>> BeginAsync(BeginCheckout begin, CancellationToken cancellationToken)
    {
        var mode = SessionModes.Normalize(begin.Mode);
        if (mode is null)
            return ApiResult<CheckoutDraft>.Fail(UiCopy.ModeRequired);

        string? address = null;
        string? landmark = null;
        if (SessionModes.IsHome(mode))
        {
            var validation = ValidateHome(begin.HomeAddress, begin.Landmark);
            if (validation is not null)
                return ApiResult<CheckoutDraft>.Fail(validation);
            address = begin.HomeAddress!.Trim();
            landmark = begin.Landmark!.Trim();
        }

        var order = await _bookings.CreateOrderAsync(new CreateBookingOrderDto
        {
            SlotId = begin.SlotId,
            HomeAddress = address,
            Landmark = landmark
        }, cancellationToken);

        if (!order.Ok || order.Data is null)
        {
            return order.Unreachable
                ? ApiResult<CheckoutDraft>.Down(order.Error ?? UiCopy.ApiUnreachable)
                : ApiResult<CheckoutDraft>.Fail(order.Error ?? UiCopy.GenericError);
        }

        _logger.LogInformation("Opened checkout {OrderId} for slot {SlotId}.", order.Data.OrderId, order.Data.SlotId);

        return ApiResult<CheckoutDraft>.Success(new CheckoutDraft(
            order.Data.CheckoutId,
            order.Data.KeyId,
            order.Data.OrderId,
            order.Data.AmountPaise,
            order.Data.Amount,
            order.Data.Currency,
            begin.ProviderId,
            order.Data.SlotId,
            string.IsNullOrWhiteSpace(order.Data.ProviderName) ? begin.ProviderName : order.Data.ProviderName,
            string.IsNullOrWhiteSpace(order.Data.Mode) ? mode : order.Data.Mode,
            begin.Date,
            begin.Start,
            begin.End,
            address,
            landmark));
    }

    public async Task<ApiResult<BookingDto>> CaptureLocalAsync(CheckoutDraft draft, CancellationToken cancellationToken)
    {
        if (!_local.Enabled)
            return ApiResult<BookingDto>.Fail(UiCopy.LocalCheckoutMisconfigured);

        string paymentId;
        string signature;
        try
        {
            paymentId = _local.CreatePaymentId();
            signature = _local.Sign(draft.OrderId, paymentId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Local Razorpay checkout could not sign the payment.");
            return ApiResult<BookingDto>.Fail(UiCopy.LocalCheckoutMisconfigured);
        }

        return await ConfirmGatewayAsync(draft.OrderId, paymentId, signature, cancellationToken);
    }

    public async Task<ApiResult<BookingDto>> ConfirmGatewayAsync(
        string? orderId,
        string? paymentId,
        string? signature,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
            return ApiResult<BookingDto>.Fail(UiCopy.PaymentIncomplete);

        var confirmed = await _bookings.ConfirmAsync(new ConfirmPaymentDto
        {
            OrderId = orderId.Trim(),
            PaymentId = paymentId.Trim(),
            Signature = signature.Trim()
        }, cancellationToken);

        if (confirmed.Ok && confirmed.Data is not null)
            _logger.LogInformation("Confirmed Razorpay order {OrderId} as booking {BookingId}.", orderId, confirmed.Data.Id);

        return confirmed;
    }

    private static string? ValidateHome(string? address, string? landmark)
    {
        if (string.IsNullOrWhiteSpace(address))
            return UiCopy.AddressRequired;
        if (address.Trim().Length > HomeVisitLimits.AddressMaxLength)
            return UiCopy.AddressTooLong;
        if (string.IsNullOrWhiteSpace(landmark))
            return UiCopy.LandmarkRequired;
        if (landmark.Trim().Length > HomeVisitLimits.LandmarkMaxLength)
            return UiCopy.LandmarkTooLong;
        return null;
    }
}
