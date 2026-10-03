using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class AdminRulesTests
{
    [Fact]
    public void Verify_clears_a_rejection_and_a_second_verify_does_not_move_the_review_time()
    {
        var provider = new Provider
        {
            Status = ProviderStatus.Rejected,
            RejectionReason = "Incomplete profile"
        };

        ProviderApproval.Verify(provider);
        Assert.Equal(ProviderStatus.Verified, provider.Status);
        Assert.Null(provider.RejectionReason);
        Assert.NotNull(provider.ReviewedAt);

        var reviewedAt = provider.ReviewedAt;
        ProviderApproval.Verify(provider);
        Assert.Equal(ProviderStatus.Verified, provider.Status);
        Assert.Equal(reviewedAt, provider.ReviewedAt);
    }

    [Fact]
    public void Reject_stores_an_optional_reason_and_can_take_a_verified_instructor_off_the_list()
    {
        var pending = new Provider { Status = ProviderStatus.Pending };
        ProviderApproval.Reject(pending, "  ");
        Assert.Equal(ProviderStatus.Rejected, pending.Status);
        Assert.Null(pending.RejectionReason);

        ProviderApproval.Reject(pending, "  Incomplete profile  ");
        Assert.Equal("Incomplete profile", pending.RejectionReason);

        var verified = new Provider { Status = ProviderStatus.Verified };
        ProviderApproval.Reject(verified, null);
        Assert.Equal(ProviderStatus.Rejected, verified.Status);
        Assert.Null(verified.RejectionReason);

        var tooLong = new string('x', ProviderApproval.MaxReasonLength + 1);
        var rejected = Assert.Throws<DomainException>(() => ProviderApproval.Reject(pending, tooLong));
        Assert.Contains("300", rejected.Message, StringComparison.Ordinal);
        Assert.Equal("Incomplete profile", pending.RejectionReason);
    }

    [Fact]
    public void Settings_apply_validates_leaves_omitted_fields_and_lists_only_real_changes()
    {
        var settings = Settings();

        var changes = PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(
            CommissionPercent: 10.5m,
            ConvenienceFee: 29m,
            CancelFreeWindowHours: 0));

        Assert.Equal(10.5m, settings.CommissionPercent);
        Assert.Equal(29m, settings.ConvenienceFee);
        Assert.Equal(0, settings.CancelFreeWindowHours);
        Assert.Equal(12, settings.RescheduleFreeWindowHours);
        Assert.Equal("TBD", settings.PolicyNote);
        Assert.Equal(
            new[] { "CommissionPercent", "ConvenienceFee", "CancelFreeWindowHours" },
            changes.Select(c => c.Field));
        Assert.Equal(new SettingChange("CommissionPercent", "15.00", "10.50"), changes[0]);

        Assert.Empty(PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(CommissionPercent: 10.5m)));

        var banner = PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(BannerTitle: "  Find calm  ", PolicyNote: "  Confirmed  "));
        Assert.Equal("Find calm", settings.BannerTitle);
        Assert.Equal("Confirmed", settings.PolicyNote);
        Assert.Equal(2, banner.Count);
        PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(BannerTitle: "   "));
        Assert.Null(settings.BannerTitle);
    }

    public static TheoryData<PlatformSettingsChanges> OutOfRange => new()
    {
        new PlatformSettingsChanges(CommissionPercent: 101m),
        new PlatformSettingsChanges(CommissionPercent: 15.555m),
        new PlatformSettingsChanges(ConvenienceFee: -1m),
        new PlatformSettingsChanges(ConvenienceFee: PlatformSettingsRules.MaxFlatAmount + 1),
        new PlatformSettingsChanges(CancelFreeWindowHours: -1),
        new PlatformSettingsChanges(CancelFreeWindowHours: PlatformSettingsRules.MaxWindowHours + 1),
        new PlatformSettingsChanges(LateCancelFeeValue: 101m),
        new PlatformSettingsChanges(BannerTitle: new string('x', PlatformSettingsRules.BannerTitleMax + 1)),
        new PlatformSettingsChanges(CommissionPercent: 10m, ConvenienceFee: -5m)
    };

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Settings_apply_rejects_out_of_range_values_and_changes_nothing(PlatformSettingsChanges changes)
    {
        var settings = Settings();
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, changes));
        Assert.Equal(15m, settings.CommissionPercent);
        Assert.Equal(0m, settings.ConvenienceFee);
        Assert.Equal(50m, settings.LateCancelFeeValue);
    }

    [Fact]
    public void Late_fee_value_is_checked_against_its_type()
    {
        var settings = Settings();
        Assert.Throws<DomainException>(() =>
            PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(LateCancelFeeValue: 250m)));

        PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(LateCancelFeeType: LateCancelFeeType.Flat, LateCancelFeeValue: 250m));
        Assert.Equal(LateCancelFeeType.Flat, settings.LateCancelFeeType);
        Assert.Equal(250m, settings.LateCancelFeeValue);

        Assert.Throws<DomainException>(() =>
            PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(LateCancelFeeType: LateCancelFeeType.Percent)));
        Assert.Equal(LateCancelFeeType.Flat, settings.LateCancelFeeType);
    }

    [Fact]
    public void Payout_periods_start_on_monday_india_time()
    {
        // Wednesday 8 Oct 2026, 10:00 IST.
        var wednesday = new DateTimeOffset(2026, 10, 8, 4, 30, 0, TimeSpan.Zero);
        var mondayIst = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.FromHours(5.5));
        Assert.Equal(mondayIst, PayoutCycleRules.CurrentPeriodStart(PayoutCycle.Weekly, wednesday));
        Assert.Equal(mondayIst.AddDays(7), PayoutCycleRules.NextPeriodStart(PayoutCycle.Weekly, wednesday));

        // Sunday 23:59 IST still belongs to the week that started on Monday.
        var sundayLate = new DateTimeOffset(2026, 10, 11, 23, 59, 0, TimeSpan.FromHours(5.5));
        Assert.Equal(mondayIst, PayoutCycleRules.CurrentPeriodStart(PayoutCycle.Weekly, sundayLate));

        // Biweekly blocks run from Monday 5 Jan 2026: 28 Sep and 12 Oct start blocks; 5 Oct does not.
        Assert.Equal(mondayIst.AddDays(-7), PayoutCycleRules.CurrentPeriodStart(PayoutCycle.Biweekly, wednesday));
        Assert.Equal(mondayIst.AddDays(7), PayoutCycleRules.NextPeriodStart(PayoutCycle.Biweekly, wednesday));
    }

    private static PlatformSettings Settings() => new()
    {
        Currency = "INR",
        CommissionPercent = 15m,
        CancelFreeWindowHours = 12,
        RescheduleFreeWindowHours = 12,
        LateCancelFeeType = LateCancelFeeType.Percent,
        LateCancelFeeValue = 50m,
        PolicyNote = "TBD"
    };

    [Fact]
    public void Area_create_requires_a_city_normalises_it_and_category_rename_does_not_touch_the_slug()
    {
        var area = CatalogRules.CreateArea(" goa ", "  Panaji  ");
        Assert.Equal("Goa", area.City);
        Assert.Equal("Panaji", area.Name);
        Assert.True(area.IsActive);

        Assert.Throws<DomainException>(() => CatalogRules.CreateArea(null, "Colaba"));
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("   ", "Colaba"));
        Assert.Equal("Pune", CatalogRules.CreateArea(" pune ", "Kothrud").City);
        Assert.Equal("New Delhi", CatalogRules.CreateArea("new   DELHI", "Saket").City);
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("P", "Kothrud"));
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("Pune1", "Kothrud"));
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("Mumbai", " "));

        var category = new Category { Name = "Yoga", Slug = "yoga", IsActive = true };
        CatalogRules.RenameCategory(category, " Hatha Yoga ");
        Assert.Equal("Hatha Yoga", category.Name);
        Assert.Equal("yoga", category.Slug);
        Assert.Throws<DomainException>(() => CatalogRules.RenameCategory(category, "Y"));
    }

    [Fact]
    public void City_length_matches_the_column()
    {
        Assert.Equal(40, CatalogRules.CityMax);
        var longest = new string('a', CatalogRules.CityMax);
        Assert.Equal(CatalogRules.CityMax, CatalogRules.CreateArea(longest, "Centre").City.Length);
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea(longest + "a", "Centre"));
    }
}
