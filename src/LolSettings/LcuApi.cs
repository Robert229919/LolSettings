using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LolSettings;

public sealed class RiotException(string message) : Exception(message);

/// <summary>Compte actuellement connecté dans le client League of Legends.</summary>
public sealed record LcuSession(string Puuid, string RiotId, string Region, string BaseUrl, string Auth);

/// <summary>Réglages de jeu (vidéo, son, interface, souris…) et raccourcis clavier d'un compte.</summary>
public sealed record LolData(JsonObject? Game, JsonObject? Input);

/// <summary>
/// Accès à l'API locale du client League of Legends (LCU, via le lockfile) : identité du compte
/// connecté et lecture / écriture des réglages (game-settings et input-settings).
/// </summary>
public static class LcuApi
{
    const string GamePath = "/lol-game-settings/v1/game-settings";
    const string InputPath = "/lol-game-settings/v1/input-settings";

    static readonly HttpClient Http = new(new HttpClientHandler
    {
        // Le client utilise un certificat auto-signé, uniquement en local.
        ServerCertificateCustomValidationCallback = (req, _, _, _) => req.RequestUri?.Host == "127.0.0.1",
    })
    { Timeout = TimeSpan.FromSeconds(15) };

    public static bool IsGameRunning() => Process.GetProcessesByName("League of Legends").Length > 0;

    static string? FindLockfile()
    {
        // 1) Le client lancé : le lockfile est à côté de son exécutable (quelle que soit l'installation).
        foreach (var name in new[] { "LeagueClientUx", "LeagueClient" })
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    var exe = p.MainModule?.FileName;
                    if (exe is null) continue;
                    var f = Path.Combine(Path.GetDirectoryName(exe)!, "lockfile");
                    if (File.Exists(f)) return f;
                }
                catch { /* accès refusé (client lancé en administrateur) : on essaie les chemins usuels */ }
            }

        // 2) Emplacements d'installation usuels.
        var roots = new List<string>();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Riot Games", "League of Legends"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files", "Riot Games", "League of Legends"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files (x86)", "Riot Games", "League of Legends"));
        }
        return roots.Select(r => Path.Combine(r, "lockfile")).FirstOrDefault(File.Exists);
    }

    static async Task<(HttpStatusCode Status, string Body)> SendAsync(string baseUrl, string auth, HttpMethod method, string path, string? body = null)
    {
        using var q = new HttpRequestMessage(method, baseUrl + path);
        q.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        if (body is not null) q.Content = new StringContent(body, Encoding.UTF8, "application/json");
        try
        {
            using var r = await Http.SendAsync(q);
            return (r.StatusCode, await r.Content.ReadAsStringAsync());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new RiotException("Impossible de joindre le client League of Legends (est-il bien lancé et connecté ?).");
        }
    }

    static Task<(HttpStatusCode Status, string Body)> SendAsync(LcuSession s, HttpMethod method, string path, string? body = null) =>
        SendAsync(s.BaseUrl, s.Auth, method, path, body);

    public static async Task<LcuSession> ConnectAsync()
    {
        var lockfile = FindLockfile()
            ?? throw new RiotException("Client League of Legends non lancé : ouvre-le et connecte-toi au compte.");

        string text;
        try
        {
            using var fs = new FileStream(lockfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            text = (await sr.ReadToEndAsync()).Trim();
        }
        catch (IOException)
        {
            throw new RiotException("Lockfile du client League of Legends illisible : relance le client.");
        }

        // Format : name:pid:port:password:protocol
        var parts = text.Split(':');
        if (parts.Length < 5) throw new RiotException("Lockfile du client League of Legends illisible.");
        var baseUrl = $"{parts[4]}://127.0.0.1:{parts[2]}";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + parts[3]));

        var (status, body) = await SendAsync(baseUrl, auth, HttpMethod.Get, "/lol-summoner/v1/current-summoner");
        if (status != HttpStatusCode.OK)
            throw new RiotException("Aucun compte connecté dans le client League of Legends (attends la fin de la connexion).");

        JsonNode? j;
        try { j = JsonNode.Parse(body); }
        catch (JsonException) { throw new RiotException("Réponse du client League of Legends illisible."); }

        var puuid = Json.Str(j?["puuid"]);
        if (string.IsNullOrEmpty(puuid))
            throw new RiotException("Aucun compte connecté dans le client League of Legends.");

        var name = Json.Str(j?["gameName"]);
        var tag = Json.Str(j?["tagLine"]);
        var riotId = !string.IsNullOrWhiteSpace(name)
            ? (string.IsNullOrWhiteSpace(tag) ? name : $"{name}#{tag}")
            : Json.Str(j?["displayName"]) ?? puuid;

        var region = "";
        try
        {
            var (rs, rb) = await SendAsync(baseUrl, auth, HttpMethod.Get, "/riotclient/region-locale");
            if (rs == HttpStatusCode.OK) region = (Json.Str(JsonNode.Parse(rb)?["region"]) ?? "").ToUpperInvariant();
        }
        catch { /* la région est facultative */ }

        return new LcuSession(puuid, riotId, region, baseUrl, auth);
    }

    static async Task<JsonObject?> GetObjectAsync(LcuSession s, string path)
    {
        var (status, body) = await SendAsync(s, HttpMethod.Get, path);
        if (status == HttpStatusCode.NotFound) return null;
        if (status != HttpStatusCode.OK)
            throw new RiotException($"Lecture des réglages refusée par le client (HTTP {(int)status}) : {Trunc(body)}");
        try { return JsonNode.Parse(body) as JsonObject; }
        catch (JsonException) { throw new RiotException("Réglages du client illisibles."); }
    }

    public static async Task<LolData> GetSettingsAsync(LcuSession s) =>
        new(await GetObjectAsync(s, GamePath), await GetObjectAsync(s, InputPath));

    static async Task PatchAsync(LcuSession s, string path, JsonObject patch)
    {
        var (status, body) = await SendAsync(s, HttpMethod.Patch, path, patch.ToJsonString());
        if ((int)status is < 200 or >= 300)
            throw new RiotException($"Écriture des réglages refusée par le client (HTTP {(int)status}) : {Trunc(body)}");
    }

    public static Task PatchGameAsync(LcuSession s, JsonObject patch) => PatchAsync(s, GamePath, patch);
    public static Task PatchInputAsync(LcuSession s, JsonObject patch) => PatchAsync(s, InputPath, patch);

    /// <summary>Demande au client d'enregistrer les réglages (point d'accès facultatif : erreurs ignorées).</summary>
    public static async Task TrySaveAsync(LcuSession s)
    {
        try { await SendAsync(s, HttpMethod.Post, "/lol-game-settings/v1/save", "{}"); }
        catch { /* pas fatal */ }
    }

    static string Trunc(string s) => s.Length > 300 ? s[..300] + "…" : s;
}

public static class Json
{
    public static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static string? Str(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
