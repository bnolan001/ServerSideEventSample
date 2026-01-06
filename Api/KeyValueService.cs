using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// Service to manage key-value pairs and notify subscribers of changes.
/// </summary>
public class KeyValueService(ConcurrentDictionary<string, string> store)
{
    private readonly ConcurrentDictionary<string, string> _store = store;
    
    // Event triggered whenever a key-value pair is updated.
    private event Action<string, string>? OnChange;

    /// <summary>
    /// Updates the value for a specific key and notifies subscribers.
    /// </summary>
    /// <param name="key">The key to update.</param>
    /// <param name="value">The new value.</param>
    public void Update(string key, string value)
    {
        _store[key] = value;
        OnChange?.Invoke(key, value);
    }

    /// <summary>
    /// Retrieves the current value for a specific key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The value if found; otherwise, null.</returns>
    public string? GetValue(string key)
    {
        _store.TryGetValue(key, out var value);
        return value;
    }

    /// <summary>
    /// Streams notifications for a specific key using an async enumerable.
    /// </summary>
    /// <param name="key">The key to listen for.</param>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>An async stream of value updates.</returns>
    public async IAsyncEnumerable<string> GetNotificationsAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // channel is used to bridge the event callback and the async iterator.
        var channel = Channel.CreateUnbounded<string>();
        
        // Local handler to filter events for the specific key.
        void Handler(string k, string v)
        {
            if (string.Equals(k, key, StringComparison.Ordinal))
            {
                channel.Writer.TryWrite(v);
            }
        }

        // Subscribe to the change event.
        OnChange += Handler;

        try
        {
            // Yield an initial value to indicate the stream has started.
            yield return "Start";

            // Loop to read from the channel until cancellation is requested.
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await channel.Reader.ReadAsync(cancellationToken);
                yield return message;
            }
        }
        finally
        {
            // Ensure we unsubscribe to prevent memory leaks.
            OnChange -= Handler;
        }
    }
}
