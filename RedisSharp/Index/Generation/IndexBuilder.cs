using RediSearchClient.Indexes;
using RediSearchClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RedisSharp.Factory;
using RedisSharp.Index.Models;

namespace RedisSharp.Index.Generation
{
    public static class IndexBuilder
    {
        private static readonly List<Type> _indexedDocuments = new List<Type>();

        public static void InitializeIndexes()
        {
            try
            {
                foreach (var asyncDocumentType in GetAllAsyncDocumentTypes())
                {
                    if (!_indexedDocuments.Contains(asyncDocumentType))
                    {
                        _indexedDocuments.Add(asyncDocumentType);

                        try
                        {
                            // Use Activator to create an instance of the actual type
                            var instance = ModelFactory.CreateEmpty(asyncDocumentType);
                            EnsureIndexAsync(instance).Wait();
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static async Task EnsureIndexAsync(IAsyncModel document)
        {
            var indexName = document.IndexName();
            var result = IndexDefinitionBuilder.Build(document);

            if (result.IndexDefinition == null || result.IndexHash == null)
            {
                // Do not index non-indexed stuff.
                return;
            }

            var indexModel = await RedisRepository.LoadAsync<IndexModel>(indexName, s => s.LastUpdated, s => s.IndexHash);
            if (indexModel == null || indexModel.RequiresUpdate(result.IndexHash))
                await UpdateIndexAsync(indexName, result.IndexDefinition, result.IndexHash);
        }

        public static bool HasIndexableProperties(Type asyncDocumentType) =>
            asyncDocumentType.GetProperties()
                .Any(prop => Attribute.IsDefined(prop, typeof(IndexedAttribute)));


        public static IEnumerable<Type> GetAllAsyncDocumentTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IAsyncModel).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                .GroupBy(type => type.FullName)
                .Select(group => group.First());

        private static async Task UpdateIndexAsync(string indexName, RediSearchIndexDefinition definition, string hash)
        {
            try { await RedisSingleton.Database.DropIndexAsync(indexName); }
            catch { /* Ignored */ }
            try
            {
                await CreateNewIndexAsync(indexName, definition, hash);
            }
            catch(Exception e)
            {

            }
        }

        private static async Task<IndexModel> CreateNewIndexAsync(string indexName, RediSearchIndexDefinition definition, string hash)
        {
            var indexModel = await RedisRepository.LoadAsync<IndexModel>(indexName);
            if (indexModel == null)
            {
                var result = await RedisRepository.CreateAsync<IndexModel>(indexName);
                if (!result.Succeeded)
                {
                    throw new Exception(result.Message);
                }

                indexModel = result.Data;
            }

            if (definition != null && !string.IsNullOrEmpty(hash))
            {
                await RedisSingleton.Database.CreateIndexAsync(indexName, definition);
                indexModel.IndexHash = hash;
            }

            indexModel.LastUpdated = DateTime.UtcNow;
            await indexModel.PushAsync(s => s.IndexHash, s => s.LastUpdated);

            return indexModel;
        }


    }
}
