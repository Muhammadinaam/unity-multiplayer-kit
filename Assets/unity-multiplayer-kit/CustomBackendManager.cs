using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class MatchData
{
    public string matchId;
    public string serverIp;
    public ushort serverPort;
}

public class CustomBackendManager : MonoBehaviour
{
    public static CustomBackendManager Instance { get; private set; }

    [SerializeField] private string apiBaseUrl = "https://yourserver.com/api";

    public MatchData CurrentMatch { get; private set; }

    public event Action<MatchData> OnMatchFound;
    public event Action<string>    OnError;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Ask backend to create/start a new dedicated server match.
    // Pass extra JSON fields (e.g. map, mode) in payload if your API needs them.
    public async Task CreateMatchAsync(string jsonPayload = "{}")
    {
        var match = await PostAsync<MatchData>("/matches/create/", jsonPayload);
        if (match != null) ConnectAsClient(match);
    }

    // Find any open match on the backend.
    public async Task QuickJoinAsync()
    {
        var match = await GetAsync<MatchData>("/matches/quickjoin/");
        if (match != null) ConnectAsClient(match);
    }

    // Join a specific match by its ID.
    public async Task JoinByIdAsync(string matchId)
    {
        var match = await GetAsync<MatchData>($"/matches/{matchId}/join/");
        if (match != null) ConnectAsClient(match);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ConnectAsClient(MatchData match)
    {
        CurrentMatch = match;
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(match.serverIp, match.serverPort);
        NetworkManager.Singleton.StartClient();
        OnMatchFound?.Invoke(match);
    }

    // ── Auth hook ─────────────────────────────────────────────────────────────
    // Add your token / session header here if your API requires authentication.
    private void AddAuthHeaders(UnityWebRequest req)
    {
        // req.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("AuthToken"));
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────

    private async Task<T> GetAsync<T>(string path)
    {
        using var req = UnityWebRequest.Get(apiBaseUrl + path);
        AddAuthHeaders(req);
        await Send(req);
        return Parse<T>(req);
    }

    private async Task<T> PostAsync<T>(string path, string json)
    {
        using var req = new UnityWebRequest(apiBaseUrl + path, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        req.SetRequestHeader("Content-Type", "application/json");
        AddAuthHeaders(req);
        await Send(req);
        return Parse<T>(req);
    }

    private Task Send(UnityWebRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
        req.SendWebRequest().completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }

    private T Parse<T>(UnityWebRequest req)
    {
        if (req.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke($"{req.url} – {req.error}");
            return default;
        }
        return JsonUtility.FromJson<T>(req.downloadHandler.text);
    }
}
