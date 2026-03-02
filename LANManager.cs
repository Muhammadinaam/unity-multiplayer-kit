// ============================================================
// LANManager  –  Local network (WiFi / LAN) play
// ============================================================
// No Unity Services. No internet. No costs.
// Works on the same WiFi network (home, LAN party, office).
//
// HOW IT WORKS
//   Host  – Starts NGO host + broadcasts "I am here" UDP packets every second.
//   Client – Listens for broadcasts, fires OnHostDiscovered with the host's IP.
//           Call ConnectToHost(ip) to join, or ConnectByIP(ip) if you already
//           know the host's IP (typed in manually).
//
// SETUP
//   Add to the same GameObject as NetworkManager.
//   Keep default ports unless something conflicts on your network.
//
// USAGE  (host side)
//   LANManager.Instance.StartLANHost();
//   Debug.Log("My IP: " + LANManager.Instance.LocalIP);  // show in UI
//
// USAGE  (client side)
//   LANManager.Instance.OnHostDiscovered += ip => ConnectToHost(ip);
//   LANManager.Instance.StartDiscovery();
//   // OR skip discovery and connect directly:
//   LANManager.Instance.ConnectByIP("192.168.1.42");
// ============================================================

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LANManager : MonoBehaviour
{
    public static LANManager Instance { get; private set; }

    [SerializeField] private ushort gamePort      = 7777;
    [SerializeField] private int    discoveryPort = 47777;

    // Host's IP on the local network – show this in UI so friends can type it.
    public string LocalIP => GetLocalIP();

    // Fires on the main thread when a host is found. Arg = host IP string.
    public event Action<string> OnHostDiscovered;

    private UdpClient   udpHost;
    private UdpClient   udpClient;
    private Thread      bgThread;
    private bool        bgRunning;

    // Thread-safe queue to pass discovered IPs to the main thread.
    private readonly ConcurrentQueue<string> foundHosts = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Host ─────────────────────────────────────────────────────────────────

    public void StartLANHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(LocalIP, gamePort);
        NetworkManager.Singleton.StartHost();

        bgRunning = true;
        udpHost   = new UdpClient { EnableBroadcast = true };
        bgThread  = new Thread(AnnounceLoop) { IsBackground = true };
        bgThread.Start();

        Debug.Log($"[LANManager] Hosting on {LocalIP}:{gamePort}");
    }

    // ── Client ───────────────────────────────────────────────────────────────

    // Listen for host broadcasts. Subscribe to OnHostDiscovered first.
    public void StartDiscovery()
    {
        bgRunning  = true;
        udpClient  = new UdpClient(discoveryPort) { EnableBroadcast = true };
        bgThread   = new Thread(DiscoverLoop) { IsBackground = true };
        bgThread.Start();
    }

    public void StopDiscovery()
    {
        bgRunning = false;
        udpClient?.Close();
        udpClient = null;
    }

    // Connect to a host whose IP you already know (skip discovery).
    public void ConnectByIP(string hostIp) => ConnectToHost(hostIp);

    // Connect to a discovered host (call from OnHostDiscovered callback).
    public void ConnectToHost(string hostIp)
    {
        StopDiscovery();
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(hostIp, gamePort);
        NetworkManager.Singleton.StartClient();
        Debug.Log($"[LANManager] Connecting to {hostIp}:{gamePort}");
    }

    // ── Main-thread pump ─────────────────────────────────────────────────────

    private void Update()
    {
        while (foundHosts.TryDequeue(out var ip))
            OnHostDiscovered?.Invoke(ip);
    }

    // ── Background threads ───────────────────────────────────────────────────

    // Host broadcasts its presence every second.
    private void AnnounceLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
        var message  = Encoding.UTF8.GetBytes($"GAME:{gamePort}");

        while (bgRunning)
        {
            try   { udpHost.Send(message, message.Length, endpoint); }
            catch { break; }
            Thread.Sleep(1000);
        }
    }

    // Client listens for host announcements.
    private void DiscoverLoop()
    {
        var sender = new IPEndPoint(IPAddress.Any, 0);
        var seen   = new System.Collections.Generic.HashSet<string>();

        while (bgRunning)
        {
            try
            {
                byte[] data    = udpClient.Receive(ref sender);
                string message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("GAME:") && seen.Add(sender.Address.ToString()))
                    foundHosts.Enqueue(sender.Address.ToString());
            }
            catch { break; }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetLocalIP()
    {
        foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            if (addr.AddressFamily == AddressFamily.InterNetwork)
                return addr.ToString();
        return "127.0.0.1";
    }

    private void OnDestroy()
    {
        bgRunning = false;
        udpHost?.Close();
        udpClient?.Close();
    }
}
