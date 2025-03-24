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
        public bool UniqueValidation { get; }

        public IndexedAttribute(IndexType type = IndexType.Auto)
        {
            IndexType = type;
        }
    }
}
