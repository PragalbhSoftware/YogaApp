using System.Text;
using Microsoft.Extensions.Hosting.Internal;
using YogaMarketplace.Infrastructure.Storage;

namespace YogaMarketplace.Api.Tests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _siteFolder = Path.Combine(Path.GetTempPath(), $"yoga-site-{Guid.NewGuid():N}");
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), $"yoga-uploads-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_absolute_folder_outside_the_site_holds_the_uploads(bool trailingSeparator)
    {
        var root = trailingSeparator ? _uploads + Path.DirectorySeparatorChar : _uploads;
        var storage = Storage(root);

        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("photo")))
            await storage.SaveAsync("photos/p1/a.jpg", content, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_uploads, "photos", "p1", "a.jpg")));
        Assert.False(Directory.Exists(_siteFolder));
        await using var read = await storage.OpenReadAsync("photos/p1/a.jpg", CancellationToken.None);
        using var reader = new StreamReader(read!);
        Assert.Equal("photo", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Keys_cannot_escape_the_folder()
    {
        var storage = Storage(_uploads);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAsync("../outside.txt", new MemoryStream(), CancellationToken.None));
    }

    private LocalFileStorage Storage(string root) => new(
        Microsoft.Extensions.Options.Options.Create(new StorageOptions { RootPath = root }),
        new HostingEnvironment { ContentRootPath = _siteFolder });

    public void Dispose()
    {
        if (Directory.Exists(_uploads))
            Directory.Delete(_uploads, recursive: true);
        GC.SuppressFinalize(this);
    }
}
