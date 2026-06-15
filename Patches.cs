using System;
using HarmonyLib;

namespace TouchGrass;

[HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
internal static class SkillsRaiseSkillPatch
{
    private static bool Prefix(Skills __instance, Skills.SkillType skillType, ref float factor)
    {
        float sourceFactor = factor;
        bool allowGain = SkillEarnGate.AdjustSkillGain(__instance, skillType, ref factor);
        TrainingMeter.RecordSkillGain(__instance, skillType, sourceFactor, allowGain ? factor : 0f);
        return allowGain;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
internal static class PlayerRaiseSkillPatch
{
    private static void Prefix(Player __instance, Skills.SkillType skill, float value, out int __state)
    {
        __state = TrainingMeter.BeginPlayerSkillRaise(__instance, skill, value);
    }

    private static Exception Finalizer(int __state, Exception __exception)
    {
        TrainingMeter.EndPlayerSkillRaise(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
internal static class SkillsLowerAllSkillsPatch
{
    private static bool Prefix(Skills __instance, float factor)
    {
        return SkillEarnGate.AdjustSkillReduction(__instance, factor);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
internal static class CharacterRPCDamagePatch
{
    private static void Prefix(Character __instance, HitData hit, out TrainingMeter.IncomingDamageState __state)
    {
        TrainingDummyDamageTest.OverrideLocalIncomingDamage(__instance, hit);
        __state = TrainingMeter.BeforeIncomingDamage(__instance, hit);
    }

    private static void Postfix(Character __instance, TrainingMeter.IncomingDamageState __state)
    {
        TrainingMeter.AfterIncomingDamage(__instance, __state);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Awake))]
internal static class CharacterAwakePatch
{
    private static void Postfix(Character __instance)
    {
        TrainingDummyRegistry.Register(__instance);
        TrainingObjectTuning.ApplyTrainingDummy(__instance);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.OnDestroy))]
internal static class CharacterOnDestroyPatch
{
    private static void Prefix(Character __instance)
    {
        TrainingDummyRegistry.Unregister(__instance);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
internal static class CharacterGetHoverTextPatch
{
    private static void Postfix(Character __instance, ref string __result)
    {
        if (TrainingDummyIdentity.IsTrainingDummy(__instance))
        {
            __result = TrainingDummySettingsWindow.GetHoverText(__result);
        }
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
internal static class CharacterDamagePatch
{
    private static void Prefix(Character __instance, HitData hit)
    {
        TrainingMeter.RecordOutgoingDamage(__instance, hit);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
internal static class PlayerTakeInputPatch
{
    private static void Postfix(ref bool __result)
    {
        if (TrainingDummySettingsWindow.IsVisible)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
internal static class PlayerTryPlacePiecePatch
{
    private static bool Prefix(Player __instance, Piece piece, ref bool __result)
    {
        if (TrainingDummyCrowding.CanPlace(__instance, piece))
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
internal static class HumanoidBlockAttackPatch
{
    private static void Postfix(Humanoid __instance, HitData hit)
    {
        TrainingMeter.RecordBlockResult(__instance, hit);
    }
}

[HarmonyPatch(typeof(ArcheryTarget), nameof(ArcheryTarget.Start))]
internal static class ArcheryTargetStartPatch
{
    private static void Postfix(ArcheryTarget __instance)
    {
        TrainingObjectTuning.ApplyArcheryTarget(__instance);
    }
}

[HarmonyPatch(typeof(ArcheryTarget), nameof(ArcheryTarget.OnProjectileHit))]
internal static class ArcheryTargetOnProjectileHitPatch
{
    private static void Prefix(ArcheryTarget __instance, ItemDrop.ItemData weapon, Projectile projectile, out float __state)
    {
        __state = __instance.m_raiseSkillMultiplier;
        if (!ArcheryTargetSkillGate.ShouldAwardSkill(weapon, projectile))
        {
            __instance.m_raiseSkillMultiplier = 0f;
        }
    }

    private static void Postfix(ArcheryTarget __instance, float __state)
    {
        __instance.m_raiseSkillMultiplier = __state;
    }

    private static Exception Finalizer(ArcheryTarget __instance, float __state, Exception __exception)
    {
        __instance.m_raiseSkillMultiplier = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class ZNetSceneAwakePatch
{
    private static void Postfix(ZNetScene __instance)
    {
        TrainingObjectTuning.ApplyPrefabTuning(__instance);
        PieceRecipeTuning.ApplyAll();
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
internal static class ObjectDBAwakePatch
{
    private static void Postfix()
    {
        PieceRecipeTuning.ApplyAll();
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
internal static class ObjectDBCopyOtherDBPatch
{
    private static void Postfix()
    {
        PieceRecipeTuning.ApplyAll();
    }
}

[HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
internal static class PieceSetCreatorPatch
{
    private static void Postfix(Piece __instance)
    {
        TrainingDummyDamageTest.StampPlacedDamageProfile(__instance);
    }
}
