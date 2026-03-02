================================================================
  MULTIPLAYER FRAMEWORK  –  HOW TO USE
  Copy the entire Multiplayer/ folder into any Unity project.
================================================================


----------------------------------------------------------------
  1. REQUIRED PACKAGES
----------------------------------------------------------------
Install via Window -> Package Manager -> Add by name:

  com.unity.netcode.gameobjects
  com.unity.services.lobby
  com.unity.services.relay

Enable Lobby + Relay in the Unity Dashboard for your project.


----------------------------------------------------------------
  2. SCRIPTS AT A GLANCE
----------------------------------------------------------------

  CORE
  ├── LobbyManager.cs          Lobby + Relay (public / private match)
  ├── GameNetworkManager.cs    Player spawning + scene loading
  ├── GameState.cs             Synced game phase + round number
  └── PlayerData.cs            Synced name, ready state, player number

  ATTACH TO PLAYER / NPC PREFABS
  ├── NetworkedPlayerMovement.cs   Server-auth movement + client prediction
  ├── NetworkedNPC.cs              Base class for server-controlled AI
  └── NetworkedHealth.cs           Synced health (damage, heal, death)


----------------------------------------------------------------
  3. SCENE SETUP
----------------------------------------------------------------

  GameObject: NetworkManager
    - NetworkManager component
    - UnityTransport component
    - LobbyManager component
    - GameNetworkManager component
      Inspector: assign Spawn Point Transforms

  GameObject: GameState  (empty GO, persists across scenes)
    - NetworkObject component
    - GameState component
    Tip: tick "Don't Destroy With Scene" on the NetworkObject.

  Player Prefab:
    - NetworkObject
    - NetworkTransform           (Authority: Server)
    - PlayerData
    - NetworkedPlayerMovement    (or your custom movement script)
    - NetworkedHealth            (optional)
    Add to NetworkManager's Network Prefabs list.

  NPC Prefab:
    - NetworkObject
    - NetworkTransform           (Authority: Server)
    - YourEnemy : NetworkedNPC   (subclass, see section 7)
    - NetworkedHealth            (optional)


----------------------------------------------------------------
  4. FLOW
----------------------------------------------------------------

  App start
    LobbyManager.Instance.InitializeAsync()      <- call once

  Host a match
    LobbyManager.Instance.CreateLobbyAsync(false)   // public
    LobbyManager.Instance.CreateLobbyAsync(true)    // private
    LobbyManager.Instance.LobbyCode                 // share this code

  Join a match
    LobbyManager.Instance.QuickJoinAsync()          // public
    LobbyManager.Instance.JoinByCodeAsync("CODE")   // private

  In lobby
    PlayerData.SetName("Alice")
    PlayerData.SetReady(true)

  Start game  (server/host)
    if (GameState.Instance.AllPlayersReady())
        GameNetworkManager.Instance.LoadScene("GameScene")

  Game over  (server/host)
    GameNetworkManager.Instance.ReturnToLobby("LobbyScene")

  Leave
    LobbyManager.Instance.LeaveLobby()


----------------------------------------------------------------
  5. LOBBYMANAGER  –  API
----------------------------------------------------------------

  InitializeAsync()              Sign in + init Unity Services.
  CreateLobbyAsync(isPrivate)    Create lobby, relay, start host.
  QuickJoinAsync()               Join random public lobby, start client.
  JoinByCodeAsync(code)          Join private lobby by code, start client.
  LeaveLobby()                   Remove self from lobby, shutdown network.

  LobbyCode                      Lobby code string to show in UI.
  CurrentLobby                   Unity Lobby object (has player list etc.)

  Events:
    OnLobbyCreated   Action<Lobby>
    OnLobbyJoined    Action<Lobby>
    OnLobbyLeft      Action
    OnError          Action<string>


----------------------------------------------------------------
  6. GAMESTATE  –  API
----------------------------------------------------------------

  Phase.Value                    Current GamePhase enum value.
  RoundNumber.Value              Current round (int).

  SetPhase(GamePhase)            Server only.
  SetRound(int)                  Server only.
  AllPlayersReady()              Server only. True when all players are ready.

  GamePhase enum:  Lobby | Starting | InGame | GameOver

  Subscribe to changes (any client):
    GameState.Instance.Phase.OnValueChanged += (old, newPhase) => ...;

  Add your own synced variables:
    public NetworkVariable<int> Score = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


----------------------------------------------------------------
  7. PLAYERDATA  –  API  (attach to Player prefab)
----------------------------------------------------------------

  PlayerNumber.Value             int,  set by server after spawn (1-based).
  PlayerName.Value               FixedString64Bytes, set by owner.
  IsReady.Value                  bool, set by owner.

  SetName(string)                Owner only. Also saves to PlayerPrefs.
  SetReady(bool)                 Owner only.
  ToggleReady()                  Owner only.

  Events:
    OnNameChanged          Action<FixedString64Bytes>
    OnReadyChanged         Action<bool>
    OnPlayerNumberChanged  Action<int>

  Example – lobby list entry:
    data.OnReadyChanged += ready => readyIcon.SetActive(ready);
    nameText.text = data.PlayerName.Value.ToString();


----------------------------------------------------------------
  8. NETWORKEDPLAYERMOVEMENT  –  API  (attach to Player prefab)
----------------------------------------------------------------

  Inspector fields:
    Move Speed           units per second
    Movement Mode        Horizontal | Vertical | TopDown2D | TopDown3D
    Input Send Threshold min axis delta before sending RPC (default 0.01)

  Movement Modes:
    Horizontal   – X axis only       (lane games, pong)
    Vertical     – Y axis only       (pong vertical)
    TopDown2D    – X + Y axes        (2D overhead RPG, shooter)
    TopDown3D    – X + Z axes        (3D overhead, WASD on ground plane)

  Client Prediction:
    Owner moves locally the same frame input is read (no perceived lag).
    Server moves authoritatively; NetworkTransform interpolates corrections.
    Hosts (IsOwner + IsServer) skip prediction – they have no latency.

  Requires NetworkTransform set to Server authority on the same prefab.


----------------------------------------------------------------
  9. NETWORKEDNPC  –  USAGE  (subclass this)
----------------------------------------------------------------

  Override GetMoveDirection() and return a direction vector.
  The base class normalizes it and applies moveSpeed for you.
  All logic runs only on the server.

  Example – simple patrol:

    public class PatrolEnemy : NetworkedNPC
    {
        [SerializeField] private Transform[] waypoints;
        private int index;

        protected override Vector3 GetMoveDirection()
        {
            Vector3 dir = waypoints[index].position - transform.position;
            if (dir.magnitude < stoppingDistance)
                index = (index + 1) % waypoints.Length;
            return dir;
        }
    }

  Helper methods available in subclasses:
    MoveToward(Vector3 target)          move toward a position
    FaceDirection(Vector3 dir, bool 2D) rotate to face direction


----------------------------------------------------------------
  10. NETWORKEDHEALTH  –  API  (attach to Player or NPC prefab)
----------------------------------------------------------------

  CurrentHealth.Value            int,  synced to all clients.
  MaxHealth                      int,  Inspector field.
  IsDead                         bool property.

  TakeDamageServerRpc(int)       Any client or server can call this.
  Heal(int)                      Server only.
  Revive(int withHealth = -1)    Server only. -1 restores full health.
  Kill()                         Server only. Sets health to 0.

  Events (fire on every client):
    OnHealthChanged    Action<int currentHealth, int maxHealth>
    OnDied             Action

  Example – health bar:
    health.OnHealthChanged += (cur, max) =>
        healthBar.fillAmount = (float)cur / max;

  Example – death:
    health.OnDied += () => gameObject.SetActive(false);

  Example – dealing damage from a projectile:
    other.GetComponent<NetworkedHealth>()?.TakeDamageServerRpc(25);


----------------------------------------------------------------
  11. DEDICATED SERVER NOTES
----------------------------------------------------------------

  All movement is already server-authoritative.
  Relay handles NAT traversal (no port forwarding needed).

  To go dedicated server:
    In LobbyManager.CreateLobbyAsync: replace StartHost() with StartServer()
    Run that build headless (BatchMode).
    Clients call StartClient() as usual – nothing else changes.


----------------------------------------------------------------
  12. NON-MOVEMENT INPUT  (fire, click, interact, abilities)
----------------------------------------------------------------

Use NetworkedActions.cs on your Player prefab.

THREE PATTERNS:

  A) FIRE / ATTACK (discrete event)
     Owner: plays cosmetic immediately (OnFireLocal event)
     ServerRpc: server does real work (spawn bullet, raycast hit)
     ClientRpc: server tells all OTHER clients to play shared effect

  B) WORLD-SPACE CLICK  (click-to-move, ability targeting, RTS)
     Owner: raycasts locally, gets Vector3 worldPos
     OnClickWorld event: show move marker / aim preview instantly
     ServerRpc: sends worldPos to server, server validates and acts

  C) TOGGLE / STATE  (reload, interact, open door)
     Owner: fires ServerRpc
     Server: updates a NetworkVariable (e.g. Ammo)
     All clients: react via OnValueChanged – no ClientRpc needed

SUBCLASS EXAMPLE:

  public class SoldierActions : NetworkedActions
  {
      [SerializeField] private NetworkObject bulletPrefab;
      [SerializeField] private Transform     muzzle;

      protected override void OnFireServer(ulong shooterClientId)
      {
          var bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
          bullet.Spawn();
      }

      protected override void OnFireEffectAllClients()
      {
          // Play muzzle flash / sound for other players watching
          muzzleParticles.Play();
      }

      protected override void OnWorldClickServer(ulong clientId, Vector3 pos)
      {
          GetComponent<NavMeshAgent>().SetDestination(pos);
      }
  }

SUBSCRIBE EXAMPLE (from a UI or companion script):

  var actions = GetComponent<NetworkedActions>();
  actions.OnFireLocal    += () => audioSource.PlayOneShot(gunshotClip);
  actions.OnClickWorld   += pos => moveMarker.transform.position = pos;
  actions.OnAmmoChanged  += (cur, max) => ammoText.text = cur + "/" + max;

  // Trigger reload from a UI button:
  actions.RequestReload();

KEY RULES:
  - Never spawn GameObjects on the client – only on the server.
  - Cosmetic effects (sound, particles, camera shake) can run on owner
    immediately (no RPC needed) for instant feedback.
  - Shared effects that all players must see require a ClientRpc from server.
  - State that UI needs (ammo, cooldown) belongs in a NetworkVariable.


----------------------------------------------------------------
  13. CUSTOM BACKEND  (skip Unity Lobby entirely)
----------------------------------------------------------------

Use CustomBackendManager.cs instead of LobbyManager.cs.
Add it to the same GO as NetworkManager.

Your Django backend is responsible for:
  - Storing open matches in a DB
  - Spinning up dedicated game server processes (or using a pool)
  - Returning the server's IP + port to the client

Required Django endpoints (adapt names to your API):

  POST /matches/create/
    Body (optional): { "map": "arena", "mode": "tdm", ... }
    Response:        { "matchId": "abc", "serverIp": "1.2.3.4", "serverPort": 7777 }

  GET  /matches/quickjoin/
    Response:        { "matchId": "abc", "serverIp": "1.2.3.4", "serverPort": 7777 }

  GET  /matches/<id>/join/
    Response:        { "matchId": "abc", "serverIp": "1.2.3.4", "serverPort": 7777 }

Unity side usage:

  await CustomBackendManager.Instance.CreateMatchAsync();
  await CustomBackendManager.Instance.QuickJoinAsync();
  await CustomBackendManager.Instance.JoinByIdAsync("abc123");

  // With custom payload (e.g. skill rating, map preference):
  await CustomBackendManager.Instance.CreateMatchAsync(
      JsonUtility.ToJson(new { map = "forest", minRank = 1200 })
  );

  // Subscribe to results:
  CustomBackendManager.Instance.OnMatchFound += match => Debug.Log(match.serverIp);
  CustomBackendManager.Instance.OnError      += err   => ShowErrorUI(err);

Auth headers:
  Open AddAuthHeaders() in CustomBackendManager.cs and uncomment the line.
  Store the token in PlayerPrefs after login:
    PlayerPrefs.SetString("AuthToken", tokenFromLoginResponse);

Django dedicated server launch example (simple approach):
  When POST /matches/create/ is called, use subprocess.Popen() to start
  the Unity server build in headless mode, wait for it to report ready
  (via a callback endpoint the Unity server calls on startup), then
  return the IP + port to the Unity client.

  subprocess.Popen([
      "./MyGame_Server", "-batchmode", "-nographics",
      "-port", str(port), "-matchId", match_id
  ])


----------------------------------------------------------------
  13. UNITY LOBBY  –  FILTERING BY SKILL / ATTRIBUTES
----------------------------------------------------------------

Unity Lobby supports filtering via QueryLobbiesAsync.
You can filter on custom numeric or string fields.

STEP 1 – Store skill in lobby data when creating:

  var opts = new CreateLobbyOptions
  {
      Data = new Dictionary<string, DataObject>
      {
          // N1-N5 = numeric indexed fields (filterable)
          // S1-S5 = string indexed fields  (filterable)
          { "MinRank", new DataObject(
              DataObject.VisibilityOptions.Public,
              "1000",
              DataObject.IndexOptions.N1) },

          { "MaxRank", new DataObject(
              DataObject.VisibilityOptions.Public,
              "1500",
              DataObject.IndexOptions.N2) },

          { "GameMode", new DataObject(
              DataObject.VisibilityOptions.Public,
              "ranked",
              DataObject.IndexOptions.S1) },
      }
  };

STEP 2 – Query with filters:

  int playerRank = 1200;

  var results = await LobbyService.Instance.QueryLobbiesAsync(
      new QueryLobbiesOptions
      {
          Filters = new List<QueryFilter>
          {
              // Lobby's MinRank <= playerRank
              new QueryFilter(QueryFilter.FieldOptions.N1,
                              playerRank.ToString(),
                              QueryFilter.OpOptions.LE),

              // Lobby's MaxRank >= playerRank
              new QueryFilter(QueryFilter.FieldOptions.N2,
                              playerRank.ToString(),
                              QueryFilter.OpOptions.GE),

              // GameMode == "ranked"
              new QueryFilter(QueryFilter.FieldOptions.S1,
                              "ranked",
                              QueryFilter.OpOptions.EQ),
          },
          Order = new List<QueryOrder>
          {
              // Most available slots first
              new QueryOrder(asc: false, field: QueryOrder.FieldOptions.AvailableSlots)
          }
      }
  );

  foreach (var lobby in results.Results)
      Debug.Log(lobby.Name + " – " + lobby.AvailableSlots + " slots");

IMPORTANT LIMITS
  You get 5 numeric (N1-N5) and 5 string (S1-S5) indexed fields per lobby.
  For real MMR/ELO matchmaking (where you want close skill gaps, timeout
  expansion, etc.) use Unity Matchmaker service instead – it has a proper
  queue system. Lobby filtering is manual and best for simple cases.


----------------------------------------------------------------
  14. LAN / WIFI PLAY  (no internet, no Unity Services, free)
----------------------------------------------------------------

Use LANManager.cs instead of LobbyManager.cs.
Add it to the same GO as NetworkManager.

All players must be on the SAME WiFi or wired network.
No Relay needed = zero latency overhead from relay servers.

HOST SIDE

  LANManager.Instance.StartLANHost();

  // Show this IP in your UI so friends can type it manually:
  string myIp = LANManager.Instance.LocalIP;

CLIENT SIDE – Option A: Auto-discover host

  // Subscribe before starting discovery
  LANManager.Instance.OnHostDiscovered += ip =>
  {
      // Called when a host is found on the network.
      // You can auto-connect or show a "Found game at {ip}" button.
      LANManager.Instance.ConnectToHost(ip);
  };

  LANManager.Instance.StartDiscovery();
  // Stop when done: LANManager.Instance.StopDiscovery();

CLIENT SIDE – Option B: Type IP manually

  LANManager.Instance.ConnectByIP("192.168.1.42");

NOTES
  - Discovery uses UDP broadcast on port 47777 (configurable in Inspector).
  - Game traffic uses port 7777 (configurable in Inspector).
  - Discovery deduplicates: OnHostDiscovered fires once per unique host IP.
  - Works on Windows, Mac, Linux, Android, iOS (same WiFi).
  - iOS/Android may need explicit network permissions in their manifests.
  - Some corporate/university networks block UDP broadcast –
    in that case use Option B (manual IP) as fallback.

MIXING LAN + ONLINE
  Nothing stops you from having both LANManager and LobbyManager in the
  scene and letting the player choose in your main menu:
    [Play Online]  ->  LobbyManager (Unity Relay, works globally)
    [Play on LAN]  ->  LANManager   (direct, no internet needed)


================================================================
  END
================================================================
