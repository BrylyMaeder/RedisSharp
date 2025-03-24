using RedisSharp;
using System;


namespace RedisSharpIdentity
{
    public class RedisIdentityRole : IAsyncModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public static RedisIdentityRole Create(string id)
        {
            return new RedisIdentityRole { Id = id };
        }

        public string IndexName()
        {
            return "identity:roles";
        }

        [Unique]
        public string Name { get; set; }

        public string NameNormalized { get; set; }

        public AsyncLinks<RedisIdentityUser> Users => new(this);

        public DateTime CreatedAt { get; set; }
    }
}
