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

// Clients never reparent loot NetworkObjects (server-only in NGO); they pin cargo items to this
// carriage every LateUpdate instead, overriding the world-space NetworkTransform that would lag behind.
[DefaultExecutionOrder(1000)]
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
        if (IsServer)
            SyncFromCargo();
    }

    private void Update()
    {
        if (IsServer)
            SyncFromCargo();
    }

    private void LateUpdate()
    {
        if (!IsSpawned || IsServer)
            return;

        foreach (NetworkCargoEntry item in Items)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    item.NetworkObjectId, out NetworkObject networkObject) ||
                !networkObject.gameObject.activeInHierarchy)
                continue;

            networkObject.transform.SetPositionAndRotation(
                transform.TransformPoint(item.LocalPosition),
                transform.rotation * item.LocalRotation);
        }
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
                LocalPosition = transform.InverseTransformPoint(item.transform.position),
                LocalRotation = Quaternion.Inverse(transform.rotation) * item.transform.rotation
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
}