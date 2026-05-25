using System.Collections.Generic;

namespace TouchGrass;

internal static class TrainingDummyRegistry
{
    private static readonly HashSet<Character> LoadedDummies = [];

    internal static void Register(Character character)
    {
        if (character == null || !TrainingDummyIdentity.HasTrainingDummyZdoPrefab(character))
        {
            return;
        }

        LoadedDummies.Add(character);
    }

    internal static void Unregister(Character character)
    {
        if (character == null)
        {
            return;
        }

        LoadedDummies.Remove(character);
    }

    internal static IReadOnlyCollection<Character> GetLoadedDummies()
    {
        LoadedDummies.RemoveWhere(character => character == null);
        return LoadedDummies;
    }
}
