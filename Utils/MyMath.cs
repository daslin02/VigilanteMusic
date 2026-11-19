using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VigilanteMusic.Utils
{
    internal class MyMath
    {
        public static T Clamp<T>(T min, T max, T Value) where T : IComparable<T>
        {
            if (Value.CompareTo(min) < 0) return min;
            if (Value.CompareTo(max) > 0) return max;
            return Value;
        }
    }
}
