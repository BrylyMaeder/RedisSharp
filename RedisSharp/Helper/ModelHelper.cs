using RedisSharp.Factory;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System.Text;
using RedisSharp.Util;

namespace RedisSharp.Helper
{
    public static class ModelHelper
    {
        public static string GetIndex<TModel>() 
        {
            return ModelFactory.CreateEmpty(typeof(TModel)).IndexName();
        }

        /// <summary>
        /// Creates a Redis key by combining the model's index name and ID, 
        /// stripping the index prefix from the ID if present.
        /// </summary>
        /// <typeparam name="TModel">The type of model</typeparam>
        /// <param name="id">The ID to create a key for</param>
        /// <returns>A formatted Redis key</returns>
        public static string CreateKey<TModel>(string id)
        {
            var index = GetIndex<TModel>();
            // Remove the index prefix from the ID if it's present
            if (id.StartsWith($"{index}:"))
            {
                id = id.Replace($"{index}:", "");
            }
            return $"{index}:{id}";
        }

        /// <summary>
        /// Creates a Redis key using a type and ID, 
        /// stripping the index prefix from the ID if present.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <param name="id">The ID to create a key for</param>
        /// <returns>A formatted Redis key</returns>
        public static string CreateKey(Type type, string id)
        {
            var instance = ModelFactory.CreateEmpty(type);
            var index = instance.IndexName();
            // Remove the index prefix from the ID if it's present
            if (id.StartsWith($"{index}:"))
            {
                id = id.Replace($"{index}:", "");
            }
            return $"{index}:{id}";
        }
        internal static RedisValue[] GetMemberNames<TModel>(Expression<Func<TModel, object>>[] expressions)
        {
            return expressions.Any()
                ? expressions.Select(exp => MemberSelector.GetMemberName(exp)).Select(name => (RedisValue)name).ToArray()
                : typeof(TModel).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(p => (RedisValue)p.Name).ToArray();
        }

        internal static void InstantiateNestedModels<TModel>(TModel model) where TModel : IAsyncModel
        {
            var props = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var prop in props)
            {
                // Only process properties that have the Descendant attribute and are of type IAsyncModel
                if (prop.GetCustomAttribute<DescendantAttribute>() != null && typeof(IAsyncModel).IsAssignableFrom(prop.PropertyType))
                {
                    string nestedId = $"{model.Id}_{prop.Name}";
                    var nestedModel = ModelFactory.Create(prop.PropertyType, nestedId);
                    prop.SetValue(model, nestedModel);

                    // Recursively instantiate nested models if the nested model is also IAsyncModel
                    if (nestedModel is IAsyncModel asyncNestedModel)
                    {
                        InstantiateNestedModels(asyncNestedModel); // Recursive call
                    }
                }
            }
        }

    }
}
