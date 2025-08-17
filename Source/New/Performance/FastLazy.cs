namespace VarietyMatters.New.Performance
{
    using System;
    using Verse;

    public class FastLazy<T> : IExposable
    {
        private T value;

        private Func<T> valueFactory;

        public FastLazy()
        {
        }

        public FastLazy(Func<T> valueFactory)
        {
            this.valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        public T Value
        {
            get
            {
                if (!isValueCreated)
                {
                    if (valueFactory == null)
                    {
                        // Handle case where valueFactory is null after loading from save
                        Verse.Log.Warning("VarietyMatters: FastLazy valueFactory is null, returning default value");
                        this.value = default(T);
                    }
                    else
                    {
                        try
                        {
                            this.value = valueFactory();
                        }
                        catch (System.Exception ex)
                        {
                            Verse.Log.Warning($"VarietyMatters: Error in FastLazy value factory: {ex.Message}");
                            this.value = default(T);
                        }
                    }
                    this.isValueCreated = true;
                }
                return this.value;
            }
        }

        private bool isValueCreated;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (!this.isValueCreated)
                {
                    _ = this.Value;
                }
            }

            Scribe_Values.Look(ref this.value, "value");
            Scribe_Values.Look(ref this.isValueCreated, "isValueCreated");

            // Note: valueFactory is not saved/loaded as it contains delegate references
            // that cannot be properly serialized. The parent class should reinitialize
            // FastLazy objects with proper factories after loading.
        }

        public override string ToString()
        {
            return this.Value.ToString();
        }

        public static implicit operator T(FastLazy<T> lazy)
        {
            return lazy.Value;
        }
    }
}
