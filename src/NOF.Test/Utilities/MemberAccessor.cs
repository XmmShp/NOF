using System.Reflection;

namespace NOF.Test;

public static class MemberAccessor
{
    extension(object obj)
    {
        /// <summary>
        /// 通过反射获取私有字段值的辅助方法
        /// </summary>
        public T GetPrivateFieldValue<T>(string fieldName)
        {
            var type = obj.GetType();
            FieldInfo? fieldInfo = null;
            while (type is not null)
            {
                fieldInfo = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo is not null)
                {
                    break;
                }
                type = type.BaseType;
            }

            return (T)fieldInfo?.GetValue(obj)!;
        }

        /// <summary>
        /// 通过反射设置私有字段值的辅助方法
        /// </summary>
        public void SetPrivateFieldValue(string fieldName, object value)
        {
            var type = obj.GetType();
            FieldInfo? fieldInfo = null;
            while (type is not null)
            {
                fieldInfo = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo is not null)
                {
                    break;
                }
                type = type.BaseType;
            }

            fieldInfo?.SetValue(obj, value);
        }

        /// <summary>
        /// 通过反射获取私有或内部属性值的辅助方法
        /// </summary>
        public T GetPrivatePropertyValue<T>(string propertyName)
        {
            var type = obj.GetType();
            PropertyInfo? propertyInfo = null;
            while (type is not null)
            {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (propertyInfo is not null)
                {
                    break;
                }
                type = type.BaseType;
            }

            return (T)propertyInfo?.GetValue(obj)!;
        }

        /// <summary>
        /// 通过反射设置私有或内部属性值的辅助方法
        /// </summary>
        public void SetPrivatePropertyValue(string propertyName, object value)
        {
            var type = obj.GetType();
            PropertyInfo? propertyInfo = null;
            while (type is not null)
            {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (propertyInfo is not null)
                {
                    break;
                }
                type = type.BaseType;
            }

            propertyInfo?.SetValue(obj, value);
        }
    }
}
