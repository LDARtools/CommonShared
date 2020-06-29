using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ldartools.Common.Extensions.Enumerable;
using Ldartools.Common.Extensions.Object;

namespace Ldartools.Common.Extensions.Reflection
{

    public static class ReflectionExtensions
    {
        #region Method Reflection

        static Dictionary<string, Dictionary<string, List<MethodInfo>>> extensionMethods = new Dictionary<string, Dictionary<string, List<MethodInfo>>>();
        static bool hasCheckedExtensions = false;

        private static void GetExtensionMethods(Type objType)
        {
            if (hasCheckedExtensions)
                return;

            hasCheckedExtensions = true;

            Assembly assembly = GetAssembly(objType);

            var types = GetTypes(assembly);

            var query = from type in types
                        where type.IsSealed && !type.IsGenericType && !type.IsNestedPublic
                        from method in GetMethods(type, t => t.IsStatic)
                        where method.IsDefined(typeof(ExtensionAttribute), false)
                        select method;

            foreach (MethodInfo method in query)
            {
                string paramTypeStr = method.GetParameters()[0].ParameterType.ToString();

                if (!extensionMethods.Keys.Contains(paramTypeStr))
                {
                    extensionMethods.Add(paramTypeStr, new Dictionary<string, List<MethodInfo>>());

                }

                if (!extensionMethods[paramTypeStr].Keys.Contains(method.Name))
                    extensionMethods[paramTypeStr][method.Name] = new List<MethodInfo>();

                extensionMethods[paramTypeStr][method.Name].Add(method);
            }
        }

#if COMPACT_FRAMEWORK
        private static Assembly GetAssembly(Type type)
        {
            return type.Assembly;
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            return assembly.GetTypes().ToArray();
        }

        private static MethodInfo[] GetMethods(Type type, Func<MethodInfo, bool> filter)
        {
            return type.GetMethods().Where(filter).ToArray();
        }

        private static MethodInfo GetMethod(Type type, string methodName, Func<MethodInfo, bool> filter)
        {
            return type.GetMethods().First(m => m.Name == methodName && filter(m));
        }

        private static FieldInfo GetField(Type type, string fieldName, Func<FieldInfo, bool> filter)
        {
            return type.GetFields().First(m => m.Name == fieldName && filter(m));
        }

        private static EventInfo[] GetEvents(Type type)
        {
            return type.GetEvents().ToArray();
        }

        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            return type.GetRuntimeProperty(propertyName);
        }
#else
        private static Assembly GetAssembly(Type type)
        {
            return type.GetTypeInfo().Assembly;
        }

        private static TypeInfo[] GetTypes(Assembly assembly)
        {
            return assembly.DefinedTypes.ToArray();
        }

        private static MethodInfo[] GetMethods(TypeInfo type, Func<MethodInfo, bool> filter)
        {
            return type.AsType().GetRuntimeMethods().Where(filter).ToArray();
        }

        private static MethodInfo GetMethod(Type type, string methodName, Func<MethodInfo, bool> filter)
        {
            return type.GetRuntimeMethods().First(m => m.Name == methodName && filter(m));
        }

        private static MethodInfo[] GetMethods(Type type, Func<MethodInfo, bool> filter)
        {
            return GetMethods(type.GetTypeInfo(), filter);
        }

        private static FieldInfo GetField(Type type, string fieldName, Func<FieldInfo, bool> filter)
        {
            return type.GetRuntimeFields().First(m => m.Name == fieldName && filter(m));
        }

        private static EventInfo[] GetEvents(Type type)
        {
            return type.GetRuntimeEvents().ToArray();
        }

        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            return type.GetRuntimeProperty(propertyName);
        }
#endif
        /// <summary>
        /// Invokes the method on the given object.
        /// </summary>
        /// <param name="obj">The object that will invoke the method</param>
        /// <param name="methodName">The name of the method</param>
        /// <param name="parameters">Parameters for the method</param>
        /// <returns></returns>
        public static object InvokeMethod(this object obj, string methodName, params object[] parameters)
        {
            GetExtensionMethods(obj.GetType());
            Type objType = obj.IsNull() ? typeof(System.Object) : obj.GetType();

            List<Type> types = new List<Type>();

            foreach (System.Object paramObj in parameters)
            {
                types.Add(paramObj.GetType());
            }

            MethodInfo methodInfo = GetMethod(objType, methodName,
                m =>
                    !m.IsStatic &&
                    m.GetParameters()
                        .Select(p => p.ParameterType)
                        .OrderBy(p => p.Name)
                        .SequenceEqual(types.OrderBy(t => t.Name)));

            if (methodInfo.IsNull())
            {
                Type tempType = objType;
                do
                {
                    if (extensionMethods.Keys.Contains(tempType.ToString()) && extensionMethods[tempType.ToString()].Keys.Contains(methodName))
                    {
                        foreach (MethodInfo mInfo in extensionMethods[tempType.ToString()][methodName])
                        {
                            if (mInfo.GetParameters().Length == types.Count + 1)
                            {
                                bool match = true;
                                ParameterInfo[] parameterInfo = mInfo.GetParameters();

                                for (int i = 1; i < parameterInfo.Length; i++)
                                {
                                    if (!parameterInfo[i].ParameterType.IsGenericParameter && parameterInfo[i].ParameterType != types[i - 1])
                                    {
                                        match = false;
                                        break;
                                    }
                                }
                                if (match)
                                {
                                    methodInfo = mInfo;
                                    break;
                                }
                            }
                        }
                    }

#if COMPACT_FRAMEWORK
                    tempType = tempType.BaseType;
#else
                    tempType = tempType.GetTypeInfo().BaseType;
#endif
                    

                } while (tempType != null && methodInfo == null);

                if (methodInfo.IsNull())
                {
                    throw new Exception(
                        string.Format("Method {0} does not exist for type {1}.", methodName, obj.IsNull() ? "null" : obj.GetType().Name));
                }
                object[] newParams = new object[parameters.Length + 1];

                newParams[0] = obj;

                for (int i = 1; i < newParams.Length; i++)
                    newParams[i] = parameters[i - 1];
                if (methodInfo.ContainsGenericParameters && !objType.IsNull())
                {
                    List<Type> genericParamTypes = new List<Type>();
                    methodInfo.GetParameters().Each((pInfo, i) =>
                    {
                        if (pInfo.ParameterType.IsGenericParameter)
                        {
                            genericParamTypes.Add(types[i - 1]);
                        }
                    });

                    methodInfo = methodInfo.MakeGenericMethod(genericParamTypes.ToArray());
                }

                return methodInfo.Invoke(null, newParams);
            }



            return methodInfo.Invoke(obj, parameters);
        }

        #endregion Method Reflection

        #region Field Reflection

        #region Get Field

        /// <summary>
        /// Returns the value of the field from the given object.
        /// </summary>
        /// <param name="obj">The object to get the field from</param>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>Returns the value of the field from the given object</returns>
        public static object GetField(this object obj, string fieldName)
        {
            return GetFieldInfo(obj, fieldName).GetValue(obj);
        }

        /// <summary>
        /// Returns the value of the field from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the field</typeparam>
        /// <param name="obj">The object to get the field from</param>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>Returns the value of the field from the given object</returns>
        public static T GetField<T>(this object obj, string fieldName)
        {
            return (T)GetFieldInfo(obj, fieldName).GetValue(obj);
        }

        #endregion Get Field

        #region Set Field

        /// <summary>
        /// Sets the value of the field from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the field</typeparam>
        /// <param name="obj">The object to get the field from</param>
        /// <param name="fieldName">The name of the field</param>
        /// <param name="value">The value to set the field to</param>
        /// <returns>Returns the value of the field from the given object</returns>
        public static T SetField<T>(this object obj, string fieldName, T value)
        {
            GetFieldInfo(obj, fieldName).SetValue(obj, value);
            return (T)GetFieldInfo(obj, fieldName).GetValue(obj);
        }

        #endregion Set Field

        #region Field Helpers

        /// <summary>
        /// Gets the FieldInfo for the given object's field
        /// </summary>
        /// <param name="obj">The object to get the field from</param>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>Returns the FieldInfo for the given object's field</returns>
        private static FieldInfo GetFieldInfo(object obj, string fieldName)
        {
            FieldInfo fieldInfo = GetField(obj.GetType(), fieldName, f => !f.IsStatic);

            if (fieldInfo.IsNull())
            {
                throw new Exception(
                    string.Format("Field {0} does not exist for type {1}.", fieldName, obj.GetType().Name));

            }

            return fieldInfo;
        }

        #endregion Field Helpers

        #endregion Field Reflection

        public static bool EventExists(this object obj, string eventName)
        {
            return GetEvents(obj.GetType()).Any(e => e.Name == eventName);
        }

        #region Property Reflection

        public static bool PropertyExists(this object obj, string propertyName)
        {
            return obj.GetType().GetProperties().Any(p => p.Name == propertyName);
        }

        /// <summary>
        /// Returns the public properties of the given object
        /// </summary>
        /// <param name="obj">The object to get the property from</param>
        /// <returns>Returns the public properties of the given object</returns>
        public static PropertyInfo[] GetProperties(this object obj)
        {
            return obj.GetType().GetProperties();
        }

        #region Get Property

        /// <summary>
        /// Returns the value of the property from the given object.
        /// </summary>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static object GetProperty(this object obj, string propertyName)
        {
            return GetProperty(obj, propertyName, null);
        }

        /// <summary>
        /// Returns the value of the property from the given object.
        /// </summary>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <param name="index">The index of the property.  If the property is not indexed use null</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static object GetProperty(this object obj, string propertyName, object[] index)
        {
            return GetPropertyInfo(obj, propertyName).GetValue(obj, index);
        }

        /// <summary>
        /// Returns the value of the property from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static T GetProperty<T>(this object obj, string propertyName)
        {
            return GetProperty<T>(obj, propertyName, null);
        }

        /// <summary>
        /// Returns the value of the property from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <param name="index">The index of the property.  If the property is not indexed use null</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static T GetProperty<T>(this object obj, string propertyName, object[] index)
        {
            return (T)GetPropertyInfo(obj, propertyName).GetValue(obj, index);
        }

        #endregion Get Property

        #region Set Property

        /// <summary>
        /// Sets the value of the property from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <param name="value">The value to set the property to</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static T SetProperty<T>(this object obj, string propertyName, T value)
        {
            return SetProperty(obj, propertyName, null, value);
        }

        /// <summary>
        /// Sets the value of the property from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <param name="index">The index of the property.  If the property is not indexed use null</param>
        /// <param name="value">The value to set the property to</param>
        /// <returns>Returns the value of the property from the given object</returns>
        public static T SetProperty<T>(this object obj, string propertyName, object[] index, T value)
        {
            GetPropertyInfo(obj, propertyName).SetValue(obj, value, index);
            return (T)GetPropertyInfo(obj, propertyName).GetValue(obj, index);
        }

        #endregion Set Property

        #region Property Helpers

        /// <summary>
        /// Gets the PropertyInfo for the given object's field
        /// </summary>
        /// <param name="obj">The object to get the property from</param>
        /// <param name="propertyName">The name of the property</param>
        /// <returns>Returns the PropertyInfo for the given object's property</returns>
        private static PropertyInfo GetPropertyInfo(object obj, string propertyName)
        {
            PropertyInfo propInfo = GetProperty(obj.GetType(), propertyName);

            if (propInfo.IsNull())
            {
                throw new MissingMemberException(
                    string.Format("Property {0} does not exist for type{1}.", propertyName, obj.GetType().Name));
            }

            return propInfo;
        }

        #endregion Property Helpers

        #endregion Property Reflection

        #region Attribute Reflection

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="attributePredicate"></param>
        /// <returns></returns>
        public static MethodInfo[] GetMethodsWithAttribute<T>(this object obj, Predicate<T> attributePredicate)
        {
            return GetMethods(obj.GetType(), m => !m.IsStatic)
                .Where(m => m.GetCustomAttributes(typeof(T), true)
                             .OfType<T>().Any(a => attributePredicate(a))).ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static MethodInfo[] GetMethodsWithAttribute<T>(this object obj)
        {
            return GetMethodsWithAttribute<T>(obj, a => true);
        }

        #endregion Attribute Reflection

        public static object GetPropertyByPath(this object obj, string path)
        {
            if (obj == null || string.IsNullOrWhiteSpace(path)) return null;
            if (path.Contains("."))
            {
                var index = path.IndexOf(".", StringComparison.InvariantCulture);
                var value = obj.GetProperty(path.Substring(0, index));
                return value?.GetPropertyByPath(path.Substring(index + 1, path.Length - index - 1));
            }
            else
            {
                return obj.GetProperty(path);
            }
        }
    }

}
