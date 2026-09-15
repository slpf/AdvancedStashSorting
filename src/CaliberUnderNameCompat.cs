using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace AdvancedStashSorting;

internal static class CaliberUnderNameCompat
{
    internal const string PluginGuid = "com.slpf.caliberundername";
    private const string NamesSection = "4. Caliber Names";
    private static Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private static bool _warningLogged;

    public static int Version { get; private set; }

    public static void Refresh()
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal);

        try
        {
            if (Chainloader.PluginInfos.TryGetValue(PluginGuid, out var plugin) && plugin.Instance != null)
                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in plugin.Instance.Config)
                    if (entry.Key.Section == NamesSection && entry.Value is ConfigEntry<string> value &&
                        !string.IsNullOrWhiteSpace(value.Value))
                        names[entry.Key.Key] = value.Value;

            _warningLogged = false;
        }
        catch (Exception exception)
        {
            names.Clear();

            if (!_warningLogged)
                Plugin.LogSource?.LogWarning($"Failed to read CaliberUnderName caliber names: {exception.Message}");

            _warningLogged = true;
        }

        if (SameNames(names)) return;

        _names = names;
        Version++;
    }

    public static bool TryGetName(string caliber, out string name)
    {
        return _names.TryGetValue(caliber, out name);
    }

    private static bool SameNames(Dictionary<string, string> names)
    {
        if (_names.Count != names.Count) return false;

        foreach (KeyValuePair<string, string> entry in names)
            if (!_names.TryGetValue(entry.Key, out string name) || name != entry.Value)
                return false;

        return true;
    }
}
