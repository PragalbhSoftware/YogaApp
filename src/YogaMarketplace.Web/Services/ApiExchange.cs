using System.Net.Http.Json;
using System.Text.Json;
using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

internal sealed class ApiExchange
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public ApiExchange(HttpClient http, ILogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(path, cancellationToken);
            return await ReadAsync<T>(response, path, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Marketplace API GET {Path} was unreachable.", path);
            return ApiResult<T>.Down(UiCopy.ApiUnreachable);
        }
    }

    public async Task<ApiResult<T>> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(path, body, Json, cancellationToken);
            return await ReadAsync<T>(response, path, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Marketplace API POST {Path} was unreachable.", path);
            return ApiResult<T>.Down(UiCopy.ApiUnreachable);
        }
    }

    private async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Marketplace API {Path} returned {Status}.", path, (int)response.StatusCode);
            return ApiResult<T>.Fail(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
        }

        try
        {
            var data = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            if (data is null)
                return ApiResult<T>.Fail(UiCopy.EmptyResponse);
            return ApiResult<T>.Success(data);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Marketplace API {Path} returned JSON the web app could not read.", path);
            return ApiResult<T>.Fail(UiCopy.GenericError);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json, cancellationToken);
            if (!string.IsNullOrWhiteSpace(body?.Error))
                return body.Error;
        }
        catch (JsonException)
        {
        }

        return UiCopy.GenericError;
    }

    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private sealed record ErrorBody(string? Error);
}
