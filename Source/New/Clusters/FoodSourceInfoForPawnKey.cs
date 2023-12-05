namespace VarietyMatters.New.Clusters
{
    using System;

    public struct FoodSourceInfoForPawnKey : IEquatable<FoodSourceInfoForPawnKey>
    {
        private string value;

        public FoodSourceInfoForPawnKey(string value)
        {
            this.value = value;
        }

        public bool Equals(FoodSourceInfoForPawnKey other)
        {
            return this.value == other.value;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FoodSourceInfoForPawnKey other))
            {
                return false;
            }

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }
    }
}
