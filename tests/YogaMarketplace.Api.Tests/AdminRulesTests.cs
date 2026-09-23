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
    public void Policy_patch_checks_fee_and_windows_and_leaves_omitted_fields()
    {
        var policy = new MarketplacePolicy
        {
            Currency = "INR",
            PlatformFeePercent = 15m,
            CancelFreeWindowHours = 12,
            RescheduleFreeWindowHours = 12,
            LateCancelFeePercent = 50m,
            PolicyNote = "TBD"
        };

        CatalogRules.UpdatePolicy(policy, 10.5m, 0, 24, 0m, null);
        Assert.Equal(10.5m, policy.PlatformFeePercent);
        Assert.Equal(0, policy.CancelFreeWindowHours);
        Assert.Equal(24, policy.RescheduleFreeWindowHours);
        Assert.Equal(0m, policy.LateCancelFeePercent);
        Assert.Equal("TBD", policy.PolicyNote);

        CatalogRules.UpdatePolicy(policy, null, null, null, null, "  Confirmed  ");
        Assert.Equal("Confirmed", policy.PolicyNote);
        Assert.Equal(10.5m, policy.PlatformFeePercent);

        Assert.Throws<DomainException>(() => CatalogRules.UpdatePolicy(policy, 101m, null, null, null, null));
        Assert.Throws<DomainException>(() => CatalogRules.UpdatePolicy(policy, null, -1, null, null, null));
        Assert.Throws<DomainException>(() => CatalogRules.UpdatePolicy(policy, null, null, CatalogRules.MaxWindowHours + 1, null, null));
        Assert.Throws<DomainException>(() => CatalogRules.UpdatePolicy(policy, 15.555m, null, null, null, null));
        Assert.Throws<DomainException>(() => CatalogRules.UpdatePolicy(policy, null, null, null, null, null));
    }

    [Fact]
    public void Area_create_stays_in_mumbai_and_category_rename_does_not_touch_the_slug()
    {
        var area = CatalogRules.CreateArea(null, "  Colaba  ");
        Assert.Equal(CatalogRules.LaunchCity, area.City);
        Assert.Equal("Colaba", area.Name);
        Assert.True(area.IsActive);

        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("Pune", "Kothrud"));
        Assert.Throws<DomainException>(() => CatalogRules.CreateArea("Mumbai", " "));

        var category = new Category { Name = "Yoga", Slug = "yoga", IsActive = true };
        CatalogRules.RenameCategory(category, " Hatha Yoga ");
        Assert.Equal("Hatha Yoga", category.Name);
        Assert.Equal("yoga", category.Slug);
        Assert.Throws<DomainException>(() => CatalogRules.RenameCategory(category, "Y"));
    }
}
