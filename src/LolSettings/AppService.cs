using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace LolSettings;

/// <summary>Actions métier : enregistrer, appliquer, exporter, importer.</summary>
public static class AppService
{
    /// <param name="include">Ids choisis (voir <see cref="Categories.Items"/>) ; null = tout enregistrer.</param>
    public static async Task<Profile> SaveCurrentAsync(LcuSession s, string name, ISet<string>? include = null)
    {
        var data = await LcuApi.GetSettingsAsync(s);
        if (data.Game is null && data.Input is null)
            throw new RiotException($"Aucun réglage trouvé pour {s.RiotId}.");
        var p = new Profile
        {
            Name = name, SourceRiotId = s.RiotId, SourcePuuid = s.Puuid,
            Game = data.Game, Input = data.Input,
        };
        if (include is not null) ApplySelection(p, include);
        ProfileStore.Save(p);
        return p;
    }

    /// <summary>Réduit un profil complet aux éléments choisis.</summary>
    public static void ApplySelection(Profile p, ISet<string> include)
    {
        var available = Categories.Items(p.Game, p.Input);
        var (game, k1) = Categories.Filter(p.Game, Categories.Game, include);
        var (input, k2) = Categories.Filter(p.Input, Categories.Input, include);
        if (k1 + k2 == 0) throw new RiotException("Choisis au moins un réglage à enregistrer.");
        p.Game = k1 > 0 ? game : null;
        p.Input = k2 > 0 ? input : null;
        p.Partial = available.Any(i => !include.Contains(i.Id));
    }

    public static async Task ApplyAsync(LcuSession s, Profile p, ISet<Category> cats, Action<string> log)
    {
        if (cats.Count == 0) throw new RiotException("Coche au moins une catégorie.");
        if (LcuApi.IsGameRunning())
            throw new RiotException("Une partie est en cours. Quitte-la (garde le client ouvert) puis réessaie.");

        log($"Lecture des réglages actuels de {s.RiotId}…");
        var current = await LcuApi.GetSettingsAsync(s);
        var backup = new Profile
        {
            Name = $"Sauvegarde {s.RiotId} {DateTime.Now:yyyy-MM-dd HH-mm-ss}",
            SourceRiotId = s.RiotId, SourcePuuid = s.Puuid, IsBackup = true,
            Game = current.Game, Input = current.Input,
            ExpiresAt = DateTime.Now + ProfileStore.BackupLifetime,
        };
        ProfileStore.Save(backup);
        log($"Sauvegarde automatique ({ProfileStore.BackupLifetime.TotalMinutes:0} min) : {backup.Name}");

        var (gamePatch, gameChanged) = Categories.BuildPatch(current.Game, p.Game, Categories.Game, cats);
        var (inputPatch, inputChanged) = Categories.BuildPatch(current.Input, p.Input, Categories.Input, cats);
        if (gameChanged.Count + inputChanged.Count == 0)
        {
            log("Réglages déjà identiques, rien à envoyer.");
            log("Terminé.");
            return;
        }

        log($"Envoi au client : {gameChanged.Count} réglage(s) de jeu, {inputChanged.Count} touche(s)…");
        if (gameChanged.Count > 0) await LcuApi.PatchGameAsync(s, gamePatch);
        if (inputChanged.Count > 0) await LcuApi.PatchInputAsync(s, inputPatch);
        await LcuApi.TrySaveAsync(s);

        await Task.Delay(700);
        var check = await LcuApi.GetSettingsAsync(s);
        var left = Categories.Mismatches(check.Game, gamePatch, gameChanged)
                 + Categories.Mismatches(check.Input, inputPatch, inputChanged);
        log(left == 0
            ? "✔ Réglages appliqués et vérifiés."
            : $"⚠ Le client a accepté l'envoi mais {left} valeur(s) diffèrent à la relecture.");
        log($"Terminé. Lance une partie avec {s.RiotId} pour voir le résultat.");
    }

    // ---------- Export / import ----------

    public const string FileExtension = ".lolprofile";
    const string CodePrefix = "LOLSETTINGS1:";

    static string Serialize(Profile p) => JsonSerializer.Serialize(new Profile
    {
        Name = p.Name, CreatedAt = p.CreatedAt, SourceRiotId = p.SourceRiotId, SourcePuuid = p.SourcePuuid,
        Game = p.Game, Input = p.Input, Partial = p.Partial,
    }, Json.Pretty);

    public static void ExportFile(Profile p, string path) => File.WriteAllText(path, Serialize(p), new UTF8Encoding(false));

    public static string ToCode(Profile p)
    {
        var raw = Encoding.UTF8.GetBytes(Serialize(p));
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)) ds.Write(raw);
        return CodePrefix + Convert.ToBase64String(ms.ToArray());
    }

    public static Profile ImportFile(string path) => ImportJson(File.ReadAllText(path));

    public static Profile ImportCode(string code)
    {
        code = string.Concat(code.Where(c => !char.IsWhiteSpace(c)));
        if (!code.StartsWith(CodePrefix, StringComparison.Ordinal))
            throw new RiotException("Ce n'est pas un code LolSettings (il doit commencer par LOLSETTINGS1:).");
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(code[CodePrefix.Length..]));
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var sr = new StreamReader(ds, Encoding.UTF8);
            return ImportJson(sr.ReadToEnd());
        }
        catch (Exception e) when (e is FormatException or InvalidDataException)
        {
            throw new RiotException("Code incomplet ou abîmé : recopie-le en entier.");
        }
    }

    static Profile ImportJson(string json)
    {
        Profile? p;
        try { p = JsonSerializer.Deserialize<Profile>(json); }
        catch (JsonException) { throw new RiotException("Fichier de profil illisible."); }
        if (p is null || (p.Game is null && p.Input is null))
            throw new RiotException("Ce profil ne contient aucun réglage.");
        p.IsBackup = false;
        p.FilePath = "";
        if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Profil importé";
        ProfileStore.Save(p);
        return p;
    }
}
