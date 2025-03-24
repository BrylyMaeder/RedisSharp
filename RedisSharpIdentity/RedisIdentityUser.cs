using RedisSharp;
using RedisSharp.Index;
using RedisSharpIdentity.Data;
using System;
using System.Collections.Generic;

namespace RedisSharpIdentity
{
    public class RedisIdentityUser : IAsyncModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Unique]
        public string UserName { get; set; }
        [Unique]
        public string NormalizedUsername { get; set; }
        [Unique]
        public string Email { get; set; }
        [Unique]
        public string NormalizedEmail { get; set; }

        public bool EmailConfirmed { get; set; }

        [Indexed(IndexType.Tag)]
        public string PhoneNumber { get; set; }

        public bool PhoneNumberConfirmed { get; set; }

        public DateTime CreationDate { get; set; }
        public DateTime LastUpdate { get; set; }

        public bool LockoutEnabled { get; set; }

        public int AccessFailedCount { get; set; }

        public DateTimeOffset LockoutEnd { get; set; }

        public string PasswordHash { get; set; }

        public AsyncLinks<IdentityClaim> Claims => new AsyncLinks<IdentityClaim>(this);

        public AsyncLinks<ExternalLogin> Logins => new AsyncLinks<ExternalLogin>(this);

        public AsyncLinks<RedisIdentityRole> Roles => new AsyncLinks<RedisIdentityRole>(this);

        public string SecurityStamp { get; set; }

        public bool TwoFactorEnabled { get; set; }

        public string AuthenticatorKey { get; set; }

        public AsyncDictionary<string, string> AuthenticatorTokens => new AsyncDictionary<string, string>(this);

        public List<string> AuthenticatorRecoveryCodes = new();

        public DateTime CreatedAt { get; set; }

        public string IndexName()
        {
            return "identity:users";
        }
    }

}
