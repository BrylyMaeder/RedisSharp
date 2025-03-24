using RedisSharp.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Helper
{
    internal class ModelDeletionHelper
    {
        internal static List<Task> CollectDeletionTasks(IAsyncModel model)
        {
            List<Task> deletionTasks = new List<Task>();

            if (model == null) new List<Task>();

            var properties = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var property in properties)
            {
                var value = property.GetValue(model);

                if (value is IAsyncModel nestedModel)
                {
                    deletionTasks.Add(nestedModel.DeleteAsync());
                }
                else if (value is IDeletable deletable)
                {
                    deletionTasks.Add(deletable.DeleteAsync());
                }
            }

            // Add listener task if applicable
            if (model is IDeletionListener listener)
            {
                deletionTasks.Add(listener.OnDeleted());
            }

            return deletionTasks;
        }

        internal static async Task CleanupRedisKeysAsync(IAsyncModel model)
        {
            const int batchSize = 100;
            var cursor = 0L;

            try
            {
                do
                {
                    var scanResult = await RedisSingleton.Database.ExecuteAsync("SCAN",
                        cursor.ToString(), "MATCH", $"{model.GetKey()}*", "COUNT", batchSize);

                    var resultArray = (RedisResult[])scanResult;
                    cursor = long.Parse(resultArray[0].ToString());
                    var keys = ((RedisResult[])resultArray[1]).Select(r => (RedisKey)r).ToArray();

                    if (keys.Any())
                    {
                        await RedisSingleton.Database.KeyDeleteAsync(keys);
                    }
                } while (cursor != 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deletion: {ex.Message}");
            }
        }
    }
}
