using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdvancedStashSorting.Sorting;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AdvancedStashSorting;

public static class Config
{
    private static string _configPath;
    private static bool _dirty;

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = new List<JsonConverter>
        {
            new StringEnumConverter()
        }
    };

    public static void Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Config path must be provided.", nameof(configPath));

        _configPath = configPath;
        _dirty = false;

        if (!File.Exists(_configPath))
        {
            _dirty = true;
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            ConfigData loaded = JsonConvert.DeserializeObject<ConfigData>(json, SerializerSettings);

            if (loaded == null)
            {
#if DEBUG
                Plugin.LogSource?.LogWarning("Invalid AdvancedStashSorting.json config value, defaults will be used.");
#endif
                return;
            }

            if (Apply(loaded))
            {
#if DEBUG
                Plugin.LogSource?.LogWarning("AdvancedStashSorting.json contained invalid values and was normalized.");
#endif
                _dirty = true;
                Save();
            }
        }
#if DEBUG
        catch (Exception exception)
#else
        catch (Exception)
#endif
        {
#if DEBUG
            Plugin.LogSource?.LogWarning(
                $"Failed to load AdvancedStashSorting.json, defaults will be used: {exception.Message}");
#endif
        }
    }

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static void Save()
    {
        if (!_dirty || string.IsNullOrEmpty(_configPath)) return;

        try
        {
            string directory = Path.GetDirectoryName(_configPath);

            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(CreateData(), SerializerSettings);
            File.WriteAllText(_configPath, json);

            _dirty = false;
        }
        catch (Exception exception)
        {
            Plugin.LogSource?.LogError(
                $"Failed to save AdvancedStashSorting.json: {exception.Message}");
        }
    }

    public static void SaveContainerCategories(CompoundItem container, IEnumerable<string> categories,
        IEnumerable<string> availableCategories)
    {
        ContainerCategorySettings.SetSelection(container, categories, availableCategories);

        MarkDirty();
        Save();
    }

    private static bool Apply(ConfigData data)
    {
        bool changed = false;
        SortSection sort = data.Sort;

        if (sort == null)
        {
            sort = new SortSection();
            changed = true;
        }

        RaritySection rarity = data.Rarity;

        if (rarity == null)
        {
            rarity = new RaritySection();
            changed = true;
        }

        SortSettings.SortOrder = NormalizeSortOrder(sort.SortOrder, out bool sortOrderChanged);
        changed |= sortOrderChanged;

        List<string> normalizedCategoryOrder = CategoryCatalog.NormalizeOrder(sort.CategoryOrder);

        changed |= sort.CategoryOrder == null || !sort.CategoryOrder.SequenceEqual(normalizedCategoryOrder);

        SortSettings.CategoryOrder = normalizedCategoryOrder;
        SortSettings.FoldingEnabled = sort.Fold;
        SortSettings.StackingEnabled = sort.Stack;
        SortSettings.NestingEnabled = sort.Nesting;
        SortSettings.RecursiveNestingEnabled = sort.RecursiveNesting;
        SortSettings.CompactSortingEnabled = sort.CompactSorting;
        SortSettings.SeparationEnabled = sort.Separation;

        changed |= ContainerCategorySettings.Load(data.ContainerCategories);

        if (!Enum.IsDefined(typeof(RarityColorPreset), rarity.Preset))
        {
            rarity.Preset = RarityColorPreset.Original;
            changed = true;
        }

        RaritySettings.ActivePreset = rarity.Preset;
        RaritySettings.CustomColors =
            RaritySettings.NormalizeCustomColors(rarity.CustomColors, out bool rarityColorsChanged);
        RaritySettings.MarkTierLookupDirty();

        if (rarityColorsChanged)
        {
#if DEBUG
            Plugin.LogSource?.LogWarning("Invalid Rarity.CustomColors config value was normalized.");
#endif
            changed = true;
        }

        return changed;
    }

    private static List<SortTypeSetting> NormalizeSortOrder(IEnumerable<SortTypeSetting> settings, out bool changed)
    {
        changed = settings == null;
        List<SortTypeSetting> normalized = [];
        HashSet<SortType> added = [];

        if (settings != null)
            foreach (SortTypeSetting setting in settings)
            {
                if (setting == null || !Enum.IsDefined(typeof(SortType), setting.Type) || !added.Add(setting.Type))
                {
                    changed = true;
                    continue;
                }

                SortDirection direction = setting.Direction;

                if (!Enum.IsDefined(typeof(SortDirection), direction))
                {
                    direction = SortDirection.Ascending;
                    changed = true;
                }

                normalized.Add(new SortTypeSetting
                {
                    Type = setting.Type,
                    Enabled = setting.Enabled,
                    Direction = direction
                });
            }

        List<SortTypeSetting> defaults = SortSettings.CreateDefaultSortOrder();

        foreach (SortTypeSetting defaultSetting in defaults)
        {
            if (!added.Add(defaultSetting.Type)) continue;

            normalized.Add(defaultSetting);
            changed = true;
        }

        return normalized;
    }

    private static ConfigData CreateData()
    {
        return new ConfigData
        {
            Sort = new SortSection
            {
                SortOrder = SortSettings.SortOrder
                    .Select(setting => new SortTypeSetting
                    {
                        Type = setting.Type, Enabled = setting.Enabled, Direction = setting.Direction
                    })
                    .ToList(),
                CategoryOrder = [..SortSettings.CategoryOrder],
                Fold = SortSettings.FoldingEnabled,
                Stack = SortSettings.StackingEnabled,
                Nesting = SortSettings.NestingEnabled,
                RecursiveNesting = SortSettings.RecursiveNestingEnabled,
                CompactSorting = SortSettings.CompactSortingEnabled,
                Separation = SortSettings.SeparationEnabled
            },
            Rarity = new RaritySection
            {
                Preset = RaritySettings.ActivePreset,
                CustomColors = [..RaritySettings.CustomColors]
            },
            ContainerCategories = ContainerCategorySettings.Export()
        };
    }

    private sealed class ConfigData
    {
        public SortSection Sort { get; set; } = new();
        public RaritySection Rarity { get; set; } = new();
        public Dictionary<string, List<string>> ContainerCategories { get; set; } = new();
    }

    private sealed class SortSection
    {
        public List<SortTypeSetting> SortOrder { get; set; } = SortSettings.CreateDefaultSortOrder();
        public List<string> CategoryOrder { get; set; } = [..CategoryCatalog.DefaultOrder];
        public bool Fold { get; set; } = true;
        public bool Stack { get; set; } = true;
        public bool Nesting { get; set; }
        public bool RecursiveNesting { get; set; }
        public bool CompactSorting { get; set; } = true;
        public bool Separation { get; set; }
    }

    private sealed class RaritySection
    {
        public RarityColorPreset Preset { get; set; } = RarityColorPreset.Original;
        public List<string> CustomColors { get; set; } = [..RaritySettings.GetPresetColors(RarityColorPreset.Original)];
    }
}
