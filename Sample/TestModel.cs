using RedisSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample
{
    public class TestModel : IAsyncModel
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [Hydrate]
        public TestModel2 Test2 {get;set;}

        public string IndexName()
        {
            return "test";
        }
    }

    public class TestModel2 : IAsyncModel
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        public string IndexName()
        {
            return "test";
        }
    }
}
