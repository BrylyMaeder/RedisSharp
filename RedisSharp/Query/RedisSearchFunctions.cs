using RedisSharp.Contracts;
using RedisSharp.Factory;
using RedisSharp.Index.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Query
{
    internal class RedisSearchFunctions
    {
        internal static async Task<(List<string> DocumentIds, int TotalCount, int TotalPages)> Execute(RedisQuery query, int pageNumber = 0, int pageSize = 1000)
        {
            try
            {
                int skipCount = Math.Max(0, pageNumber * pageSize);
                int takeCount = Math.Max(1, pageSize);

                var builtQuery = query.Build();
                string searchQuery = string.IsNullOrWhiteSpace(builtQuery) ? "*" : builtQuery;

                var commandArgs = new List<object>
        {
            query.IndexName,
            searchQuery
        };

                // Only add "NOCONTENT" if Hydration is false
                if (!query.Hydrate)
                {
                    commandArgs.Add("NOCONTENT");
                }

                commandArgs.Add("LIMIT");
                commandArgs.Add(skipCount);
                commandArgs.Add(takeCount);

                // Only add SORTBY if SortFields exist
                if (query.SortFields != null && query.SortFields.Count > 0)
                {
                    commandArgs.Add("SORTBY");
                    foreach (var sortField in query.SortFields)
                    {
                        commandArgs.Add(sortField.Key); // Field name
                        commandArgs.Add(sortField.Value ? "DESC" : "ASC"); // true = DESC, false = ASC
                    }
                }

                // DIALECT always goes at the end
                commandArgs.Add("DIALECT");
                commandArgs.Add(2);

                if (RedisSingleton.OutputLogs)
                {
                    Console.WriteLine("FT.SEARCH " + string.Join(" ", commandArgs));
                }

                var result = await RedisSingleton.Database.ExecuteAsync("FT.SEARCH", commandArgs.ToArray());

                var resultsArray = (RedisResult[])result;

                if (resultsArray == null || resultsArray.Length == 0)
                {
                    return (new List<string>(), 0, 0);
                }

                int totalCount = Convert.ToInt32(resultsArray[0]);
                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var documentIds = new List<string>();

                // If Hydration is true, we assume the full document (with fields) is returned
                // This loop must handle RedisResult arrays of key-value pairs (e.g., [id, {field1, value1, ...}])
                if (query.Hydrate)
                {
                    for (int i = 1; i < resultsArray.Length; i += 2)
                    {
                        documentIds.Add(resultsArray[i - 1].ToString()); 
                    }
                }
                else
                {
                    for (int i = 1; i < resultsArray.Length; i++)
                    {
                        documentIds.Add(resultsArray[i].ToString());
                    }
                }

                return (documentIds, totalCount, totalPages);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error at SearchAsync: {e.Message}\n{e.StackTrace}");
                return (new List<string>(), 0, 0);
            }
        }

    }
}
