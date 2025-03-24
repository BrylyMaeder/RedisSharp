using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace RedisSharp.Util
{
    public static class MemberSelector
    {
        public static string GetMemberName<T>(Expression<Func<T, object>> expression)
        {
            var body = expression.Body;

            switch (body)
            {
                case MemberExpression member:
                    return member.Member.Name;

                case UnaryExpression unary:
                    if (unary.Operand is MemberExpression memberExp)
                        return memberExp.Member.Name;
                    break;
            }

            throw new ArgumentException("Invalid expression");
        }

        public static string GetMemberName<T>(Expression<Func<T, long>> expression)
        {
            var body = expression.Body;

            switch (body)
            {
                case MemberExpression member:
                    return member.Member.Name;

                case UnaryExpression unary:
                    if (unary.Operand is MemberExpression memberExp)
                        return memberExp.Member.Name;
                    break;
            }

            throw new ArgumentException("Invalid expression");
        }
    }
}
