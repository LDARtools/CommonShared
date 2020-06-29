using System;
using Ldartools.Common.Util;

namespace Ldartools.Common.Expression
{
    public static class ExpressionFactory
    {
        public static Func<TType, object> GetPropertyValue<TType>(string path)
        {
            return (Func<TType, object>)GetPropertyValue(typeof(TType), path);
        }

        public static Delegate GetPropertyValue(Type type, string path)
        {
            var instance = System.Linq.Expressions.Expression.Parameter(type);

            if (string.IsNullOrWhiteSpace(path) || "." == path)
            {
                return System.Linq.Expressions.Expression.Lambda(instance, instance).Compile();
            }

            var reader = new PropertyPathReader(path);

            System.Linq.Expressions.Expression property = instance;
            while (reader.Read())
            {
                var propertyInfo = type.GetProperty(reader.Value);
                if (propertyInfo == null) throw new ArgumentException(@"Path must be made up of properties.", nameof(path));
                type = propertyInfo.PropertyType;
                property = System.Linq.Expressions.Expression.Property(property, propertyInfo);
            }

            var convert = System.Linq.Expressions.Expression.Convert(property, typeof(object));

            var compiled = System.Linq.Expressions.Expression.Lambda(convert, instance).Compile();
            return compiled;
        }
    }
}
