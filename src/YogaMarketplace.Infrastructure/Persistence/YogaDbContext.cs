using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

public class YogaDbContext : DbContext
{
    private readonly bool _sqlite;

    public YogaDbContext(DbContextOptions<YogaDbContext> options) : base(options)
    {
        _sqlite = options.Extensions.Any(e => e.GetType().Name.Contains("Sqlite", StringComparison.Ordinal));
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CheckoutIntent> CheckoutIntents => Set<CheckoutIntent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PayoutPending> PayoutsPending => Set<PayoutPending>();
    public DbSet<MarketplacePolicy> Policies => Set<MarketplacePolicy>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelConfiguration.Configure(modelBuilder, _sqlite);
    }
}
