using RedisSharp.Index;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedisSharp
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexedAttribute : Attribute
    {
        public IndexType IndexType { get; }
        public bool Sortable { get; }
        public IndexedAttribute(IndexType type = IndexType.Auto, bool sortable = false)
        {
            IndexType = type;
            Sortable = sortable;
        }
    }
}
