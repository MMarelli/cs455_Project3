using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyServer
{
    /// <summary>
    /// Case and Whitespace insensative comparator
    /// </summary>
    public class CWIcompparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            x = x.Replace(" ", "").ToLower();
            y = y.Replace(" ", "").ToLower();
            return x == y;
        }

        public int GetHashCode(string obj)
        {
            return obj.Replace(" ", "").ToLower().GetHashCode();
        }
    }
}
