using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

// One crew member's end-of-run numbers; replicated as-is by NetworkRunState.
public struct PlayerRunStats : INetworkSerializable, IEquatable<PlayerRunStats>
{
    public ulong ClientId;
    public FixedString64Bytes PlayerName;
    public int EnemiesKilled;
    public int Deaths;
    public int Revives;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref EnemiesKilled);
        serializer.SerializeValue(ref Deaths);
        serializer.SerializeValue(ref Revives);
    }

    public bool Equals(PlayerRunStats other)
    {
        return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName) &&
            EnemiesKilled == other.EnemiesKilled && Deaths == other.Deaths && Revives == other.Revives;
    }

    public override bool Equals(object obj) => obj is PlayerRunStats other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ClientId, EnemiesKilled, Deaths, Revives);
}

// Host-side tally keyed by client id (0 offline). Only counts while the run is active.
public class RunStatsTracker
{
    private readonly Dictionary<ulong, PlayerRunStats> stats = new();

    public event Action OnStatsChanged;

    public IEnumerable<PlayerRunStats> All => stats.Values;

    public void RecordKill(ulong clientId)
    {
        if (!TryGetEntry(clientId, null, out PlayerRunStats entry))
            return;

        entry.EnemiesKilled++;
        Store(entry);
    }

    public void RecordDeath(ulong clientId, string playerName)
    {
        if (!TryGetEntry(clientId, playerName, out PlayerRunStats entry))
            return;

        entry.Deaths++;
        Store(entry);
    }

    public void RecordRevive(ulong clientId, string playerName)
    {
        if (!TryGetEntry(clientId, playerName, out PlayerRunStats entry))
            return;

        entry.Revives++;
        Store(entry);
    }

    // Players who never died or killed still get a row on the end screen.
    public void EnsurePlayer(ulong clientId, string playerName)
    {
        if (TryGetEntry(clientId, playerName, out PlayerRunStats entry))
            Store(entry);
    }

    public void Clear()
    {
        stats.Clear();
        OnStatsChanged?.Invoke();
    }

    public void ApplyReplicated(IEnumerable<PlayerRunStats> replicated)
    {
        stats.Clear();
        foreach (PlayerRunStats entry in replicated)
            stats[entry.ClientId] = entry;

        OnStatsChanged?.Invoke();
    }

    private bool TryGetEntry(ulong clientId, string playerName, out PlayerRunStats entry)
    {
        entry = default;
        if (NetworkRole.IsClientOnly)
            return false;

        stats.TryGetValue(clientId, out entry);
        entry.ClientId = clientId;
        if (!string.IsNullOrEmpty(playerName))
            entry.PlayerName = new FixedString64Bytes(playerName);

        return true;
    }

    private void Store(PlayerRunStats entry)
    {
        stats[entry.ClientId] = entry;
        OnStatsChanged?.Invoke();
    }
}
