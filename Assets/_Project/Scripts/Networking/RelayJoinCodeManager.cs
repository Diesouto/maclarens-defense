using System;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayJoinCodeManager : MonoBehaviour
{
    private const string ConnectionType = "dtls";

    public static RelayJoinCodeManager Instance { get; private set; }

    [SerializeField] private NetworkBootstrapper bootstrapper;

    private UnityTransport transport;
    private Task servicesInitialization;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (bootstrapper == null)
            bootstrapper = GetComponent<NetworkBootstrapper>();
        transport = bootstrapper != null ? bootstrapper.Transport : GetComponent<UnityTransport>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public async Task<string> StartHostAsync(int maxPlayers)
    {
        await EnsureServicesReadyAsync();

        if (bootstrapper.IsRunning)
            throw new InvalidOperationException("A network session is already running.");

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Clamp(maxPlayers - 1, 1, 3));
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
        bootstrapper.SetMaxPlayers(maxPlayers);

        if (!bootstrapper.StartHost())
            throw new InvalidOperationException("NGO could not start the host.");

        return joinCode;
    }

    public async Task JoinHostAsync(string joinCode)
    {
        await EnsureServicesReadyAsync();

        if (bootstrapper.IsRunning)
            throw new InvalidOperationException("A network session is already running.");

        string normalizedCode = string.IsNullOrWhiteSpace(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length == 0)
            throw new ArgumentException("Enter a join code.", nameof(joinCode));

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(normalizedCode);
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
        if (!bootstrapper.StartClient())
            throw new InvalidOperationException("NGO could not start the client.");
    }

    public void LeaveSession()
    {
        bootstrapper?.Shutdown();
    }

    private async Task EnsureServicesReadyAsync()
    {
        if (bootstrapper == null)
            bootstrapper = NetworkBootstrapper.Instance != null ? NetworkBootstrapper.Instance : GetComponent<NetworkBootstrapper>();
        if (transport == null && bootstrapper != null)
            transport = bootstrapper.Transport;
        if (transport == null)
            transport = GetComponent<UnityTransport>();

        if (bootstrapper == null || transport == null)
            throw new InvalidOperationException("Assign NetworkBootstrapper and UnityTransport before using Relay.");

        if (servicesInitialization == null)
            servicesInitialization = UnityServices.InitializeAsync();

        await servicesInitialization;
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}