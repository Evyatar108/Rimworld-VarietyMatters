using System.Collections.Generic;
using Verse;

namespace VarietyMattersMoreCompat
{
    public class VarietyExtension : DefModExtension
    {
        public bool ignoreFoodPreferability = false;
        public bool isArchotech = false;
        public int dessertQuality = -1;

        public override IEnumerable<string> ConfigErrors()
        {
            base.ConfigErrors();

            if (dessertQuality < -1)
            {
                yield return $"{nameof(dessertQuality)} is {dessertQuality}, it must be -1 if not a dessert or more if a dessert.";
                dessertQuality = -1;
            }
        }
    }
}