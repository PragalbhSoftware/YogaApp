using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<UserBlockEvent> UserBlockEvents => Set<UserBlockEvent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ProviderPhoto> ProviderPhotos => Set<ProviderPhoto>();
    public DbSet<ProviderCertificate> ProviderCertificates => Set<ProviderCertificate>();
    public DbSet<CheckoutIntent> CheckoutIntents => Set<CheckoutIntent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PayoutPending> PayoutsPending => Set<PayoutPending>();
    public DbSet<PlatformSettings> Settings => Set<PlatformSettings>();
    public DbSet<SettingsAudit> SettingsAudits => Set<SettingsAudit>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<CreditPack> CreditPacks => Set<CreditPack>();
    public DbSet<CreditBatch> CreditBatches => Set<CreditBatch>();
    public DbSet<CreditLedgerEntry> CreditLedger => Set<CreditLedgerEntry>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (!_sqlite)
            return;
        // SQLite stores DateTimeOffset as text it cannot order or compare; tests run on SQLite, production on SQL Server.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelConfiguration.Configure(modelBuilder, _sqlite);
    }
}
