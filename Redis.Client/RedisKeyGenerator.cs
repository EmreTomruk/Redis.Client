using System;
using System.Collections.Generic;
using System.Linq;

namespace Redis.Client
{
    public static class RedisKeyGenerator
    {
        public static string GetKey(Type type)
        {
            return !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>)
                ? type.Name
                : type.GetGenericArguments().Single().Name;
        }
    }
}
