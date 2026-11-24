using System.Text.Json;

namespace LolSettings;

/// <summary>Ce que l'appli retient d'un compte (Riot ID vu à la connexion, région, surnom choisi).</summary>
public sealed class AccountInfo
{
    public string? RiotId { get; set; }
    public string? Label { get; set; }
    public string? Region { get; set; }
    public DateTime? LastSeen { get; set; }
}

public static class AccountStore
{
    static string FilePath => Path.Combine(AppConfig.Dir, "accounts.json");
    static readonly object Gate = new();

    public static Dictionary<string, AccountInfo> Load()
    {
        lock (Gate)
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Dictionary<string, AccountInfo>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch { /* fichier corrompu : on repart de zéro */ }
            return new();
        }
    }

    static void Save(Dictionary<string, AccountInfo> d)
    {
        lock (Gate) File.WriteAllText(FilePath, JsonSerializer.Serialize(d, Json.Pretty));
    }

    /// <summary>Mémorise le Riot ID et la région du compte connecté.</summary>
    public static void Seen(string puuid, string riotId, string region)
    {
        puuid = puuid.ToLowerInvariant();
        var d = Load();
        var a = d.TryGetValue(puuid, out var x) ? x : d[puuid] = new AccountInfo();
        if (a.RiotId == riotId && a.Region == region && a.LastSeen > DateTime.Now.AddMinutes(-10)) return;
        a.RiotId = riotId;
        a.Region = region;
        a.LastSeen = DateTime.Now;
        Save(d);
    }

    public static void SetLabel(string puuid, string? label)
    {
        var d = Load();
        var a = d.TryGetValue(puuid, out var x) ? x : d[puuid] = new AccountInfo();
        a.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        Save(d);
    }
}
