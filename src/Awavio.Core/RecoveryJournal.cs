using System.Text.Json;

namespace Awavio.Core;

/// <summary>What a session changed, written to disk before the change is made,
/// so a crash, a kill or a power loss can still be undone later.</summary>
public sealed record JournalEntry(
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    Guid? Scheme,
    LidActions? OriginalLid);

public sealed class JournalCorruptException(string path, Exception inner)
    : Exception($"The recovery journal at {path} can't be read.", inner);

public sealed class RecoveryJournal(string path)
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    /// <summary>Null when there is no journal. Throws <see cref="JournalCorruptException"/>
    /// rather than guessing, because a wrong guess would write the wrong lid action.</summary>
    public JournalEntry? Load()
    {
        if (!File.Exists(Path)) return null;
        try
        {
            var contents = File.ReadAllText(Path);
            using var document = JsonDocument.Parse(contents);
            var root = document.RootElement;
            foreach (var name in new[] { "StartedAt", "EndsAt", "Scheme", "OriginalLid" })
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out _))
                    throw new JsonException($"Missing journal field: {name}.");
            var entry = JsonSerializer.Deserialize<JournalEntry>(contents, Options)
                ?? throw new JsonException("empty journal");
            if (entry.StartedAt == default || entry.EndsAt < entry.StartedAt ||
                (entry.Scheme is null) != (entry.OriginalLid is null) || entry.Scheme == Guid.Empty)
                throw new JsonException("Invalid recovery journal state.");
            if (entry.OriginalLid is { } lid)
            {
                var original = root.GetProperty("OriginalLid");
                if (!original.TryGetProperty("Ac", out _) || !original.TryGetProperty("Dc", out _) || lid.Ac > 3 || lid.Dc > 3)
                    throw new JsonException("Invalid original lid settings.");
            }
            return entry;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new JournalCorruptException(Path, e);
        }
    }

    public void Save(JournalEntry entry) => AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(entry, Options));

    public void Delete()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
