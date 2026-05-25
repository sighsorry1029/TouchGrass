using System;
using UnityEngine;

namespace TouchGrass;

internal static class TrainingDummyIdentity
{
    internal const string PiecePrefabName = "piece_TrainingDummy";
    internal const string CharacterPrefabName = "TrainingDummy";
    internal const string LocalizationKey = "$piece_trainingdummy";

    internal static readonly string[] PrefabNames = [PiecePrefabName, CharacterPrefabName];
    private static readonly int PiecePrefabHash = StringExtensionMethods.GetStableHashCode(PiecePrefabName);
    private static readonly int CharacterPrefabHash = StringExtensionMethods.GetStableHashCode(CharacterPrefabName);

    internal static bool IsTrainingDummy(Character character)
    {
        if (character == null)
        {
            return false;
        }

        if (character.m_name == LocalizationKey)
        {
            return true;
        }

        ZNetView? netView = GetNetView(character);
        if (HasTrainingDummyPrefab(netView))
        {
            return true;
        }

        return IsTrainingDummyPrefabName(Utils.GetPrefabName(character.gameObject));
    }

    internal static bool IsTrainingDummy(Piece piece)
    {
        if (piece == null)
        {
            return false;
        }

        if (piece.m_name == LocalizationKey)
        {
            return true;
        }

        ZNetView? netView = GetNetView(piece);
        if (HasTrainingDummyPrefab(netView))
        {
            return true;
        }

        return IsTrainingDummyPrefabName(Utils.GetPrefabName(piece.gameObject));
    }

    internal static bool HasTrainingDummyZdoPrefab(Character character)
    {
        ZNetView? netView = GetNetView(character);
        return netView != null &&
               netView.IsValid() &&
               netView.GetZDO() != null &&
               IsTrainingDummyPrefabHash(netView.GetZDO().GetPrefab());
    }

    internal static ZNetView? GetNetView(Component? component)
    {
        return component != null ? component.GetComponent<ZNetView>() : null;
    }

    private static bool HasTrainingDummyPrefab(ZNetView? netView)
    {
        if (netView == null)
        {
            return false;
        }

        if (netView.IsValid() && netView.GetZDO() != null && IsTrainingDummyPrefabHash(netView.GetZDO().GetPrefab()))
        {
            return true;
        }

        return IsTrainingDummyPrefabName(netView.GetPrefabName());
    }

    private static bool IsTrainingDummyPrefabName(string prefabName)
    {
        return !string.IsNullOrEmpty(prefabName) &&
               (string.Equals(prefabName, PiecePrefabName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prefabName, CharacterPrefabName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTrainingDummyPrefabHash(int prefabHash)
    {
        return prefabHash == PiecePrefabHash || prefabHash == CharacterPrefabHash;
    }
}
