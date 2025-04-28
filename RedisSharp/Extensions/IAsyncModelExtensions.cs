using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using RedisSharp.Models;
using RedisSharp.Helper;
using System.IO;
using System.Text.Json;

namespace RedisSharp
{
    public static class IAsyncModelExtensions
    {
        /// <summary>
        /// Fully loads all data. Any nested IAsyncModels with the 'Hydrate' Attribute will also be filled.
        /// </summary>
        /// <typeparam name="TModel">The type of the model, constrained to implement IAsyncModel</typeparam>
        /// <param name="model">The model to hydrate</param>
        /// <returns>The hydrated model</returns>
        public static async Task<TModel> HydrateAsync<TModel>(this TModel model, params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (model == null) return default;
            await ModelHydrationHelper.HydrateAsync(model, selectors);
            return model;  // Return the hydrated model
        }

        /// <summary>
        /// Fully loads all data. Any nested IAsyncModels with the 'Hydrate' Attribute will also be filled.
        /// </summary>
        /// <typeparam name="TModel">The type of the model, constrained to implement IAsyncModel</typeparam>
        /// <param name="models">The collection of models to hydrate</param>
        /// <returns>The hydrated collection of models</returns>
        public static async Task<IEnumerable<TModel>> HydrateAsync<TModel>(this IEnumerable<TModel> models, params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (models == null || !models.Any()) return models;
            await ModelHydrationHelper.HydrateAsync(models, selectors);
            return models;  // Return the hydrated collection of models
        }

        /// <summary>
        /// Fully loads all data from an async task that returns a single IAsyncModel.
        /// Any nested IAsyncModels with the 'Hydrate' Attribute will also be filled.
        /// </summary>
        /// <typeparam name="TModel">The type of the model, constrained to implement IAsyncModel</typeparam>
        /// <param name="modelTask">The task returning the model to hydrate</param>
        /// <returns>The hydrated model</returns>
        public static async Task<TModel> HydrateAsync<TModel>(this Task<TModel> modelTask)
            where TModel : IAsyncModel
        {
            if (modelTask == null) return default;
            var model = await modelTask;
            if (model == null) return default;
            await ModelHydrationHelper.HydrateAsync(model);
            return model;  // Return the hydrated model
        }

        /// <summary>
        /// Fully loads all data from an async task that returns an IEnumerable of IAsyncModels.
        /// Any nested IAsyncModels with the 'Hydrate' Attribute will also be filled.
        /// </summary>
        /// <typeparam name="TModel">The type of the model, constrained to implement IAsyncModel</typeparam>
        /// <param name="modelsTask">The task returning the collection of models to hydrate</param>
        /// <returns>The hydrated collection of models</returns>
        public static async Task<IEnumerable<TModel>> HydrateAsync<TModel>(this Task<IEnumerable<TModel>> modelsTask)
            where TModel : IAsyncModel
        {
            if (modelsTask == null) return null;
            var models = await modelsTask;
            if (models == null || !models.Any()) return models;
            await ModelHydrationHelper.HydrateAsync(models);
            return models;  // Return the hydrated collection of models
        }

        public static async Task<List<TModel>> HydrateAsync<TModel>(this Task<List<TModel>> modelsTask)
    where TModel : IAsyncModel
        {
            if (modelsTask == null) return null;

            var models = await modelsTask;
            if (models == null || models.Count == 0) return models;

            // Reuse existing hydration logic for IEnumerable
            await ModelHydrationHelper.HydrateAsync(models);
            return models;
        }




        public static string GetKey(this IAsyncModel document)
        {
            return $"{document.IndexName()}:{document.Id}";
        }

        public static async Task DeleteAsync(this IAsyncModel model)
        {
            if (model == null) return;

            // Recursively collect all deletions
            var deletionTasks = ModelDeletionHelper.CollectDeletionTasks(model);

            // Execute all deletions concurrently
            await Task.WhenAll(deletionTasks);

            // Handle related keys cleanup
            await ModelDeletionHelper.CleanupRedisKeysAsync(model);
        }

        public static async Task<ModelCreationResult<TModel>> CloneAsync<TModel>(this TModel model, string newId = "") where TModel : IAsyncModel
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model), "Model cannot be null.");

            // Hydrate the model if needed (ensure any async loading is complete)
            await model.HydrateAsync();

            // Deep clone the model using JSON serialization
            TModel newModel;
            try
            {
                using (var ms = new MemoryStream())
                {
                    // Serialize the model to memory stream
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    };
                    await JsonSerializer.SerializeAsync(ms, model, options);
                    ms.Seek(0, SeekOrigin.Begin);

                    // Deserialize the cloned object from the memory stream
                    newModel = await JsonSerializer.DeserializeAsync<TModel>(ms);
                }

                // Update the new model's Id
                newModel.Id = newId;

                // Store the new model asynchronously (e.g., Redis)
                return await RedisRepository.CreateAsync(newModel);
            }
            catch (Exception ex)
            {
                // Log or handle the error as needed
                throw new InvalidOperationException("Failed to clone the model.", ex);
            }
        }

        public static async Task<PushResult> PushAsync<TModel>(
            this TModel entity, params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (!RedisSingleton.BypassUnsafePractices && selectors == null || !RedisSingleton.BypassUnsafePractices && selectors.Length == 0)
                throw new ArgumentException("At least one selector must be used on PushAsync.");

            return await ModelPushHelper.PushAsync(entity, selectors);
        }

        public static async Task<PushResult> PushAsync<TModel>(
            this IEnumerable<TModel> entities, params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (!RedisSingleton.BypassUnsafePractices && selectors == null || !RedisSingleton.BypassUnsafePractices && selectors.Length == 0)
                throw new ArgumentException("At least one selector must be used on PushAsync.");

            return await ModelPushHelper.PushAsync(entities, selectors);
        }

        public static async Task PullAsync<TModel>(this TModel entity, params Expression<Func<TModel, object>>[] selectors) where TModel : IAsyncModel
        {
            await ModelHydrationHelper.HydrateAsync(entity, selectors);
        }


        public static async Task<long> IncrementAsync<T>(this T entity, Expression<Func<T, long>> expression) where T : IAsyncModel
        {
            var db = RedisSingleton.Database;

            if (!(expression.Body is MemberExpression memberExpr))
                throw new ArgumentException("Invalid expression.");

            var property = memberExpr.Member as PropertyInfo;
            if (property == null)
                throw new ArgumentException("Expression must be a property.");

            // Get the property name from the expression
            string propertyName = property.Name;

            // Create a specific key for this property using the entity key and property name
            var entityKey = entity.GetKey();
            var propertyKey = $"{entityKey}:{propertyName}";

            // Increment the specific property value in Redis
            long newValue = await db.StringIncrementAsync(propertyKey);

            // Update entity property
            property.SetValue(entity, newValue);

            return newValue;
        }


    }
}
