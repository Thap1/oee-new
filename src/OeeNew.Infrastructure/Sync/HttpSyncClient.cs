using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OeeNew.Application.Sync;

namespace OeeNew.Infrastructure.Sync;

/// <summary>
/// Site-side transport for the sync push (Story 5.1, AC #2) — posts to Central's
/// <c>SyncController</c> over plain HTTP with a shared API key header, since both sides run the
/// identical binary and share the <see cref="SyncBatch"/> wire type. Never throws out of
/// <see cref="TryPushAsync"/>: any transport/HTTP failure is caught and reported as `false`, which
/// <see cref="PushSyncBatchUseCase"/> treats as "try again next cycle."
/// </summary>
public sealed class HttpSyncClient(HttpClient httpClient, IOptions<SyncOptions> options) : ISyncClient
{
    public async Task<bool> TryPushAsync(SyncBatch batch, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/sync/batch")
            {
                Content = JsonContent.Create(batch),
            };
            request.Headers.Add("X-Sync-Api-Key", options.Value.ApiKey ?? string.Empty);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout, not a caller-requested cancellation — treat the same as any other transport failure.
            return false;
        }
    }
}
