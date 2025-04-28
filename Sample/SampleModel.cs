using RedisSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample
{
    public class SampleModel : IAsyncModel
    {
        public enum TestEnum 
        {
            A,
            B,
            C
        }
        public string Id { get; set; }

        [Indexed(sortable: false)]
        public DateTime CreatedAt { get; set; }

        [Indexed(RedisSharp.Index.IndexType.Tag)]
        public string Username { get; set; } = "James";

        [Indexed(sortable: false)]
        public int Number { get; set; } = 5;
        [Indexed]
        public bool Boolean { get; set; } = true;

        public string TestId { get; set; } = Guid.NewGuid().ToString();

        [Indexed]
        public TestEnum MyEnum { get; set; } = TestEnum.B;

        [Descendant]
        public TestModel Test { get; set; }
        public string IndexName()
        {
            return "samples";
        }
    }
}
