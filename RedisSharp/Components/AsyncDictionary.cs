using RedisSharp.Components;
using RedisSharp;
using RedisSharp.Extensions;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp
{
    public class AsyncDictionary<TKey, TValue> : BaseComponent
    {
        public AsyncDictionary(IAsyncModel document, [CallerMemberName] string propertyName = null) : base(document, propertyName) { }

        // Check if a specific field key exists within the hash
        public async Task<bool> ContainsKeyAsync(TKey key)
        {
            return await RedisSingleton.Database.HashExistsAsync(_fullKey, RedisConverters.SerializeToRedis(key));
        }

        public async Task<TValue> SetAsync(TKey key, TValue value)
        {
            var redisValue = RedisConverters.SerializeToRedis(value);
            await RedisSingleton.Database.HashSetAsync(_fullKey, new HashEntry[] { new HashEntry(RedisConverters.SerializeToRedis(key), redisValue) });
            return value;
        }
        // Set a value in the hash by field key
        public async Task SetAsync(TKey key, string value)
        {
            var redisValue = RedisConverters.SerializeToRedis(value);
            await RedisSingleton.Database.HashSetAsync(_fullKey, new HashEntry[] { new HashEntry(RedisConverters.SerializeToRedis(key), redisValue) });
        }

        // Get a value from the hash by field key, or return a default value if the key does not exist
        public async Task<TValue> GetByKeyAsync(TKey key, TValue defaultValue = default)
        {
            var result = await RedisSingleton.Database.HashGetAsync(_fullKey, RedisConverters.SerializeToRedis(key));
            if (result.IsNullOrEmpty)
            {
                return defaultValue;
            }
            return (TValue)RedisConverters.DeserializeFromRedis(result, typeof(TValue));
        }


        // Remove a value from the hash by field key
        public async Task<bool> RemoveAsync(TKey key)
        {
            return await RedisSingleton.Database.HashDeleteAsync(_fullKey, RedisConverters.SerializeToRedis(key));
        }

        // Get the total count of items in the hash
        public async Task<long> CountAsync()
        {
            return await RedisSingleton.Database.HashLengthAsync(_fullKey);
        }

        // Get all values in the hash as a dictionary
        public async Task<Dictionary<TKey, TValue>> GetAsync()
        {
            var entries = await RedisSingleton.Database.HashGetAllAsync(_fullKey);
            return entries.ToDictionary(
                entry => (TKey)RedisConverters.DeserializeFromRedis(entry.Name, typeof(TKey)),
                entry => (TValue)RedisConverters.DeserializeFromRedis(entry.Value, typeof(TValue))
            );
        }
    }
}
