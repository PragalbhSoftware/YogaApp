using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class AdminApiTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public AdminApiTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Non_admin_calls_are_rejected_and_the_seeded_admin_can_read_the_summary()
    {
        var anonymous = await _factory.CreateClient().GetAsync("/api/admin/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains("Sign in required", (await anonymous.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Admin Gate Customer", "Other");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        var admin = await SignInAsync(SeedIds.AdminPhone);
        Assert.Equal("Admin", admin.Role);

        foreach (var path in new[]
        {
            "/api/admin/providers",
            "/api/admin/users",
            "/api/admin/bookings",
            "/api/admin/payments",
            "/api/admin/payouts",
            "/api/admin/reports/summary",
            "/api/admin/areas"
        })
        {
            var customerResponse = await customer.GetAsync(path);
            var instructorResponse = await instructor.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, instructorResponse.StatusCode);
            Assert.Contains("Admin access required", (await customerResponse.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);
        }

        var policy = await customer.PatchAsJsonAsync("/api/admin/policy", new { platformFeePercent = 10 });
        Assert.Equal(HttpStatusCode.Forbidden, policy.StatusCode);

        var summary = await admin.Client.GetFromJsonAsync<ReportBody>("/api/admin/reports/summary", Json);
        Assert.Equal("INR", summary!.Currency);
        Assert.Equal(
            new[] { "PendingAccept", "Upcoming", "Declined", "Completed", "NoShow", "Cancelled" },
            summary.BookingsByStatus.Select(row => row.Status).ToArray());
    }

    [Fact]
    public async Task Verify_lists_the_instructor_and_reject_hides_them_again()
    {
        var applicant = _factory.CreateClient();
        var (token, phone) = await SignUpAsync(applicant, "Kavya Shah", "Female");
        applicant.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var areas = await applicant.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var bandra = areas!.Single(area => area.Name == "Bandra");
        var created = await PostAsync<RegisterBody>(applicant, "/api/providers/register", new
        {
            displayName = "Kavya Shah",
            age = 31,
            areaId = bandra.Id,
            offersOnline = true,
            onlineRate = 650,
            googleMeetLink = "https://meet.google.com/kavya-admin-review"
        });
        Assert.Equal("Pending", created.Provider.Status);

        var publicBefore = await _factory.CreateClient().GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json);
        Assert.DoesNotContain(publicBefore!, provider => provider.Id == created.Provider.Id);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        var pending = await admin.Client.GetFromJsonAsync<List<ProviderAdminBody>>("/api/admin/providers", Json);
        Assert.Contains(pending!, provider => provider.Id == created.Provider.Id && provider.Status == "Pending");
        Assert.Contains(pending!, provider => provider.Phone == phone);

        var verified = await PostAsync<ProviderAdminBody>(admin.Client, $"/api/admin/providers/{created.Provider.Id}/verify", new { });
        Assert.Equal("Verified", verified.Status);
        Assert.Null(verified.RejectionReason);
        Assert.Contains("meet.google.com", verified.GoogleMeetLink, StringComparison.OrdinalIgnoreCase);

        var again = await PostAsync<ProviderAdminBody>(admin.Client, $"/api/admin/providers/{created.Provider.Id}/verify", new { });
        Assert.Equal(verified.ReviewedAt, again.ReviewedAt);

        var publicAfter = await _factory.CreateClient().GetFromJsonAsync<List<ProviderBody>>("/api/providers?category=yoga", Json);
        Assert.Contains(publicAfter!, provider => provider.Id == created.Provider.Id && provider.Status == "Verified");
        var publicProfile = await _factory.CreateClient().GetStringAsync($"/api/providers/{created.Provider.Id}");
        Assert.DoesNotContain("meet.google.com", publicProfile, StringComparison.OrdinalIgnoreCase);

        var tooLong = await admin.Client.PostAsJsonAsync(
            $"/api/admin/providers/{created.Provider.Id}/reject",
            new { reason = new string('x', 301) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("300", (await tooLong.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        var rejected = await PostAsync<ProviderAdminBody>(
            admin.Client,
            $"/api/admin/providers/{created.Provider.Id}/reject",
            new { reason = "  Incomplete profile  " });
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Incomplete profile", rejected.RejectionReason);

        var hidden = await _factory.CreateClient().GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json);
        Assert.DoesNotContain(hidden!, provider => provider.Id == created.Provider.Id);

        var restored = await PostAsync<ProviderAdminBody>(admin.Client, $"/api/admin/providers/{created.Provider.Id}/verify", new { });
        Assert.Equal("Verified", restored.Status);
        Assert.Null(restored.RejectionReason);

        var second = _factory.CreateClient();
        var (secondToken, _) = await SignUpAsync(second, "Dev Patel", "Male");
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        var secondCreated = await PostAsync<RegisterBody>(second, "/api/providers/register", new
        {
            displayName = "Dev Patel",
            age = 28,
            areaId = bandra.Id,
            offersHome = true,
            homeRate = 500
        });
        var withoutReason = await admin.Client.PostAsync($"/api/admin/providers/{secondCreated.Provider.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, withoutReason.StatusCode);
        var withoutBody = await withoutReason.Content.ReadFromJsonAsync<ProviderAdminBody>(Json);
        Assert.Equal("Rejected", withoutBody!.Status);
        Assert.Null(withoutBody.RejectionReason);
        var stillHidden = await _factory.CreateClient().GetAsync($"/api/providers/{secondCreated.Provider.Id}");
        Assert.Equal(HttpStatusCode.NotFound, stillHidden.StatusCode);
    }

    [Fact]
    public async Task User_search_returns_customers_and_providers_without_otp_secrets()
    {
        var customer = _factory.CreateClient();
        var (token, phone) = await SignUpAsync(customer, "Sana Qureshi", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await customer.GetFromJsonAsync<UserBody>("/api/auth/me", Json);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        var byName = await admin.Client.GetFromJsonAsync<List<UserBody>>("/api/admin/users?q=sana%20qureshi", Json);
        var match = Assert.Single(byName!, user => user.Phone == phone);
        Assert.Equal("Customer", match.Role);
        Assert.Null(match.Provider);

        var byPhone = await admin.Client.GetFromJsonAsync<List<UserBody>>($"/api/admin/users?q={phone[3..]}", Json);
        Assert.Contains(byPhone!, user => user.Id == me!.Id);

        var instructors = await admin.Client.GetFromJsonAsync<List<UserBody>>("/api/admin/users?q=Ananya&role=Provider", Json);
        var ananya = Assert.Single(instructors!, user => user.Provider is not null && user.Provider.DisplayName == "Ananya Desai");
        Assert.Equal("Verified", ananya.Provider!.Status);

        var customersOnly = await admin.Client.GetFromJsonAsync<List<UserBody>>("/api/admin/users", Json);
        Assert.DoesNotContain(customersOnly!, user => user.Role == "Admin");
        var admins = await admin.Client.GetFromJsonAsync<List<UserBody>>("/api/admin/users?role=Admin", Json);
        Assert.Contains(admins!, user => user.Phone == SeedIds.AdminPhone && user.Role == "Admin");

        var detailJson = await admin.Client.GetStringAsync($"/api/admin/users/{ananya.Id}");
        Assert.Contains("Ananya Desai", detailJson, StringComparison.Ordinal);
        Assert.Contains("meet.google.com", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codeHash", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otp", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", detailJson, StringComparison.OrdinalIgnoreCase);

        var missing = await admin.Client.GetAsync($"/api/admin/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Bookings_payments_and_reports_are_read_only_oversight()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var before = await admin.Client.GetFromJsonAsync<ReportBody>("/api/admin/reports/summary", Json);

        Guid pendingId;
        Guid declinedId;
        Guid completedId;
        Guid unsettledId;
        Guid failedPaymentId;
        Guid pendingPaymentId;
        Guid payoutId;
        DateOnly sessionDate;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var customer = new User
            {
                Id = Guid.NewGuid(),
                Phone = NewPhone(),
                Name = "Oversight Customer",
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(customer);
            sessionDate = MumbaiClock.Today().AddDays(40);
            var otherDate = sessionDate.AddDays(2);
            var slots = Enumerable.Range(0, 5).Select(index => new AvailabilitySlot
            {
                Id = Guid.NewGuid(),
                ProviderId = SeedIds.AnanyaProviderId,
                Mode = SessionMode.Online,
                Date = index is 1 or 4 ? otherDate : sessionDate,
                StartTime = new TimeOnly(6, 0).AddMinutes(index * 15),
                EndTime = new TimeOnly(6, 0).AddMinutes(index * 15 + 30),
                IsBlocked = false
            }).ToList();
            db.AvailabilitySlots.AddRange(slots);

            var now = DateTimeOffset.UtcNow;
            var pending = AddBooking(db, customer.Id, slots[0], BookingStatus.PendingAccept, 599m, now);
            var declined = AddBooking(db, customer.Id, slots[1], BookingStatus.Declined, 599m, now.AddMinutes(1));
            var completed = AddBooking(db, customer.Id, slots[2], BookingStatus.Completed, 100m, now.AddMinutes(2));
            var failed = AddBooking(db, customer.Id, slots[3], BookingStatus.Upcoming, 50m, now.AddMinutes(3));
            var unsettled = AddBooking(db, customer.Id, slots[4], BookingStatus.PendingAccept, 10m, now.AddMinutes(4));
            pendingId = pending.Id;
            declinedId = declined.Id;
            completedId = completed.Id;
            unsettledId = unsettled.Id;

            AddPayment(db, pending, PaymentStatus.Paid, "pay_admin_paid");
            AddPayment(db, declined, PaymentStatus.Refunded, "pay_admin_refunded");
            AddPayment(db, completed, PaymentStatus.Paid, "pay_admin_gmv");
            var failedPayment = AddPayment(db, failed, PaymentStatus.Failed, "pay_admin_failed");
            var openPayment = AddPayment(db, unsettled, PaymentStatus.Pending, "pay_admin_pending");
            failedPaymentId = failedPayment.Id;
            pendingPaymentId = openPayment.Id;
            var payout = new PayoutPending
            {
                Id = Guid.NewGuid(),
                BookingId = completed.Id,
                ProviderId = SeedIds.AnanyaProviderId,
                GrossAmount = 100m,
                FeePercent = 15m,
                FeeAmount = 15m,
                NetAmount = 85m,
                Status = PayoutStatus.Pending,
                CreatedAt = now
            };
            payoutId = payout.Id;
            db.PayoutsPending.Add(payout);
            await db.SaveChangesAsync();
        }

        var onDate = await admin.Client.GetFromJsonAsync<List<BookingBody>>(
            $"/api/admin/bookings?providerId={SeedIds.AnanyaProviderId}&from={sessionDate:yyyy-MM-dd}&to={sessionDate:yyyy-MM-dd}&status=PendingAccept",
            Json);
        Assert.Contains(onDate!, booking => booking.Id == pendingId);
        Assert.DoesNotContain(onDate!, booking => booking.Id == declinedId || booking.Id == completedId || booking.Id == unsettledId);

        var detail = await admin.Client.GetFromJsonAsync<BookingBody>($"/api/admin/bookings/{completedId}", Json);
        Assert.Equal("Completed", detail!.Status);
        Assert.Equal("Paid", detail.PaymentStatus);
        Assert.Equal(85m, detail.PayoutNet);
        Assert.Equal("Pending", detail.PayoutStatus);
        Assert.Equal("Oversight Customer", detail.CustomerName);
        Assert.Contains("meet.google.com", detail.MeetLink, StringComparison.OrdinalIgnoreCase);

        var backwards = await admin.Client.GetAsync(
            $"/api/admin/bookings?from={sessionDate.AddDays(2):yyyy-MM-dd}&to={sessionDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        var unknown = await admin.Client.GetAsync("/api/admin/bookings?status=NotAStatus");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("status", (await unknown.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var payments = await admin.Client.GetFromJsonAsync<List<PaymentBody>>("/api/admin/payments", Json);
        Assert.Contains(payments!, payment => payment.BookingId == pendingId && payment.Status == "Paid");
        Assert.Contains(payments!, payment => payment.BookingId == declinedId && payment.Status == "Refunded");
        Assert.Contains(payments!, payment => payment.Id == failedPaymentId && payment.Status == "Failed");
        Assert.DoesNotContain(payments!, payment => payment.Id == pendingPaymentId);

        var failedOnly = await admin.Client.GetFromJsonAsync<List<PaymentBody>>("/api/admin/payments?status=Failed", Json);
        Assert.Contains(failedOnly!, payment => payment.Id == failedPaymentId);
        Assert.DoesNotContain(failedOnly!, payment => payment.BookingId == pendingId);
        var pendingStatus = await admin.Client.GetAsync("/api/admin/payments?status=Pending");
        Assert.Equal(HttpStatusCode.BadRequest, pendingStatus.StatusCode);

        var payouts = await admin.Client.GetFromJsonAsync<List<PayoutBody>>("/api/admin/payouts", Json);
        var listedPayout = Assert.Single(payouts!, payout => payout.Id == payoutId);
        Assert.Equal("Pending", listedPayout.Status);
        Assert.Equal(85m, listedPayout.NetAmount);
        Assert.Equal("Ananya Desai", listedPayout.ProviderName);

        var after = await admin.Client.GetFromJsonAsync<ReportBody>("/api/admin/reports/summary", Json);
        Assert.Equal(before!.GmvPaid + 699m, after!.GmvPaid);
        Assert.Equal(before.PendingPayouts.Count + 1, after.PendingPayouts.Count);
        Assert.Equal(before.PendingPayouts.Gross + 100m, after.PendingPayouts.Gross);
        Assert.Equal(before.PendingPayouts.Net + 85m, after.PendingPayouts.Net);
        Assert.Equal(Count(before, "PendingAccept") + 2, Count(after, "PendingAccept"));
        Assert.Equal(Count(before, "Declined") + 1, Count(after, "Declined"));
        Assert.Equal(Count(before, "Completed") + 1, Count(after, "Completed"));
        Assert.Equal(Count(before, "Upcoming") + 1, Count(after, "Upcoming"));
    }

    [Fact]
    public async Task Masters_patch_areas_the_category_label_and_the_policy_without_rewriting_payouts()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        Guid payoutId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var slot = await db.AvailabilitySlots
                .Where(row => row.ProviderId == SeedIds.AnanyaProviderId && row.Mode == SessionMode.Studio)
                .OrderBy(row => row.Date)
                .FirstAsync();
            var customer = new User
            {
                Id = Guid.NewGuid(),
                Phone = NewPhone(),
                Name = "Policy Customer",
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(customer);
            var booking = AddBooking(db, customer.Id, slot, BookingStatus.Completed, 749m, DateTimeOffset.UtcNow);
            var payout = new PayoutPending
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                ProviderId = SeedIds.AnanyaProviderId,
                GrossAmount = 749m,
                FeePercent = 15m,
                FeeAmount = 112.35m,
                NetAmount = 636.65m,
                Status = PayoutStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };
            payoutId = payout.Id;
            db.PayoutsPending.Add(payout);
            await db.SaveChangesAsync();
        }

        var created = await PostAsync<AreaAdminBody>(admin.Client, "/api/admin/areas", new { name = "Colaba" });
        Assert.Equal("Mumbai", created.City);
        Assert.True(created.IsActive);
        var publicAreas = await _factory.CreateClient().GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        Assert.Contains(publicAreas!, area => area.Name == "Colaba");

        var duplicate = await admin.Client.PostAsJsonAsync("/api/admin/areas", new { name = " colaba ", city = "mumbai" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var otherCity = await admin.Client.PostAsJsonAsync("/api/admin/areas", new { name = "Kothrud", city = "Pune" });
        Assert.Equal(HttpStatusCode.BadRequest, otherCity.StatusCode);

        var inactive = await PatchAsync<AreaAdminBody>(admin.Client, $"/api/admin/areas/{created.Id}", new { isActive = false });
        Assert.False(inactive.IsActive);
        var publicAfter = await _factory.CreateClient().GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        Assert.DoesNotContain(publicAfter!, area => area.Id == created.Id);
        var adminAreas = await admin.Client.GetFromJsonAsync<List<AreaAdminBody>>("/api/admin/areas", Json);
        Assert.Contains(adminAreas!, area => area.Id == created.Id && area.IsActive == false);

        try
        {
            var renamed = await PatchAsync<CategoryAdminBody>(
                admin.Client,
                $"/api/admin/categories/{SeedIds.YogaCategoryId}",
                new { name = "Hatha Yoga" });
            Assert.Equal("Hatha Yoga", renamed.Name);
            Assert.Equal("yoga", renamed.Slug);
            var categories = await _factory.CreateClient().GetFromJsonAsync<List<CategoryBody>>("/api/categories", Json);
            Assert.Contains(categories!, category => category.Slug == "yoga" && category.Name == "Hatha Yoga");
            var browse = await _factory.CreateClient().GetFromJsonAsync<List<ProviderBody>>("/api/providers?category=yoga", Json);
            Assert.Contains(browse!, provider => provider.DisplayName == "Ananya Desai");

            var policy = await PatchAsync<PolicyBody>(admin.Client, "/api/admin/policy", new
            {
                platformFeePercent = 20,
                cancelFreeWindowHours = 24
            });
            Assert.Equal(20m, policy.PlatformFeePercent);
            Assert.Equal(24, policy.CancelFreeWindowHours);
            Assert.Equal(12, policy.RescheduleFreeWindowHours);
            var publicPolicy = await _factory.CreateClient().GetFromJsonAsync<PolicyBody>("/api/policy", Json);
            Assert.Equal(20m, publicPolicy!.PlatformFeePercent);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var payout = await db.PayoutsPending.SingleAsync(row => row.Id == payoutId);
            Assert.Equal(15m, payout.FeePercent);
            Assert.Equal(636.65m, payout.NetAmount);
        }
        finally
        {
            await PatchAsync<CategoryAdminBody>(
                admin.Client,
                $"/api/admin/categories/{SeedIds.YogaCategoryId}",
                new { name = "Yoga" });
            await PatchAsync<PolicyBody>(admin.Client, "/api/admin/policy", new
            {
                platformFeePercent = 15,
                cancelFreeWindowHours = 12,
                policyNote = "Platform fee, cancel window, and reschedule window are TBD defaults for the Mumbai launch. Confirm with ops before taking live payments."
            });
        }
    }

    private static int Count(ReportBody report, string status) =>
        report.BookingsByStatus.Single(row => row.Status == status).Count;

    private static Booking AddBooking(
        YogaDbContext db,
        Guid customerId,
        AvailabilitySlot slot,
        BookingStatus status,
        decimal amount,
        DateTimeOffset createdAt)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProviderId = slot.ProviderId,
            ServiceId = SeedIds.AnanyaServiceId,
            SlotId = slot.Id,
            Mode = slot.Mode,
            Status = status,
            Amount = amount,
            MeetLinkSnapshot = "https://meet.google.com/abc-defg-hij",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        db.Bookings.Add(booking);
        return booking;
    }

    private static Payment AddPayment(YogaDbContext db, Booking booking, PaymentStatus status, string paymentId)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = booking.Amount,
            Status = status,
            Gateway = "Razorpay",
            GatewayOrderId = "order_" + paymentId,
            GatewayPaymentId = paymentId,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.CreatedAt
        };
        db.Payments.Add(payment);
        return payment;
    }

    private async Task<(HttpClient Client, string Role)> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.User.Role);
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name, string gender)
    {
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static string NewPhone() => "+9198" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record ProviderBody(Guid Id, string DisplayName, string Status);
    private sealed record RegisterProviderBody(Guid Id, string Status);
    private sealed record RegisterBody(RegisterProviderBody Provider, string Token);
    private sealed record ProviderAdminBody(
        Guid Id,
        string Phone,
        string Status,
        string? GoogleMeetLink,
        string? RejectionReason,
        DateTimeOffset? ReviewedAt);
    private sealed record UserProviderBody(Guid Id, string DisplayName, string Status);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string Role, UserProviderBody? Provider);
    private sealed record BookingBody(
        Guid Id,
        string? CustomerName,
        string Status,
        string? PaymentStatus,
        string? MeetLink,
        decimal? PayoutNet,
        string? PayoutStatus);
    private sealed record PaymentBody(Guid Id, Guid BookingId, string Status);
    private sealed record PayoutBody(Guid Id, string ProviderName, decimal NetAmount, string Status);
    private sealed record StatusCount(string Status, int Count);
    private sealed record PayoutTotals(int Count, decimal Gross, decimal Net);
    private sealed record ReportBody(List<StatusCount> BookingsByStatus, decimal GmvPaid, string Currency, PayoutTotals PendingPayouts);
    private sealed record AreaAdminBody(Guid Id, string City, string Name, bool IsActive);
    private sealed record CategoryBody(Guid Id, string Name, string Slug);
    private sealed record CategoryAdminBody(Guid Id, string Name, string Slug, bool IsActive);
    private sealed record PolicyBody(
        string Currency,
        decimal PlatformFeePercent,
        int CancelFreeWindowHours,
        int RescheduleFreeWindowHours,
        decimal LateCancelFeePercent,
        string PolicyNote);
}
