using RedisSharp.Contracts;
using RedisSharp.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedisSharp.Factory
{
    public static class ModelFactory
    {
        public static TModel Create<TModel>(string id) where TModel : IAsyncModel
        {
            return (TModel)Create(typeof(TModel), id);
        }

        internal static IAsyncModel Create(Type asyncDocumentType, string id) 
        {
            var instance = CreateEmpty(asyncDocumentType);

            var index = instance.IndexName();
            id = id.Replace($"{index}:", "");

            instance.Id = id;

            ModelHelper.InstantiateNestedModels(instance);

            return instance;
        }
        internal static IAsyncModel CreateEmpty(Type asyncDocumentType)
        {
            if (asyncDocumentType == null)
                throw new ArgumentNullException(nameof(asyncDocumentType));

            // Ensure the type implements IAsyncModel
            if (!typeof(IAsyncModel).IsAssignableFrom(asyncDocumentType))
                throw new InvalidOperationException($"{asyncDocumentType.FullName} does not implement IAsyncModel");

            // Ensure the type has a parameterless constructor
            if (asyncDocumentType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"{asyncDocumentType.FullName} must have a public parameterless constructor");

            // Enforce only using the default constructor
            return (IAsyncModel)Activator.CreateInstance(asyncDocumentType);
        }
    }
}