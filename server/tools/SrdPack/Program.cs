using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Tools.SrdPack;

// Converts the SRD 5.1 dataset into the base content pack of the D&D 5e module (format 3). Usage, from server/:
//   dotnet run --project tools/SrdPack [-- --seed <dir> --out <file>]
// Defaults: src/Systems/Dnd5e/seed/srd and src/Systems/Dnd5e/seed/srd-5.1.pack.json. The pack version is "5.1.<n>":
// <n> stays when the content written is the same as the existing file's and grows by one when it changes, so that
// running the tool again without changes leaves the file as it is and instances re-import only real changes.
var seed = Option("--seed") ?? Path.Combine(ServerDirectory(), "src", "Systems", "Dnd5e", "seed", "srd");
var output = Option("--out") ?? Path.Combine(ServerDirectory(), "src", "Systems", "Dnd5e", "seed", "srd-5.1.pack.json");

SrdDataset.Directory = seed;
var optionSets = JsonNode.Parse(File.ReadAllText(Path.Combine(seed, "option-sets.json")))!.AsObject();
var levelChoices = JsonNode.Parse(File.ReadAllText(Path.Combine(seed, "level-choices.json")))!.AsObject();
var pack = new PackWriter(SrdDataset.Load(), SrdBeasts.Load(), optionSets, levelChoices).Write();

var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var revision = 1;
if (File.Exists(output) && JsonNode.Parse(File.ReadAllText(output)) is JsonObject existing
    && existing["version"]?.GetValue<string>() is { } version && version.StartsWith("5.1.", StringComparison.Ordinal)
    && int.TryParse(version["5.1.".Length..], out var previous))
{
    existing["version"] = string.Empty;
    revision = JsonNode.DeepEquals(existing, pack) ? previous : previous + 1;
}

pack["version"] = $"5.1.{revision}";
File.WriteAllText(output, pack.ToJsonString(options) + "\n");
Console.WriteLine($"{output}: version {pack["version"]}, {new FileInfo(output).Length / 1024} KiB");
foreach (var (key, value) in pack)
{
    if (value is JsonArray array)
    {
        Console.WriteLine($"  {key}: {array.Count}");
    }
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

// The server directory: the nearest ancestor of the working directory with OpenTrpg.slnx.
static string ServerDirectory()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "OpenTrpg.slnx")))
        {
            return directory.FullName;
        }

        if (File.Exists(Path.Combine(directory.FullName, "server", "OpenTrpg.slnx")))
        {
            return Path.Combine(directory.FullName, "server");
        }
    }

    throw new InvalidOperationException("Run the tool inside the repository (OpenTrpg.slnx not found).");
}
