using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Personality and optional tables of the backgrounds, and generic roll tables of a pack (phase 22).
internal sealed partial class ContentPackValidator
{
    private const int TableEntryMaxLength = BackgroundPersonality.EntryMaxLength;
    private const int TableMaxEntries = BackgroundPersonality.MaxEntries;

    private BackgroundPersonality? Personality(string path, PackPersonalityJson personality)
    {
        if (personality.Traits is null && personality.Ideals is null && personality.Bonds is null && personality.Flaws is null)
        {
            AddError(path, "Indica al menos una tabla: traits, ideals, bonds o flaws.");
            return null;
        }

        var traits = TableEntries($"{path}.traits", personality.Traits);
        var bonds = TableEntries($"{path}.bonds", personality.Bonds);
        var flaws = TableEntries($"{path}.flaws", personality.Flaws);

        var ideals = new List<BackgroundIdeal>();
        if (personality.Ideals is { } list)
        {
            if (list.Count is 0 or > TableMaxEntries)
            {
                AddError($"{path}.ideals", $"Debe tener entre 1 y {TableMaxEntries} entradas.");
            }
            else
            {
                ForEach($"{path}.ideals", list, (idealPath, ideal) =>
                {
                    var text = RequiredText($"{idealPath}.text", ideal.Text, TableEntryMaxLength);
                    var alignment = NullableText($"{idealPath}.alignment", ideal.Alignment, ShortTextMaxLength);
                    if (text.Length > 0)
                    {
                        ideals.Add(new BackgroundIdeal(text, alignment));
                    }
                });
            }
        }

        var result = new BackgroundPersonality(traits, ideals, bonds, flaws);
        return result.IsEmpty ? null : result;
    }

    private List<BackgroundTable> BackgroundTables(string path, List<PackBackgroundTableJson?>? tables)
    {
        var result = new List<BackgroundTable>();
        if (tables is null)
        {
            return result;
        }

        if (tables.Count > BackgroundTable.MaxTables)
        {
            AddError(path, $"No puede tener más de {BackgroundTable.MaxTables} tablas.");
            return result;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        ForEach(path, tables, (tablePath, table) =>
        {
            var key = TableKey($"{tablePath}.key", table.Key);
            var name = RequiredText($"{tablePath}.name", table.Name, NameMaxLength);
            var entries = TableEntries($"{tablePath}.entries", table.Entries, required: true);
            if (key is null)
            {
                return;
            }

            if (!keys.Add(key))
            {
                AddError($"{tablePath}.key", $"La tabla '{key}' está repetida en el trasfondo.");
                return;
            }

            if (name.Length > 0 && entries.Count > 0)
            {
                result.Add(new BackgroundTable(key, name, entries));
            }
        });
        return result;
    }

    /// <summary>Texts of a background table: 1..<see cref="TableMaxEntries"/> non-blank entries of at most 500 characters.</summary>
    private List<string> TableEntries(string path, List<string?>? entries, bool required = false)
    {
        if (entries is null)
        {
            if (required)
            {
                AddError(path, "Campo obligatorio.");
            }

            return [];
        }

        if (entries.Count is 0 or > TableMaxEntries)
        {
            AddError(path, $"Debe tener entre 1 y {TableMaxEntries} entradas.");
            return [];
        }

        var result = new List<string>();
        ForEachText(path, entries, (entryPath, text) =>
        {
            if (text.Length > TableEntryMaxLength)
            {
                AddError(entryPath, $"No puede superar los {TableEntryMaxLength} caracteres.");
            }
            else
            {
                result.Add(text);
            }
        });
        return result;
    }

    /// <summary>Required key of lowercase letters, digits and hyphens. Null when invalid.</summary>
    private string? TableKey(string path, string? value)
    {
        var key = value?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            AddError(path, "Campo obligatorio.");
            return null;
        }

        if (key.Length > RollTable.KeyMaxLength || !IndexPattern().IsMatch(key))
        {
            AddError(path, $"Solo puede contener minúsculas, números y guiones (máximo {RollTable.KeyMaxLength} caracteres).");
            return null;
        }

        return key;
    }

    private void RollTables(List<PackRollTableJson?>? tables, ContentPackRows rows, Dictionary<string, string> packSubclasses)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        ForEach("rollTables", tables, (path, table) =>
        {
            var key = TableKey($"{path}.key", table.Key);
            if (key is not null && !keys.Add(key))
            {
                AddError($"{path}.key", $"La tabla '{key}' está repetida en el paquete.");
                key = null;
            }

            var name = RequiredText($"{path}.name", table.Name, NameMaxLength);
            var faces = RollTable.Faces(table.Dice);
            if (faces is null)
            {
                AddError($"{path}.dice", $"Dado no admitido; usa {string.Join(", ", RollTable.AllowedDice.Select(d => $"\"d{d}\""))}.");
            }

            var (classIndex, subclassIndex) = RollTableOwner(path, table, packSubclasses);
            var entries = faces is { } f ? RollTableEntries($"{path}.entries", table.Entries, f) : null;
            if (key is null || name.Length == 0 || faces is null || entries is null)
            {
                return;
            }

            rows.RollTables.Add(new RollTable
            {
                Source = _id,
                Key = key,
                Name = name,
                Dice = $"d{faces}",
                ClassIndex = classIndex,
                SubclassIndex = subclassIndex,
                EntriesJson = RollTable.SerializeEntries(entries),
            });
        });
    }

    /// <summary>Class and subclass of a roll table; the class is taken from the subclass when only that is given.</summary>
    private (string? ClassIndex, string? SubclassIndex) RollTableOwner(string path, PackRollTableJson table, Dictionary<string, string> packSubclasses)
    {
        var classIndex = table.ClassIndex?.Trim();
        if (string.IsNullOrEmpty(classIndex))
        {
            classIndex = null;
        }
        else if (!_classes.ContainsKey(classIndex))
        {
            AddError($"{path}.classIndex", $"La clase '{classIndex}' no existe en el catálogo.");
            classIndex = null;
        }

        var subclassIndex = table.SubclassIndex?.Trim();
        if (string.IsNullOrEmpty(subclassIndex))
        {
            return (classIndex, null);
        }

        string? subclassClass = null;
        if (packSubclasses.TryGetValue(subclassIndex, out var own) || _context.SrdSubclasses.TryGetValue(subclassIndex, out own))
        {
            subclassClass = own;
        }
        else if (_context.PackSubclasses?.TryGetValue(subclassIndex, out var other) == true && other.Source != _id)
        {
            subclassClass = other.ClassIndex;
        }

        if (subclassClass is null)
        {
            AddError($"{path}.subclassIndex", $"La subclase '{subclassIndex}' no existe en el SRD, en este paquete ni en otro paquete importado.");
            return (classIndex, null);
        }

        if (table.ClassIndex?.Trim() is { Length: > 0 } given && given != subclassClass)
        {
            AddError($"{path}.subclassIndex", $"La subclase '{subclassIndex}' es de la clase '{subclassClass}', no de '{given}'.");
        }

        return (subclassClass, subclassIndex);
    }

    /// <summary>Ranges of a roll table: every result 1..faces exactly once. Null when any entry is invalid.</summary>
    private List<RollTableEntry>? RollTableEntries(string path, List<PackRollTableEntryJson?>? entries, int faces)
    {
        if (entries is null || entries.Count == 0)
        {
            AddError(path, "Debe tener al menos una entrada.");
            return null;
        }

        var errors = _errors.Count;
        var result = new List<(RollTableEntry Entry, string Path)>();
        ForEach(path, entries, (entryPath, entry) =>
        {
            var from = RequiredInt($"{entryPath}.from", entry.From, 1, faces);
            var to = entry.To is null ? from : OptionalInt($"{entryPath}.to", entry.To, 1, faces);
            var text = RequiredText($"{entryPath}.text", entry.Text, RollTable.EntryMaxLength);
            if (from is { } a && to is { } b && a > b)
            {
                AddError($"{entryPath}.to", $"No puede ser menor que from ({a}).");
            }
            else if (from is { } f && to is { } t && text.Length > 0)
            {
                result.Add((new RollTableEntry(f, t, text), entryPath));
            }
        });

        if (_errors.Count > errors)
        {
            return null;
        }

        var next = 1;
        foreach (var (entry, entryPath) in result.OrderBy(r => r.Entry.From).ThenBy(r => r.Entry.To))
        {
            if (entry.From < next)
            {
                AddError(entryPath, $"El rango {Range(entry)} se solapa con otra entrada.");
                return null;
            }

            if (entry.From > next)
            {
                AddError(path, $"Faltan los resultados {Range(new RollTableEntry(next, entry.From - 1, string.Empty))} del d{faces}.");
                return null;
            }

            next = entry.To + 1;
        }

        if (next <= faces)
        {
            AddError(path, $"Faltan los resultados {Range(new RollTableEntry(next, faces, string.Empty))} del d{faces}.");
            return null;
        }

        return result.Select(r => r.Entry).ToList();
    }

    private static string Range(RollTableEntry entry) => entry.From == entry.To ? $"{entry.From}" : $"{entry.From}-{entry.To}";
}
