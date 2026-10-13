using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;
using YogaMarketplace.Infrastructure.Persistence.Migrations;

namespace YogaMarketplace.Api.Tests;

public class ContentPagesTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ContentPagesTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("refund-policy", "Refund policy")]
    [InlineData("terms", "Terms of use")]
    [InlineData("privacy", "Privacy policy")]
    public async Task Signed_out_visitors_can_read_the_seeded_pages(string slug, string title)
    {
        var page = await _factory.CreateClient().GetFromJsonAsync<PageBody>($"/api/pages/{slug}", Json);
        Assert.Equal(title, page!.Title);
        Assert.NotEmpty(page.Body);
        Assert.DoesNotContain("Mumbai", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Legal_text_migration_updates_only_unedited_pages_both_ways_on_sqlite()
    {
        await using var factory = new YogaApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        const string edited = "Terms written by the admin, with an 'apostrophe'.";
        await SetBodyAsync(db, "refund-policy", RefreshLegalPageDefaults.OldRefund);
        await SetBodyAsync(db, "terms", edited);

        await RunAsync(db, m => m.UpOperations);
        Assert.Equal((RefreshLegalPageDefaults.NewRefund, edited), await BodiesAsync(db));
        await RunAsync(db, m => m.UpOperations);
        Assert.Equal((RefreshLegalPageDefaults.NewRefund, edited), await BodiesAsync(db));

        await RunAsync(db, m => m.DownOperations);
        Assert.Equal((RefreshLegalPageDefaults.OldRefund, edited), await BodiesAsync(db));

        static Task SetBodyAsync(YogaDbContext db, string slug, string body) =>
            db.ContentPages.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Body, body));

        static async Task<(string Refund, string Terms)> BodiesAsync(YogaDbContext db)
        {
            var bodies = await db.ContentPages.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Body);
            return (bodies["refund-policy"], bodies["terms"]);
        }

        static async Task RunAsync(YogaDbContext db, Func<Migration, IReadOnlyList<MigrationOperation>> operations)
        {
            var migration = new RefreshLegalPageDefaults { ActiveProvider = db.Database.ProviderName };
            foreach (var operation in operations(migration).Cast<SqlOperation>())
                await db.Database.ExecuteSqlRawAsync(operation.Sql);
        }
    }

    [Fact]
    public async Task Unknown_pages_are_404()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/pages/about")).StatusCode);
    }

    [Theory]
    [InlineData("draft-terms")]
    [InlineData("TERMS")]
    [InlineData("cookie-policy")]
    public async Task Pages_that_are_not_published_are_404(string slug)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/pages/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Starter_text_has_the_off_platform_rule_and_no_fixed_refund_numbers_or_cities()
    {
        var anon = _factory.CreateClient();
        var terms = await anon.GetFromJsonAsync<PageBody>("/api/pages/terms", Json);
        var refund = await anon.GetFromJsonAsync<PageBody>("/api/pages/refund-policy", Json);
        var privacy = await anon.GetFromJsonAsync<PageBody>("/api/pages/privacy", Json);

        Assert.Contains("## Off-platform bookings", terms!.Body, StringComparison.Ordinal);
        Assert.Contains("platform settings", refund!.Body, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d", refund.Body);

        using var scope = _factory.Services.CreateScope();
        var cities = scope.ServiceProvider.GetRequiredService<YogaDbContext>().Areas.Select(a => a.City).Distinct().ToList();
        Assert.NotEmpty(cities);
        foreach (var body in new[] { terms.Body, refund.Body, privacy!.Body })
            Assert.All(cities, city => Assert.DoesNotContain(city, body, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Only_admins_edit_pages_and_a_stale_version_is_409()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/pages")).StatusCode);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        var pages = await admin.GetFromJsonAsync<List<PageBody>>("/api/admin/pages", Json);
        Assert.Equal(["refund-policy", "terms", "privacy"], pages!.Select(p => p.Slug));

        var before = pages.Single(p => p.Slug == "terms");
        try
        {
            var saved = await PutAsync(admin, "terms", new
            {
                title = "  Terms  ",
                body = "Hello.\r\n\r\n## Section\r\n- One",
                version = before.Version
            });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var after = (await saved.Content.ReadFromJsonAsync<PageBody>(Json))!;
            Assert.Equal(before.Version + 1, after.Version);
            Assert.Equal("Terms", after.Title);
            Assert.Equal("Hello.\n\n## Section\n- One", after.Body);
            Assert.Equal(after.Body, (await anon.GetFromJsonAsync<PageBody>("/api/pages/terms", Json))!.Body);

            var stale = await PutAsync(admin, "terms", new { title = "Old tab", body = "Old text", version = before.Version });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Contains("reloaded", await stale.Content.ReadAsStringAsync());

            var empty = await PutAsync(admin, "terms", new { title = "Terms", body = "  ", version = after.Version });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

            var unknown = await PutAsync(admin, "about", new { title = "About", body = "Text", version = 1 });
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            var current = (await anon.GetFromJsonAsync<PageBody>("/api/pages/terms", Json))!;
            await PutAsync(admin, "terms", new { title = before.Title, body = before.Body, version = current.Version });
        }
    }

    [Fact]
    public void Saving_unchanged_text_keeps_the_version()
    {
        var page = new ContentPage { Slug = "terms", Title = "Terms", Body = "Text", Version = 3 };
        Assert.False(ContentPageRules.Apply(page, "Terms", "Text\r\n", Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(3, page.Version);
        Assert.Throws<DomainException>(() => ContentPageRules.Apply(page, "T", "Text", Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() =>
            ContentPageRules.Apply(page, "Terms", new string('a', ContentPageRules.BodyMax + 1), Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string slug, object body) =>
        client.PutAsJsonAsync($"/api/admin/pages/{slug}", body);

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = false }))
            .Content.ReadFromJsonAsync<OtpBody>(Json);
        var auth = await (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = otp!.DevCode }))
            .Content.ReadFromJsonAsync<AuthBody>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record PageBody(string Slug, string Title, string Body, int Version);
}
