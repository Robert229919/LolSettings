using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LolSettings;

public enum Category { Souris, Touches, Interface, Audio, Graphismes }

/// <summary>
/// Classement des réglages League of Legends par catégorie, sélection fine et fusion source → cible.
/// Les réglages ont deux origines : "game" (game-settings : section → clé → valeur) et
/// "input" (input-settings : raccourcis clavier, même forme).
/// </summary>
public static partial class Categories
{
    public const string Game = "game";
    public const string Input = "input";

    /// <summary>Catégories proposées dans l'interface, dans l'ordre d'affichage.</summary>
    public static readonly Category[] Selectable = [.. Enum.GetValues<Category>()];

    public static string Label(Category c) => c switch
    {
        Category.Souris => "Souris & caméra",
        Category.Touches => "Touches (raccourcis)",
        Category.Interface => "Interface & HUD",
        Category.Audio => "Audio & voix",
        Category.Graphismes => "Graphismes & vidéo",
        _ => c.ToString(),
    };

    [GeneratedRegex(@"volume|voice|sound|audio|music|mute|mic(?:rophone)?|speaker", RegexOptions.IgnoreCase)]
    private static partial Regex AudioRx();

    [GeneratedRegex(@"width|height|windowmode|resolution|quality|shadow|frame|vsync|antialias|gamma|brightness|inset|colors|aspect|monitor|graphics|fps|effects|hudanimation|cinematic", RegexOptions.IgnoreCase)]
    private static partial Regex GraphicsRx();

    [GeneratedRegex(@"mouse|camera|scroll|cursor|click|edgepan|follow|autoattack|targetchampions|smartcast|quickcast", RegexOptions.IgnoreCase)]
    private static partial Regex MouseRx();

    public static Category Classify(string kind, string section, string key)
    {
        if (kind == Input) return Category.Touches;
        if (AudioRx().IsMatch(section) || AudioRx().IsMatch(key)) return Category.Audio;
        if (section.Equals("Performance", StringComparison.OrdinalIgnoreCase)) return Category.Graphismes;
        if (section.Equals("Mouse", StringComparison.OrdinalIgnoreCase) || section.Equals("Camera", StringComparison.OrdinalIgnoreCase))
            return Category.Souris;
        if (!section.Equals("HUD", StringComparison.OrdinalIgnoreCase))
        {
            if (GraphicsRx().IsMatch(key)) return Category.Graphismes;
            if (MouseRx().IsMatch(key)) return Category.Souris;
        }
        return Category.Interface;
    }

    /// <summary>Parcourt "section → clé → valeur" (une valeur posée à la racine a une section vide).</summary>
    static IEnumerable<(string Section, string Key, JsonNode? Value)> Flatten(JsonObject? o)
    {
        if (o is null) yield break;
        foreach (var (k, v) in o)
        {
            if (v is JsonObject sec)
            {
                foreach (var (k2, v2) in sec) yield return (k, k2, v2);
            }
            else yield return ("", k, v);
        }
    }

    static string Id(string kind, string section, string key) => $"{kind}|{section}|{key}";

    static JsonNode? Get(JsonObject? o, string section, string key) =>
        section.Length == 0 ? o?[key] : (o?[section] as JsonObject)?[key];

    /// <summary>Nombre de valeurs par catégorie.</summary>
    public static Dictionary<Category, int> Count(JsonObject? game, JsonObject? input)
    {
        var d = Enum.GetValues<Category>().ToDictionary(c => c, _ => 0);
        foreach (var (s, k, _) in Flatten(game)) d[Classify(Game, s, k)]++;
        foreach (var (s, k, _) in Flatten(input)) d[Classify(Input, s, k)]++;
        return d;
    }

    // ---------- Détail des réglages (sélection fine) ----------

    /// <summary>Un élément sélectionnable : une valeur de réglage.</summary>
    public sealed record SettingItem(string Id, Category Category, string Name, string Value);

    public static List<SettingItem> Items(JsonObject? game, JsonObject? input)
    {
        var list = new List<SettingItem>();
        var seen = new HashSet<string>();
        foreach (var (s, k, v) in Flatten(game))
            if (seen.Add(Id(Game, s, k))) list.Add(new(Id(Game, s, k), Classify(Game, s, k), k, Display(v)));
        foreach (var (s, k, v) in Flatten(input))
            if (seen.Add(Id(Input, s, k))) list.Add(new(Id(Input, s, k), Category.Touches, k, Display(v)));
        return list;
    }

    /// <summary>Valeur JSON en texte court pour l'affichage.</summary>
    public static string Display(JsonNode? v)
    {
        var s = v is null ? "null" : Norm(v);
        if (v is JsonValue jv && jv.TryGetValue<string>(out var str)) s = str;
        return s.Length > 60 ? s[..60] + "…" : s;
    }

    /// <summary>Forme normalisée d'une valeur pour comparer (1 = "1", true = "True", 0.50 = 0.5).</summary>
    static string Norm(JsonNode? n)
    {
        if (n is null) return "null";
        if (n is JsonValue v)
        {
            if (v.TryGetValue<string>(out var str))
                return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)
                    ? ds.ToString("0.#####", CultureInfo.InvariantCulture)
                    : str.ToLowerInvariant();
            var kind = v.GetValueKind();
            if (kind == JsonValueKind.True) return "true";
            if (kind == JsonValueKind.False) return "false";
            if (kind == JsonValueKind.Number && v.TryGetValue<double>(out var d))
                return d.ToString("0.#####", CultureInfo.InvariantCulture);
        }
        return n.ToJsonString();
    }

    static bool Same(JsonNode? a, JsonNode? b) => Norm(a) == Norm(b);

    /// <summary>Ne garde que les valeurs dont l'id est dans <paramref name="include"/>. Renvoie aussi leur nombre.</summary>
    public static (JsonObject Result, int Kept) Filter(JsonObject? src, string kind, ISet<string> include)
    {
        var result = new JsonObject();
        int kept = 0;
        foreach (var (s, k, v) in Flatten(src))
        {
            if (!include.Contains(Id(kind, s, k))) continue;
            if (s.Length == 0) result[k] = v?.DeepClone();
            else
            {
                if (result[s] is not JsonObject sec) { sec = new JsonObject(); result[s] = sec; }
                sec[k] = v?.DeepClone();
            }
            kept++;
        }
        return (result, kept);
    }

    /// <summary>
    /// Construit ce qu'il faut envoyer au client : pour chaque section touchée, la section actuelle de la cible
    /// avec les valeurs du profil (des catégories choisies) par-dessus. Ne renvoie que ce qui change vraiment.
    /// </summary>
    public static (JsonObject Patch, List<(string Section, string Key)> Changed) BuildPatch(
        JsonObject? current, JsonObject? data, string kind, ISet<Category> cats)
    {
        var patch = new JsonObject();
        var changed = new List<(string, string)>();
        foreach (var (s, k, v) in Flatten(data))
        {
            if (!cats.Contains(Classify(kind, s, k))) continue;
            if (Same(Get(current, s, k), v)) continue;
            changed.Add((s, k));
            if (s.Length == 0) { patch[k] = v?.DeepClone(); continue; }
            if (patch[s] is not JsonObject sec)
            {
                sec = current?[s] is JsonObject cs ? cs.DeepClone().AsObject() : new JsonObject();
                patch[s] = sec;
            }
            sec[k] = v?.DeepClone();
        }
        return (patch, changed);
    }

    /// <summary>Nombre de valeurs de <paramref name="changed"/> qui ne valent pas ce que contient <paramref name="expected"/>.</summary>
    public static int Mismatches(JsonObject? actual, JsonObject expected, IEnumerable<(string Section, string Key)> changed) =>
        changed.Count(c => !Same(Get(actual, c.Section, c.Key), Get(expected, c.Section, c.Key)));
}
