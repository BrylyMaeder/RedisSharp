using RedisSharp.Index;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RedisSharp.Query
{
    public class RedisQuery 
    {
        public readonly List<string> Conditions;
        
        public readonly string IndexName;
        public Dictionary<string, bool> SortFields { get; set; }

        public string Build()
        {
            if (Conditions.Count == 0)
                return "*";

            return Conditions.Count == 1
                ? Conditions[0]
                : $"{string.Join(" ", Conditions)}";
        }

        public RedisQuery(string indexName)
        {
            IndexName = indexName;
            Conditions = new List<string>();
            SortFields = new Dictionary<string, bool>();
        }
    }
    public class RedisQuery<TModel> : RedisQuery where TModel : IAsyncModel
    {
        public RedisQuery(string indexName) : base(indexName)
        {

        }

        public RedisQuery<TModel> SortBy(params SortField<TModel>[] sortFields)
        {
            foreach (var sortField in sortFields)
            {
                var fieldName = GetFieldNameFromSelector(sortField.Selector);
                SortFields[fieldName] = sortField.Descending;
            }
            return this;
        }

        private string GetFieldNameFromSelector(Expression<Func<TModel, object>> selector)
        {
            if (selector.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            if (selector.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            throw new NotSupportedException("Expression must resolve to a member");
        }

        public RedisQuery<TModel> Where(Expression<Func<TModel, bool>> predicate)
        {
            var condition = ParseExpression(predicate.Body, predicate.Parameters[0]);
            if (!string.IsNullOrEmpty(condition))
                Conditions.Add(condition);
            return this;
        }

        private string ParseExpression(Expression expression, ParameterExpression parameter)
        {
            switch (expression)
            {
                case BinaryExpression binary:
                    return ParseBinaryExpression(binary, parameter);

                case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                    return HandleUnaryNegation(unary, parameter);

                case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                    var convertedValue = EvaluateExpression(unary.Operand, parameter);
                    if (convertedValue != null && convertedValue.GetType().IsEnum)
                    {
                        return Convert.ToInt32(convertedValue).ToString();
                    }
                    return convertedValue?.ToString() ?? "null";

                case MethodCallExpression methodCall:
                    return ParseMethodCallExpression(methodCall, parameter);

                case ConstantExpression constant:
                    if (constant.Value != null && constant.Value.GetType().IsEnum)
                    {
                        return Convert.ToInt32(constant.Value).ToString();
                    }
                    return constant.Value?.ToString() ?? "null";

                case MemberExpression member:
                    return HandleMemberExpression(member, parameter);

                default:
                    throw new NotSupportedException($"Expression type {expression.NodeType} is not supported");
            }
        }

        private string HandleUnaryNegation(UnaryExpression unary, ParameterExpression parameter)
        {
            // Parse the operand (e.g., IsLegendary in !IsLegendary)
            var operand = ParseExpression(unary.Operand, parameter);

            // If the operand is a simple member (like IsLegendary), we assume it's a boolean check
            if (operand.Contains("@"))
            {
                // Invert the condition by adding a negative sign for Redis query (check if false)
                return $"-({operand})";
            }

            return operand;
        }

        private string HandleMemberExpression(MemberExpression member, ParameterExpression parameter)
        {
            // Ensure the member is part of the parameter (e.g., s.MyEnum)
            if (member.Expression == parameter)
            {
                var fieldName = member.Member.Name;
                var propertyInfo = member.Member as PropertyInfo;

                if (propertyInfo != null)
                {
                    // Check if the property is an enum
                    if (propertyInfo.PropertyType.IsEnum)
                    {
                        // Treat as a numeric field in Redis query (e.g., @MyEnum)
                        return $"@{fieldName}";
                    }
                    // Handle boolean properties
                    if (propertyInfo.PropertyType == typeof(bool))
                    {
                        return $"@{fieldName}:{{True}}";
                    }
                }
                // Fallback for other types
                return $"@{fieldName}";
            }

            // Evaluate constant or static members
            var value = EvaluateExpression(member, parameter);
            if (value != null && value.GetType().IsEnum)
            {
                return Convert.ToInt32(value).ToString();
            }
            return value?.ToString() ?? "null";
        }


        private string ParseBinaryExpression(BinaryExpression binary, ParameterExpression parameter)
        {
            string left;
            IndexType indexType = IndexType.Auto;

            // Handle Convert expressions on the left side
            Expression leftExpression = binary.Left;
            if (leftExpression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                leftExpression = unary.Operand;
            }

            if (leftExpression is MemberExpression member)
            {
                left = GetFieldName(member);
                var propertyInfo = member.Member as PropertyInfo;
                if (propertyInfo != null)
                {
                    var indexedAttr = propertyInfo.GetCustomAttribute<IndexedAttribute>();
                    indexType = indexedAttr?.IndexType ?? IndexType.Auto;
                }
            }
            else
            {
                left = ParseExpression(binary.Left, parameter);
            }

            // Handle logical AND/OR
            if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
            {
                string leftExpr = ParseExpression(binary.Left, parameter);
                string rightExpr = ParseExpression(binary.Right, parameter);
                return binary.NodeType == ExpressionType.AndAlso
                    ? "(" + leftExpr + " " + rightExpr + ")"
                    : "(" + leftExpr + " | " + rightExpr + ")";
            }

            object rightValue = EvaluateExpression(binary.Right, parameter);

            // Handle enums and Convert expressions explicitly
            if (binary.Right is UnaryExpression convertExpr && convertExpr.NodeType == ExpressionType.Convert)
            {
                rightValue = EvaluateExpression(convertExpr.Operand, parameter);
                if (rightValue != null && rightValue.GetType().IsEnum)
                {
                    rightValue = Convert.ToInt32(rightValue);
                }
            }
            else if (rightValue != null && rightValue.GetType().IsEnum)
            {
                rightValue = Convert.ToInt32(rightValue);
            }

            if (rightValue is bool booleanValue)
            {
                string booleanStr = booleanValue ? "True" : "False";
                switch (binary.NodeType)
                {
                    case ExpressionType.Equal:
                        return "@" + left + ":{" + booleanStr + "}";
                    case ExpressionType.NotEqual:
                        return "-@" + left + ":{" + booleanStr + "}";
                }
            }
            else if (rightValue is DateTime dateTime)
            {
                rightValue = ToUnixSeconds(dateTime);
            }
            else if (rightValue is TimeSpan timeSpan)
            {
                rightValue = (long)timeSpan.TotalSeconds;
            }

            if (binary.Right is ParameterExpression paramExprRight)
            {
                return "@" + left + ":{" + paramExprRight.Name + "}";
            }

            bool isNumeric = rightValue is int || rightValue is double || rightValue is float || rightValue is long || rightValue is decimal;

            if (isNumeric || indexType == IndexType.Numeric)
            {
                double numericValue = Convert.ToDouble(rightValue);
                switch (binary.NodeType)
                {
                    case ExpressionType.Equal:
                        return "@" + left + ":[" + numericValue + " " + numericValue + "]";
                    case ExpressionType.NotEqual:
                        return "-@" + left + ":[" + numericValue + " " + numericValue + "]";
                    case ExpressionType.GreaterThan:
                        return "@" + left + ":[" + (numericValue + 0.001) + " +inf]";
                    case ExpressionType.GreaterThanOrEqual:
                        return "@" + left + ":[" + numericValue + " +inf]";
                    case ExpressionType.LessThan:
                        return "@" + left + ":[-inf " + (numericValue - 0.001) + "]";
                    case ExpressionType.LessThanOrEqual:
                        return "@" + left + ":[-inf " + numericValue + "]";
                    default:
                        throw new NotSupportedException("Operator " + binary.NodeType + " is not supported for numerics");
                }
            }

            if (rightValue is string textValue)
            {
                string escapedValue = EscapeValue(textValue);
                if (indexType == IndexType.Text || (indexType == IndexType.Auto && !isNumeric))
                {
                    switch (binary.NodeType)
                    {
                        case ExpressionType.Equal:
                            return "@" + left + ":" + escapedValue;
                        case ExpressionType.NotEqual:
                            return "-@" + left + ":" + escapedValue;
                        default:
                            throw new NotSupportedException("Operator " + binary.NodeType + " is not supported for Text index");
                    }
                }
                else if (indexType == IndexType.Tag)
                {
                    switch (binary.NodeType)
                    {
                        case ExpressionType.Equal:
                            return "@" + left + ":{\"" + escapedValue + "\"}";
                        case ExpressionType.NotEqual:
                            return "-@" + left + ":{\"" + escapedValue + "\"}";
                        default:
                            throw new NotSupportedException("Operator " + binary.NodeType + " is not supported for Tag index");
                    }
                }
            }

            // Log for debugging
            throw new NotSupportedException($"Cannot process value '{rightValue}' (type: {(rightValue?.GetType()?.Name ?? "null")}) with index type {indexType}");
        }

        private string GetFieldName(Expression expression)
        {
            // Handle Convert expressions
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                expression = unary.Operand;
            }

            if (expression is MemberExpression member)
            {
                return member.Member.Name;
            }

            throw new NotSupportedException("Expression must resolve to a property");
        }




        private string ParseMethodCallExpression(MethodCallExpression methodCall, ParameterExpression parameter)
        {
            if (methodCall.Object != null && methodCall.Method.DeclaringType == typeof(string))
            {
                if (methodCall.Object is MemberExpression member)
                {
                    var fieldName = member.Member.Name;
                    var propertyInfo = member.Member as System.Reflection.PropertyInfo;
                    var indexedAttr = propertyInfo?.GetCustomAttribute<IndexedAttribute>();
                    var indexType = indexedAttr?.IndexType ?? IndexType.Auto;

                    var value = EvaluateExpression(methodCall.Arguments[0], parameter);

                    // 🚨 Null check added 🚨
                    if (value == null)
                        throw new ArgumentNullException(nameof(value), "Input value cannot be null");

                    if (value is DateTime dt)
                        value = ToUnixSeconds(dt);
                    else if (value is TimeSpan ts)
                        value = (long)ts.TotalSeconds;

                    string stringValue = value.ToString();
                    string escapedValue = EscapeValue(stringValue);

                    if (indexType == IndexType.Text || (indexType == IndexType.Auto && value is string))
                    {
                        // Text search with wildcard support
                        switch (methodCall.Method.Name)
                        {
                            case "Contains":
                                return $"@{fieldName}:*{escapedValue}*";
                            case "StartsWith":
                                return $"@{fieldName}:{escapedValue}*";
                            case "EndsWith":
                                return $"@{fieldName}:*{escapedValue}";
                            default:
                                throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for Text index");
                        }
                    }
                    else if (indexType == IndexType.Tag)
                    {
                        // Tag search (no wildcards)
                        switch (methodCall.Method.Name)
                        {
                            case "Contains":
                                return $"@{fieldName}:{{*{escapedValue}*}}";
                            case "StartsWith":
                                return $"@{fieldName}:{{{escapedValue}*}}";
                            case "EndsWith":
                                return $"@{fieldName}:{{*{escapedValue}}}";
                            default:
                                throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for Tag index");
                        }
                    }
                }
            }
            throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
        }

        private object EvaluateExpression(Expression expression, ParameterExpression parameter)
        {
            try
            {
                if (expression is ConstantExpression constant)
                {
                    if (constant.Value != null && constant.Value.GetType().IsEnum)
                    {
                        return Convert.ToInt32(constant.Value);
                    }
                    return constant.Value;
                }

                if (expression is MemberExpression member)
                {
                    // Avoid evaluating parameter properties dynamically
                    if (member.Expression == parameter)
                    {
                        // Return null or defer evaluation; handled in ParseExpression
                        return null;
                    }

                    if (member.Expression == null) // Static members
                    {
                        if (member.Member.DeclaringType == typeof(DateTime) && member.Member.Name == "Now")
                            return DateTime.Now;

                        var staticField = member.Member as FieldInfo;
                        if (staticField != null && staticField.IsStatic)
                        {
                            var value = staticField.GetValue(null);
                            if (value != null && value.GetType().IsEnum)
                            {
                                return Convert.ToInt32(value);
                            }
                            return value;
                        }

                        var staticProp = member.Member as PropertyInfo;
                        if (staticProp != null && staticProp.GetMethod.IsStatic)
                        {
                            var value = staticProp.GetValue(null);
                            if (value != null && value.GetType().IsEnum)
                            {
                                return Convert.ToInt32(value);
                            }
                            return value;
                        }
                    }

                    var obj = member.Expression != null ? EvaluateExpression(member.Expression, parameter) : null;
                    var field = member.Member as FieldInfo;
                    if (field != null)
                    {
                        var value = field.GetValue(obj);
                        if (value != null && value.GetType().IsEnum)
                        {
                            return Convert.ToInt32(value);
                        }
                        return value;
                    }

                    var prop = member.Member as PropertyInfo;
                    if (prop != null)
                    {
                        var value = prop.GetValue(obj);
                        if (value != null && value.GetType().IsEnum)
                        {
                            return Convert.ToInt32(value);
                        }
                        return value;
                    }

                    throw new NotSupportedException("Unsupported member type");
                }

                if (expression is MethodCallExpression methodCall)
                {
                    object instance = null;
                    if (methodCall.Object != null)
                    {
                        instance = EvaluateExpression(methodCall.Object, parameter);
                    }

                    var arguments = new object[methodCall.Arguments.Count];
                    for (int i = 0; i < methodCall.Arguments.Count; i++)
                    {
                        arguments[i] = EvaluateExpression(methodCall.Arguments[i], parameter);
                    }

                    var result = methodCall.Method.Invoke(instance, arguments);
                    if (result != null && result.GetType().IsEnum)
                    {
                        return Convert.ToInt32(result);
                    }
                    return result;
                }

                var lambda = Expression.Lambda(expression, parameter);
                var compiled = lambda.Compile();

                if (lambda.Parameters.Count == 0)
                {
                    var result = compiled.DynamicInvoke();
                    if (result != null && result.GetType().IsEnum)
                    {
                        return Convert.ToInt32(result);
                    }
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate expression: {expression}", ex);
            }
        }

        private long ToUnixSeconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private string EscapeValue(string value)
        {

            return Regex.Replace(value, @"([\\"" :@\-_.?,!'; &=+#$%^*~`|{}\[\]\(\)<>])", "\\$1"); // Escape special characters

        }
    }
}
