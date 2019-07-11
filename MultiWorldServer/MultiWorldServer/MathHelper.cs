using System;
using System.Linq;

namespace MultiWorldServer
{
    public static class MathHelper
    {
        public static int LCM(params int[] numbers)
        {
            if (numbers.Length == 2)
            {
                return Math.Abs(numbers[0] * numbers[1]) / GCD(numbers[0], numbers[1]);
            }

            return numbers.Aggregate((a, b) => LCM(a, b));
        }

        public static int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
        }
    }
}
