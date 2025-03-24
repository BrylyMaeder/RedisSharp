using StackExchange.Redis;
using System;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using RedisSharp.Util;

namespace RedisSharp
{
    public partial class RedisRepository
    {
        private static RedisValue[] GetMemberNames<TModel>(Expression<Func<TModel, object>>[] expressions)
        {
            return expressions.Any()
                ? expressions.Select(exp => MemberSelector.GetMemberName(exp)).Select(name => (RedisValue)name).ToArray()
                : typeof(TModel).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(p => (RedisValue)p.Name).ToArray();
        }
    }
}
