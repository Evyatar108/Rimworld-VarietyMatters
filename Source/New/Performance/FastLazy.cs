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
                    this.value = valueFactory();
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
