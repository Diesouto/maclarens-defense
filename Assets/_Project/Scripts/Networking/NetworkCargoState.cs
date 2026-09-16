using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public struct NetworkCargoEntry : INetworkSerializable, System.IEquatable<NetworkCargoEntry>
{
    public ulong NetworkObjectId;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NetworkObjectId);
        serializer.SerializeValue(ref LocalPosition);
        serializer.SerializeValue(ref LocalRotation);
    }

    public bool Equals(NetworkCargoEntry other)
    {
        return NetworkObjectId == other.NetworkObjectId &&
            LocalPosition == other.LocalPosition &&
            LocalRotation == other.LocalRotation;
    }
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(TrainCargo))]
public class NetworkCargoState : NetworkBehaviour
{
    public NetworkList<NetworkCargoEntry> Items { get; private set; }
    public NetworkVariable<int> CargoValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private TrainCargo cargo;
    private readonly List<NetworkCargoEntry> cachedItems = new();
    private int cachedCargoValue = -1;

    private void Awake()
    {
        cargo = GetComponent<TrainCargo>();
        Items = new NetworkList<NetworkCargoEntry>();
    }

    public override void OnNetworkSpawn()
    {
        Items.OnListChanged += HandleCargoChanged;

        if (IsServer)
            SyncFromCargo();
        else
            ApplyClientCargo();
    }

    public override void OnNetworkDespawn()
    {
        Items.OnListChanged -= HandleCargoChanged;
    }

    private void Update()
    {
        if (IsServer)
            SyncFromCargo();
    }

    private void SyncFromCargo()
    {
        List<NetworkCargoEntry> currentItems = new();
        int value = 0;

        foreach (LootItem item in cargo.ItemsInCargo)
        {
            if (item == null)
                continue;

            NetworkObject networkObject = item.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned)
                continue;

            currentItems.Add(new NetworkCargoEntry
            {
                NetworkObjectId = networkObject.NetworkObjectId,
                LocalPosition = item.transform.localPosition,
                LocalRotation = item.transform.localRotation
            });
            if (item.Data != null)
                value += item.Data.Value;
        }

        currentItems.Sort((left, right) => left.NetworkObjectId.CompareTo(right.NetworkObjectId));
        if (cachedItems.SequenceEqual(currentItems) && cachedCargoValue == value)
            return;

        Items.Clear();
        foreach (NetworkCargoEntry item in currentItems)
            Items.Add(item);

        cachedItems.Clear();
        cachedItems.AddRange(currentItems);
        cachedCargoValue = value;

        if (CargoValue.Value != value)
            CargoValue.Value = value;
    }

    private void HandleCargoChanged(NetworkListEvent<NetworkCargoEntry> changeEvent)
    {
        if (!IsServer)
            ApplyClientCargo();
    }

    private void ApplyClientCargo()
    {
        foreach (NetworkCargoEntry previousItem in cachedItems)
        {
            if (Items.Contains(previousItem))
                continue;

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    previousItem.NetworkObjectId, out NetworkObject previousObject) &&
                previousObject.transform.parent == transform)
                previousObject.transform.SetParent(null, true);
        }

        foreach (NetworkCargoEntry item in Items)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    item.NetworkObjectId, out NetworkObject networkObject))
                continue;

            networkObject.transform.SetParent(transform, false);
            networkObject.transform.localPosition = item.LocalPosition;
            networkObject.transform.localRotation = item.LocalRotation;
        }

        cachedItems.Clear();
        foreach (NetworkCargoEntry item in Items)
            cachedItems.Add(item);
    }
}