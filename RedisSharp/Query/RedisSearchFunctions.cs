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
            searchQuery,
            "NOCONTENT",
            "LIMIT", skipCount, takeCount
        };

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
                for (int i = 1; i < resultsArray.Length; i++)
                {
                    documentIds.Add(resultsArray[i].ToString());
                }

                return (documentIds, totalCount, totalPages);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error at SearchAsync: {e.Message}\n{e.StackTrace}");
                return (new List<string>(), 0, 0);
            }
        }





        internal static List<string> GetSelectedFields<TModel>(Expression<Func<TModel, object>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body is NewExpression newExpr)
            {
                return newExpr.Members?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            else if (expression.Body is MemberExpression memberExpr)
            {
                return new List<string> { memberExpr.Member.Name };
            }
            else if (expression.Body is UnaryExpression unaryExpr)
            {
                return GetSelectedFields<TModel>(Expression.Lambda<Func<TModel, object>>(unaryExpr.Operand, expression.Parameters));
            }

            throw new ArgumentException("Unsupported select expression.", nameof(expression));
        }

        internal static async Task<(List<TModel> models, int TotalCount, int TotalPages)> SelectAsync<TModel>(
    RedisQuery<TModel> query,
    List<string> selectedFields,
    int pageNumber = 0,
    int pageSize = 1000
) where TModel : IAsyncModel
        {
            try
            {
                int offset = (pageNumber) * pageSize;
                var builtQuery = query.Build();
                string searchQuery = string.IsNullOrWhiteSpace(builtQuery) ? "*" : builtQuery;

                var redisQuery = new List<object>
        {
            query.IndexName,
            searchQuery,
            "LIMIT", offset, pageSize
        };

                if (selectedFields.Count > 0)
                {
                    redisQuery.Add("RETURN");
                    redisQuery.Add(selectedFields.Count);
                    redisQuery.AddRange(selectedFields);
                }

                redisQuery.Add("DIALECT");
                redisQuery.Add(2);


                var result = await RedisSingleton.Database.ExecuteAsync("FT.SEARCH", redisQuery.ToArray());
                var resultsArray = (RedisResult[])result;

                if (resultsArray.Length == 0)
                {
                    return (new List<TModel>(), 0, 0); // Return empty list with counts
                }

                var parsedResults = new List<TModel>();

                // Extract total count from the first element in the result
                int totalCount = Convert.ToInt32(resultsArray[0]);
                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Process the document results starting from index 1 (skip total count)
                for (int i = 1; i < resultsArray.Length; i += 2) // Increment by 2 to skip over document IDs
                {
                    var item = (RedisResult[])resultsArray[i + 1]; // itemProperties are in the next index (i + 1)
                    var model = ModelFactory.Create<TModel>($"{resultsArray[i]}"); 

                    // Map selected fields to model properties
                    for (int j = 0; j < selectedFields.Count; j++)
                    {
                        var propertyName = selectedFields[j];
                        var value = item[j]; // item[j] contains the property value

                        var propertyInfo = typeof(TModel).GetProperty(propertyName);
                        if (propertyInfo != null && value != null && propertyInfo.CanWrite)
                        {
                            var convertedValue = Convert.ChangeType(value, propertyInfo.PropertyType);
                            propertyInfo.SetValue(model, convertedValue);
                        }
                    }

                    parsedResults.Add(model);
                }

                return (parsedResults, totalCount, totalPages);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error at ExecuteSelect: {e.Message}\n{e.StackTrace}");
                return (new List<TModel>(), 0, 0); // Return empty list with counts on error
            }
        }




    }
}
