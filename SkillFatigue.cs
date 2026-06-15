using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TouchGrass;

internal static class SkillEarnGate
{
    private const float TrainingChainGrace = 10f;

    private static readonly Skills.SkillType[] LocationFatigueSkillOrder =
    [
        Skills.SkillType.Swords,
        Skills.SkillType.Knives,
        Skills.SkillType.Clubs,
        Skills.SkillType.Polearms,
        Skills.SkillType.Spears,
        Skills.SkillType.Blocking,
        Skills.SkillType.Axes,
        Skills.SkillType.Bows,
        Skills.SkillType.ElementalMagic,
        Skills.SkillType.BloodMagic,
        Skills.SkillType.Unarmed,
        Skills.SkillType.Pickaxes,
        Skills.SkillType.WoodCutting,
        Skills.SkillType.Crossbows,
        Skills.SkillType.Jump,
        Skills.SkillType.Sneak,
        Skills.SkillType.Run,
        Skills.SkillType.Swim,
        Skills.SkillType.Dodge,
        Skills.SkillType.Ride
    ];
    private static readonly HashSet<Skills.SkillType> LocationFatigueSkills = new(LocationFatigueSkillOrder);

    private static LocationFatigueState _locationFatigue;

    internal static bool AdjustSkillGain(Skills skills, Skills.SkillType skillType, ref float factor)
    {
        if (skills == null || skills.m_player != Player.m_localPlayer)
        {
            return true;
        }

        if (skillType == Skills.SkillType.None)
        {
            return true;
        }

        float sourceFactor = factor;
        factor *= GetPerSkillGainRate(skillType);

        if (ShouldApplyLocationFatigue(skillType, sourceFactor))
        {
            factor *= GetLocationMultiplier(skills.m_player);
        }

        return factor > 0.0001f;
    }

    internal static bool AdjustSkillReduction(Skills skills, float factor)
    {
        if (skills == null || skills.m_player != Player.m_localPlayer)
        {
            return true;
        }

        foreach (KeyValuePair<Skills.SkillType, Skills.Skill> skillData in skills.m_skillData)
        {
            float reductionFactor = Mathf.Clamp01(factor * GetPerSkillReductionRate(skillData.Key));
            skillData.Value.m_level -= skillData.Value.m_level * reductionFactor;
            skillData.Value.m_accumulator = 0f;
        }

        skills.m_player.Message(MessageHud.MessageType.TopLeft, "$msg_skills_lowered");
        return false;
    }

    internal static IReadOnlyList<Skills.SkillType> GetLocationFatigueSkillTypes()
    {
        return LocationFatigueSkillOrder;
    }

    private static bool ShouldApplyLocationFatigue(Skills.SkillType skillType, float sourceFactor)
    {
        if (TouchGrassPlugin._fatigueStationarySkillGains.Value != TouchGrassPlugin.Toggle.On ||
            !LocationFatigueSkills.Contains(skillType))
        {
            return false;
        }

        // Dodge gets a normal input gain and a larger successful evade gain. Only fade the normal input gain.
        return skillType != Skills.SkillType.Dodge || IsNormalDodgeGain(sourceFactor);
    }

    private static float GetPerSkillGainRate(Skills.SkillType skillType)
    {
        return GetPerSkillConfigValue(TouchGrassPlugin._skillGainRates, skillType, 1f);
    }

    private static float GetPerSkillReductionRate(Skills.SkillType skillType)
    {
        return GetPerSkillConfigValue(TouchGrassPlugin._skillReductionRates, skillType, 1f);
    }

    private static float GetPerSkillConfigValue(IReadOnlyDictionary<Skills.SkillType, ConfigEntry<float>> configs, Skills.SkillType skillType, float fallback)
    {
        return configs.TryGetValue(skillType, out ConfigEntry<float> configEntry) ? configEntry.Value : fallback;
    }

    private static float GetLocationMultiplier(Player player)
    {
        if (player == null)
        {
            return 1f;
        }

        float now = Time.time;
        Vector3 position = player.transform.position;
        if (!IsInsideStationaryRadius(position))
        {
            ResetLocationFatigue();
            FatigueStatusEffectManager.Update(player);
            return 1f;
        }

        float globalMultiplier = RecordLocationFatigueGain(ref _locationFatigue, now);
        FatigueStatusEffectManager.Update(player);
        return globalMultiplier;
    }

    private static float RecordLocationFatigueGain(ref LocationFatigueState state, float now)
    {
        if (state.HasGain)
        {
            float elapsed = Mathf.Max(0f, now - state.LastGainTime);
            if (elapsed <= TrainingChainGrace)
            {
                state.ActiveSeconds += elapsed;
            }
        }

        state.HasGain = true;
        state.LastGainTime = now;
        state.ActiveSeconds = Mathf.Min(GetMaxTrackedActiveSeconds(), state.ActiveSeconds);
        return ComputeLocationMultiplier(state.ActiveSeconds);
    }

    private static void ResetLocationFatigue()
    {
        _locationFatigue.ClearGain();
    }

    private static bool IsInsideStationaryRadius(Vector3 position)
    {
        float radius = TouchGrassPlugin._stationaryRadius.Value;
        if (radius <= 0f)
        {
            return false;
        }

        if (!_locationFatigue.HasAnchor)
        {
            _locationFatigue.SetAnchor(position);
            return false;
        }

        float deltaX = position.x - _locationFatigue.AnchorX;
        float deltaZ = position.z - _locationFatigue.AnchorZ;
        if (deltaX * deltaX + deltaZ * deltaZ > radius * radius)
        {
            _locationFatigue.SetAnchor(position);
            return false;
        }

        return true;
    }

    private static float ComputeLocationMultiplier(float activeSeconds)
    {
        float fullSeconds = TouchGrassPlugin._locationFullEfficiencySeconds.Value;
        if (activeSeconds <= fullSeconds)
        {
            return 1f;
        }

        float fadeSeconds = Mathf.Max(1f, TouchGrassPlugin._locationFadeSeconds.Value);
        float minimum = Mathf.Clamp01(TouchGrassPlugin._locationMinimumMultiplier.Value);
        float progress = Mathf.Clamp01((activeSeconds - fullSeconds) / fadeSeconds);
        return Mathf.Lerp(1f, minimum, progress);
    }

    private static bool IsNormalDodgeGain(float sourceFactor)
    {
        return sourceFactor <= 0.11f;
    }

    internal static bool TryGetFatigueSnapshot(out FatigueSnapshot snapshot)
    {
        snapshot = FatigueSnapshot.Empty;
        if (TouchGrassPlugin._fatigueStationarySkillGains.Value != TouchGrassPlugin.Toggle.On ||
            TouchGrassPlugin._fatigueStatusEffectDisplay.Value == TouchGrassPlugin.FatigueStatusEffectDisplay.Off ||
            !_locationFatigue.HasGain)
        {
            return false;
        }

        float globalMultiplier = ComputeLocationMultiplier(_locationFatigue.ActiveSeconds);
        if (globalMultiplier >= 0.999f)
        {
            return false;
        }

        snapshot = new FatigueSnapshot(globalMultiplier);
        return true;
    }

    private static float GetMaxTrackedActiveSeconds()
    {
        return Mathf.Max(1f, TouchGrassPlugin._locationFullEfficiencySeconds.Value + TouchGrassPlugin._locationFadeSeconds.Value);
    }

    internal readonly struct FatigueSnapshot(float multiplier)
    {
        internal static readonly FatigueSnapshot Empty = new(1f);
        internal readonly float Multiplier = multiplier;
    }

    private struct LocationFatigueState
    {
        internal bool HasGain;
        internal bool HasAnchor;
        internal float LastGainTime;
        internal float ActiveSeconds;
        internal float AnchorX;
        internal float AnchorZ;

        internal void SetAnchor(Vector3 position)
        {
            HasAnchor = true;
            AnchorX = position.x;
            AnchorZ = position.z;
        }

        internal void ClearGain()
        {
            HasGain = false;
            LastGainTime = 0f;
            ActiveSeconds = 0f;
        }

        internal void Clear()
        {
            HasAnchor = false;
            AnchorX = 0f;
            AnchorZ = 0f;
            ClearGain();
        }
    }
}

internal static class FatigueStatusEffectManager
{
    private const string EffectObjectName = "TouchGrass_Fatigue";
    private static TouchGrassFatigueStatusEffect? _prototype;

    internal static void Update(Player player)
    {
        if (player == null || player.GetSEMan() == null)
        {
            return;
        }

        if (!SkillEarnGate.TryGetFatigueSnapshot(out _))
        {
            Remove(player);
            return;
        }

        SEMan seMan = player.GetSEMan();
        if (seMan.GetStatusEffect(EffectHash) == null)
        {
            seMan.AddStatusEffect(Prototype);
        }
    }

    internal static void Remove(Player player)
    {
        player?.GetSEMan()?.RemoveStatusEffect(EffectHash, quiet: true);
    }

    private static int EffectHash => Prototype.NameHash();

    private static TouchGrassFatigueStatusEffect Prototype
    {
        get
        {
            if (_prototype != null)
            {
                return _prototype;
            }

            _prototype = ScriptableObject.CreateInstance<TouchGrassFatigueStatusEffect>();
            _prototype.name = EffectObjectName;
            _prototype.m_name = "TouchGrass Fatigue";
            _prototype.m_category = "TouchGrass";
            _prototype.m_icon = CreateIcon();
            _prototype.m_startMessage = "";
            _prototype.m_stopMessage = "";
            UnityEngine.Object.DontDestroyOnLoad(_prototype);
            return _prototype;
        }
    }

    private static Sprite CreateIcon()
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        Color32 clear = new(0, 0, 0, 0);
        Color32 green = new(89, 162, 105, 255);
        Color32 dark = new(26, 54, 41, 255);
        Color32 pale = new(202, 231, 179, 255);
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= 14.5f ? dark : clear;
                if (distance <= 12.5f)
                {
                    pixels[y * size + x] = green;
                }
            }
        }

        for (int y = 8; y <= 23; y++)
        {
            int x = 15 + Mathf.RoundToInt((y - 16) * 0.15f);
            pixels[y * size + x] = pale;
            pixels[y * size + x + 1] = pale;
        }

        for (int y = 8; y <= 16; y++)
        {
            int width = 16 - y;
            for (int x = 16; x <= 16 + width; x++)
            {
                pixels[y * size + x] = pale;
            }
        }

        for (int y = 14; y <= 22; y++)
        {
            int width = y - 14;
            for (int x = 14 - width; x <= 14; x++)
            {
                pixels[y * size + x] = pale;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        UnityEngine.Object.DontDestroyOnLoad(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        UnityEngine.Object.DontDestroyOnLoad(sprite);
        return sprite;
    }
}

internal class TouchGrassFatigueStatusEffect : StatusEffect
{
    public override bool CanAdd(Character character)
    {
        return character == Player.m_localPlayer;
    }

    public override bool IsDone()
    {
        return base.IsDone() || !SkillEarnGate.TryGetFatigueSnapshot(out _);
    }

    public override string GetIconText()
    {
        return SkillEarnGate.TryGetFatigueSnapshot(out SkillEarnGate.FatigueSnapshot snapshot)
            ? $"{Mathf.RoundToInt(snapshot.Multiplier * 100f)}%"
            : "";
    }

    public override string GetTooltipString()
    {
        return "Touch the grass Bro!";
    }
}
