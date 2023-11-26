namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class MealQualifiers
    {
        private static readonly List<string> Values = new List<string>
        {
            "simple",
            "fine",
            "lavish",
            "gourmet",
            "carnivore",
            "vegetarian"
        };

        public static string RemoveMealQualifiersFromMealLabel(string mealLabel)
        {
            var words = mealLabel.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            StringBuilder sb = new StringBuilder();

            foreach (var word in words)
            {
                if (IsMealQualifier(word))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" ");
                }

                sb.Append(word);
            }

            return sb.ToString();
        }

        private static bool IsMealQualifier(string mealLabel)
        {
            foreach (string value in Values)
            {
                if (value.Equals(mealLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

            }

            return false;
        }
    }
}
