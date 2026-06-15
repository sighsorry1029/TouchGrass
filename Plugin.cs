using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace TouchGrass;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class TouchGrassPlugin : BaseUnityPlugin
{
    internal const string ModName = "TouchGrass";
    internal const string ModVersion = "1.0.4";
    internal const string Author = "sighsorry";
    private const string ModGUID = $"{Author}.{ModName}";
    private static string ConfigFileName = $"{ModGUID}.cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    internal static string ConnectionError = "";
    private readonly Harmony _harmony = new(ModGUID);
    public static readonly ManualLogSource TouchGrassLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
    private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
    private FileSystemWatcher? _watcher;
    private readonly object _reloadLock = new();
    private DateTime _lastConfigReloadTime;
    private const long RELOAD_DELAY = 10000000; // One second

    public enum Toggle
    {
        On = 1,
        Off = 0
    }

    public enum FatigueStatusEffectDisplay
    {
        Off = 0,
        Detailed = 1
    }

    public enum TrainingDummyDamageType
    {
        Blunt = 0,
        Slash = 1,
        Pierce = 2,
        Fire = 3,
        Frost = 4,
        Lightning = 5,
        Poison = 6,
        Spirit = 7
    }

    public enum TrainingMeterDisplay
    {
        Off = 0,
        Detailed = 1
    }

    public void Awake()
    {
        bool saveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;

        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        _locationFullEfficiencySeconds = config("2 - Skill Gate", "Location Full Efficiency Seconds", 120f, new ConfigDescription("Stationary farmable skill gains stay at full value for this many chained seconds before fading.", new AcceptableValueRange<float>(0f, 3600f)));
        _locationFadeSeconds = config("2 - Skill Gate", "Location Fade Seconds", 180f, new ConfigDescription("After full-efficiency time, stationary farmable skill gains fade to the minimum multiplier over this many seconds.", new AcceptableValueRange<float>(1f, 7200f)));
        _locationMinimumMultiplier = config("2 - Skill Gate", "Location Minimum Multiplier", 0.1f, new ConfigDescription("Lowest multiplier applied to repeated stationary farmable skill gains.", new AcceptableValueRange<float>(0f, 1f)));
        _stationaryRadius = config("2 - Skill Gate", "Stationary XZ Radius", 4f, new ConfigDescription("Radius on the X/Z plane. If farmable skill gains keep happening without leaving this radius, location efficiency fades.", new AcceptableValueRange<float>(0.5f, 50f)));
        _fatigueStationarySkillGains = config("2 - Skill Gate", "Fatigue Stationary Skill Gains", Toggle.On, $"If on, repeated farmable skill gains within the stationary X/Z radius share one global fading multiplier instead of being blocked outright. Affected skills: {SkillNameFormatter.FormatList(SkillEarnGate.GetLocationFatigueSkillTypes())}.");
        _fatigueStatusEffectDisplay = config("2 - Skill Gate", "Fatigue Status Effect Display", FatigueStatusEffectDisplay.Detailed, "Controls the local player's fatigue status effect display. Detailed shows the status effect icon and static compendium details; Off hides it.");

        _trainingDummyHealth = config("3 - Training Dummy", "Training Dummy Health", 2500f, new ConfigDescription("Max health applied to piece_TrainingDummy.", new AcceptableValueRange<float>(1f, 100000f)));
        _trainingDummyCrowdingRadius = config("3 - Training Dummy", "Training Dummy Crowding Radius", 4f, new ConfigDescription("XZ radius used to discourage placing too many training dummies in one spot. 0 disables the crowding check.", new AcceptableValueRange<float>(0f, 50f)));
        _trainingDummyCrowdingMaxCount = config("3 - Training Dummy", "Training Dummy Crowding Max Count", 4, new ConfigDescription("Maximum existing training dummies allowed inside the crowding radius. 4 means the 5th placement is discouraged. 0 disables the crowding check.", new AcceptableValueRange<int>(0, 100)));
        _trainingDummyNightAggro = config("3 - Training Dummy", "Training Dummy Night Aggro", Toggle.Off, "If on, training dummies detect players in a 16m radius at night and slide close enough for their native attack.");
        _trainingDummyRecipe = config("3 - Training Dummy", "Training Dummy Recipe", "FineWood:5,BronzeNails:10,Ectoplasm:5", "Recipe override for piece_TrainingDummy. Leave empty to keep vanilla, use None for no cost, or use ItemPrefab:Amount,ItemPrefab:Amount. Default: FineWood:5,BronzeNails:10,Ectoplasm:5. Materials are always recovered when dismantling.");
        _localTrainingDummyDamageType = config("3 - Training Dummy", "Training Dummy Damage Type", TrainingDummyDamageType.Blunt, "Damage type used by local training dummy damage tests.", false);
        _localTrainingDummyDamageAmount = config("3 - Training Dummy", "Training Dummy Damage", 1f, new ConfigDescription("Damage amount used by local training dummy damage tests.", new AcceptableValueRange<float>(1f, 500f)), false);
        _trainingMeterDisplay = config("3 - Training Dummy", "Training Meter Display", TrainingMeterDisplay.Detailed, "Controls the integrated client-side training HUD for piece_TrainingDummy damage, incoming hits, and skill gains.", false);
        _trainingMeterWindowSeconds = config("3 - Training Dummy", "Training Meter Window Seconds", 15f, new ConfigDescription("Rolling time window used by the training HUD.", new AcceptableValueRange<float>(5f, 300f)), false);

        _archeryTargetSkillMultiplier = config("4 - Archery Target", "Archery Target Skill Multiplier", 1f, new ConfigDescription("Multiplier applied to skill experience awarded by piece_ArcheryTarget. 0 disables archery target skill gain.", new AcceptableValueRange<float>(0f, 10f)));
        _archeryTargetArrowBoltSkillOnly = config("4 - Archery Target", "Archery Target Arrow Bolt Skill Only", Toggle.On, "If on, piece_ArcheryTarget awards skill experience only for projectiles fired with arrow or bolt ammo. Other projectiles can still score hits but award no skill experience.");
        _archeryTargetRecipe = config("4 - Archery Target", "Archery Target Recipe", "FineWood:4,LeatherScraps:10", "Recipe override for piece_ArcheryTarget. Leave empty to keep vanilla, use None for no cost, or use ItemPrefab:Amount,ItemPrefab:Amount. Default: FineWood:4,LeatherScraps:10. Materials are always recovered when dismantling.");

        BindTrainingObjectConfigEvents();
        BindPerSkillModifierConfigs();


        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();

        Config.Save();
        if (saveOnSet)
        {
            Config.SaveOnConfigSet = saveOnSet;
        }
    }

    private void OnDestroy()
    {
        SaveWithRespectToConfigSet();
        _watcher?.Dispose();
    }

    private void OnGUI()
    {
        TrainingMeter.OnGUI();
        TrainingDummySettingsWindow.OnGUI();
    }

    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
        _watcher.Changed += ReadConfigValues;
        _watcher.Created += ReadConfigValues;
        _watcher.Renamed += ReadConfigValues;
        _watcher.IncludeSubdirectories = true;
        _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        _watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        DateTime now = DateTime.Now;
        long time = now.Ticks - _lastConfigReloadTime.Ticks;
        if (time < RELOAD_DELAY)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (!File.Exists(ConfigFileFullPath))
            {
                TouchGrassLogger.LogWarning("Config file does not exist. Skipping reload.");
                return;
            }

            try
            {
                TouchGrassLogger.LogDebug("Reloading configuration...");
                SaveWithRespectToConfigSet(true);
                TouchGrassLogger.LogInfo("Configuration reload complete.");
            }
            catch (Exception ex)
            {
                TouchGrassLogger.LogError($"Error reloading configuration: {ex.Message}");
            }
        }

        _lastConfigReloadTime = now;
    }

    private void SaveWithRespectToConfigSet(bool reload = false)
    {
        bool originalSaveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        if (reload)
            Config.Reload();
        Config.Save();
        TrainingObjectTuning.ApplyAll();
        PieceRecipeTuning.ApplyAll();
        if (originalSaveOnSet)
        {
            Config.SaveOnConfigSet = originalSaveOnSet;
        }
    }


    #region ConfigOptions

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    internal static ConfigEntry<float> _locationFullEfficiencySeconds = null!;
    internal static ConfigEntry<float> _locationFadeSeconds = null!;
    internal static ConfigEntry<float> _locationMinimumMultiplier = null!;
    internal static ConfigEntry<float> _stationaryRadius = null!;
    internal static ConfigEntry<Toggle> _fatigueStationarySkillGains = null!;
    internal static ConfigEntry<FatigueStatusEffectDisplay> _fatigueStatusEffectDisplay = null!;
    internal static ConfigEntry<float> _trainingDummyHealth = null!;
    internal static ConfigEntry<float> _trainingDummyCrowdingRadius = null!;
    internal static ConfigEntry<int> _trainingDummyCrowdingMaxCount = null!;
    internal static ConfigEntry<Toggle> _trainingDummyNightAggro = null!;
    internal static ConfigEntry<float> _archeryTargetSkillMultiplier = null!;
    internal static ConfigEntry<Toggle> _archeryTargetArrowBoltSkillOnly = null!;
    internal static ConfigEntry<string> _trainingDummyRecipe = null!;
    internal static ConfigEntry<string> _archeryTargetRecipe = null!;
    internal static ConfigEntry<TrainingDummyDamageType> _localTrainingDummyDamageType = null!;
    internal static ConfigEntry<float> _localTrainingDummyDamageAmount = null!;
    internal static ConfigEntry<TrainingMeterDisplay> _trainingMeterDisplay = null!;
    internal static ConfigEntry<float> _trainingMeterWindowSeconds = null!;
    internal static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> _skillGainRates = new();
    internal static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> _skillReductionRates = new();

    private void BindPerSkillModifierConfigs()
    {
        foreach (Skills.SkillType skillType in GetConfigurableSkillTypes())
        {
            string skillName = skillType.ToString();
            _skillGainRates[skillType] = config("4 - Skill Gain Rate", skillName, 1f, new ConfigDescription("Multiplier applied to this skill's gained experience. Vanilla equivalent is 1. 0 disables gains for this skill, 2 doubles them.", new AcceptableValueRange<float>(0f, 10f)));
            _skillReductionRates[skillType] = config("5 - Skill Reduction Rate", skillName, 1f, new ConfigDescription("Multiplier applied to this skill's death skill loss. Vanilla equivalent is 1. 0 disables death loss for this skill, 2 doubles it.", new AcceptableValueRange<float>(0f, 10f)));
        }
    }

    private void BindTrainingObjectConfigEvents()
    {
        _trainingDummyHealth.SettingChanged += (_, _) => TrainingObjectTuning.ApplyAll();
        _trainingDummyNightAggro.SettingChanged += (_, _) => TrainingObjectTuning.ApplyAll();
        _archeryTargetSkillMultiplier.SettingChanged += (_, _) => TrainingObjectTuning.ApplyAll();
        _trainingDummyRecipe.SettingChanged += (_, _) => PieceRecipeTuning.ApplyAll();
        _archeryTargetRecipe.SettingChanged += (_, _) => PieceRecipeTuning.ApplyAll();
    }

    private static IEnumerable<Skills.SkillType> GetConfigurableSkillTypes()
    {
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType is Skills.SkillType.None or Skills.SkillType.All)
            {
                continue;
            }

            yield return skillType;
        }
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    #endregion
}
