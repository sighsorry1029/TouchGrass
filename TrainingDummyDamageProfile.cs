using System;
using UnityEngine;

namespace TouchGrass;

internal static class TrainingDummyDamageTest
{
    internal const string DummyDamageProfileVersionKey = "TouchGrass_DummyDamageProfileVersion";
    internal const string DummyDamageTypeKey = "TouchGrass_DummyDamageType";
    internal const string DummyDamageAmountKey = "TouchGrass_DummyDamageAmount";
    internal const int DummyDamageProfileVersion = 1;

    internal static void OverrideLocalIncomingDamage(Character victim, HitData hit)
    {
        if (hit == null ||
            victim == null ||
            victim != Player.m_localPlayer)
        {
            return;
        }

        Character attacker = hit.GetAttacker();
        if (!IsTrainingDummy(attacker))
        {
            return;
        }

        if (!TryReadStampedDamageProfile(attacker, out DamageProfile profile) &&
            !TryGetLocalDamageProfile(out profile))
        {
            return;
        }

        hit.m_damage = BuildDamageTypes(profile.DamageType, profile.Amount);
    }

    internal static void StampPlacedDamageProfile(Piece piece)
    {
        if (piece == null ||
            !IsTrainingDummy(piece) ||
            !TryGetLocalDamageProfile(out DamageProfile profile))
        {
            return;
        }

        ZNetView? netView = piece.m_nview != null ? piece.m_nview : TrainingDummyIdentity.GetNetView(piece);
        if (netView == null || !netView.IsValid() || !netView.IsOwner())
        {
            return;
        }

        ZDO zdo = netView.GetZDO();
        if (zdo == null || piece.GetCreator() == 0L || zdo.GetInt(DummyDamageTypeKey, -1) >= 0)
        {
            return;
        }

        zdo.Set(DummyDamageProfileVersionKey, DummyDamageProfileVersion);
        zdo.Set(DummyDamageTypeKey, (int)profile.DamageType);
        zdo.Set(DummyDamageAmountKey, profile.Amount);
    }

    internal static bool TryGetEffectiveDamageProfile(Character attacker, out TouchGrassPlugin.TrainingDummyDamageType damageType, out float amount)
    {
        if (TryReadStampedDamageProfile(attacker, out DamageProfile stampedProfile))
        {
            damageType = stampedProfile.DamageType;
            amount = stampedProfile.Amount;
            return true;
        }

        if (TryGetLocalDamageProfile(out DamageProfile localProfile))
        {
            damageType = localProfile.DamageType;
            amount = localProfile.Amount;
            return true;
        }

        damageType = TouchGrassPlugin.TrainingDummyDamageType.Blunt;
        amount = 1f;
        return false;
    }

    internal static void SetDamageProfile(Character character, TouchGrassPlugin.TrainingDummyDamageType damageType, float amount)
    {
        ZNetView? netView = character != null ? character.m_nview != null ? character.m_nview : TrainingDummyIdentity.GetNetView(character) : null;
        if (netView == null || !netView.IsValid())
        {
            return;
        }

        netView.ClaimOwnership();
        ZDO zdo = netView.GetZDO();
        if (zdo == null)
        {
            return;
        }

        zdo.Set(DummyDamageProfileVersionKey, DummyDamageProfileVersion);
        zdo.Set(DummyDamageTypeKey, (int)damageType);
        zdo.Set(DummyDamageAmountKey, Mathf.Clamp(amount, 1f, 500f));
    }

    internal static bool IsTrainingDummy(Character character)
    {
        return TrainingDummyIdentity.IsTrainingDummy(character);
    }

    internal static bool IsTrainingDummy(Piece piece)
    {
        return TrainingDummyIdentity.IsTrainingDummy(piece);
    }

    internal static bool HasTrainingDummyZdoPrefab(Character character)
    {
        return TrainingDummyIdentity.HasTrainingDummyZdoPrefab(character);
    }

    private static bool TryReadStampedDamageProfile(Character attacker, out DamageProfile profile)
    {
        profile = default;
        ZNetView? netView = TrainingDummyIdentity.GetNetView(attacker);
        if (netView == null || !netView.IsValid())
        {
            return false;
        }

        ZDO zdo = netView.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        int damageTypeValue = zdo.GetInt(DummyDamageTypeKey, -1);
        if (!Enum.IsDefined(typeof(TouchGrassPlugin.TrainingDummyDamageType), damageTypeValue))
        {
            return false;
        }

        float amount = zdo.GetFloat(DummyDamageAmountKey, 0f);
        if (amount <= 0f)
        {
            return false;
        }

        profile = new DamageProfile((TouchGrassPlugin.TrainingDummyDamageType)damageTypeValue, amount);
        return true;
    }

    private static bool TryGetLocalDamageProfile(out DamageProfile profile)
    {
        profile = new DamageProfile(
            TouchGrassPlugin._localTrainingDummyDamageType.Value,
            TouchGrassPlugin._localTrainingDummyDamageAmount.Value);
        return true;
    }

    private static HitData.DamageTypes BuildDamageTypes(TouchGrassPlugin.TrainingDummyDamageType damageType, float amount)
    {
        amount = Mathf.Clamp(amount, 1f, 500f);
        HitData.DamageTypes damageTypes = new();
        switch (damageType)
        {
            case TouchGrassPlugin.TrainingDummyDamageType.Blunt:
                damageTypes.m_blunt = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Slash:
                damageTypes.m_slash = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Pierce:
                damageTypes.m_pierce = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Fire:
                damageTypes.m_fire = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Frost:
                damageTypes.m_frost = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Lightning:
                damageTypes.m_lightning = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Poison:
                damageTypes.m_poison = amount;
                break;
            case TouchGrassPlugin.TrainingDummyDamageType.Spirit:
                damageTypes.m_spirit = amount;
                break;
            default:
                damageTypes.m_blunt = amount;
                break;
        }

        return damageTypes;
    }

    private readonly struct DamageProfile(TouchGrassPlugin.TrainingDummyDamageType damageType, float amount)
    {
        internal readonly TouchGrassPlugin.TrainingDummyDamageType DamageType = damageType;
        internal readonly float Amount = amount;
    }
}
