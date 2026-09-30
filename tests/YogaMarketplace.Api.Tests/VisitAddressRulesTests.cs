using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class VisitAddressRulesTests
{
    [Fact]
    public void Set_stores_a_complete_India_home_address()
    {
        var user = Customer();
        VisitAddressRules.Set(user, "  12 Hill Road  ", "Bandra West", "Mumbai", "400050", "Near the station");

        Assert.True(VisitAddressRules.IsComplete(user));
        Assert.Equal("12 Hill Road", user.HomeLine1);
        Assert.Equal("400050", user.HomePin);
        Assert.Equal("12 Hill Road, Bandra West, Mumbai 400050", VisitAddressRules.Compose(
            user.HomeLine1!, user.HomeArea!, user.HomeCity!, user.HomePin!));

        var booking = VisitAddressRules.ForBooking(user);
        Assert.Equal("12 Hill Road, Bandra West, Mumbai 400050", booking.HomeAddress);
        Assert.Equal("Near the station", booking.Landmark);
    }

    [Fact]
    public void Set_rejects_an_incomplete_or_invalid_address()
    {
        var user = Customer();
        Assert.False(VisitAddressRules.IsComplete(user));
        Assert.Throws<DomainException>(() => VisitAddressRules.ForBooking(user));

        var missingStreet = Assert.Throws<DomainException>(() =>
            VisitAddressRules.Set(user, "12", "Bandra West", "Mumbai", "400050", "Near the station"));
        Assert.Contains("house", missingStreet.Message, StringComparison.OrdinalIgnoreCase);

        var badPin = Assert.Throws<DomainException>(() =>
            VisitAddressRules.Set(user, "12 Hill Road", "Bandra West", "Mumbai", "050001", "Near the station"));
        Assert.Contains("PIN", badPin.Message, StringComparison.OrdinalIgnoreCase);

        Assert.False(VisitAddressRules.IsComplete(user));
    }

    private static User Customer() => new()
    {
        Id = Guid.NewGuid(),
        Phone = "+919800000001",
        Name = "Rahul Sharma",
        Role = UserRole.Customer,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
