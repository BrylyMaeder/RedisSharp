using RedisSharp.Index;

namespace RedisSharp
{
    public class UniqueAttribute : IndexedAttribute
    {
        public UniqueAttribute(IndexType indexType = IndexType.Tag) : base(indexType) { }
    }
}
