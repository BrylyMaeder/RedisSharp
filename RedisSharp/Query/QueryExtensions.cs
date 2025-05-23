﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using RedisSharp.Factory;
using RedisSharp.Query;

namespace RedisSharp
{
    public static class QueryExtensions
    {
        public static async Task<(List<TModel> Documents, int TotalCount, int TotalPages)> ToPagedListAsync<TModel>(this RedisQuery<TModel> query, int page = 0, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, page, pageSize);

            var documents = (List<TModel>)ModelFactory.CreateMany<TModel>(results.DocumentIds);

            return (documents, results.TotalCount, results.TotalPages);
        }

        public static async Task<List<TModel>> ToListAsync<TModel>(this RedisQuery<TModel> builder, int page = 0, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(builder, page, pageSize);

            // Load the models asynchronously and convert the result to a list
            var documentIds = results.DocumentIds;

            if (documentIds == null || !documentIds.Any())
            {
                return new List<TModel>(); // Return an empty list if no document IDs
            }

            var loadedItems = ModelFactory.CreateMany<TModel>(documentIds);

            // Convert to a List<TModel> if it's not already a List<TModel>
            return loadedItems.ToList();
        }

        public static async Task<TModel> FirstOrDefaultAsync<TModel>(this RedisQuery<TModel> query) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, 0, 1);

            if (results.DocumentIds.Count == 0)
                return default;

            return ModelFactory.Create<TModel>(results.DocumentIds.FirstOrDefault());
        }

        public static async Task<bool> AnyAsync(this RedisQuery query)
        {
            var results = await RedisSearchFunctions.Execute(query, 0, 1);

            return results.DocumentIds.Count > 0;
        }

        internal static async Task<List<string>> SearchAsync<TModel>(this RedisQuery<TModel> query, int page = 0, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, page, pageSize);
            return results.DocumentIds;
        }
    }
}
