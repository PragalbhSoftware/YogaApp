namespace YogaMarketplace.Domain;

public class DomainException : Exception
{
    public int StatusCode { get; }
    public string? Code { get; }

    public DomainException(string message, int statusCode = 400, string? code = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

/// <summary>A captured payment whose amount or currency differs from the order. Retrying can never fix it.</summary>
public sealed class PaymentMismatchException(string message) : DomainException(message);
