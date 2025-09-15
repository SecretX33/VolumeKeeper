using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services.Managers;

public class ProcessDataManager
{
    private readonly ConcurrentDictionary<int, string> _knownProcesses = new();

    public bool AddProcess(int processId, string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        return _knownProcesses.TryAdd(processId, executableName);
    }

    public bool UpdateProcess(int processId, string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        _knownProcesses[processId] = executableName;
        return true;
    }

    public bool RemoveProcess(int processId) => _knownProcesses.TryRemove(processId, out _);

    public string? GetProcess(int processId) => _knownProcesses.GetOrNull(processId);

    public IReadOnlyDictionary<int, string> GetAllProcesses() => new Dictionary<int, string>(_knownProcesses);

    public bool IsProcessKnown(int processId) => _knownProcesses.ContainsKey(processId);

    public IEnumerable<int> GetProcessesByExecutable(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return Enumerable.Empty<int>();

        return _knownProcesses
            .Where(kvp => string.Equals(kvp.Value, executableName, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key);
    }

    public void RemoveProcesses(IEnumerable<int> processIds)
    {
        foreach (var id in processIds)
        {
            _knownProcesses.TryRemove(id, out _);
        }
    }

    public void Clear() => _knownProcesses.Clear();

    public int Count => _knownProcesses.Count;
}
