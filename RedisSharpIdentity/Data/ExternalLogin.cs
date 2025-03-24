using RedisSharpIdentity.Util;
using Microsoft.AspNetCore.Identity;
using RedisSharp;
using System;

namespace RedisSharpIdentity.Data
{
    public class ExternalLogin : IAsyncModel
    {
        public string Id { get; set; }
        public string UserId { get; set; }

        public static ExternalLogin Create(string id)
        {
            return new ExternalLogin { Id = id };
        }

        public static ExternalLogin Create(UserLoginInfo loginInfo) 
        {
            return Create(loginInfo.ToKey());
        }

        public UserLoginInfo LoginInfo { get; set; }

        public DateTime CreatedAt { get; set; }

        public AsyncLink<RedisIdentityUser> User => new(this);
        
        public string IndexName()
        {
            return $"identity:externalLogins";
        }
    }
}
