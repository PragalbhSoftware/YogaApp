namespace YogaMarketplace.Api.Services;

internal sealed class IndiaLocationState
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public List<string> Cities { get; set; } = [];
}
