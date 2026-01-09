
using Microsoft.Extensions.Primitives;


/// <summary>
/// Allows to pair cache entry with cancellation token.
/// This way I can invalidate cache when new data is uploaded.
/// </summary>
public class CacheSignalService
{
    private CancellationTokenSource _resetToken = new();
    private readonly ILogger<CacheSignalService> _logger;

    public CacheSignalService(ILogger<CacheSignalService> logger)
    {
        _logger = logger;
    }

    public IChangeToken GetToken()
    {
        // TODO: Read more on this type.
        return new CancellationChangeToken(_resetToken.Token);
    }

    public void Reset()
    {
        _logger.LogDebug("Resetting cache...");
        // Cancel token to invalidate cache linked with it.
        _resetToken.Cancel();

        // Recreate token to allow new cache values to be linked.
        _resetToken = new();
    }
}
