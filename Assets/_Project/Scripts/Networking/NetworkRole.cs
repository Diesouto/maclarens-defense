using Unity.Netcode;

public static class NetworkRole
{
    // True on a connected pure client; false offline, on the host and on a dedicated server.
    public static bool IsClientOnly
    {
        get
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening && !networkManager.IsServer;
        }
    }
}
