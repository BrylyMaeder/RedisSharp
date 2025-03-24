using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Globalization;

namespace RedisSharp.Extensions
{
    public static class RedisConverters
    {
        public static RedisValue SerializeToRedis(this object value)
        {
            if (value == null)
            {
                return RedisValue.Null;
            }

            Type type = value.GetType();

            // Handle built-in types directly
            switch (value)
            {
                case string str:
                    return str;
                case bool b:
                    return b.ToString();
                case int i:
                    return i;
                case long l:
                    return l;
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);
                case decimal dec:
                    return dec.ToString(CultureInfo.InvariantCulture);
                case short s:
                    return s;
                case DateTime dateTime:
                    // Normalize to UTC to ensure consistent Unix time
                    if (dateTime.Kind == DateTimeKind.Unspecified)
                    {
                        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc); // Assume UTC if unspecified
                    }
                    else if (dateTime.Kind == DateTimeKind.Local)
                    {
                        dateTime = dateTime.ToUniversalTime();
                    }
                    return ((DateTimeOffset)dateTime).ToUnixTimeSeconds().ToString();
                case DateTimeOffset dto:
                    // Already offset-aware, convert directly to Unix time
                    return dto.ToUnixTimeSeconds().ToString();
                case TimeSpan ts:
                    return ts.Ticks.ToString();
                case Guid guid:
                    return guid.ToString();
                case Enum enumValue:
                    return enumValue.ToString();
            }

            // Serialize complex/custom classes to JSON
            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
            {
                return JsonConvert.SerializeObject(value);
            }

            return (RedisValue)value;
        }

        public static object DeserializeFromRedis(this RedisValue value, Type targetType)
        {
            if (value.IsNull)
            {
                return null;
            }

            // Handle built-in types directly
            if (targetType == typeof(string))
            {
                return (string)value;
            }
            if (targetType == typeof(bool))
            {
                return bool.Parse(value);
            }
            if (targetType == typeof(int))
            {
                return (int)value;
            }
            if (targetType == typeof(long))
            {
                return (long)value;
            }
            if (targetType == typeof(double))
            {
                return double.Parse(value, CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(float))
            {
                return float.Parse(value, CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(decimal))
            {
                return decimal.Parse(value, CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(short))
            {
                return (short)value;
            }
            if (targetType == typeof(DateTime))
            {
                var unixTime = long.Parse(value);
                // Return as UTC to avoid local timezone assumptions
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
            }
            if (targetType == typeof(DateTimeOffset))
            {
                var unixTime = long.Parse(value);
                return DateTimeOffset.FromUnixTimeSeconds(unixTime);
            }
            if (targetType == typeof(TimeSpan))
            {
                return TimeSpan.FromTicks(long.Parse(value));
            }
            if (targetType == typeof(Guid))
            {
                return Guid.Parse(value);
            }
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value);
            }

            // Deserialize complex/custom classes from JSON
            if (targetType.IsClass || (targetType.IsValueType && !targetType.IsPrimitive))
            {
                return JsonConvert.DeserializeObject(value, targetType);
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}