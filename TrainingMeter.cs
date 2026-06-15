using System.Collections.Generic;
using UnityEngine;

namespace TouchGrass;

internal static class TrainingMeter
{
    private const float HugeDamageThreshold = 1_000_000_000f;
    private const string HudTitle = "TouchGrass Training Meter";
    private const float HudPaddingX = 10f;
    private const float HudPaddingY = 8f;
    private const float HudLineSpacing = 2f;
    private const float HudHeaderSpacing = 4f;
    private const float HudHeightSafetyPadding = 8f;
    private const float HudHeightHoldSeconds = 0.35f;
    private const float HudScale = 1.6f;
    private const float HudBaseWidth = 420f;
    private const float DefaultHudAnchorX = 0.02f;
    private const float DefaultHudAnchorY = 0.86f;

    private static readonly List<OutgoingDamageEvent> OutgoingDamageEvents = [];
    private static readonly List<IncomingDamageEvent> IncomingDamageEvents = [];
    private static readonly List<SkillGainEvent> SkillGainEvents = [];
    private static readonly List<PlayerSkillRaiseContext> PlayerSkillRaiseContexts = [];
    private static readonly HashSet<Skills.SkillType> DisplayedSkillTypes =
    [
        Skills.SkillType.Swords,
        Skills.SkillType.Knives,
        Skills.SkillType.Clubs,
        Skills.SkillType.Polearms,
        Skills.SkillType.Spears,
        Skills.SkillType.Blocking,
        Skills.SkillType.Axes,
        Skills.SkillType.Bows,
        Skills.SkillType.Crossbows,
        Skills.SkillType.Unarmed,
        Skills.SkillType.Pickaxes,
        Skills.SkillType.WoodCutting,
        Skills.SkillType.ElementalMagic,
        Skills.SkillType.BloodMagic
    ];
    private static float _lastTrainingDummyInteractionTime = -9999f;
    private static float _lastHudHeight;
    private static float _lastHudHeightHoldUntil = -9999f;
    private static Vector2 _hudPosition;
    private static Vector2 _hudDragOffset;
    private static bool _hudPositionInitialized;
    private static bool _isDraggingHud;
    private static IncomingDamageState? _activeIncomingDamageState;
    private static GUIStyle? _boxStyle;
    private static GUIStyle? _headerStyle;
    private static GUIStyle? _labelStyle;
    private static Texture2D? _backgroundTexture;
    private static float _styleScale = -1f;
    private static int _nextSkillRaiseContextId;

    internal static void RecordOutgoingDamage(Character target, HitData hit)
    {
        if (!IsEnabled() || target == null || hit == null || !TrainingDummyIdentity.IsTrainingDummy(target))
        {
            return;
        }

        if (hit.GetAttacker() != Player.m_localPlayer)
        {
            return;
        }

        float statusDamage = GetStatusDamage(hit.m_damage);
        float impactDamage = Mathf.Max(0f, hit.m_damage.GetTotalDamage() - statusDamage);
        if ((impactDamage <= 0f && statusDamage <= 0f) || impactDamage + statusDamage > HugeDamageThreshold)
        {
            return;
        }

        float now = Time.time;
        MarkTrainingDummyInteraction(now);
        OutgoingDamageEvents.Add(new OutgoingDamageEvent(
            now,
            impactDamage,
            statusDamage));
        Prune(now);
    }

    internal static IncomingDamageState BeforeIncomingDamage(Character victim, HitData hit)
    {
        IncomingDamageState state = new();
        if (!IsEnabled() || victim == null || hit == null)
        {
            return state;
        }

        if (victim != Player.m_localPlayer)
        {
            return state;
        }

        _activeIncomingDamageState = null;
        Character attacker = hit.GetAttacker();
        if (!TrainingDummyIdentity.IsTrainingDummy(attacker))
        {
            return state;
        }

        Player player = Player.m_localPlayer;
        float now = Time.time;
        MarkTrainingDummyInteraction(now);
        state.IsTrainingDummyHit = true;
        state.Time = now;
        state.Health = victim.GetHealth();
        state.WasBlocking = hit.m_blockable && victim.IsBlocking();
        state.RawDamage = hit.m_damage.GetTotalDamage();
        state.RawDamageType = GetDamageTypeText(hit.m_damage);
        state.DamageAfterBlock = hit.m_damage.Clone();
        _activeIncomingDamageState = state;
        return state;
    }

    internal static void AfterIncomingDamage(Character victim, IncomingDamageState state)
    {
        if (state == null || !state.IsTrainingDummyHit || victim == null || victim != Player.m_localPlayer)
        {
            return;
        }

        _activeIncomingDamageState = null;
        Player player = Player.m_localPlayer;
        DamageReductionBreakdown reduction = ComputeIncomingBreakdown(player, state.DamageAfterBlock);
        IncomingDamageEvents.Add(new IncomingDamageEvent(
            state.Time,
            state.RawDamage,
            state.RawDamageType,
            state.BlockedDamage,
            reduction.ResistReduction,
            reduction.ArmorReduction,
            reduction.FinalDamage,
            reduction.StatusDamage,
            reduction.StatusDamageType));
        Prune(Time.time);
    }

    internal static void RecordBlockResult(Humanoid blocker, HitData hit)
    {
        if (_activeIncomingDamageState == null ||
            !_activeIncomingDamageState.IsTrainingDummyHit ||
            blocker == null ||
            blocker != Player.m_localPlayer ||
            hit == null)
        {
            return;
        }

        _activeIncomingDamageState.DamageAfterBlock = hit.m_damage.Clone();
        _activeIncomingDamageState.BlockedDamage = Mathf.Max(0f, _activeIncomingDamageState.RawDamage - hit.m_damage.GetTotalDamage());
    }

    internal static int BeginPlayerSkillRaise(Player player, Skills.SkillType skillType, float rawFactor)
    {
        if (player == null ||
            player != Player.m_localPlayer ||
            skillType == Skills.SkillType.None)
        {
            return 0;
        }

        int id = ++_nextSkillRaiseContextId;
        PlayerSkillRaiseContexts.Add(new PlayerSkillRaiseContext(id, skillType, Mathf.Max(0f, rawFactor)));
        return id;
    }

    internal static void EndPlayerSkillRaise(int contextId)
    {
        if (contextId == 0)
        {
            return;
        }

        for (int i = PlayerSkillRaiseContexts.Count - 1; i >= 0; i--)
        {
            if (PlayerSkillRaiseContexts[i].Id == contextId)
            {
                PlayerSkillRaiseContexts.RemoveAt(i);
                return;
            }
        }
    }

    internal static void RecordSkillGain(Skills skills, Skills.SkillType skillType, float sourceFactor, float finalFactor)
    {
        if (!IsEnabled() ||
            skills == null ||
            skills.m_player != Player.m_localPlayer ||
            skillType == Skills.SkillType.None ||
            !DisplayedSkillTypes.Contains(skillType))
        {
            return;
        }

        float now = Time.time;
        if (!IsTrainingSessionActive(now))
        {
            return;
        }

        float baseExp = ComputeBaseSkillExperience(skills, skillType, GetBaseSkillFactor(skills.m_player, skillType, sourceFactor));
        float finalExp = ComputeSkillExperience(skills, skillType, Mathf.Max(0f, finalFactor));
        if (baseExp <= 0f && finalExp <= 0f)
        {
            return;
        }

        float efficiency = baseExp > 0f ? Mathf.Max(0f, finalExp / baseExp) : 1f;
        SkillGainEvents.Add(new SkillGainEvent(now, skillType, baseExp, finalExp, efficiency));
        Prune(now);
    }

    internal static void OnGUI()
    {
        if (!IsEnabled() || Player.m_localPlayer == null)
        {
            StopHudDrag();
            return;
        }

        float now = Time.time;
        Prune(now);
        if (!IsTrainingSessionActive(now))
        {
            StopHudDrag();
            return;
        }

        List<string> lines = BuildHudLines(now);
        if (lines.Count == 0)
        {
            StopHudDrag();
            return;
        }

        float scale = HudScale;
        EnsureStyles(scale);
        GUIStyle headerStyle = _headerStyle!;
        GUIStyle labelStyle = _labelStyle!;
        float paddingX = HudPaddingX * scale;
        float paddingY = HudPaddingY * scale;
        float lineSpacing = HudLineSpacing * scale;
        float headerSpacing = HudHeaderSpacing * scale;
        float width = GetHudWidth();
        float contentWidth = width - paddingX * 2f;
        float height = GetStableHudHeight(GetHudHeight(lines, contentWidth, headerStyle, labelStyle, paddingY, lineSpacing, headerSpacing, scale), now);
        Rect rect = GetHudRect(width, height);
        rect = HandleHudDrag(rect);
        GUI.Box(rect, GUIContent.none, _boxStyle);

        float x = rect.x + paddingX;
        float y = rect.y + paddingY;
        float headerHeight = Mathf.Ceil(headerStyle.CalcHeight(new GUIContent(HudTitle), contentWidth));
        GUI.Label(new Rect(x, y, contentWidth, headerHeight), HudTitle, headerStyle);
        y += headerHeight + headerSpacing;
        foreach (string line in lines)
        {
            float lineHeight = Mathf.Ceil(labelStyle.CalcHeight(new GUIContent(line), contentWidth));
            GUI.Label(new Rect(x, y, contentWidth, lineHeight), line, labelStyle);
            y += lineHeight + lineSpacing;
        }
    }

    private static List<string> BuildHudLines(float now)
    {
        float windowStart = now - GetWindowSeconds();
        float firstTrainingEventTime = now;
        float lastTrainingEventTime = 0f;
        float totalImpactDamage = 0f;
        float totalStatusDamage = 0f;
        int hitCount = 0;
        int incomingCount = 0;
        OutgoingDamageEvent? lastOutgoing = null;
        IncomingDamageEvent? lastIncoming = null;
        SkillGainEvent? lastSkill = null;

        foreach (OutgoingDamageEvent damageEvent in OutgoingDamageEvents)
        {
            if (damageEvent.Time < windowStart)
            {
                continue;
            }

            firstTrainingEventTime = Mathf.Min(firstTrainingEventTime, damageEvent.Time);
            lastTrainingEventTime = Mathf.Max(lastTrainingEventTime, damageEvent.Time);
            totalImpactDamage += damageEvent.ImpactDamage;
            totalStatusDamage += damageEvent.StatusDamage;
            hitCount++;
            if (lastOutgoing == null || damageEvent.Time > lastOutgoing.Value.Time)
            {
                lastOutgoing = damageEvent;
            }
        }

        foreach (IncomingDamageEvent damageEvent in IncomingDamageEvents)
        {
            if (damageEvent.Time < windowStart)
            {
                continue;
            }

            firstTrainingEventTime = Mathf.Min(firstTrainingEventTime, damageEvent.Time);
            lastTrainingEventTime = Mathf.Max(lastTrainingEventTime, damageEvent.Time);
            incomingCount++;
            if (lastIncoming == null || damageEvent.Time > lastIncoming.Value.Time)
            {
                lastIncoming = damageEvent;
            }
        }

        foreach (SkillGainEvent skillEvent in SkillGainEvents)
        {
            if (skillEvent.Time < windowStart)
            {
                continue;
            }

            firstTrainingEventTime = Mathf.Min(firstTrainingEventTime, skillEvent.Time);
            lastTrainingEventTime = Mathf.Max(lastTrainingEventTime, skillEvent.Time);
            if (lastSkill == null || skillEvent.Time > lastSkill.Value.Time)
            {
                lastSkill = skillEvent;
            }
        }

        if (hitCount == 0 && incomingCount == 0 && !lastSkill.HasValue)
        {
            return [];
        }

        if (lastTrainingEventTime <= 0f)
        {
            lastTrainingEventTime = now;
        }

        float duration = Mathf.Max(1f, Mathf.Min(GetWindowSeconds(), now - firstTrainingEventTime));
        float dps = totalImpactDamage / duration;
        float dph = hitCount > 0 ? totalImpactDamage / hitCount : 0f;
        List<string> lines = [];

        if (hitCount > 0)
        {
            if (totalStatusDamage > 0f)
            {
                lines.Add("ToDummy: Status | Attempt | DPH | Hits | DPS | Time");
                lines.Add($"ToDummy: {FormatFloat(totalStatusDamage)} | {FormatFloat(totalImpactDamage)} | {FormatFloat(dph)} | {hitCount} | {FormatFloat(dps)} | {FormatFloat(duration)}s");
            }
            else
            {
                lines.Add("ToDummy: Attempt | DPH | Hits | DPS | Time");
                lines.Add($"ToDummy: {FormatFloat(totalImpactDamage)} | {FormatFloat(dph)} | {hitCount} | {FormatFloat(dps)} | {FormatFloat(duration)}s");
            }
        }

        if (lastIncoming.HasValue)
        {
            IncomingDamageEvent incoming = lastIncoming.Value;
            string header = "Raw - Blocked - Resist - Armor = Final";
            string baseFormula = $"{FormatFloat(incoming.RawDamage)}({incoming.RawDamageType}) - {FormatFloat(incoming.BlockedDamage)} - {FormatSubtractedSigned(incoming.ResistReduction)} - {FormatFloat(incoming.ArmorReduction)}";
            string finalFormula = $"{baseFormula} = {FormatFloat(incoming.FinalDamage)}";
            if (incoming.StatusDamage > 0f)
            {
                lines.Add($"FromDummy: {header} | Status");
                lines.Add($"FromDummy: {finalFormula} | {FormatFloat(incoming.StatusDamage)}({incoming.StatusDamageType})");
            }
            else
            {
                lines.Add($"FromDummy: {header}");
                lines.Add($"FromDummy: {finalFormula}");
            }
        }

        if (lastSkill.HasValue)
        {
            SkillGainEvent skill = lastSkill.Value;
            lines.Add($"Skill: {FormatSkill(skill.SkillType)} +{FormatFloat(skill.FinalExp)} / +{FormatFloat(skill.BaseExp)} base exp ({FormatPercent(skill.Efficiency)})");
        }

        return lines;
    }

    private static void MarkTrainingDummyInteraction(float now)
    {
        _lastTrainingDummyInteractionTime = now;
    }

    private static bool IsTrainingSessionActive(float now)
    {
        return now - _lastTrainingDummyInteractionTime <= GetWindowSeconds();
    }

    private static bool IsEnabled()
    {
        return TouchGrassPlugin._trainingMeterDisplay != null &&
               TouchGrassPlugin._trainingMeterDisplay.Value != TouchGrassPlugin.TrainingMeterDisplay.Off;
    }

    private static float GetWindowSeconds()
    {
        return Mathf.Clamp(TouchGrassPlugin._trainingMeterWindowSeconds?.Value ?? 15f, 5f, 300f);
    }

    private static float GetHudWidth()
    {
        return Mathf.Clamp(HudBaseWidth * HudScale, 240f, Screen.width);
    }

    private static Rect GetHudRect(float width, float height)
    {
        if (!_hudPositionInitialized)
        {
            _hudPosition = new Vector2(DefaultHudAnchorX * Screen.width, (1f - DefaultHudAnchorY) * Screen.height);
            _hudPositionInitialized = true;
        }

        _hudPosition = ClampHudPosition(_hudPosition, width, height);
        return new Rect(_hudPosition.x, _hudPosition.y, width, height);
    }

    private static Rect HandleHudDrag(Rect rect)
    {
        Event current = Event.current;
        if (current == null)
        {
            return rect;
        }

        if (_isDraggingHud && !Input.GetMouseButton(0))
        {
            StopHudDrag();
        }

        switch (current.type)
        {
            case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
                _isDraggingHud = true;
                _hudDragOffset = current.mousePosition - new Vector2(rect.x, rect.y);
                current.Use();
                break;
            case EventType.MouseDrag when current.button == 0 && _isDraggingHud:
                _hudPosition = ClampHudPosition(current.mousePosition - _hudDragOffset, rect.width, rect.height);
                rect.position = _hudPosition;
                current.Use();
                break;
            case EventType.MouseUp when current.button == 0 && _isDraggingHud:
                _isDraggingHud = false;
                current.Use();
                break;
        }

        if (_isDraggingHud)
        {
            rect.position = _hudPosition;
        }

        return rect;
    }

    private static void StopHudDrag()
    {
        _isDraggingHud = false;
    }

    private static Vector2 ClampHudPosition(Vector2 position, float width, float height)
    {
        position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, Screen.width - width));
        position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, Screen.height - height));
        return position;
    }

    private static float GetHudHeight(List<string> lines, float contentWidth, GUIStyle headerStyle, GUIStyle labelStyle, float paddingY, float lineSpacing, float headerSpacing, float scale)
    {
        float height = paddingY * 2f;
        height += Mathf.Ceil(headerStyle.CalcHeight(new GUIContent(HudTitle), contentWidth)) + headerSpacing;
        foreach (string line in lines)
        {
            height += Mathf.Ceil(labelStyle.CalcHeight(new GUIContent(line), contentWidth)) + lineSpacing;
        }

        return height + HudHeightSafetyPadding * scale;
    }

    private static float GetStableHudHeight(float desiredHeight, float now)
    {
        if (desiredHeight >= _lastHudHeight)
        {
            _lastHudHeight = desiredHeight;
            _lastHudHeightHoldUntil = now + HudHeightHoldSeconds;
            return desiredHeight;
        }

        if (now <= _lastHudHeightHoldUntil)
        {
            return _lastHudHeight;
        }

        _lastHudHeight = desiredHeight;
        _lastHudHeightHoldUntil = now + HudHeightHoldSeconds;
        return desiredHeight;
    }

    private static void Prune(float now)
    {
        float oldest = now - GetWindowSeconds();
        OutgoingDamageEvents.RemoveAll(e => e.Time < oldest);
        IncomingDamageEvents.RemoveAll(e => e.Time < oldest);
        SkillGainEvents.RemoveAll(e => e.Time < oldest);
    }

    private static float ComputeSkillExperience(Skills skills, Skills.SkillType skillType, float factor)
    {
        if (factor <= 0f)
        {
            return 0f;
        }

        Skills.Skill skill = skills.GetSkill(skillType);
        if (skill == null || skill.m_level >= 100f)
        {
            return 0f;
        }

        return skill.m_info.m_increseStep * factor * Game.m_skillGainRate;
    }

    private static float ComputeBaseSkillExperience(Skills skills, Skills.SkillType skillType, float factor)
    {
        if (factor <= 0f)
        {
            return 0f;
        }

        Skills.Skill skill = skills.GetSkill(skillType);
        if (skill == null || skill.m_level >= 100f)
        {
            return 0f;
        }

        return skill.m_info.m_increseStep * factor;
    }

    private static float GetBaseSkillFactor(Player player, Skills.SkillType skillType, float fallbackFactor)
    {
        for (int i = PlayerSkillRaiseContexts.Count - 1; i >= 0; i--)
        {
            PlayerSkillRaiseContext context = PlayerSkillRaiseContexts[i];
            if (context.SkillType == skillType)
            {
                return context.RawFactor;
            }
        }

        return Mathf.Max(0f, fallbackFactor);
    }

    private static DamageReductionBreakdown ComputeIncomingBreakdown(Player player, HitData.DamageTypes damageAfterBlock)
    {
        HitData simulated = new()
        {
            m_damage = damageAfterBlock.Clone()
        };

        float afterBlock = simulated.m_damage.GetTotalDamage();
        simulated.ApplyResistance(player.GetDamageModifiers(), out _);
        float afterResistance = simulated.m_damage.GetTotalDamage();
        simulated.ApplyArmor(player.GetBodyArmor());
        float afterArmor = simulated.m_damage.GetTotalDamage();
        float statusDamage = GetStatusDamage(simulated.m_damage);
        float finalDamage = Mathf.Max(0f, afterArmor - statusDamage);
        if (finalDamage <= 0.1f)
        {
            finalDamage = 0f;
        }

        return new DamageReductionBreakdown(
            afterBlock - afterResistance,
            Mathf.Max(0f, afterResistance - afterArmor),
            finalDamage,
            statusDamage,
            GetStatusDamageTypeText(simulated.m_damage));
    }

    private static float GetStatusDamage(HitData.DamageTypes damage)
    {
        return Mathf.Max(0f, damage.m_fire) + Mathf.Max(0f, damage.m_poison) + Mathf.Max(0f, damage.m_spirit);
    }

    private static string GetStatusDamageTypeText(HitData.DamageTypes damage)
    {
        string name = "Status";
        float value = 0f;
        UseIfHigher("Fire", damage.m_fire);
        UseIfHigher("Poison", damage.m_poison);
        UseIfHigher("Spirit", damage.m_spirit);
        return value > 0f ? name : "";

        void UseIfHigher(string candidateName, float candidateValue)
        {
            if (candidateValue > value)
            {
                name = candidateName;
                value = candidateValue;
            }
        }
    }

    private static string GetDamageTypeText(HitData.DamageTypes damage)
    {
        string name = "Generic";
        float value = damage.m_damage;
        UseIfHigher("Blunt", damage.m_blunt);
        UseIfHigher("Slash", damage.m_slash);
        UseIfHigher("Pierce", damage.m_pierce);
        UseIfHigher("Fire", damage.m_fire);
        UseIfHigher("Frost", damage.m_frost);
        UseIfHigher("Lightning", damage.m_lightning);
        UseIfHigher("Poison", damage.m_poison);
        UseIfHigher("Spirit", damage.m_spirit);
        return value > 0f ? name : "None";

        void UseIfHigher(string candidateName, float candidateValue)
        {
            if (candidateValue > value)
            {
                name = candidateName;
                value = candidateValue;
            }
        }
    }

    private static string FormatSkill(Skills.SkillType skillType)
    {
        if (skillType == Skills.SkillType.None)
        {
            return "-";
        }

        return SkillNameFormatter.Format(skillType);
    }

    private static string FormatFloat(float value)
    {
        if (value >= 100f)
        {
            return value.ToString("0");
        }

        return value >= 10f ? value.ToString("0.0") : value.ToString("0.00");
    }

    private static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Max(0f, value) * 100f)}%";
    }

    private static string FormatSigned(float value)
    {
        return value >= 0f ? $"+ {FormatFloat(value)}" : $"- {FormatFloat(Mathf.Abs(value))}";
    }

    private static string FormatSubtractedSigned(float value)
    {
        return value >= 0f ? FormatFloat(value) : $"({FormatSigned(value)})";
    }

    private static void EnsureStyles(float scale)
    {
        if (_boxStyle != null && _headerStyle != null && _labelStyle != null && Mathf.Approximately(_styleScale, scale))
        {
            return;
        }

        _styleScale = scale;
        if (_backgroundTexture == null)
        {
            _backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _backgroundTexture.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.05f, 0.82f));
            _backgroundTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            UnityEngine.Object.DontDestroyOnLoad(_backgroundTexture);
        }

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = _backgroundTexture },
            border = new RectOffset(
                Mathf.RoundToInt(8f * scale),
                Mathf.RoundToInt(8f * scale),
                Mathf.RoundToInt(8f * scale),
                Mathf.RoundToInt(8f * scale))
        };

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(8, Mathf.RoundToInt(14f * scale)),
            richText = false
        };
        _headerStyle.normal.textColor = new Color(0.82f, 1f, 0.78f, 1f);

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(8, Mathf.RoundToInt(13f * scale)),
            richText = false,
            wordWrap = true
        };
        _labelStyle.normal.textColor = Color.white;
    }

    internal sealed class IncomingDamageState
    {
        internal bool IsTrainingDummyHit;
        internal bool WasBlocking;
        internal float Time;
        internal float Health;
        internal float RawDamage;
        internal float BlockedDamage;
        internal string RawDamageType = "";
        internal HitData.DamageTypes DamageAfterBlock;
    }

    private readonly struct OutgoingDamageEvent(float time, float impactDamage, float statusDamage)
    {
        internal readonly float Time = time;
        internal readonly float ImpactDamage = impactDamage;
        internal readonly float StatusDamage = statusDamage;
    }

    private readonly struct IncomingDamageEvent(
        float time,
        float rawDamage,
        string rawDamageType,
        float blockedDamage,
        float resistReduction,
        float armorReduction,
        float finalDamage,
        float statusDamage,
        string statusDamageType)
    {
        internal readonly float Time = time;
        internal readonly float RawDamage = rawDamage;
        internal readonly string RawDamageType = rawDamageType;
        internal readonly float BlockedDamage = blockedDamage;
        internal readonly float ResistReduction = resistReduction;
        internal readonly float ArmorReduction = armorReduction;
        internal readonly float FinalDamage = finalDamage;
        internal readonly float StatusDamage = statusDamage;
        internal readonly string StatusDamageType = statusDamageType;
    }

    private readonly struct PlayerSkillRaiseContext(int id, Skills.SkillType skillType, float rawFactor)
    {
        internal readonly int Id = id;
        internal readonly Skills.SkillType SkillType = skillType;
        internal readonly float RawFactor = rawFactor;
    }

    private readonly struct SkillGainEvent(float time, Skills.SkillType skillType, float baseExp, float finalExp, float efficiency)
    {
        internal readonly float Time = time;
        internal readonly Skills.SkillType SkillType = skillType;
        internal readonly float BaseExp = baseExp;
        internal readonly float FinalExp = finalExp;
        internal readonly float Efficiency = efficiency;
    }

    private readonly struct DamageReductionBreakdown(float resistReduction, float armorReduction, float finalDamage, float statusDamage, string statusDamageType)
    {
        internal readonly float ResistReduction = resistReduction;
        internal readonly float ArmorReduction = armorReduction;
        internal readonly float FinalDamage = finalDamage;
        internal readonly float StatusDamage = statusDamage;
        internal readonly string StatusDamageType = statusDamageType;
    }

}
