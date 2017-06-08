using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicToString
{
    public static class InternalExtensions
    {
        public static string NullString { get; set; }
        public static StringWrappers NullWrapper { get; set; }
        public static StringWrappers ListTypeWrapper { get; set; }
        public static StringWrappers ComplexTypeWrapper { get; set; }
        public static StringWrappers PrimitiveTypeWrapper { get; set; }
        public static StringWrappers StringTypeWrapper { get; set; }
        public static StringWrappers NullableTypeWrapper { get; set; }
        public static string NullPlaceholder => NullString.WrapString(NullWrapper);

        public static void ResetWrapperSettings()
        {
            NullString = "null";
            NullWrapper = StringWrappers.AngleBrackets;
            ListTypeWrapper = StringWrappers.SquareBrackets;
            ComplexTypeWrapper = StringWrappers.CurlyBrackets;
            PrimitiveTypeWrapper = StringWrappers.None;
            StringTypeWrapper = StringWrappers.DoubleQuotes;
            NullableTypeWrapper = StringWrappers.None;
        }

        private static Dictionary<Type, Func<object, string>> _toStringMethodCache;
        public static ReadOnlyDictionary<Type, Func<object, string>> ToStringMethodCache => new ReadOnlyDictionary<Type, Func<object, string>>(_toStringMethodCache);
        //private static Stopwatch _timer;

        static InternalExtensions()
        {
            ResetWrapperSettings();
            ClearCache();
            //_timer = new Stopwatch();
        }

        public static string AutoString(this object obj)
        {
            if (obj == null)
            {
                return NullPlaceholder;
            }

            var objectType = obj.GetType();
            Func<object, string> toStringMethod;
            if (!_toStringMethodCache.TryGetValue(objectType, out toStringMethod))
            {
                toStringMethod = CacheStringMethod(objectType);
            }
            return toStringMethod(obj);
        }

        public static void SetAutoStringMethod(this Type objectType, Func<object, string> toStringMethod)
        {
            _toStringMethodCache[objectType] = toStringMethod;
        }

        public static void SetAutoStringMethod<TObject>(Func<TObject, string> toStringMethod)
        {
            SetAutoStringMethod(typeof(TObject), obj => toStringMethod((TObject)obj));
        }

        private static Func<object, string> CacheStringMethod(Type objectType)
        {
            Expression<Func<object, string>> toStringExpression;
            if (objectType.IsStringType())
            {
                toStringExpression = o => o.ToString().WrapString(StringTypeWrapper);
            }
            else if (objectType.IsPrimitiveType())
            {
                toStringExpression = o => o.ToString().WrapString(PrimitiveTypeWrapper);
            }
            else if (objectType.IsNullableType())
            {
                toStringExpression = o => o.ToString().WrapString(NullableTypeWrapper);
            }
            else if (objectType.IsEnumerableType())
            {

                toStringExpression = o => string.Join(", ", (o as IEnumerable).Cast<object>().Select(v => v.AutoString()).OrderByDescending(str => str == null || str == NullPlaceholder).ThenBy(str => str)).WrapString(ListTypeWrapper);
            }
            else
            {
                MethodInfo info;
                if (objectType.TryGetClassToStringMethodInfo(out info))
                {
                    toStringExpression = obj => obj == null ? NullPlaceholder : (info.Invoke(obj, null) as string).WrapString(ComplexTypeWrapper);
                }
                else
                {
                    toStringExpression = o => string.Join(", ", objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(prop => $"{prop.Name}: {prop.GetValue(o).AutoString()}")).WrapString(ComplexTypeWrapper);
                }
            }
            return (_toStringMethodCache[objectType] = toStringExpression.Compile());
        }

        //public static string TimeAutoToString<TObject>(this TObject obj)
        //{
        //    _timer.Restart();
        //    var output = obj.AutoString();
        //    _timer.Stop();
        //    return $"ELAPSED: {_timer.Elapsed.TotalMilliseconds}ms STRING: {output}";
        //}

        //public static string TimeListAutoToString<TObject>(this IEnumerable<TObject> objList)
        //{
        //    _timer.Restart();
        //    var output = objList.AutoString();
        //    _timer.Stop();
        //    var milliseconds = _timer.Elapsed.TotalMilliseconds;
        //    return string.Format("ELAPSED: {0}ms AVERAGE: {1:##.00000} STRING: {2}", milliseconds, milliseconds / ((double)objList.Count()), output);
        //}

        public static void ClearCache(Type t = null)
        {
            if (t != null)
            {
                _toStringMethodCache.Remove(t);
            }
            else
            {
                _toStringMethodCache = new Dictionary<Type, Func<object, string>>();
            }
        }

        public static bool IsNullableType(this Type t)
        {
            return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static bool IsPrimitiveType(this Type t)
        {
            return t.IsPrimitive;
        }

        public static bool IsStringType(this Type t)
        {
            return t == typeof(string);
        }

        public static bool IsSimpleType(this Type t)
        {
            return t.IsPrimitiveType() || t.IsStringType();
        }

        public static bool IsEnumerableType(this Type t, out Type elementType)
        {
            if (t.IsArray)
            {
                elementType = t.GetElementType();
                return true;
            }

            if (typeof(IEnumerable).IsAssignableFrom(t))
            {
                elementType = t.GenericTypeArguments.FirstOrDefault();
                return true;
            }

            elementType = null;
            return false;
        }

        public static bool IsEnumerableType(this Type t)
        {
            Type trash;
            return t.IsEnumerableType(out trash);
        }

        public static bool TryGetClassToStringFunction(this Type type, out Func<object, string> method)
        {
            Expression<Func<object, string>> exp;
            if (type.TryGetClassToStringExpression(out exp))
            {
                method = exp.Compile();
                return true;
            }
            method = null;
            return false;
        }

        public static bool TryGetClassToStringExpression(this Type type, out Expression<Func<object, string>> exp)
        {
            MethodInfo info;
            if (type.TryGetClassToStringMethodInfo(out info))
            {
                exp = obj => (info.Invoke(obj, null) as string);
                return true;
            }

            exp = null;
            return false;
        }

        public static bool TryGetClassToStringMethodInfo(this Type type, out MethodInfo methodInfo)
        {
            var methodMatch = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(info => info.Name == "ToString"
                                        && info.ReturnParameter?.ParameterType == typeof(string)
                                        && info.GetParameters().Length == 0
                                        && info.DeclaringType != typeof(object));
            if (methodMatch != null)
            {
                methodInfo = methodMatch;
                return true;
            }
            methodInfo = null;
            return false;
        }

        public enum StringWrappers
        {
            None,
            CurlyBrackets,
            SquareBrackets,
            AngleBrackets,
            Parentheses,
            DoubleQuotes,
            SingleQuotes
        }

        public static string WrapString(this string s, StringWrappers wrapper)
        {
            s = s ?? String.Empty;;
            switch (wrapper)
            {
                case StringWrappers.None:
                    return s;
                case StringWrappers.CurlyBrackets:
                    return "{" + s + "}";
                case StringWrappers.SquareBrackets:
                    return "[" + s + "]";
                case StringWrappers.AngleBrackets:
                    return "<" + s + ">";
                case StringWrappers.Parentheses:
                    return "(" + s + ")";
                case StringWrappers.DoubleQuotes:
                    return "\"" + s + "\"";
                case StringWrappers.SingleQuotes:
                    return "'" + s + "'";
                default:
                    throw new NotImplementedException($"No wrapping has been defined for {wrapper} yet.");
            }
        }
    }
}
