using System;
using UnityEngine;

namespace TouchGrass;

internal static class TrainingDummyIdentity
{
    internal const string PiecePrefabName = "piece_TrainingDummy";

    private static readonly int PiecePrefabHash = StringExtensionMethods.GetStableHashCode(PiecePrefabName);

    internal static bool IsTrainingDummy(Character character)
    {
        if (character == null)
        {
            return false;
        }

        ZNetView? netView = GetNetView(character);
        if (HasTrainingDummyPrefab(netView))
        {
            return true;
        }

        Piece? piece = character.GetComponentInParent<Piece>();
        if (piece != null && IsTrainingDummy(piece))
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

        ZNetView? netView = GetNetView(piece);
        if (HasTrainingDummyPrefab(netView))
        {
            return true;
        }

        return IsTrainingDummyPrefabName(Utils.GetPrefabName(piece.gameObject));
    }

    internal static ZNetView? GetNetView(Component? component)
    {
        return component != null ? component.GetComponent<ZNetView>() ?? component.GetComponentInParent<ZNetView>() : null;
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
               string.Equals(prefabName, PiecePrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrainingDummyPrefabHash(int prefabHash)
    {
        return prefabHash == PiecePrefabHash;
    }
}
