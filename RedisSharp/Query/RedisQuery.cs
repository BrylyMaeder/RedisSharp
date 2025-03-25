using RedisSharp.Index;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace RedisSharp.Query
{
    public class RedisQuery 
    {
        public readonly List<string> Conditions;
        public readonly string IndexName;

        public string Build()
        {
            if (Conditions.Count == 0)
                return "*";

            return Conditions.Count == 1
                ? Conditions[0]
                : $"({string.Join(" ", Conditions)})";
        }

        public RedisQuery(string indexName)
        {
            IndexName = indexName;
            Conditions = new List<string>();
        }
    }
    public class RedisQuery<TModel> : RedisQuery where TModel : IAsyncModel
    {
        public RedisQuery(string indexName) : base(indexName)
        {

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
                    // Handle negation (e.g., !IsLegendary)
                    return HandleUnaryNegation(unary, parameter);

                case MethodCallExpression methodCall:
                    return ParseMethodCallExpression(methodCall, parameter);

                case ConstantExpression constant:
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
            // Get the field name
            var fieldName = member.Member.Name;

            // Check if the member represents a boolean property (like IsLegendary)
            var propertyInfo = member.Member as System.Reflection.PropertyInfo;
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(bool))
            {
                // For boolean properties, we assume the field is checked against `true` if no explicit comparison is made
                return $"@{fieldName}:{{True}}";  // Check if the property is true
            }

            // Handle other types of members (non-boolean properties)
            return $"@{fieldName}:{{{member.ToString()}}}";
        }


        private string ParseBinaryExpression(BinaryExpression binary, ParameterExpression parameter)
        {
            string left;
            IndexType indexType = IndexType.Auto; // Default to Auto
            if (binary.Left is MemberExpression member)
            {
                left = GetFieldName(member);
                var propertyInfo = member.Member as System.Reflection.PropertyInfo;
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

            // ✅ Handle logical AND (`&&`) and OR (`||`)
            if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
            {
                string leftExpr = ParseExpression(binary.Left, parameter);
                string rightExpr = ParseExpression(binary.Right, parameter);
                return binary.NodeType == ExpressionType.AndAlso
                    ? "(" + leftExpr + " " + rightExpr + ")"  // Implicit AND
                    : "(" + leftExpr + " | " + rightExpr + ")"; // Explicit OR
            }

            object rightValue = EvaluateExpression(binary.Right, parameter);

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

            // ✅ Distinguish between Auto (Text) and Auto (Numeric)
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

            // ✅ Handle string-based indexes (Text/Tag)
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
                            return "@" + left + ":{" + escapedValue + "}";
                        case ExpressionType.NotEqual:
                            return "-@" + left + ":{" + escapedValue + "}";
                        default:
                            throw new NotSupportedException("Operator " + binary.NodeType + " is not supported for Tag index");
                    }
                }
            }

            throw new NotSupportedException("Cannot process value " + rightValue + " with index type " + indexType);
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
                            case "StartsWith":
                            case "EndsWith":
                                return $"@{fieldName}:{{{escapedValue}}}"; // Tags don't support wildcards, so treat as exact match
                            default:
                                throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for Tag index");
                        }
                    }
                }
            }
            throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
        }


        private string GetFieldName(Expression expression)
        {
            if (expression is MemberExpression member)
                return member.Member.Name;

            throw new NotSupportedException("Left side of expression must be a property");
        }

        private object EvaluateExpression(Expression expression, ParameterExpression parameter)
        {
            try
            {
                if (expression is ConstantExpression constant)
                    return constant.Value;

                if (expression is MemberExpression member)
                {
                    if (member.Expression == null) // Handle static members
                    {
                        if (member.Member.DeclaringType == typeof(DateTime) && member.Member.Name == "Now")
                            return DateTime.Now; // Special handling for DateTime.Now

                        var staticField = member.Member as System.Reflection.FieldInfo;
                        if (staticField != null && staticField.IsStatic)
                            return staticField.GetValue(null);

                        var staticProp = member.Member as System.Reflection.PropertyInfo;
                        if (staticProp != null && staticProp.GetMethod.IsStatic)
                            return staticProp.GetValue(null);
                    }

                    var obj = member.Expression != null ? EvaluateExpression(member.Expression, parameter) : null;
                    var field = member.Member as System.Reflection.FieldInfo;
                    if (field != null)
                        return field.GetValue(obj);

                    var prop = member.Member as System.Reflection.PropertyInfo;
                    if (prop != null)
                        return prop.GetValue(obj);

                    throw new NotSupportedException("Unsupported member type");
                }

                // ✅ Fix for MethodCallExpression
                if (expression is MethodCallExpression methodCall)
                {
                    // Evaluate the instance (object) on which the method is called
                    object instance = null;
                    if (methodCall.Object != null)
                    {
                        instance = EvaluateExpression(methodCall.Object, parameter);
                    }

                    // Evaluate all method arguments
                    var arguments = new object[methodCall.Arguments.Count];
                    for (int i = 0; i < methodCall.Arguments.Count; i++)
                    {
                        arguments[i] = EvaluateExpression(methodCall.Arguments[i], parameter);
                    }

                    // Invoke the method dynamically
                    return methodCall.Method.Invoke(instance, arguments);
                }

                // Handle boolean literals explicitly
                if (expression is ConstantExpression boolConstant && boolConstant.Value is bool)
                    return boolConstant.Value;

                // Compile the expression with the parameter, but don't invoke it with a value since we're just parsing
                var lambda = Expression.Lambda(expression, parameter);
                var compiled = lambda.Compile();

                // If it's a simple value we can evaluate immediately
                if (lambda.Parameters.Count == 0)
                    return compiled.DynamicInvoke();

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
            return value.Replace("\"", "\\\"")    // Escape quotes
                        .Replace(" ", "\\ ")      // Escape spaces
                        .Replace(":", "\\:")      // Escape colons
                        .Replace("@", "\\@")     // Escape @ symbol
                        .Replace(".", "\\.");     // Escape @ symbol

        }
    }
}
