namespace YogaMarketplace.Domain;

public class DomainException : Exception
{
    public int StatusCode { get; }

    public DomainException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>A captured payment whose amount or currency differs from the order. Retrying can never fix it.</summary>
public sealed class PaymentMismatchException(string message) : DomainException(message);
