using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedisSharp.Models
{
    public struct BulkModelCreationResult<TModel> where TModel : IAsyncModel
    {
        public string Message { get; set; }
        public bool Succeeded { get; set; }
        public IEnumerable<TModel> Models { get; set; }

        public static BulkModelCreationResult<TModel> Success(IEnumerable<TModel> models, string message = "Bulk creation succeeded")
        {
            return new BulkModelCreationResult<TModel>
            {
                Succeeded = true,
                Message = message,
                Models = models
            };
        }

        public static BulkModelCreationResult<TModel> Failed(string message = "Bulk creation failed")
        {
            return new BulkModelCreationResult<TModel>
            {
                Succeeded = false,
                Message = message,
                Models = Enumerable.Empty<TModel>()
            };
        }
    }

    public struct ModelCreationResult<TModel> where TModel : IAsyncModel
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public TModel Data { get; set; }

        public static ModelCreationResult<TModel> Success(TModel data, string message = "Creation succeeded")
        {
            return new ModelCreationResult<TModel>
            {
                Succeeded = true,
                Message = message,
                Data = data
            };
        }

        public static ModelCreationResult<TModel> Failed(string message = "Creation failed")
        {
            return new ModelCreationResult<TModel>
            {
                Succeeded = false,
                Message = message,
                Data = default
            };
        }
    }
}
