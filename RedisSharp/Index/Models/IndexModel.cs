using RedisSharp.Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharp.Index.Models
{
    class IndexModel : IAsyncModel
    {
        public string Id { get; set; }

        public string IndexHash { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool RequiresUpdate(string newHash)
        {
            if (string.Equals(newHash, IndexHash))
                return false;

            return true;
        }

        public string IndexName()
        {
            return "index";
        }
    }
}
