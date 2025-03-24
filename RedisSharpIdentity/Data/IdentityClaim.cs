using RedisSharp;
using RedisSharpIdentity.Util;
using System;
using System.Security.Claims;

namespace RedisSharpIdentity.Data
{
    public class IdentityClaim : IAsyncModel
    {
        public string Id { get; set; }

        public static IdentityClaim Create(string id)
        {
            return new IdentityClaim() { Id = id };
        }

        public static IdentityClaim Create(Claim claim) 
        {
            return Create(claim.ToKey());
        }

        public string IndexName()
        {
            return "identity:claims";
        }

        public Claim Claim { get; set; }

        public AsyncLinks<RedisIdentityUser> Users => new AsyncLinks<RedisIdentityUser>(this);

        public DateTime CreatedAt { get; set; }
    }
}
