using System.Text;
using System.Text.Json.Nodes;

namespace LolSettings;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Diagnostic en lecture seule : LolSettings.exe --check <dossier>
        if (args.Length >= 2 && args[0] == "--check")
            return CheckAsync(args[1]).GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    static async Task<int> CheckAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        int code = 0;
        try
        {
            var s = await LcuApi.ConnectAsync();
            sb.AppendLine($"Compte : {s.RiotId} ({s.Puuid}) région {s.Region}");
            var d = await LcuApi.GetSettingsAsync(s);
            sb.AppendLine($"game-settings : {(d.Game is null ? "absent" : "ok")} ; input-settings : {(d.Input is null ? "absent" : "ok")}");
            foreach (var (c, n) in Categories.Count(d.Game, d.Input)) sb.AppendLine($"  {c} : {n}");
            File.WriteAllText(Path.Combine(outDir, "game-settings.json"), (d.Game ?? new JsonObject()).ToJsonString(Json.Pretty));
            File.WriteAllText(Path.Combine(outDir, "input-settings.json"), (d.Input ?? new JsonObject()).ToJsonString(Json.Pretty));
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERREUR : " + ex.Message);
            code = 1;
        }
        File.WriteAllText(Path.Combine(outDir, "check.txt"), sb.ToString());
        return code;
    }
}
