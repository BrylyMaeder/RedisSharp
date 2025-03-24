using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharpIdentity.Util
{
    public static class ClaimExtensions
    {
        public static string ToKey(this Claim claim)
        {
            return $"{claim.Type}.{claim.Value}";
        }
    }
}
