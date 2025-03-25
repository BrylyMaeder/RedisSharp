using RedisSharp.Factory;
using RedisSharp.Helper;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Expressions;
using RedisSharp.Models;


namespace RedisSharp
{
    public partial class RedisRepository
    {
        #region Creation
        public static async Task<BulkModelCreationResult<TModel>> CreateManyAsync<TModel>(IEnumerable<TModel> models) where TModel : IAsyncModel
        {
            if (models == null || !models.Any() || models.Any(m => string.IsNullOrEmpty(m.Id)))
            {
                return BulkModelCreationResult<TModel>.Failed("Invalid models: missing ID or empty list.");
            }

            var db = RedisSingleton.Database;
            var test = models.FirstOrDefault();
            var batch = db.CreateBatch();
            var tasks = models.Select(model => new { Model = model, ExistsTask = batch.KeyExistsAsync(model.GetKey()) }).ToList();
            batch.Execute();

            var existingModels = (await Task.WhenAll(tasks.Select(t => t.ExistsTask)))
                .Select((exists, index) => new { Exists = exists, Model = tasks[index].Model })
                .Where(x => x.Exists)
                .Select(x => x.Model.Id)
                .ToList();

            if (existingModels.Any())
            {
                return BulkModelCreationResult<TModel>.Failed($"The following models already exist: {string.Join(", ", existingModels)}");
            }

            // Assign CreatedAt to UTC now if it's default
            foreach (var model in models)
            {
                if (model.CreatedAt == default)
                {
                    model.CreatedAt = DateTime.UtcNow;
                }
            }

            var result = await ModelPushHelper.PushAsync(models);

            if (result.Succeeded)
            {
                return BulkModelCreationResult<TModel>.Success(models);
            }
            else return BulkModelCreationResult<TModel>.Failed(result.Message);
        }


        public static async Task<BulkModelCreationResult<TModel>> CreateManyAsync<TModel>(IEnumerable<string> ids = null) where TModel : IAsyncModel
        {
            if (ids == null || !ids.Any())
            {
                ids = Enumerable.Range(0, 1).Select(_ => Guid.NewGuid().ToString());
            }
            else
            {
                ids = ids.Select(id => string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id);
            }

            var models = ids.Select(CreateModel<TModel>).ToList();

            return await CreateManyAsync(models);
        }


        private static TModel CreateModel<TModel>(string id) where TModel : IAsyncModel
        {
            var model = ModelFactory.Create<TModel>(id);
            model.CreatedAt = DateTime.UtcNow;
            return model;
        }


        #endregion

        #region Loading
        public static async Task<IEnumerable<TModel>> LoadManyAsync<TModel>(
    IEnumerable<string> ids,
    params Expression<Func<TModel, object>>[] selectors)
    where TModel : IAsyncModel
        {
            // Get the Redis database connection
            var database = RedisSingleton.Database;

            // Create a batch operation for better performance
            var batch = database.CreateBatch();

            // Create tasks to check which IDs exist in Redis
            var keyTasks = ids.ToDictionary(
                id => id,
                id => batch.KeyExistsAsync(ModelHelper.CreateKey<TModel>(id))
            );

            // Execute all batched operations
            batch.Execute();

            // Wait for all length checks to complete
            await Task.WhenAll(keyTasks.Values);

            // Filter to only valid IDs (those with data in Redis)
            var validIds = keyTasks
                .Where(kvp => kvp.Value.Result)
                .Select(kvp => kvp.Key)
                .ToList();

            // Create model instances for each valid ID
            var models = validIds
                .Select(id => ModelFactory.Create<TModel>(id))
                .ToList();

            // If specific properties were requested, hydrate them
            if (selectors?.Length > 0)
            {
                await models.HydrateAsync(selectors);
            }

            return models;
        }

        #endregion

    }
}
