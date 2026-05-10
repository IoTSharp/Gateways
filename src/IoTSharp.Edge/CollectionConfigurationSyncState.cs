namespace IoTSharp.Edge;

public sealed class CollectionConfigurationSyncState
{
    private readonly object _lock = new();
    private CollectionConfigurationSyncSnapshot _snapshot = new(
        "idle",
        "Waiting for collection configuration sync.",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false);

    public CollectionConfigurationSyncSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }

    public void MarkDisabled(string? baseUrl, bool hasAccessToken)
        => Update("disabled", "Edge reporting is disabled; collection config sync is paused.", baseUrl, hasAccessToken);

    public void MarkWaitingBootstrap(string message, string? baseUrl, bool hasAccessToken)
        => Update("waiting-bootstrap", message, baseUrl, hasAccessToken);

    public void MarkSyncing(string? baseUrl, bool hasAccessToken)
        => Update("syncing", "Pulling collection configuration from IoTSharp.", baseUrl, hasAccessToken);

    public void MarkSynced(int version, DateTime updatedAtUtc, string updatedBy, string? baseUrl, bool hasAccessToken, bool applied)
        => Update(
            applied ? "synced" : "up-to-date",
            applied ? "Collection configuration has been applied to the local execution cache." : "Collection configuration is already up to date.",
            baseUrl,
            hasAccessToken,
            version,
            updatedAtUtc,
            updatedBy,
            lastSuccessAtUtc: DateTime.UtcNow,
            lastError: null);

    public void MarkError(string message, string? baseUrl, bool hasAccessToken, int? remoteVersion = null)
        => Update(
            "error",
            message,
            baseUrl,
            hasAccessToken,
            remoteVersion,
            _snapshot.RemoteUpdatedAtUtc,
            _snapshot.UpdatedBy,
            lastFailureAtUtc: DateTime.UtcNow,
            lastError: message);

    private void Update(
        string status,
        string message,
        string? baseUrl,
        bool hasAccessToken,
        int? remoteVersion = null,
        DateTime? remoteUpdatedAtUtc = null,
        string? updatedBy = null,
        DateTime? lastSuccessAtUtc = null,
        DateTime? lastFailureAtUtc = null,
        string? lastError = null)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with
            {
                Status = status,
                Message = message,
                BaseUrl = baseUrl,
                HasAccessToken = hasAccessToken,
                LastAttemptAtUtc = DateTime.UtcNow,
                LastSuccessAtUtc = lastSuccessAtUtc ?? _snapshot.LastSuccessAtUtc,
                LastFailureAtUtc = lastFailureAtUtc ?? _snapshot.LastFailureAtUtc,
                RemoteVersion = remoteVersion ?? _snapshot.RemoteVersion,
                RemoteUpdatedAtUtc = remoteUpdatedAtUtc ?? _snapshot.RemoteUpdatedAtUtc,
                UpdatedBy = updatedBy ?? _snapshot.UpdatedBy,
                LastError = lastError
            };
        }
    }
}

public sealed record CollectionConfigurationSyncSnapshot(
    string Status,
    string Message,
    DateTime? LastAttemptAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    int? RemoteVersion,
    DateTime? RemoteUpdatedAtUtc,
    string? UpdatedBy,
    string? LastError,
    string? BaseUrl,
    bool HasAccessToken);
