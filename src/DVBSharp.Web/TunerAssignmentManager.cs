using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;

namespace DVBSharp.Web;

public sealed class TunerAssignmentManager
{
    private readonly string _storagePath;
    private readonly ConcurrentDictionary<string, TunerAssignment> _assignments = new(StringComparer.OrdinalIgnoreCase);

    public TunerAssignmentManager(string storagePath)
    {
        _storagePath = storagePath;
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Load();
    }

    public IReadOnlyCollection<TunerAssignment> GetAssignments() => _assignments.Values.ToList();

    public TunerAssignment? GetAssignment(string tunerId)
        => _assignments.TryGetValue(tunerId, out var assignment) ? assignment : null;

    public async Task<TunerAssignment> SetAssignmentAsync(string tunerId, int frequency, string? label)
    {
        var assignment = new TunerAssignment
        {
            TunerId = tunerId,
            Frequency = frequency,
            Label = label
        };

        _assignments[tunerId] = assignment;
        await PersistAsync();
        return assignment;
    }

    public async Task<bool> RemoveAssignmentAsync(string tunerId)
    {
        var removed = _assignments.TryRemove(tunerId, out _);
        if (removed)
        {
            await PersistAsync();
        }

        return removed;
    }

    private void Load()
    {
        if (!File.Exists(_storagePath)) return;
        var json = File.ReadAllText(_storagePath);
        var payload = JsonSerializer.Deserialize<List<TunerAssignment>>(json) ?? new List<TunerAssignment>();
        foreach (var assignment in payload)
        {
            _assignments[assignment.TunerId] = assignment;
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_assignments.Values, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_storagePath, json);
    }
}
