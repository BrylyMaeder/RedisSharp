using RedisSharp.Components;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RedisSharp
{
    public class AsyncLinks<TModel> : BaseComponent where TModel : IAsyncModel
    {
        public AsyncLinks(IAsyncModel document, [CallerMemberName] string propertyName = "") : base(document, propertyName)
        {

        }

        public virtual async Task SetAsync(List<TModel> documents)
        {
            await RedisSingleton.Database.KeyDeleteAsync(_fullKey);

            if (documents != null && documents.Any())
            {
                var documentKeys = documents.Select(doc => (RedisValue)doc.Id).ToArray();
                await RedisSingleton.Database.SetAddAsync(_fullKey, documentKeys);
            }
        }

        public virtual async Task<List<TModel>> GetAllAsync()
        {
            var documentIds = await RedisSingleton.Database.SetMembersAsync(_fullKey);
            var models = await RedisRepository.LoadManyAsync<TModel>(documentIds.Select(id => (string)id));
            return models.ToList();
        }


        public virtual async Task<Dictionary<string, TModel>> GetAsDictionaryAsync()
        {
            var documentIds = await RedisSingleton.Database.SetMembersAsync(_fullKey);
            var keys = documentIds.Select(id => (string)id).ToArray(); // Convert to array for batching

            var models = await RedisRepository.LoadManyAsync<TModel>(keys); // Load all models at once
            var dictionary = models.ToDictionary(model => model.Id, model => model); // Assuming models have a `Key` property

            return dictionary;
        }


        public virtual async Task<bool> ContainsAsync(IAsyncModel document) => await ContainsAsync(document.Id);

        public virtual async Task<bool> ContainsAsync(string id)
        {
            return await RedisSingleton.Database.SetContainsAsync(_fullKey, id);
        }

        public virtual async Task<bool> AddOrUpdateAsync(TModel document)
        {
            if (document == null) return false;

            return await RedisSingleton.Database.SetAddAsync(_fullKey, document.Id);
        }

        public virtual async Task<TModel> GetAsync(IAsyncModel document) => await GetAsync(document.Id);

        public virtual async Task<TModel> GetAsync(string id)
        {
            if (!await ContainsAsync(id))
                return default;

            return await RedisRepository.LoadAsync<TModel>(id);
        }

        public virtual async Task<bool> RemoveAsync(IAsyncModel document) => await RemoveAsync(document.Id);

        public virtual async Task<bool> RemoveAsync(string id)
        {
            return await RedisSingleton.Database.SetRemoveAsync(_fullKey, id);
        }

        public virtual async Task<int> CountAsync()
        {
            return (int)await RedisSingleton.Database.SetLengthAsync(_fullKey);
        }

        public virtual async Task ClearAsync()
        {
            await RedisSingleton.Database.KeyDeleteAsync(_fullKey);
        }
    }

}
