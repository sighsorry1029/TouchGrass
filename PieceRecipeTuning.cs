using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TouchGrass;

internal static class PieceRecipeTuning
{
    private const string TrainingDummyPrefabName = "piece_TrainingDummy";
    private const string ArcheryTargetPrefabName = "piece_ArcheryTarget";
    private static readonly Dictionary<string, Piece.Requirement[]> VanillaRecipes = new(StringComparer.OrdinalIgnoreCase);

    internal static void ApplyAll()
    {
        ZNetScene scene = ZNetScene.instance;
        if (scene == null || ObjectDB.instance == null)
        {
            return;
        }

        ApplyRecipe(scene, TrainingDummyPrefabName, TouchGrassPlugin._trainingDummyRecipe?.Value);
        ApplyRecipe(scene, ArcheryTargetPrefabName, TouchGrassPlugin._archeryTargetRecipe?.Value);
    }

    private static void ApplyRecipe(ZNetScene scene, string prefabName, string? recipeText)
    {
        GameObject prefab = scene.GetPrefab(prefabName);
        if (prefab == null)
        {
            return;
        }

        Piece piece = prefab.GetComponentInChildren<Piece>(includeInactive: true);
        if (piece == null)
        {
            return;
        }

        StoreVanillaRecipe(prefabName, piece);

        string recipeOverride = recipeText ?? "";
        if (string.IsNullOrWhiteSpace(recipeOverride))
        {
            piece.m_resources = CloneRequirements(VanillaRecipes[prefabName]);
            return;
        }

        recipeOverride = recipeOverride.Trim();
        if (!TryParseRecipe(recipeOverride, out Piece.Requirement[] requirements))
        {
            piece.m_resources = CloneRequirements(VanillaRecipes[prefabName]);
            TouchGrassPlugin.TouchGrassLogger.LogWarning($"Invalid {prefabName} recipe override '{recipeOverride}'. Restored vanilla recipe.");
            return;
        }

        piece.m_resources = requirements;
    }

    private static void StoreVanillaRecipe(string prefabName, Piece piece)
    {
        if (VanillaRecipes.ContainsKey(prefabName))
        {
            return;
        }

        VanillaRecipes[prefabName] = CloneRequirements(piece.m_resources);
    }

    private static bool TryParseRecipe(string recipeText, out Piece.Requirement[] requirements)
    {
        requirements = Array.Empty<Piece.Requirement>();
        string trimmed = recipeText.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("free", StringComparison.OrdinalIgnoreCase) ||
            trimmed == "-")
        {
            return true;
        }

        string[] entries = trimmed.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
        List<Piece.Requirement> parsed = [];
        foreach (string rawEntry in entries)
        {
            string[] parts = rawEntry.Trim().Split([':', '='], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            string itemName = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 1)
            {
                return false;
            }

            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemName);
            ItemDrop? itemDrop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
            if (itemDrop == null)
            {
                TouchGrassPlugin.TouchGrassLogger.LogWarning($"Unknown recipe item prefab '{itemName}'.");
                return false;
            }

            parsed.Add(new Piece.Requirement
            {
                m_resItem = itemDrop,
                m_amount = amount,
                m_recover = true
            });
        }

        requirements = parsed.ToArray();
        return true;
    }

    private static Piece.Requirement[] CloneRequirements(Piece.Requirement[]? requirements)
    {
        if (requirements == null || requirements.Length == 0)
        {
            return Array.Empty<Piece.Requirement>();
        }

        Piece.Requirement[] copy = new Piece.Requirement[requirements.Length];
        for (int i = 0; i < requirements.Length; i++)
        {
            Piece.Requirement requirement = requirements[i];
            copy[i] = new Piece.Requirement
            {
                m_resItem = requirement.m_resItem,
                m_amount = requirement.m_amount,
                m_extraAmountOnlyOneIngredient = requirement.m_extraAmountOnlyOneIngredient,
                m_amountPerLevel = requirement.m_amountPerLevel,
                m_recover = requirement.m_recover
            };
        }

        return copy;
    }
}
