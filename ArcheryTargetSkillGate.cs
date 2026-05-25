using System;

namespace TouchGrass;

internal static class ArcheryTargetSkillGate
{
    internal static bool ShouldAwardSkill(ItemDrop.ItemData weapon, Projectile projectile)
    {
        if (TouchGrassPlugin._archeryTargetArrowBoltSkillOnly?.Value != TouchGrassPlugin.Toggle.On)
        {
            return true;
        }

        return IsArrowOrBoltAmmo(projectile != null ? projectile.m_ammo : null) ||
               IsArrowOrBoltAmmo(weapon);
    }

    private static bool IsArrowOrBoltAmmo(ItemDrop.ItemData? item)
    {
        if (item == null || item.m_shared == null)
        {
            return false;
        }

        ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
        if (itemType is not ItemDrop.ItemData.ItemType.Ammo and not ItemDrop.ItemData.ItemType.AmmoNonEquipable)
        {
            return false;
        }

        return ContainsArrowOrBolt(item.m_shared.m_ammoType) ||
               ContainsArrowOrBolt(item.m_shared.m_name) ||
               ContainsArrowOrBolt(item.m_dropPrefab != null ? Utils.GetPrefabName(item.m_dropPrefab) : "");
    }

    private static bool ContainsArrowOrBolt(string? value)
    {
        if (value == null)
        {
            return false;
        }

        string text = value.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        return text.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("bolt", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
