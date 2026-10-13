using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ProviderTrustTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ProviderTrustTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Instructor_photos_are_public_and_typed_by_their_bytes()
    {
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        var photo = await UploadAsync<PhotoBody>(instructor, "/api/providers/me/photos", ProviderMediaRulesTests.PngHeader, "studio.png");
        try
        {
            var anon = _factory.CreateClient();
            var detail = await anon.GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
            Assert.Contains(photo.Url, detail!.PhotoUrls);

            var browse = await anon.GetFromJsonAsync<List<SummaryBody>>("/api/providers", Json);
            Assert.Equal(photo.Url, Assert.Single(browse!, p => p.Id == SeedIds.AnanyaProviderId).PhotoUrl);

            var image = await anon.GetAsync(photo.Url);
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
            Assert.True(image.Headers.CacheControl!.Public);
            Assert.Equal(ProviderMediaRulesTests.PngHeader, await image.Content.ReadAsByteArrayAsync());

            var disguised = await UploadRawAsync(instructor, "/api/providers/me/photos", "<script>alert(1)</script>"u8.ToArray(), "photo.png");
            Assert.Equal(HttpStatusCode.BadRequest, disguised.StatusCode);
            Assert.Contains("JPEG, PNG or WebP", await disguised.Content.ReadAsStringAsync());
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await instructor.DeleteAsync($"/api/providers/me/photos/{photo.Id}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(photo.Url)).StatusCode);
    }

    [Fact]
    public async Task Certificates_stay_private_and_the_badge_shows_only_after_approval()
    {
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var anon = _factory.CreateClient();
        var certificate = await UploadAsync<CertificateBody>(
            instructor, "/api/providers/me/certificates", ProviderMediaRulesTests.PdfHeader, "ryt200.pdf", title: "RYT 200");
        try
        {
            Assert.Equal("Pending", certificate.Status);
            var before = await anon.GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
            Assert.False(before!.IsVerified);
            Assert.Empty(before.Certificates);

            var fileUrl = $"/api/providers/me/certificates/{certificate.Id}/file";
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(fileUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/admin/certificates/{certificate.Id}/file")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/media/photos/{certificate.Id}")).StatusCode);

            var customer = await SignUpAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(fileUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/certificates/{certificate.Id}/file")).StatusCode);

            var own = await instructor.GetAsync(fileUrl);
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            Assert.True(own.Headers.CacheControl!.NoStore);

            var pending = await admin.GetFromJsonAsync<List<AdminCertificateBody>>("/api/admin/certificates", Json);
            Assert.Contains(pending!, c => c.Id == certificate.Id && c.ProviderName.Length > 0);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/certificates/{certificate.Id}/file")).StatusCode);

            var noReason = await admin.PostAsJsonAsync($"/api/admin/certificates/{certificate.Id}/reject", new { reason = " " });
            Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

            var approved = await admin.PostAsJsonAsync($"/api/admin/certificates/{certificate.Id}/approve", new { });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

            var after = await anon.GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
            Assert.True(after!.IsVerified);
            Assert.Equal(["RYT 200"], after.Certificates);
            var json = await anon.GetStringAsync($"/api/providers/{SeedIds.AnanyaProviderId}");
            Assert.DoesNotContain(certificate.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/media/photos/{certificate.Id}")).StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await instructor.DeleteAsync($"/api/providers/me/certificates/{certificate.Id}")).StatusCode);
        }

        var cleared = await anon.GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
        Assert.False(cleared!.IsVerified);
    }

    [Fact]
    public async Task Intro_video_accepts_youtube_links_only()
    {
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        try
        {
            var saved = await instructor.PutAsJsonAsync("/api/providers/me/intro-video", new { url = "https://youtu.be/dQw4w9WgXcQ" });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.Equal("dQw4w9WgXcQ", (await saved.Content.ReadFromJsonAsync<MediaBody>(Json))!.IntroVideoId);

            var detail = await _factory.CreateClient().GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
            Assert.Equal("dQw4w9WgXcQ", detail!.IntroVideoId);

            var other = await instructor.PutAsJsonAsync("/api/providers/me/intro-video", new { url = "https://vimeo.com/1" });
            Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        }
        finally
        {
            var cleared = await instructor.PutAsJsonAsync("/api/providers/me/intro-video", new { url = "" });
            Assert.Null((await cleared.Content.ReadFromJsonAsync<MediaBody>(Json))!.IntroVideoId);
        }
    }

    [Fact]
    public async Task Customers_cannot_manage_instructor_media()
    {
        var customer = await SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/providers/me/media")).StatusCode);
        var upload = await UploadRawAsync(customer, "/api/providers/me/photos", ProviderMediaRulesTests.PngHeader, "a.png");
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<HttpClient> SignUpAsync()
    {
        var client = _factory.CreateClient();
        var phone = "+9198" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Meera Rao", gender = "Female", isNewUser = true, acceptedPolicies = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> UploadAsync<T>(HttpClient client, string url, byte[] bytes, string fileName, string? title = null)
    {
        var response = await UploadRawAsync(client, url, bytes, fileName, title);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static Task<HttpResponseMessage> UploadRawAsync(HttpClient client, string url, byte[] bytes, string fileName, string? title = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (title is not null)
            form.Add(new StringContent(title), "title");
        return client.PostAsync(url, form);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record PhotoBody(Guid Id, string Url, int SortOrder);
    private sealed record CertificateBody(Guid Id, string Title, string Status);
    private sealed record MediaBody(string? IntroVideoId, bool IsVerified);
    private sealed record AdminCertificateBody(Guid Id, string ProviderName, string Title, string Status);
    private sealed record SummaryBody(Guid Id, string? PhotoUrl, bool IsVerified);
    private sealed record DetailBody(List<string> PhotoUrls, string? IntroVideoId, List<string> Certificates, bool IsVerified);
}
