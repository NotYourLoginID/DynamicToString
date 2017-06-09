using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DynamicToString.Enumerations;

namespace DynamicToString
{
    public static class InternalExtensions
    {

        public static class Defaults
        {
            public const string NullString = "null";
            public const TextEnclosures NullWrapper = TextEnclosures.AngleBrackets;
            public const TextEnclosures PrimitiveTypeWrapper = TextEnclosures.None;
            public const TextEnclosures StringTypeWrapper = TextEnclosures.DoubleQuotes;
            public const TextEnclosures EnumerationTypeWrapper = TextEnclosures.None;
            public const TextEnclosures NullableTypeWrapper = TextEnclosures.None;
            public const TextEnclosures EnumerableTypeWrapper = TextEnclosures.SquareBrackets;
            public const TextEnclosures ComplexTypeWrapper = TextEnclosures.CurlyBrackets;

            public const SourceRestrictions SourceRestriction = SourceRestrictions.None;
        }

        public static string NullString
        {
            get { return _nullString; }
            set { _nullString = value; }
        }

        public static TextEnclosures NullWrapper { get; set; }
        public static TextEnclosures PrimitiveTypeWrapper { get; set; }
        public static TextEnclosures StringTypeWrapper { get; set; }
        public static TextEnclosures EnumerationTypeWrapper { get; set; }
        public static TextEnclosures NullableTypeWrapper { get; set; }
        public static TextEnclosures EnumerableTypeWrapper { get; set; }
        public static TextEnclosures ComplexTypeWrapper { get; set; }

        private static SourceRestrictions _sourceRestrictionLevel;
        public static SourceRestrictions SourceRestrictionLevel
        {
            get => _sourceRestrictionLevel;
            set => ChangeRestrictionLevels(value);
        }

        public static string NullPlaceholder => NullString.WrapString(NullWrapper);


        private static Dictionary<Type, MethodSource> _methodSources;
        private static Dictionary<Type, Func<object, string>> _methodCache;
        private static string _nullString;

        public static IReadOnlyDictionary<Type, Func<object, string>> MethodCache => new ReadOnlyDictionary<Type, Func<object, string>>(_methodCache);
        public static IReadOnlyDictionary<Type, MethodSource> MethodSources => new ReadOnlyDictionary<Type, MethodSource>(_methodSources);

        private static void RebuildCachedMethods(bool updateCustomMethods = false)
        {
            
        }

        private static void ChangeRestrictionLevels(SourceRestrictions newRestrictionLevel)
        {
            if (_sourceRestrictionLevel != newRestrictionLevel)
            {
                var oldRestrictionLevel = _sourceRestrictionLevel;
                if (newRestrictionLevel.HasFlag(oldRestrictionLevel))
                {
                    // less restrictions
                    _sourceRestrictionLevel = newRestrictionLevel;
                    _methodSources.Where(kvp => kvp.Key.IsComplexType() && kvp.Value != MethodSource.Custom).Select(kvp => {
                        MethodInfo info;
                        MethodSource source;
                        var hasInfo = kvp.Key.TryGetClassToStringMethodInfo(out info, out source);
                        return new { HasInfo = hasInfo, Info = info, Source = source, TypeKey = kvp.Key };
                    }).Where(i => i.HasInfo && i.Source.ValidForRestriction(newRestrictionLevel)).ToList().ForEach(m => {
                        var function = m.Info.GenerateToStringFunction(ComplexTypeWrapper);
                        m.TypeKey.RegisterMethod(function, m.Source);
                    });
                }
                else
                {
                    var typesToRebuild = _methodSources.Where(kvp => kvp.Value != MethodSource.Custom && !kvp.Value.ValidForRestriction(newRestrictionLevel)).Select(kvp => kvp.Key).ToList();
                    _sourceRestrictionLevel = newRestrictionLevel;
                    typesToRebuild.ForEach(t => {
                        _methodSources.Remove(t);
                        _methodCache.Remove(t);
                        MethodSource source;
                        var function = t.GenerateToStringFunction(out source);
                        t.RegisterMethod(function, source);
                    });
                }
            }
        }

        private static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue> d) { return d == null || !d.Any(); }

        public static void ResetCache(bool keepCustomMethods = true)
        {
            var tempMethodCache = new Dictionary<Type, Func<object, string>>();
            var tempMethodSources = new Dictionary<Type, MethodSource>();

            if (keepCustomMethods && !_methodSources.IsNullOrEmpty() && !_methodCache.IsNullOrEmpty())
            {
                foreach (var typeKey in _methodSources.Where(kvp => kvp.Value == MethodSource.Custom).Select(kvp => kvp.Key).Intersect(_methodCache.Keys))
                {
                    tempMethodCache[typeKey] = _methodCache[typeKey];
                    tempMethodSources[typeKey] = _methodSources[typeKey];
                }
            }

            _methodCache = tempMethodCache;
            _methodSources = tempMethodSources;
        }

        private static void FullCacheSourceAndSettingsReset()
        {
            _sourceRestrictionLevel = Defaults.SourceRestriction;
            ResetCache(false);
        }

        public static void ResetWrapperSettings()
        {
            NullWrapper = Defaults.NullWrapper;
            EnumerableTypeWrapper = Defaults.EnumerableTypeWrapper;
            ComplexTypeWrapper = Defaults.ComplexTypeWrapper;
            PrimitiveTypeWrapper = Defaults.PrimitiveTypeWrapper;
            EnumerationTypeWrapper = Defaults.EnumerationTypeWrapper;
            StringTypeWrapper = Defaults.StringTypeWrapper;
            NullableTypeWrapper = Defaults.NullableTypeWrapper;
        }

        public static void ResetNullSettings()
        {
            NullString = Defaults.NullString;
            NullWrapper = Defaults.NullWrapper;
        }

        public static void ResetSourceRestrictions(bool preserveCache = true)
        {
            var tempMethodCache = new Dictionary<Type, Func<object, string>>();
            var tempMethodSources = new Dictionary<Type, MethodSource>();
            _sourceRestrictionLevel = Defaults.SourceRestriction;

            if (preserveCache && !_methodSources.IsNullOrEmpty() && !_methodCache.IsNullOrEmpty())
            {
                foreach (var typeKey in _methodSources.Where(kvp => kvp.Value.ValidForRestriction(_sourceRestrictionLevel)).Select(kvp => kvp.Key).Intersect(_methodCache.Keys))
                {
                    tempMethodCache[typeKey] = _methodCache[typeKey];
                    tempMethodSources[typeKey] = _methodSources[typeKey];
                }
            }
            _methodCache = tempMethodCache;
            _methodSources = tempMethodSources;
        }

        public static bool RemoveFromCache(Type type)
        {
            return _methodCache.Remove(type) && _methodSources.Remove(type);
        }

        public static Func<object, string> AddToCache(Type type, Func<object, string> function, bool replaceExisting = true)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (_methodCache.ContainsKey(type) && !replaceExisting)
            {
                return null;
            }
            return RegisterMethod(type, function, MethodSource.Custom);
        }

        public static Func<object, string> AddToCache<TObject>(Func<TObject, string> function, bool replaceExisting = true)
        {
            return AddToCache(typeof(TObject), obj => function((TObject)obj), replaceExisting);
        }

        private static Func<object, string> RegisterMethod(this Type type, Func<object, string> function, MethodSource source)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (source == MethodSource.Invalid)
            {
                throw new ArgumentException($"Functions with a {nameof(MethodSource)} of {MethodSource.Invalid} cannot be used.");
            }
            _methodSources[type] = source;
            return _methodCache[type] = function;
        }

        static InternalExtensions()
        {
            FullCacheSourceAndSettingsReset();
            ResetNullSettings();
            ResetWrapperSettings();
        }

        public static string AutoString(this object obj)
        {
            if (obj == null)
            {
                return NullPlaceholder;
            }
            var objectType = obj.GetType();
            Func<object, string> toStringMethod;
            if (!_methodCache.TryGetValue(objectType, out toStringMethod))
            {
                Type elementType;
                if (objectType.IsEnumerableType(out elementType))
                {

                    Func<object, string> elementToStringMethod;
                    if (!_methodCache.TryGetValue(elementType, out elementToStringMethod))
                    {
                        MethodSource elementMethodSource;
                        elementToStringMethod = GenerateToStringFunction(elementType, out elementMethodSource);
                        RegisterMethod(elementType, elementToStringMethod, elementMethodSource);
                    }
                }
                MethodSource source;
                toStringMethod = GenerateToStringFunction(objectType, out source);
                RegisterMethod(objectType, toStringMethod, source);
            }
            return toStringMethod(obj);
        }


        public static Func<object, string> GenerateToStringFunction(this Type objectType, out MethodSource source, bool forceAutoMethod = false)
        {
            if (objectType.IsStringType())
            {
                source = MethodSource.AutoMethod;
                return StringAutoStringMethod;
            }
            if (objectType.IsEnumerationType())
            {
                source = MethodSource.AutoMethod;
                return EnumerationAutoStringMethod;
            }
            if (objectType.IsPrimitiveType())
            {
                source = MethodSource.AutoMethod;
                return PimitiveAutoStringMethod;
            }
            if (objectType.IsNullableType())
            {
                source = MethodSource.AutoMethod;
                return NullableAutoStringMethod;
            }
            if (objectType.IsEnumerableType())
            {
                source = MethodSource.AutoMethod;
                return EnumerableAutoStringMethod;
            }

            if (SourceRestrictionLevel.AllowsSource(MethodSource.ClassMethod))
            {
                MethodInfo info;
                MethodSource methodInfoSource;
                if (objectType.TryGetClassToStringMethodInfo(out info, out methodInfoSource))
                {
                    if (SourceRestrictionLevel.AllowsSource(methodInfoSource))
                    {
                        source = methodInfoSource;
                        return info.GenerateToStringFunction(ComplexTypeWrapper);
                    }
                }
            }

            source = MethodSource.AutoMethod;
            return ComplexAutoStringMethod;
        }

        private static Func<object, string> StringAutoStringMethod => ((Expression<Func<object, string>>)(o => o.ToString().WrapString(StringTypeWrapper))).Compile();
        private static Func<object, string> EnumerationAutoStringMethod => ((Expression<Func<object, string>>)(o => $"{o.GetType().Name}.{o.ToString()}".WrapString(PrimitiveTypeWrapper))).Compile();
        private static Func<object, string> PimitiveAutoStringMethod => ((Expression<Func<object, string>>)(o => o.ToString().WrapString(PrimitiveTypeWrapper))).Compile();
        private static Func<object, string> NullableAutoStringMethod => ((Expression<Func<object, string>>)(o => o.ToString().WrapString(NullableTypeWrapper))).Compile();
        private static Func<object, string> EnumerableAutoStringMethod => ((Expression<Func<object, string>>)(o => string.Join(", ", (o as IEnumerable).Cast<object>().Select(v => v.AutoString()).OrderByDescending(str => str == null || str == NullPlaceholder).ThenBy(str => str)).WrapString(EnumerableTypeWrapper))).Compile();
        private static Func<object, string> ComplexAutoStringMethod => ((Expression<Func<object, string>>)(o => string.Join(", ", o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(prop => $"{prop.Name}: {prop.GetValue(o).AutoString()}")).WrapString(ComplexTypeWrapper))).Compile();

        private static Func<object, string> GenerateToStringFunction(this MethodInfo info, TextEnclosures wrapper = TextEnclosures.None)
        {
            return ((Expression<Func<object, string>>)(o => o == null ? NullPlaceholder : (info.Invoke(o, null) as string).WrapString(wrapper))).Compile();
        }



        public static bool IsNullableType(this Type t)
        {
            Type dummy;
            return t.IsNullableType(out dummy);
        }

        public static bool IsNullableType(this Type t, out Type underlyingType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                underlyingType = t.GetGenericArguments().FirstOrDefault();
                return true;
            }
            underlyingType = null;
            return false;
        }

        public static bool IsPrimitiveType(this Type t)
        {
            return t.IsPrimitive;
        }

        public static bool IsStringType(this Type t)
        {
            return t == typeof(string);
        }

        public static bool IsEnumerationType(this Type t, out Type underlyingType)
        {
            if (t.IsEnum)
            {
                underlyingType = t.GetEnumUnderlyingType();
                return true;
            }
            underlyingType = null;
            return false;
        }

        public static bool IsEnumerationType(this Type t)
        {
            Type dummy;
            return t.IsEnumerationType(out dummy);
        }

        public static bool IsSimpleType(this Type t)
        {
            Type innerType;
            if (t.IsNullableType(out innerType))
            {
                return (!t.IsEnumerableType()) && innerType.IsSimpleType();
            }
            return (!t.IsEnumerableType()) && (t.IsPrimitiveType() || t.IsStringType() || t.IsEnumerationType());
        }

        public static bool IsSimpleEnumerableType(this Type t)
        {
            Type innerType;
            if (t.IsEnumerableType(out innerType))
            {
                return innerType.IsSimpleType();
            }
            return false;
        }

        public static bool IsComplexType(this Type t, bool failIfEnumerable = true, bool failIfNullable = false)
        {
            Type innerType;
            if (t.IsNullableType(out innerType))
            {
                return failIfNullable ? false : !innerType.IsSimpleType();
            }
            if (t.IsEnumerableType(out innerType))
            {
                return failIfEnumerable ? false : !innerType.IsSimpleType();
            }
            return !t.IsSimpleType();
        }

        public static bool IsEnumerableType(this Type t, out Type elementType)
        {
            //if (t == typeof(string))
            //{
            //    elementType = null;
            //    return false;
            //}

            if (t.IsArray)
            {
                elementType = t.GetElementType();
                return true;
            }

            if (typeof(IEnumerable).IsAssignableFrom(t))
            {
                var elementTypes = t.GenericTypeArguments;
                if (elementTypes.Any())
                {
                    elementType = elementTypes[0];
                    return true;
                }
            }

            elementType = null;
            return false;
        }

        public static bool IsEnumerableType(this Type t)
        {
            Type dummy;
            return t.IsEnumerableType(out dummy);
        }

        public static bool TryGetClassToStringMethodInfo(this Type type, out MethodInfo methodInfo, out MethodSource source)
        {
            var info = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "ToString"
                                     && m.ReturnParameter?.ParameterType == typeof(string)
                                     && m.GetParameters().Length == 0
                                     && m.DeclaringType != typeof(object));
            if (info != null)
            {
                methodInfo = info;
                source = info.DeclaringType == type ? MethodSource.DeclaringClassMethod : MethodSource.ParentClassMethod;
                return true;
            }
            methodInfo = null;
            source = MethodSource.Invalid;
            return false;
        }

        public static bool ValidForRestriction(this MethodSource source, SourceRestrictions restriction)
        {
            return (MethodSource)((int)restriction & (int)source) == source;
        }

        public static bool AllowsSource(this SourceRestrictions restriction, MethodSource source)
        {
            return source.ValidForRestriction(restriction);
        }

        public static string WrapString(this string s, TextEnclosures wrapper)
        {
            return s.WrapString(wrapper, NullValueOptions.UsePlaceholderValue);
        }

        public static string WrapString(this string s, TextEnclosures wrapper, string nullStringValue, bool overrideNullStringWrapperType = true)
        {
            if (nullStringValue == null)
            {
                throw new ArgumentNullException(nameof(nullStringValue));
            }

            if (s == null)
            {
                if (overrideNullStringWrapperType)
                {
                    return nullStringValue.WrapString(wrapper);
                }
                return nullStringValue.WrapString(NullableTypeWrapper);
            }
            return s.WrapString(wrapper);
        }

        public static string WrapString(this string s, TextEnclosures wrapper, NullValueOptions nullOptions)
        {
            if (s == null)
            {
                switch (nullOptions)
                {
                    case NullValueOptions.None:
                        return null;
                    case NullValueOptions.UseEmptyString:
                        return string.Empty.WrapString(wrapper);
                    case NullValueOptions.UsePlaceholderValue:
                        return NullPlaceholder;
                    default:
                        throw new InvalidEnumArgumentException(nameof(nullOptions), (int)nullOptions, typeof(NullValueOptions));
                }
            }
            switch (wrapper)
            {
                case TextEnclosures.None:
                    return s;
                case TextEnclosures.CurlyBrackets:
                    return "{" + s + "}";
                case TextEnclosures.SquareBrackets:
                    return "[" + s + "]";
                case TextEnclosures.AngleBrackets:
                    return "<" + s + ">";
                case TextEnclosures.Parentheses:
                    return "(" + s + ")";
                case TextEnclosures.DoubleQuotes:
                    return "\"" + s + "\"";
                case TextEnclosures.SingleQuotes:
                    return "'" + s + "'";
                default:
                    throw new NotImplementedException($"No wrapping has been defined for {wrapper} yet.");
            }
        }
    }
}
