using RedisSharp.Factory;
using RedisSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace RedisSharp
{
    public partial class RedisRepository
    {
        public static async Task<ModelCreationResult<TModel>> CreateAsync<TModel>(TModel newModel) where TModel : IAsyncModel
        {
            if (string.IsNullOrEmpty(newModel.Id))
                newModel.Id = Guid.NewGuid().ToString();

            var result = await CreateManyAsync(new List<TModel> { newModel });

            return new ModelCreationResult<TModel>
            {
                Data = result.Succeeded ? newModel : default,
                Message = result.Message,
                Succeeded = result.Succeeded
            };
        }

        public static async Task<TModel> LoadOrCreateAsync<TModel>(string modelId = "") where TModel : IAsyncModel
        {
            if (string.IsNullOrEmpty(modelId))
            {
                modelId = Guid.NewGuid().ToString();
            }

            TModel model = await LoadAsync<TModel>(modelId);

            if (model == null)
            {
                var createResult = await CreateAsync<TModel>(modelId);
                return createResult.Data;
            }

            return model;
        }

        public static async Task<ModelCreationResult<TModel>> CreateAsync<TModel>(string id = "") where TModel : IAsyncModel
        {
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }

            var newModel = ModelFactory.Create<TModel>(id);

            return await CreateAsync(newModel);
        }

        internal static async Task<IAsyncModel> LoadAsync(Type modelType, string id)
        {
            if (!typeof(IAsyncModel).IsAssignableFrom(modelType))
            {
                throw new ArgumentException($"Type {modelType.Name} must implement IAsyncModel");
            }

            // Get the static LoadAsync method from RedisRepository that matches the actual signature
            var methodInfo = typeof(RedisRepository).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m =>
                    m.Name == nameof(LoadAsync) &&
                    m.IsGenericMethod &&
                    m.GetParameters().Length == 2 &&  // Changed from 1 to 2 parameters
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters()[1].ParameterType.IsArray);  // Looking for the params array

            if (methodInfo == null)
            {
                throw new InvalidOperationException($"No matching LoadAsync method found in RedisRepository for type {modelType.Name}.");
            }

            // Create the generic version with the specified modelType
            var genericMethod = methodInfo.MakeGenericMethod(modelType);

            // Invoke with an empty expressions array since we don't need expressions
            var emptyExpressions = Array.CreateInstance(
                typeof(Expression<>).MakeGenericType(
                    typeof(Func<,>).MakeGenericType(modelType, typeof(object))),
                0);

            var task = (Task)genericMethod.Invoke(null, new object[] { id, emptyExpressions });
            await task.ConfigureAwait(false);

            return (IAsyncModel)task.GetType().GetProperty("Result").GetValue(task);
        }

        public static async Task<TModel> LoadAsync<TModel>(string id, params Expression<Func<TModel, object>>[] selectors) where TModel : IAsyncModel
        {
            var ids = new[] { id };
            var result = await LoadManyAsync(ids, selectors);
            return result.FirstOrDefault();
        }
    }
}
