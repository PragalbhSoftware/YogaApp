using System.Text;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class ProviderMediaRulesTests
{
    internal static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    internal static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    internal static readonly byte[] WebpHeader = [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBP"u8];
    internal static readonly byte[] PdfHeader = [.. "%PDF-1.7\n%"u8, 0xE2, 0xE3];

    [Fact]
    public void File_type_comes_from_the_bytes_not_the_name()
    {
        Assert.Equal(ProviderMediaRules.Png, ProviderMediaRules.DetectContentType(PngHeader));
        Assert.Equal(ProviderMediaRules.Jpeg, ProviderMediaRules.DetectContentType(JpegHeader));
        Assert.Equal(ProviderMediaRules.Webp, ProviderMediaRules.DetectContentType(WebpHeader));
        Assert.Equal(ProviderMediaRules.Pdf, ProviderMediaRules.DetectContentType(PdfHeader));
        Assert.Null(ProviderMediaRules.DetectContentType(Encoding.ASCII.GetBytes("<html><script>")));
        Assert.Null(ProviderMediaRules.DetectContentType("MZ\u0090\0"u8));
        Assert.Null(ProviderMediaRules.DetectContentType(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Photos_are_images_within_the_size_and_count_limits()
    {
        Assert.Equal(ProviderMediaRules.Webp, ProviderMediaRules.EnsurePhoto(WebpHeader, ProviderMediaRules.MaxPhotoBytes, 0));

        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsurePhoto(PdfHeader, 1_000, 0));
        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsurePhoto(PngHeader, ProviderMediaRules.MaxPhotoBytes + 1, 0));
        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsurePhoto(PngHeader, 0, 0));
        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsurePhoto(PngHeader, 1_000, ProviderMediaRules.MaxPhotos));
    }

    [Fact]
    public void Certificates_are_pdf_or_image_within_the_size_and_count_limits()
    {
        Assert.Equal(ProviderMediaRules.Pdf, ProviderMediaRules.EnsureCertificate(PdfHeader, ProviderMediaRules.MaxCertificateBytes, 0));
        Assert.Equal(ProviderMediaRules.Jpeg, ProviderMediaRules.EnsureCertificate(JpegHeader, 1_000, 4));

        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsureCertificate(WebpHeader, 1_000, 0));
        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsureCertificate(PdfHeader, ProviderMediaRules.MaxCertificateBytes + 1, 0));
        Assert.Throws<DomainException>(() => ProviderMediaRules.EnsureCertificate(PdfHeader, 1_000, ProviderMediaRules.MaxCertificates));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=30", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("  https://youtube.com/live/dQw4w9WgXcQ  ", "dQw4w9WgXcQ")]
    public void YouTube_links_give_the_video_id(string url, string expected)
    {
        Assert.Equal(expected, ProviderMediaRules.ParseYouTubeId(url));
    }

    [Theory]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com.evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\"><script>")]
    [InlineData("dQw4w9WgXcQ")]
    public void Other_links_are_rejected(string url)
    {
        Assert.Throws<DomainException>(() => ProviderMediaRules.ParseYouTubeId(url));
    }

    [Fact]
    public void An_empty_video_link_clears_the_video()
    {
        Assert.Null(ProviderMediaRules.ParseYouTubeId("  "));
        Assert.Null(ProviderMediaRules.ParseYouTubeId(null));
    }

    [Fact]
    public void Verified_only_after_a_certificate_is_approved_and_rejecting_needs_a_reason()
    {
        var certificate = new ProviderCertificate { Status = CertificateStatus.Pending };
        Assert.False(ProviderMediaRules.IsVerified([certificate]));

        Assert.Throws<DomainException>(() => ProviderMediaRules.Reject(certificate, "  ", Guid.NewGuid(), DateTimeOffset.UtcNow));
        ProviderMediaRules.Reject(certificate, "Blurry scan", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.False(ProviderMediaRules.IsVerified([certificate]));

        ProviderMediaRules.Approve(certificate, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.True(ProviderMediaRules.IsVerified([certificate, new ProviderCertificate { Status = CertificateStatus.Rejected }]));
        Assert.Null(certificate.RejectionReason);
    }
}
