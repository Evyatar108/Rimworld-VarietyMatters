namespace VarietyMatters
{
    using Verse;

    public class RaceSetting : IExposable
    {

        public string name;

        public string modName;

        public bool isVarietyEnabled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.name, "name");
            Scribe_Values.Look(ref this.modName, "modName");
            Scribe_Values.Look(ref this.isVarietyEnabled, "isVarietyEnabled");
        }
    }
}
