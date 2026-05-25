using UnityEngine;

namespace TouchGrass;

internal static class TrainingDummyCrowding
{
    private const string CrowdedMessage = "Too crowded Bro!";

    internal static bool CanPlace(Player player, Piece piece)
    {
        if (player == null ||
            piece == null ||
            !TrainingDummyDamageTest.IsTrainingDummy(piece) ||
            player.m_placementGhost == null)
        {
            return true;
        }

        Vector3 position = player.m_placementGhost.transform.position;
        if (!IsCrowded(position))
        {
            return true;
        }

        player.Message(MessageHud.MessageType.Center, CrowdedMessage);
        return false;
    }

    private static bool IsCrowded(Vector3 position)
    {
        float radius = Mathf.Clamp(TouchGrassPlugin._trainingDummyCrowdingRadius?.Value ?? 4f, 0f, 50f);
        int maxCount = Mathf.Clamp(TouchGrassPlugin._trainingDummyCrowdingMaxCount?.Value ?? 4, 0, 100);
        if (radius <= 0f || maxCount <= 0)
        {
            return false;
        }

        float radiusSquared = radius * radius;
        int count = 0;
        foreach (Character character in TrainingDummyRegistry.GetLoadedDummies())
        {
            if (character == null)
            {
                continue;
            }

            Vector3 dummyPosition = character.transform.position;
            float deltaX = dummyPosition.x - position.x;
            float deltaZ = dummyPosition.z - position.z;
            if (deltaX * deltaX + deltaZ * deltaZ > radiusSquared)
            {
                continue;
            }

            count++;
            if (count >= maxCount)
            {
                return true;
            }
        }

        return false;
    }
}
