using BepInEx.Logging;
using EFT;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ombarella
{
    internal static class ReflectionHelper
    {
        private static readonly Assembly PluginAssembly;

        private static readonly Assembly TarkovAssembly;

        private static Dictionary<string, Type> TypeCache;

        private static Dictionary<string, FieldInfo> FieldCache;

        private static Dictionary<string, PropertyInfo> PropertyCache;

        private static Dictionary<string, MethodInfo> MethodCache;

        public static ManualLogSource Logger { get; set; }

        static ReflectionHelper()
        {
            TypeCache = new Dictionary<string, Type>();
            FieldCache = new Dictionary<string, FieldInfo>();
            PropertyCache = new Dictionary<string, PropertyInfo>();
            MethodCache = new Dictionary<string, MethodInfo>();
            PluginAssembly = Assembly.GetExecutingAssembly();
            TarkovAssembly = Assembly.GetAssembly(typeof(TarkovApplication));
        }

        public static string GenerateCacheKey(params object[] parameters)
        {
            string text = "";
            foreach (object obj in parameters)
            {
                if (obj is Array)
                {
                    foreach (object item in (Array)obj)
                    {
                        text += $"{item}-";
                    }
                }
                else
                {
                    text += $"{obj}-";
                }
            }
            return text.TrimEnd('-');
        }

        private static bool TryGetFromCache<T>(string key, out T cachedOutput) where T : MemberInfo
        {
            Type typeFromHandle = typeof(T);
            if (typeFromHandle == typeof(Type))
            {
                if (TypeCache.TryGetValue(key, out var value))
                {
                    cachedOutput = (T)(MemberInfo)value;
                    return true;
                }
                cachedOutput = null;
                return false;
            }
            if (typeFromHandle == typeof(FieldInfo))
            {
                if (FieldCache.TryGetValue(key, out var value2))
                {
                    cachedOutput = (T)(MemberInfo)value2;
                    return true;
                }
                cachedOutput = null;
                return false;
            }
            if (typeFromHandle == typeof(PropertyInfo))
            {
                if (PropertyCache.TryGetValue(key, out var value3))
                {
                    cachedOutput = (T)(MemberInfo)value3;
                    return true;
                }
                cachedOutput = null;
                return false;
            }
            if (typeFromHandle == typeof(MethodInfo))
            {
                if (MethodCache.TryGetValue(key, out var value4))
                {
                    cachedOutput = (T)(MemberInfo)value4;
                    return true;
                }
                cachedOutput = null;
                return false;
            }
            throw new Exception($"ReflectionHelper.TryGetFromCache<{typeof(T)}> can't be used with type {typeof(T)}.");
        }

        private static void AddToCache<T>(string key, T objectToCache)
        {
            Type typeFromHandle = typeof(T);
            if (typeFromHandle == typeof(Type))
            {
                TypeCache.Add(key, objectToCache as Type);
                return;
            }
            if (typeFromHandle == typeof(FieldInfo))
            {
                FieldCache.Add(key, objectToCache as FieldInfo);
                return;
            }
            if (typeFromHandle == typeof(PropertyInfo))
            {
                PropertyCache.Add(key, objectToCache as PropertyInfo);
                return;
            }
            if (typeFromHandle == typeof(MethodInfo))
            {
                MethodCache.Add(key, objectToCache as MethodInfo);
                return;
            }
            throw new Exception($"ReflectionHelper.AddToCache<{typeof(T)}> can't be used with type {typeof(T)}.");
        }

        public static Type FindClassTypeByMethodNames(string[] names, Assembly targetAssembly = null, bool searchInAllTypes = false)
        {
            object[] parameters = names;
            string text = GenerateCacheKey(parameters);
            if (TryGetFromCache<Type>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            if ((object)targetAssembly == null)
            {
                targetAssembly = TarkovAssembly;
            }
            IEnumerable<Type> source;
            if (!searchInAllTypes)
            {
                IEnumerable<Type> typesFromAssembly = AccessTools.GetTypesFromAssembly(targetAssembly);
                source = typesFromAssembly;
            }
            else
            {
                source = AccessTools.AllTypes();
            }
            IEnumerable<Type> enumerable = source.Where(delegate (Type type2)
            {
                if (!type2.Assembly.Equals(PluginAssembly) && type2.IsClass)
                {
                    List<string> methods = AccessTools.GetMethodNames(type2);
                    return names.All((string searchedMethodName) => methods.Contains(searchedMethodName));
                }
                return false;
            });
            if (enumerable.Count() > 1)
            {
                Logger.LogWarning((object)("ReflectionHelper.FindClassTypeByMethodNames [AmbiguousMatch-Key]: " + text));
                foreach (Type item in enumerable)
                {
                    Logger.LogWarning((object)("ReflectionHelper.FindClassTypeByMethodNames [AmbiguousMatch]: " + item.AssemblyQualifiedName));
                }
                throw new AmbiguousMatchException();
            }
            Type type = enumerable.FirstOrDefault();
            if (type == null)
            {
                throw GetNotFoundException(text);
            }
            if (type != null)
            {
                AddToCache(text, type);
            }
            return type;
        }

        public static Type FindClassTypeByFieldNames(string[] names, Assembly targetAssembly = null, bool searchInAllTypes = false)
        {
            object[] parameters = names;
            string text = GenerateCacheKey(parameters);
            if (TryGetFromCache<Type>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            if ((object)targetAssembly == null)
            {
                targetAssembly = TarkovAssembly;
            }
            IEnumerable<Type> source;
            if (!searchInAllTypes)
            {
                IEnumerable<Type> typesFromAssembly = AccessTools.GetTypesFromAssembly(targetAssembly);
                source = typesFromAssembly;
            }
            else
            {
                source = AccessTools.AllTypes();
            }
            IEnumerable<Type> enumerable = source.Where(delegate (Type type2)
            {
                if (!type2.Assembly.Equals(PluginAssembly) && type2.IsClass)
                {
                    List<string> fields = AccessTools.GetFieldNames(type2);
                    return names.All((string searchedFieldName) => fields.Contains(searchedFieldName));
                }
                return false;
            });
            if (enumerable.Count() > 1)
            {
                Logger.LogWarning((object)("ReflectionHelper.FindClassTypeByFieldNames [AmbiguousMatch-Key]: " + text));
                foreach (Type item in enumerable)
                {
                    Logger.LogWarning((object)("ReflectionHelper.FindClassTypeByFieldNames [AmbiguousMatch]: " + item.AssemblyQualifiedName));
                }
                throw new AmbiguousMatchException();
            }
            Type type = enumerable.FirstOrDefault();
            if (type == null)
            {
                throw GetNotFoundException(text);
            }
            if (type != null)
            {
                AddToCache(text, type);
            }
            return type;
        }

        public static MethodInfo FindMethodByArgTypes(this object instance, Type[] methodArgTypes, BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            return FindMethodByArgTypes(instance.GetType(), methodArgTypes, bindingAttr);
        }

        public static MethodInfo FindMethodByArgTypes(Type type, Type[] methodArgTypes, BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            string text = GenerateCacheKey(type, methodArgTypes, bindingAttr);
            if (TryGetFromCache<MethodInfo>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            IEnumerable<MethodInfo> enumerable = type.GetMethods(bindingAttr).Where(delegate (MethodInfo method)
            {
                ParameterInfo[] parameters = method.GetParameters();
                return methodArgTypes.All((Type argType) => parameters.Any((ParameterInfo param) => param.ParameterType == argType));
            });
            if (enumerable.Count() > 1)
            {
                Logger.LogWarning((object)("ReflectionHelper.FindMethodByArgTypes [AmbiguousMatch-Key]: " + text));
                Logger.LogWarning((object)("ReflectionHelper.FindMethodByArgTypes [AmbiguousMatch-Type-AssemblyQualifiedName]: " + type.AssemblyQualifiedName));
                foreach (MethodInfo item in enumerable)
                {
                    Logger.LogWarning((object)("ReflectionHelper.FindMethodByArgTypes [AmbiguousMatch]: " + item.Name));
                }
                throw new AmbiguousMatchException();
            }
            MethodInfo methodInfo = enumerable.FirstOrDefault();
            if (methodInfo == null)
            {
                throw GetNotFoundException(text);
            }
            if (methodInfo != null)
            {
                AddToCache(text, methodInfo);
            }
            return methodInfo;
        }

        private static FieldInfo GetFieldWithCache(Type type, string name)
        {
            string text = GenerateCacheKey(type, name);
            if (TryGetFromCache<FieldInfo>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            FieldInfo fieldInfo = AccessTools.Field(type, name);
            if (fieldInfo == null)
            {
                throw GetNotFoundException(text);
            }
            AddToCache(text, fieldInfo);
            return fieldInfo;
        }

        private static PropertyInfo GetPropertyWithCache(Type type, string name)
        {
            string text = GenerateCacheKey(type, name);
            if (TryGetFromCache<PropertyInfo>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            PropertyInfo propertyInfo = AccessTools.Property(type, name);
            if (propertyInfo == null)
            {
                throw GetNotFoundException(text);
            }
            AddToCache(text, propertyInfo);
            return propertyInfo;
        }

        private static MethodInfo GetMethodWithCache(Type type, string name, Type[] methodArgTypes = null)
        {
            string text = GenerateCacheKey(type, name, methodArgTypes);
            if (TryGetFromCache<MethodInfo>(text, out var cachedOutput))
            {
                return cachedOutput;
            }
            MethodInfo methodInfo = AccessTools.Method(type, name, methodArgTypes, (Type[])null);
            if (methodInfo == null)
            {
                throw GetNotFoundException(text);
            }
            AddToCache(text, methodInfo);
            return methodInfo;
        }

        private static Exception GetNotFoundException(string searchParamsKey)
        {
            return new Exception("ReflectionHelper | Couldn't find member with parameters key " + searchParamsKey + ".");
        }

        public static T InvokeMethod<T>(this Type staticType, string name, object[] args = null, Type[] methodArgTypes = null)
        {
            return (T)InvokeMethod(staticType, name, args, methodArgTypes);
        }

        public static object InvokeMethod(this Type staticType, string name, object[] args = null, Type[] methodArgTypes = null)
        {
            MethodInfo methodWithCache = GetMethodWithCache(staticType, name, methodArgTypes);
            ParameterInfo[] parameters = methodWithCache.GetParameters();
            if (args.Length < parameters.Length)
            {
                Array.Resize(ref args, parameters.Length);
                for (int i = 0; i < args.Length; i++)
                {
                    object[] array = args;
                    int num = i;
                    if (array[num] == null)
                    {
                        array[num] = Type.Missing;
                    }
                }
            }
            return methodWithCache.Invoke(null, args);
        }

        public static object GetFieldValue(this Type staticType, string name)
        {
            return GetField(staticType, name).GetValue(null);
        }

        public static bool TryGetFieldValue(this Type staticType, string name, out object value)
        {
            try
            {
                value = GetFieldValue(staticType, name);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public static object GetFieldValueOrDefault(this Type staticType, string name)
        {
            if (TryGetFieldValue(staticType, name, out var value))
            {
                return value;
            }
            return null;
        }

        public static T GetFieldValue<T>(this Type staticType, string name)
        {
            return (T)GetFieldValue(staticType, name);
        }

        public static bool TryGetFieldValue<T>(this Type staticType, string name, out T value)
        {
            try
            {
                value = GetFieldValue<T>(staticType, name);
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }

        public static T GetFieldValueOrDefault<T>(this Type staticType, string name)
        {
            if (TryGetFieldValue(staticType, name, out T value))
            {
                return value;
            }
            return default(T);
        }

        public static T InvokeMethod<T>(this object targetObj, string name, object[] args = null, Type[] methodArgTypes = null)
        {
            return (T)InvokeMethod(targetObj, name, args, methodArgTypes);
        }

        public static object InvokeMethod(this object targetObj, string name, object[] args = null, Type[] methodArgTypes = null)
        {
            MethodInfo methodWithCache = GetMethodWithCache(targetObj.GetType(), name, methodArgTypes);
            ParameterInfo[] parameters = methodWithCache.GetParameters();
            if (args.Length < parameters.Length)
            {
                Array.Resize(ref args, parameters.Length);
                for (int i = 0; i < args.Length; i++)
                {
                    object[] array = args;
                    int num = i;
                    if (array[num] == null)
                    {
                        array[num] = Type.Missing;
                    }
                }
            }
            return methodWithCache.Invoke(targetObj, args);
        }

        public static T GetFieldValue<T>(this object targetObj, string name)
        {
            return (T)GetFieldValue(targetObj, name);
        }

        public static bool TryGetFieldValue<T>(this object targetObj, string name, out T value)
        {
            try
            {
                value = GetFieldValue<T>(targetObj, name);
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }

        public static T GetFieldValueOrDefault<T>(this object targetObj, string name)
        {
            if (TryGetFieldValue(targetObj, name, out T value))
            {
                return value;
            }
            return default(T);
        }

        public static object GetFieldValue(this object targetObj, string name)
        {
            return GetField(targetObj, name).GetValue(targetObj);
        }

        public static bool TryGetFieldValue(this object targetObj, string name, out object value)
        {
            try
            {
                value = GetFieldValue(targetObj, name);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public static object GetFieldValueOrDefault(this object targetObj, string name)
        {
            if (TryGetFieldValue(targetObj, name, out var value))
            {
                return value;
            }
            return null;
        }

        public static T GetPropertyValue<T>(this object targetObj, string name)
        {
            return (T)GetPropertyValue(targetObj, name);
        }

        public static bool TryGetPropertyValue<T>(this object targetObj, string name, out T value)
        {
            try
            {
                value = GetPropertyValue<T>(targetObj, name);
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }

        public static T GetPropertyValueOrDefault<T>(this object targetObj, string name)
        {
            if (TryGetPropertyValue(targetObj, name, out T value))
            {
                return value;
            }
            return default(T);
        }

        public static object GetPropertyValue(this object targetObj, string name)
        {
            return GetProperty(targetObj, name).GetValue(targetObj);
        }

        public static bool TryGetPropertyValue(this object targetObj, string name, out object value)
        {
            try
            {
                value = GetPropertyValue(targetObj, name);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public static object GetPropertyValueOrDefault(this object targetObj, string name)
        {
            if (TryGetPropertyValue(targetObj, name, out var value))
            {
                return value;
            }
            return null;
        }

        public static FieldInfo GetField(this object targetObj, string name)
        {
            return GetFieldWithCache(targetObj.GetType(), name);
        }

        public static PropertyInfo GetProperty(this object targetObj, string name)
        {
            return GetPropertyWithCache(targetObj.GetType(), name);
        }

        public static MethodInfo GetMethod(this object targetObj, string name, Type[] methodArgTypes = null)
        {
            return GetMethodWithCache(targetObj.GetType(), name, methodArgTypes);
        }

        public static FieldInfo GetField(this Type staticType, string name)
        {
            return GetFieldWithCache(staticType, name);
        }

        public static PropertyInfo GetProperty(this Type staticType, string name)
        {
            return GetPropertyWithCache(staticType, name);
        }

        public static MethodInfo GetMethod(this Type staticType, string name, Type[] methodArgTypes = null)
        {
            return GetMethodWithCache(staticType, name, methodArgTypes);
        }
    }
}
