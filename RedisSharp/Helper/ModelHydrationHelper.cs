using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RedisSharp.Extensions;
using StackExchange.Redis;
using RedisSharp.Contracts;

namespace RedisSharp.Helper
{
    internal static class ModelHydrationHelper
    {
        internal static async Task<TModel> HydrateAsync<TModel>(TModel model,
            params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (model == null) return default;
            await HydrateModelsAsync(new List<TModel> { model }, selectors);
            return model;
        }


        internal static async Task<IEnumerable<TModel>> HydrateAsync<TModel>(IEnumerable<TModel> models,
            params Expression<Func<TModel, object>>[] selectors)
            where TModel : IAsyncModel
        {
            if (models == null || !models.Any()) return models;

            await HydrateModelsAsync(models.ToList(), selectors);

            return models;  // Return the hydrated models
        }

        private static async Task HydrateModelsAsync<TModel>(List<TModel> models, Expression<Func<TModel, object>>[] propertySelectors)
    where TModel : IAsyncModel
        {
            if (models == null || !models.Any())
                return;

            var batch = RedisSingleton.Database.CreateBatch();
            var operationTasks = new List<Task<List<ModelData>>>();

            // Queue all recursive operations
            foreach (var model in models)
            {
                operationTasks.Add(RecursiveOperations(batch, model, null, propertySelectors));
            }

            batch.Execute();
            // Wait for all recursive operations to complete their setup
            var modelDataResults = await Task.WhenAll(operationTasks);

            // Populate the models with retrieved data
            foreach (var modelDataList in modelDataResults)
            {
                foreach (var modelData in modelDataList)
                {
                    // Get the Redis values from the completed task
                    RedisValue[] values = await modelData.ValuesTask;

                    // Map values back to properties
                    for (int i = 0; i < modelData.Properties.Count; i++)
                    {
                        var property = modelData.Properties[i].Property;
                        var value = values[i];

                        if (!value.IsNull)
                        {
                            // Convert Redis value to property type and set it
                            object convertedValue = RedisConverters.DeserializeFromRedis(value, property.PropertyType);
                            // Use the stored instance instead of ReflectedType
                            property.SetValue(modelData.Instance, convertedValue);
                        }
                    }
                }
            }
        }

        // Updated ModelData struct to include the instance
        private struct ModelData
        {
            public List<PropertyObject> Properties { get; set; }
            public Task<RedisValue[]> ValuesTask { get; set; }
            public object Instance { get; set; } // Add the instance here
        }

        private struct PropertyObject
        {
            public RedisValue HashKey;
            public PropertyInfo Property;
        }

        private static async Task<List<ModelData>> RecursiveOperations<TModel>(IBatch batch, TModel model, List<ModelData> currentData, Expression<Func<TModel, object>>[] expressions)
            where TModel : IAsyncModel
        {
            if (currentData == null)
                currentData = new List<ModelData>();

            var propertyObjects = new List<PropertyObject>();
            var properties = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var memberNames = expressions == null ? null : ModelHelper.GetMemberNames(expressions);

            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;

                if (typeof(IModelComponent).IsAssignableFrom(propertyType) || (expressions != null && !memberNames.Contains(property.Name)))
                    continue;

                if (property.Name == "Id")
                    continue;

                var propertyObject = new PropertyObject
                {
                    HashKey = (RedisValue)property.Name,
                    Property = property
                };

                if (typeof(IAsyncModel).IsAssignableFrom(propertyType))
                {
                    if (propertyType.GetCustomAttributes(typeof(HydrateAttribute), false).Any())
                    {
                        var propertyValue = (IAsyncModel)property.GetValue(model);
                        if (propertyValue != null) // Ensure the nested model exists
                        {
                            currentData = await RecursiveOperations(batch, propertyValue, currentData, null);
                        }
                    }
                    continue;
                }

                propertyObjects.Add(propertyObject);
            }

            try
            {
                var members = propertyObjects.Select(s => s.HashKey).ToArray();
                var task = batch.HashGetAsync(model.GetKey(), members);

                currentData.Add(new ModelData
                {
                    Properties = propertyObjects,
                    ValuesTask = task,
                    Instance = model // Store the model instance here
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return currentData;
        }

    }

}
