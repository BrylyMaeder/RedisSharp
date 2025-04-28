using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace RedisSharp
{
    public class SortField<TModel>
    {
        public Expression<Func<TModel, object>> Selector { get; private set; }
        public bool Descending { get; private set; }

        public SortField(Expression<Func<TModel, object>> selector, bool descending = false)
        {
            Selector = selector;
            Descending = descending;
        }
    }

}
