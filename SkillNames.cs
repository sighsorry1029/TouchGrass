using System.Collections.Generic;
using System.Text;

namespace TouchGrass;

internal static class SkillNameFormatter
{
    internal static string FormatList(IReadOnlyList<Skills.SkillType> skillTypes)
    {
        StringBuilder builder = new();
        for (int i = 0; i < skillTypes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Format(skillTypes[i]));
        }

        return builder.ToString();
    }

    internal static string Format(Skills.SkillType skillType)
    {
        string token = "$skill_" + skillType.ToString().ToLowerInvariant();
        if (Localization.instance != null)
        {
            string localized = Localization.instance.Localize(token);
            if (!string.IsNullOrWhiteSpace(localized) && localized != token)
            {
                return localized;
            }
        }

        return skillType == Skills.SkillType.Unarmed ? "Fists" : skillType.ToString();
    }
}
