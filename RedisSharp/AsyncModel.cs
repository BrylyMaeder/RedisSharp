using System;

namespace RedisSharp
{
    public abstract class AsyncModel : IAsyncModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Indexed]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public abstract string IndexName();
    }
}
