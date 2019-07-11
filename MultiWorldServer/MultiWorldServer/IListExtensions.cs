using System;
using System.Collections.Generic;

namespace MultiWorldServer
{
    public static class IListExtensions
    {
        public static T GetRandom<T>(this IList<T> self, Random rnd)
        {
            return self[rnd.Next(self.Count)];
        }
    }
}
