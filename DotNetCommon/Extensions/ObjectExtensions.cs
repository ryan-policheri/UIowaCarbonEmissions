using DotNetCommon.Constants;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace DotNetCommon.Extensions
{
    public static class ObjectExtensions
    {
        private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsPrimitive(this Type type)
        {
            if (type == typeof(String)) return true;
            return (type.IsValueType & type.IsPrimitive);
        }

        public static Object Copy(this Object originalObject)
        {
            return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
        }

        public static T Copy<T>(this T original)
        {
            return (T)Copy((Object)original);
        }

        private static Object InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
        {
            if (originalObject == null) return null;
            var typeToReflect = originalObject.GetType();
            if (IsPrimitive(typeToReflect)) return originalObject;
            if (visited.ContainsKey(originalObject)) return visited[originalObject];
            if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
            var cloneObject = CloneMethod.Invoke(originalObject, null);
            if (typeToReflect.IsArray)
            {
                var arrayType = typeToReflect.GetElementType();
                if (IsPrimitive(arrayType) == false)
                {
                    Array clonedArray = (Array)cloneObject;
                    clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                }

            }
            visited.Add(originalObject, cloneObject);
            CopyFields(originalObject, visited, cloneObject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
            return cloneObject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
        {
            if (typeToReflect.BaseType != null)
            {
                RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
                CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
            }
        }

        private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
        {
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
            {
                if (filter != null && filter(fieldInfo) == false) continue;
                if (IsPrimitive(fieldInfo.FieldType)) continue;
                var originalFieldValue = fieldInfo.GetValue(originalObject);
                var clonedFieldValue = InternalCopy(originalFieldValue, visited);
                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }
        }

        private class ReferenceEqualityComparer : EqualityComparer<Object>
        {
            public override bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }
            public override int GetHashCode(object obj)
            {
                if (obj == null) return 0;
                return obj.GetHashCode();
            }
        }

        public static string ToArgumentString<T>(this object source)
        {
            StringBuilder builder = new StringBuilder();
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                string propName = prop.Name;
                string propValue = prop.GetValue(source)?.ToString();
                if (propValue != null)
                {
                    builder.Append(ParameterStyle.DoubleDash.ToDescription()).Append(propName).Append(" \"").Append(propValue).Append("\" ");
                }
            }
            return builder.ToString();
        }

        public static string ToJson(this object source)
        {
            return JsonSerializer.Serialize(source);
        }

        public static string ToBeautifulJson(this object source)
        {
            return JsonSerializer.Serialize(source, CommonJsonSerializerOptions.CaseInsensitiveBeautiful);
        }

        public static bool TrySetValueWithTypeRespect(this PropertyInfo property, object instance, string rawValue)
        {
            try
            {
                property.SetValueWithTypeRespect(instance, rawValue);
                return true;
            }
            catch (Exception ex) { return false; }
        }

        public static void SetValueWithTypeRespect(this PropertyInfo property, object instance, string rawValue)
        {
            if (!property.CanWrite) return;
            Type propertyType = property.PropertyType;

            if (propertyType == typeof(string)) property.SetValue(instance, rawValue);
            else if (propertyType == typeof(bool))
            {
                if (bool.TryParse(rawValue, out bool result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(bool));
            }
            else if (propertyType == typeof(int))
            {
                if (int.TryParse(rawValue, out int result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(int));
            }
            else if (propertyType == typeof(long))
            {
                if (long.TryParse(rawValue, out long result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(long));
            }
            else if (propertyType == typeof(decimal))
            {
                if (decimal.TryParse(rawValue, out decimal result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(decimal));
            }
            else if (propertyType == typeof(double))
            {
                if (double.TryParse(rawValue, out double result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(double));
            }
            else if (propertyType == typeof(DateTime))
            {
                if (DateTime.TryParse(rawValue, out DateTime result)) property.SetValue(instance, result);
                else property.SetValue(instance, default(DateTime));
            }
            else if (propertyType.IsEnum)
            {
                if (!String.IsNullOrWhiteSpace(rawValue))
                {
                    Type enumType = propertyType.UnderlyingSystemType;
                    property.SetValue(instance, Enum.Parse(enumType, rawValue, true));
                }
            }
            else throw new NotImplementedException("Parsing for type " + propertyType.Name + " is not implemented.");
        }
    }
}
