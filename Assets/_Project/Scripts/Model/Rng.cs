using System;

namespace HexaSort.Model
{
    /// <summary>
    /// Small seeded PRNG (xorshift128+ seeded by splitmix64). Unlike System.Random its sequence is
    /// specified here, so a seed gives identical levels on every platform and runtime.
    /// </summary>
    public sealed class Rng
    {
        private ulong _s0;
        private ulong _s1;

        public Rng(int seed)
        {
            ulong x = unchecked((ulong)seed);
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return unchecked(_s1 + s0);
        }

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>Uniform int in [minInclusive, maxInclusive].</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            // 64-bit span: (max - min + 1) overflows int for wide ranges such as [0, int.MaxValue].
            ulong span = (ulong)((long)maxInclusive - minInclusive + 1);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        private static ulong SplitMix(ref ulong x)
        {
            unchecked
            {
                ulong z = x += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
