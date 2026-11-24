using System.Text.Json;
using System.Text.Json.Nodes;

namespace LolSettings;

public sealed class AppConfig
{
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LolSettings");

    static string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Current { get; private set; } = Load();

    static AppConfig Load()
    {
        Directory.CreateDirectory(Dir);
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch { /* config corrompue : valeurs par défaut */ }
        var c = new AppConfig();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(c, Json.Pretty));
        return c;
    }
}

public sealed class Profile
{
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SourceRiotId { get; set; } = "";
    public string SourcePuuid { get; set; } = "";
    public bool IsBackup { get; set; }
    /// <summary>Réglages de jeu (game-settings) : section → clé → valeur.</summary>
    public JsonObject? Game { get; set; }
    /// <summary>Raccourcis clavier (input-settings).</summary>
    public JsonObject? Input { get; set; }
    /// <summary>Profil ne contenant qu'une partie des réglages.</summary>
    public bool Partial { get; set; }

    /// <summary>Sauvegardes auto : date de suppression définitive (null = CreatedAt + durée de vie).</summary>
    public DateTime? ExpiresAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? Expiry => IsBackup ? ExpiresAt ?? CreatedAt + ProfileStore.BackupLifetime : null;

    [System.Text.Json.Serialization.JsonIgnore]
    public string FilePath { get; set; } = "";
}

/// <summary>Profils enregistrés dans %APPDATA%\LolSettings\profiles (sauvegardes auto dans backups\).</summary>
public static class ProfileStore
{
    public static string ProfilesDir => Path.Combine(AppConfig.Dir, "profiles");
    public static string BackupsDir => Path.Combine(ProfilesDir, "backups");

    /// <summary>Durée de vie d'une sauvegarde automatique avant suppression définitive.</summary>
    public static readonly TimeSpan BackupLifetime = TimeSpan.FromHours(1);

    /// <summary>Supprime définitivement les sauvegardes automatiques expirées. Renvoie le nombre supprimé.</summary>
    public static int PurgeExpiredBackups()
    {
        if (!Directory.Exists(BackupsDir)) return 0;
        int n = 0;
        foreach (var f in Directory.GetFiles(BackupsDir, "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(f));
                if (p is null) continue;
                p.IsBackup = true;
                if (p.Expiry <= DateTime.Now) { File.Delete(f); n++; }
            }
            catch { /* fichier illisible ou verrouillé : on réessaiera */ }
        }
        return n;
    }

    public static List<Profile> LoadAll(bool includeBackups)
    {
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(BackupsDir);
        PurgeExpiredBackups();
        var files = Directory.GetFiles(ProfilesDir, "*.json").AsEnumerable();
        if (includeBackups) files = files.Concat(Directory.GetFiles(BackupsDir, "*.json"));
        var list = new List<Profile>();
        foreach (var f in files)
        {
            try
            {
                var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(f));
                if (p is null) continue;
                p.FilePath = f;
                list.Add(p);
            }
            catch { /* fichier illisible ignoré */ }
        }
        return list.OrderBy(p => p.IsBackup).ThenByDescending(p => p.CreatedAt).ToList();
    }

    public static void Save(Profile p)
    {
        var dir = p.IsBackup ? BackupsDir : ProfilesDir;
        Directory.CreateDirectory(dir);
        if (string.IsNullOrEmpty(p.FilePath))
        {
            var baseName = Sanitize(p.Name);
            var path = Path.Combine(dir, baseName + ".json");
            for (int i = 2; File.Exists(path); i++) path = Path.Combine(dir, $"{baseName} ({i}).json");
            p.FilePath = path;
        }
        File.WriteAllText(p.FilePath, JsonSerializer.Serialize(p, Json.Pretty));
    }

    public static void Delete(Profile p)
    {
        if (File.Exists(p.FilePath)) File.Delete(p.FilePath);
    }

    static string Sanitize(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(s) ? "profil" : s;
    }
}
