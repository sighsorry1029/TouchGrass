using System;
using System.Globalization;
using UnityEngine;

namespace TouchGrass;

internal sealed class TrainingDummySettingsInteractable : MonoBehaviour, Interactable
{
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user == null || user != Player.m_localPlayer)
        {
            return false;
        }

        Character character = GetComponent<Character>();
        if (character == null || !TrainingDummyDamageTest.IsTrainingDummy(character))
        {
            return false;
        }

        if (!PrivateArea.CheckAccess(transform.position))
        {
            return true;
        }

        TrainingDummySettingsWindow.Open(character);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }
}

internal static class TrainingDummySettingsWindow
{
    private const float WindowWidth = 430f;
    private const float WindowHeight = 260f;
    private static readonly TouchGrassPlugin.TrainingDummyDamageType[] DamageTypes =
    [
        TouchGrassPlugin.TrainingDummyDamageType.Blunt,
        TouchGrassPlugin.TrainingDummyDamageType.Slash,
        TouchGrassPlugin.TrainingDummyDamageType.Pierce,
        TouchGrassPlugin.TrainingDummyDamageType.Fire,
        TouchGrassPlugin.TrainingDummyDamageType.Frost,
        TouchGrassPlugin.TrainingDummyDamageType.Lightning,
        TouchGrassPlugin.TrainingDummyDamageType.Poison,
        TouchGrassPlugin.TrainingDummyDamageType.Spirit
    ];
    private static readonly string[] DamageTypeNames = Array.ConvertAll(DamageTypes, type => type.ToString());

    private static Character? _target;
    private static Rect _windowRect = new(260f, 160f, WindowWidth, WindowHeight);
    private static string _damageText = "";
    private static int _damageTypeIndex;
    private static string _message = "";
    private static GUIStyle? _messageStyle;

    internal static bool IsVisible => _target != null;

    internal static void Open(Character target)
    {
        if (target == null || !TrainingDummyDamageTest.IsTrainingDummy(target))
        {
            return;
        }

        _target = target;
        if (!TrainingDummyDamageTest.TryGetEffectiveDamageProfile(target, out TouchGrassPlugin.TrainingDummyDamageType damageType, out float amount))
        {
            damageType = TouchGrassPlugin._localTrainingDummyDamageType.Value;
            amount = TouchGrassPlugin._localTrainingDummyDamageAmount.Value;
        }

        _damageText = FormatInput(amount);
        _damageTypeIndex = Mathf.Max(0, Array.IndexOf(DamageTypes, damageType));
        _message = "";
        CenterWindowIfNeeded();
    }

    internal static void OnGUI()
    {
        if (_target == null)
        {
            return;
        }

        if (Player.m_localPlayer == null || _target == null)
        {
            Close();
            return;
        }

        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
            Event.current.Use();
            return;
        }

        EnsureStyles();
        _windowRect = GUI.Window(99150524, _windowRect, DrawWindow, "TouchGrass Dummy Settings");
    }

    internal static string GetHoverText(string existingText)
    {
        string prompt = $"[<color=yellow><b>{GetUseKeyLabel()}</b></color>] Configure";
        return string.IsNullOrWhiteSpace(existingText) ? prompt : existingText + "\n" + prompt;
    }

    private static string GetUseKeyLabel()
    {
        string label = GetBoundKeyLabel(ZInput.IsGamepadActive() ? "JoyUse" : "Use");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = GetBoundKeyLabel("Use");
        }

        return string.IsNullOrWhiteSpace(label) ? "E" : label;
    }

    private static string GetBoundKeyLabel(string buttonName)
    {
        string label = ZInput.instance?.GetBoundKeyString(buttonName, emptyStringOnMissing: true) ?? "";
        if (string.IsNullOrWhiteSpace(label))
        {
            return "";
        }

        string localized = Localization.instance != null ? Localization.instance.Localize(label) : label;
        return localized.Contains("$KEY_") ? "" : localized.Trim();
    }

    private static void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        GUILayout.Label("Damage");
        _damageText = GUILayout.TextField(_damageText, 16);

        GUILayout.Space(6f);
        GUILayout.Label("Damage Type");
        _damageTypeIndex = GUILayout.SelectionGrid(_damageTypeIndex, DamageTypeNames, 4);

        GUILayout.Space(8f);
        if (!string.IsNullOrWhiteSpace(_message))
        {
            GUILayout.Label(_message, _messageStyle);
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Config Defaults", GUILayout.Height(28f)))
        {
            LoadConfigDefaults();
        }

        if (GUILayout.Button("Apply", GUILayout.Height(28f)))
        {
            Apply();
        }

        if (GUILayout.Button("Close", GUILayout.Height(28f)))
        {
            Close();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 24f));
    }

    private static void LoadConfigDefaults()
    {
        _damageText = FormatInput(TouchGrassPlugin._localTrainingDummyDamageAmount.Value);
        _damageTypeIndex = Mathf.Max(0, Array.IndexOf(DamageTypes, TouchGrassPlugin._localTrainingDummyDamageType.Value));
        _message = "Loaded damage defaults. Press Apply to save them to this dummy.";
    }

    private static void Apply()
    {
        if (_target == null)
        {
            Close();
            return;
        }

        if (!TryParseFloat(_damageText, 1f, 500f, out float damage))
        {
            _message = "Damage must be between 1 and 500.";
            return;
        }

        if (!PrivateArea.CheckAccess(_target.transform.position))
        {
            _message = "No access.";
            return;
        }

        TouchGrassPlugin.TrainingDummyDamageType damageType = DamageTypes[Mathf.Clamp(_damageTypeIndex, 0, DamageTypes.Length - 1)];
        TrainingDummyDamageTest.SetDamageProfile(_target, damageType, damage);
        _message = "Saved damage settings to this dummy.";
    }

    private static bool TryParseFloat(string text, float min, float max, out float value)
    {
        text = text.Trim().Replace(',', '.');
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            value = 0f;
            return false;
        }

        return value >= min && value <= max;
    }

    private static string FormatInput(float value)
    {
        return value.ToString(value >= 100f ? "0" : "0.##", CultureInfo.InvariantCulture);
    }

    private static void EnsureStyles()
    {
        _messageStyle ??= new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };
    }

    private static void CenterWindowIfNeeded()
    {
        _windowRect.width = WindowWidth;
        _windowRect.height = WindowHeight;
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    private static void Close()
    {
        _target = null;
        _message = "";
        GUI.FocusControl(null);
    }
}
