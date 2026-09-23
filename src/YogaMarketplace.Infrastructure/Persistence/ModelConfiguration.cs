using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

internal static class ModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder, bool sqlite = false)
    {
        ConfigureCategory(modelBuilder.Entity<Category>());
        ConfigureArea(modelBuilder.Entity<Area>());
        ConfigureUser(modelBuilder.Entity<User>());
        ConfigureProvider(modelBuilder.Entity<Provider>());
        ConfigureService(modelBuilder.Entity<Service>());
        ConfigureSlot(modelBuilder.Entity<AvailabilitySlot>());
        ConfigureBooking(modelBuilder.Entity<Booking>());
        ConfigureCheckout(modelBuilder.Entity<CheckoutIntent>());
        ConfigurePayment(modelBuilder.Entity<Payment>(), sqlite);
        ConfigureReview(modelBuilder.Entity<Review>());
        ConfigurePayout(modelBuilder.Entity<PayoutPending>());
        ConfigurePolicy(modelBuilder.Entity<MarketplacePolicy>());
        ConfigureOtp(modelBuilder.Entity<OtpChallenge>());
    }

    private static void ConfigureCategory(EntityTypeBuilder<Category> entity)
    {
        entity.Property(c => c.Name).HasMaxLength(80);
        entity.Property(c => c.Slug).HasMaxLength(40);
        entity.HasIndex(c => c.Slug).IsUnique();
    }

    private static void ConfigureArea(EntityTypeBuilder<Area> entity)
    {
        entity.Property(a => a.City).HasMaxLength(40);
        entity.Property(a => a.Name).HasMaxLength(80);
        entity.HasIndex(a => new { a.City, a.Name }).IsUnique();
    }

    private static void ConfigureUser(EntityTypeBuilder<User> entity)
    {
        entity.Property(u => u.Phone).HasMaxLength(16);
        entity.Property(u => u.Name).HasMaxLength(80);
        entity.Property(u => u.Email).HasMaxLength(200);
        entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        entity.Property(u => u.Gender).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(u => u.Phone).IsUnique();
    }

    private static void ConfigureProvider(EntityTypeBuilder<Provider> entity)
    {
        entity.Property(p => p.DisplayName).HasMaxLength(80);
        entity.Property(p => p.Bio).HasMaxLength(1000);
        entity.Property(p => p.StudioAddress).HasMaxLength(300);
        entity.Property(p => p.GoogleMeetLink).HasMaxLength(300);
        entity.Property(p => p.RejectionReason).HasMaxLength(300);
        entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        entity.Property(p => p.HomeRate).HasPrecision(10, 2);
        entity.Property(p => p.StudioRate).HasPrecision(10, 2);
        entity.Property(p => p.OnlineRate).HasPrecision(10, 2);
        entity.HasIndex(p => p.UserId).IsUnique();

        entity.HasOne(p => p.User)
            .WithOne(u => u.Provider)
            .HasForeignKey<Provider>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(p => p.Area)
            .WithMany()
            .HasForeignKey(p => p.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureService(EntityTypeBuilder<Service> entity)
    {
        entity.Property(s => s.Title).HasMaxLength(120);
        entity.HasIndex(s => new { s.ProviderId, s.CategoryId }).IsUnique();

        entity.HasOne(s => s.Provider)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSlot(EntityTypeBuilder<AvailabilitySlot> entity)
    {
        entity.Property(s => s.Mode).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(s => new { s.ProviderId, s.Mode, s.Date, s.StartTime }).IsUnique();

        entity.HasOne(s => s.Provider)
            .WithMany(p => p.Slots)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureBooking(EntityTypeBuilder<Booking> entity)
    {
        entity.Property(b => b.Mode).HasConversion<string>().HasMaxLength(16);
        entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
        entity.Property(b => b.Amount).HasPrecision(10, 2);
        entity.Property(b => b.HomeAddress).HasMaxLength(300);
        entity.Property(b => b.Landmark).HasMaxLength(160);
        entity.Property(b => b.MeetLinkSnapshot).HasMaxLength(300);
        entity.Property(b => b.StudioAddressSnapshot).HasMaxLength(300);
        entity.HasIndex(b => new { b.ProviderId, b.Status });
        // Keep this list aligned with BookingRules.OccupiesSlot so a slot cannot be double-booked.
        entity.HasIndex(b => b.SlotId)
            .IsUnique()
            .HasFilter("Status IN ('PendingAccept', 'Upcoming', 'Completed', 'NoShow')");

        entity.HasOne(b => b.Customer).WithMany().HasForeignKey(b => b.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(b => b.Provider).WithMany().HasForeignKey(b => b.ProviderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(b => b.Service).WithMany().HasForeignKey(b => b.ServiceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(b => b.Slot).WithMany().HasForeignKey(b => b.SlotId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePayment(EntityTypeBuilder<Payment> entity, bool sqlite)
    {
        entity.Property(p => p.Amount).HasPrecision(10, 2);
        entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        entity.Property(p => p.Gateway).HasMaxLength(40);
        entity.Property(p => p.GatewayOrderId).HasMaxLength(80);
        entity.Property(p => p.GatewayPaymentId).HasMaxLength(80);
        entity.HasIndex(p => p.BookingId).IsUnique();
        // Payments are inserted only after capture, with a gateway id. The filter keeps
        // SQL Server able to store a row that has not been assigned an id yet.
        entity.HasIndex(p => p.GatewayPaymentId)
            .IsUnique()
            .HasFilter(sqlite ? "\"GatewayPaymentId\" IS NOT NULL" : "[GatewayPaymentId] IS NOT NULL");

        entity.HasOne(p => p.Booking)
            .WithOne(b => b.Payment)
            .HasForeignKey<Payment>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCheckout(EntityTypeBuilder<CheckoutIntent> entity)
    {
        entity.Property(c => c.Mode).HasConversion<string>().HasMaxLength(16);
        entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        entity.Property(c => c.Amount).HasPrecision(10, 2);
        entity.Property(c => c.Currency).HasMaxLength(8);
        entity.Property(c => c.HomeAddress).HasMaxLength(300);
        entity.Property(c => c.Landmark).HasMaxLength(160);
        entity.Property(c => c.Gateway).HasMaxLength(40);
        entity.Property(c => c.GatewayOrderId).HasMaxLength(80);
        entity.HasIndex(c => c.GatewayOrderId).IsUnique();
        entity.HasIndex(c => new { c.CustomerId, c.Status });

        entity.HasOne<User>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Provider>().WithMany().HasForeignKey(c => c.ProviderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Service>().WithMany().HasForeignKey(c => c.ServiceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<AvailabilitySlot>().WithMany().HasForeignKey(c => c.SlotId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Booking>().WithMany().HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureReview(EntityTypeBuilder<Review> entity)
    {
        entity.Property(r => r.Comment).HasMaxLength(1000);
        entity.HasIndex(r => r.BookingId).IsUnique();

        entity.HasOne(r => r.Booking)
            .WithOne(b => b.Review)
            .HasForeignKey<Review>(r => r.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<User>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Provider>().WithMany().HasForeignKey(r => r.ProviderId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePayout(EntityTypeBuilder<PayoutPending> entity)
    {
        entity.ToTable("PayoutsPending");
        entity.Property(p => p.GrossAmount).HasPrecision(10, 2);
        entity.Property(p => p.FeeAmount).HasPrecision(10, 2);
        entity.Property(p => p.NetAmount).HasPrecision(10, 2);
        entity.Property(p => p.FeePercent).HasPrecision(5, 2);
        entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(p => p.BookingId).IsUnique();

        entity.HasOne(p => p.Booking)
            .WithOne(b => b.Payout)
            .HasForeignKey<PayoutPending>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<Provider>().WithMany().HasForeignKey(p => p.ProviderId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePolicy(EntityTypeBuilder<MarketplacePolicy> entity)
    {
        entity.Property(p => p.Currency).HasMaxLength(8);
        entity.Property(p => p.PolicyNote).HasMaxLength(400);
        entity.Property(p => p.PlatformFeePercent).HasPrecision(5, 2);
        entity.Property(p => p.LateCancelFeePercent).HasPrecision(5, 2);
    }

    private static void ConfigureOtp(EntityTypeBuilder<OtpChallenge> entity)
    {
        entity.Property(c => c.Phone).HasMaxLength(16);
        entity.Property(c => c.CodeHash).HasMaxLength(64);
        entity.Property(c => c.PendingName).HasMaxLength(80);
        entity.Property(c => c.PendingGender).HasConversion<string>().HasMaxLength(16);
        entity.Property(c => c.IntendedRole).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(c => new { c.Phone, c.CreatedAt });
    }
}
