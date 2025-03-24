using RedisSharp.Contracts;
using RedisSharp.Extensions;
using RedisSharp.Models;
using RedisSharp.Query;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace RedisSharp.Helper
{
    internal static class ModelPushHelper
    {
        internal static async Task<PushResult> PushAsync<TModel>(TModel model,
            params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (model == null) return PushResult.Failed(model, "Input model cannot be null or empty.");
            return await PushModelsAsync(new List<TModel> { model }, selectors);
        }

        internal static async Task<PushResult> PushAsync<TModel>(IEnumerable<TModel> models,
            params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            return await PushModelsAsync(models.ToList(), selectors);
        }

        private static async Task<PushResult> PushModelsAsync<TModel>(List<TModel> models,
            Expression<Func<TModel, object>>[] propertySelectors)
            where TModel : IAsyncModel
        {
            if (models == null || !models.Any())
                return PushResult.Failed(models, "Input model(s) cannot be null or empty.");

            var memberNames = propertySelectors != null ? ModelHelper.GetMemberNames(propertySelectors) : null;
            var batch = RedisSingleton.Database.CreateBatch();
            var operationTasks = new List<Task>();
            var uniqueChecks = new Dictionary<string, List<Tuple<TModel, RedisValue>>>();
            var modelData = new Dictionary<TModel, Tuple<List<HashEntry>, List<RedisValue>>>();

            // Single pass through all models and properties
            foreach (var model in models)
            {
                var properties = model.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(p => !typeof(IModelComponent).IsAssignableFrom(p.PropertyType) &&
                               !typeof(IAsyncModel).IsAssignableFrom(p.PropertyType) &&
                               p.Name != "Id");

                var hashEntries = new List<HashEntry>();
                var nullKeys = new List<RedisValue>();

                foreach (var property in properties)
                {
                    if (memberNames != null && !memberNames.Contains(property.Name))
                        continue;

                    var value = property.GetValue(model);
                    var redisValue = RedisConverters.SerializeToRedis(value);

                    if (value == null)
                    {
                        nullKeys.Add(property.Name);
                    }
                    else
                    {
                        hashEntries.Add(new HashEntry(property.Name, redisValue));

                        if (property.GetCustomAttribute<UniqueAttribute>() != null)
                        {
                            if (!uniqueChecks.ContainsKey(property.Name))
                                uniqueChecks[property.Name] = new List<Tuple<TModel, RedisValue>>();
                            uniqueChecks[property.Name].Add(Tuple.Create(model, redisValue));
                        }
                    }
                }

                modelData[model] = Tuple.Create(hashEntries, nullKeys);
            }

            // Check unique constraints
            var violations = await CheckUniqueConstraints<TModel>(uniqueChecks, ModelHelper.GetIndex<TModel>());
            if (violations.Any())
            {
                return PushResult.Failed(models,
                    "Multiple properties with a Unique Tag have the same value: " + string.Join(", ",
                        violations.Select(kv => kv.Key + ": " + string.Join(", ", kv.Value))));
            }

            // Prepare batch operations
            foreach (var kvp in modelData)
            {
                var model = kvp.Key;
                var hashEntries = kvp.Value.Item1;
                var nullKeys = kvp.Value.Item2;

                var tasks = new List<Task>();
                if (hashEntries.Count > 0)
                    tasks.Add(batch.HashSetAsync(model.GetKey(), hashEntries.ToArray()));
                if (nullKeys.Count > 0)
                    tasks.Add(batch.HashDeleteAsync(model.GetKey(), nullKeys.ToArray()));

                if (tasks.Any())
                    operationTasks.Add(Task.WhenAll(tasks));
            }

            batch.Execute();
            await Task.WhenAll(operationTasks);
            return PushResult.Success();
        }

        private static async Task<Dictionary<string, HashSet<string>>> CheckUniqueConstraints<TModel>(
            Dictionary<string, List<Tuple<TModel, RedisValue>>> uniqueChecks,
            string indexName)
            where TModel : IAsyncModel
        {
            var violations = new Dictionary<string, HashSet<string>>();

            foreach (var kvp in uniqueChecks)
            {
                var propertyName = kvp.Key;
                var values = kvp.Value;

                // Check for in-batch duplicates
                var batchDuplicates = values.GroupBy(x => x.Item2)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key.ToString());

                if (batchDuplicates.Any())
                {
                    violations[propertyName] = new HashSet<string>(batchDuplicates);
                    continue;
                }

                // Check against existing in Redis
                var redisQuery = new RedisQuery<TModel>(indexName);
                foreach (var valueTuple in values)
                {
                    var model = valueTuple.Item1;
                    var value = valueTuple.Item2;

                    redisQuery.Conditions.Add(QueryHelper.Tag(propertyName, value));
                    var existing = await redisQuery.FirstOrDefaultAsync();

                    if (existing != null && existing.Id != model.Id)
                    {
                        if (!violations.ContainsKey(propertyName))
                            violations[propertyName] = new HashSet<string>();
                        violations[propertyName].Add(value);
                    }
                    redisQuery.Conditions.Clear();
                }
            }

            return violations;
        }
    }
}

