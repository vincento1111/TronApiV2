using System.Collections.Concurrent;

namespace TronApi.Hubs;

/// <summary>Disconnect all live sockets belonging to a revoked browser session.</summary>
public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Action>> sessions = new();
    public void Add(string sessionId, string connectionId, Action abort) =>
        sessions.GetOrAdd(sessionId, _ => new())[connectionId] = abort;

    public void Remove(string sessionId, string connectionId)
    {
        if (sessions.TryGetValue(sessionId, out var connections)) connections.TryRemove(connectionId, out _);
    }

    public void AbortSession(string sessionId)
    {
        if (!sessions.TryRemove(sessionId, out var connections)) return;
        foreach (var abort in connections.Values) abort();
    }
}
