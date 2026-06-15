using UnityEngine;

namespace TouchGrass;

internal static class TrainingObjectTuning
{
    internal static void ApplyAll()
    {
        ApplyPrefabTuning();
        ApplyLoadedTrainingDummies();
        ApplyLoadedArcheryTargets();
    }

    internal static void ApplyPrefabTuning(ZNetScene? scene = null)
    {
        scene ??= ZNetScene.instance;
        if (scene == null)
        {
            return;
        }

        GameObject trainingDummyPrefab = scene.GetPrefab(TrainingDummyIdentity.PiecePrefabName);
        Character? trainingDummy = trainingDummyPrefab != null ? trainingDummyPrefab.GetComponentInChildren<Character>(includeInactive: true) : null;
        if (trainingDummy != null)
        {
            ApplyTrainingDummy(trainingDummy);
        }

        GameObject archeryTargetPrefab = scene.GetPrefab("piece_ArcheryTarget");
        ArcheryTarget? archeryTarget = archeryTargetPrefab != null ? archeryTargetPrefab.GetComponentInChildren<ArcheryTarget>(includeInactive: true) : null;
        if (archeryTarget != null)
        {
            ApplyArcheryTarget(archeryTarget);
        }
    }

    internal static void ApplyTrainingDummy(Character character)
    {
        if (character == null || !TrainingDummyIdentity.IsTrainingDummy(character))
        {
            return;
        }

        EnsureTrainingDummyInteraction(character);
        EnsureTrainingDummyNightAggro(character);

        ZNetView? netView = character.m_nview != null ? character.m_nview : TrainingDummyIdentity.GetNetView(character);
        float desiredHealth = GetTrainingDummyHealth();
        if (netView == null || !netView.IsValid())
        {
            character.m_health = desiredHealth;
            return;
        }

        if (!netView.IsOwner())
        {
            return;
        }

        float oldMaxHealth = Mathf.Max(0.001f, character.GetMaxHealth());
        float oldHealth = Mathf.Max(0f, character.GetHealth());
        float healthRatio = Mathf.Clamp01(oldHealth / oldMaxHealth);
        bool wasFullHealth = oldHealth >= oldMaxHealth - 0.01f;

        character.m_health = desiredHealth;
        character.SetMaxHealth(desiredHealth);
        character.SetHealth(wasFullHealth ? desiredHealth : Mathf.Clamp(desiredHealth * healthRatio, 0f, desiredHealth));
    }

    internal static void ApplyArcheryTarget(ArcheryTarget archeryTarget)
    {
        if (archeryTarget == null)
        {
            return;
        }

        archeryTarget.m_raiseSkillMultiplier = GetArcheryTargetSkillMultiplier();
    }

    internal static float GetTrainingDummyHealth()
    {
        return Mathf.Clamp(TouchGrassPlugin._trainingDummyHealth?.Value ?? 2500f, 1f, 100000f);
    }

    internal static void EnsureTrainingDummyInteraction(Character character)
    {
        if (character == null || !TrainingDummyIdentity.IsTrainingDummy(character))
        {
            return;
        }

        if (character.GetComponent<TrainingDummySettingsInteractable>() == null)
        {
            character.gameObject.AddComponent<TrainingDummySettingsInteractable>();
        }
    }

    private static void EnsureTrainingDummyNightAggro(Character character)
    {
        if (character == null || !TrainingDummyIdentity.IsTrainingDummy(character))
        {
            return;
        }

        TrainingDummyNightAggro component = character.GetComponent<TrainingDummyNightAggro>();
        if (component == null)
        {
            component = character.gameObject.AddComponent<TrainingDummyNightAggro>();
        }

        component.enabled = TouchGrassPlugin._trainingDummyNightAggro?.Value == TouchGrassPlugin.Toggle.On;
    }

    private static void ApplyLoadedTrainingDummies()
    {
        foreach (Character character in TrainingDummyRegistry.GetLoadedDummies())
        {
            ApplyTrainingDummy(character);
        }
    }

    private static void ApplyLoadedArcheryTargets()
    {
        foreach (ArcheryTarget archeryTarget in Object.FindObjectsByType<ArcheryTarget>(FindObjectsSortMode.None))
        {
            ApplyArcheryTarget(archeryTarget);
        }
    }

    private static float GetArcheryTargetSkillMultiplier()
    {
        return Mathf.Clamp(TouchGrassPlugin._archeryTargetSkillMultiplier?.Value ?? 1f, 0f, 10f);
    }
}
