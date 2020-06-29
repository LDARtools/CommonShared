using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ldartools.Common.Extensions.Reflection;

namespace Ldartools.Common.Util
{
    public class ConversionHelper
    {
        public static T ConvertTo<T>(object input)
            where T : new()
        {
            T output = new T();

            return FillFrom(input, output);
        }

        public static T FillFrom<T>(object input, T output)
        {
            Type inputType = input.GetType();
            Type outputType = typeof(T);

            var inputProperties = inputType.GetProperties();

            foreach (var outputPropertyInfo in outputType.GetProperties().Where(p => p.CanRead && p.CanWrite))
            {
                var inputPropertyInfo = inputProperties.FirstOrDefault(p => p.Name == outputPropertyInfo.Name);

                if (inputPropertyInfo == null)
                    continue;

                if (inputPropertyInfo.PropertyType == outputPropertyInfo.PropertyType)
                {
                    outputPropertyInfo.SetValue(output, inputPropertyInfo.GetValue(input));
                }
            }

            return output;
        }
    }
}
