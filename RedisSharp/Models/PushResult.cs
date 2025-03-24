using System;
using System.Collections.Generic;
using System.Text;

namespace RedisSharp.Models
{
    public struct PushResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public TModel GetModel<TModel>() => (TModel)Data;

        public static PushResult Failed(object data, string reason) 
        {
            return new PushResult
            {
                Data = data,
                Message = reason,
                Succeeded = false
            };
        }

        public static PushResult Success()
        {
            return new PushResult
            {
                Succeeded = true
            };
        }
    }
}
