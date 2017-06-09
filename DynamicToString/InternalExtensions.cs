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
            set
            {
                _nullString = value; 
                PopulateRebuildQueue();
            }
        }

        public static TextEnclosures NullWrapper
        {
            get { return _nullWrapper; }
            set
            {
                _nullWrapper = value;
                PopulateRebuildQueue();
            }
        }

        public static TextEnclosures PrimitiveTypeWrapper
        {
            get { return _primitiveTypeWrapper; }
            set
            {
                _primitiveTypeWrapper = value; 
                PopulateRebuildQueue();
            }
        }

        public static TextEnclosures StringTypeWrapper
        {
            get { return _stringTypeWrapper; }
            set
            {
                _stringTypeWrapper = value; 
                PopulateRebuildQueue();
            }
        }

        public static TextEnclosures EnumerationTypeWrapper
        {
            get { return _enumerationTypeWrapper; }
            set
            {
                _enumerationTypeWrapper = value; 
                PopulateRebuildQueue();

            }
        }

        public static TextEnclosures NullableTypeWrapper
        {
            get { return _nullableTypeWrapper; }
            set
            {
                _nullableTypeWrapper = value; 
                PopulateRebuildQueue();

            }
        }

        public static TextEnclosures EnumerableTypeWrapper
        {
            get { return _enumerableTypeWrapper; }
            set
            {
                _enumerableTypeWrapper = value;
                PopulateRebuildQueue();

            }
        }

        public static TextEnclosures ComplexTypeWrapper
        {
            get { return _complexTypeWrapper; }
            set
            {
                _complexTypeWrapper = value; 
                PopulateRebuildQueue();

            }
        }

        private static SourceRestrictions _sourceRestrictionLevel;
        public static SourceRestrictions SourceRestrictionLevel
        {
            get { return _sourceRestrictionLevel; }
            set
            {
                var previousRestrictionLevel = _sourceRestrictionLevel;
                _sourceRestrictionLevel = value;

                if (previousRestrictionLevel != _sourceRestrictionLevel)
                {
                    // check for a more-restrictive change
                    if (!_sourceRestrictionLevel.HasFlag(previousRestrictionLevel))
                    {
                        foreach (var affectedType in _methodSources
                            .Where(kvp => kvp.Value != MethodSource.Custom && !kvp.Value.ValidForRestriction(value))
                            .Select(kvp => kvp.Key))
                        {
                            affectedType.AddToRebuildQueue();
                        }
                    }
                }
                else
                {
                    PopulateRebuildQueue();
                }
            } 
        }

        public static string NullPlaceholder => NullString.WrapString(NullWrapper);

        private static Dictionary<Type, MethodSource> _methodSources;
        private static Dictionary<Type, Func<object, string>> _methodCache;
        private static Dictionary<Type, HashSet<Type>> _dependentTypes;
        private static Dictionary<Type, HashSet<Type>> _typeUsages;

        private static HashSet<Type> _typeRebuildQueue;
        private static string _nullString;
        private static TextEnclosures _nullWrapper;
        private static TextEnclosures _primitiveTypeWrapper;
        private static TextEnclosures _stringTypeWrapper;
        private static TextEnclosures _enumerationTypeWrapper;
        private static TextEnclosures _nullableTypeWrapper;
        private static TextEnclosures _enumerableTypeWrapper;
        private static TextEnclosures _complexTypeWrapper;

        public static IReadOnlyDictionary<Type, Func<object, string>> MethodCache => new ReadOnlyDictionary<Type, Func<object, string>>(_methodCache);
        public static IReadOnlyDictionary<Type, MethodSource> MethodSources => new ReadOnlyDictionary<Type, MethodSource>(_methodSources);


        public static HashSet<Type> GetDependentTypes(this Type type)
        {
            HashSet<Type> dependencies;
            if (!_dependentTypes.TryGetValue(type, out dependencies)) {
                dependencies = new HashSet<Type>();
                if (type.IsSimpleType()) {
                    _dependentTypes.Add(type, dependencies);
                }
                else {
                    var allPropertyTypes = new HashSet<Type>(type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.PropertyType));
                    dependencies.UnionWith(allPropertyTypes);
                    foreach (var propertyType in allPropertyTypes) {
                        dependencies.UnionWith(GetDependentTypes(propertyType));
                    }
                    _dependentTypes.Add(type, dependencies);
                }
            }
            return dependencies;
        }

        public static void ResetTypeDependencies(bool clearUsages = false)
        {
            _dependentTypes = new Dictionary<Type, HashSet<Type>>();
            if (clearUsages)
            {
                ResetTypeUsages(false);
            }
        }

        public static void ResetTypeUsages(bool clearDependencies = false)
        {
            _typeUsages = new Dictionary<Type, HashSet<Type>>();
            if (clearDependencies)
            {
                ResetTypeDependencies(false);
            }
        }

        private static bool AddToRebuildQueue(this Type t, bool ignoreCustomFunctions = true)
        {
            MethodSource source;
            if (_methodSources.TryGetValue(t, out source))
            {
                if (ignoreCustomFunctions && source == MethodSource.Custom)
                {
                    return false;
                }
            }
            return _typeRebuildQueue.Add(t);
        }

        private static bool RemoveFromRebuildQueue(this Type t)
        {
            return _typeRebuildQueue.Remove(t);
        }

        private static IEnumerable<Type> PopulateRebuildQueue(bool ignoreCustomFunctions = true)
        {
            var scheduledKeys = new List<Type>();
            foreach (var typeKey in _methodSources.Keys.Union(_methodCache.Keys))
            {
                if (typeKey.AddToRebuildQueue(ignoreCustomFunctions))
                {
                    scheduledKeys.Add(typeKey);
                }
            }
            return scheduledKeys;
        }

        //private static IEnumerable<Type> RebuildQueuedTypes()
        //{
        //    foreach (var queuedType in _typeRebuildQueue.ToList())
        //    {
        //        queuedType.Get
        //    }
        //}

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


        private static bool UpdateCachedFunction(this Type t, bool ignoreCustomFunctions = true)
        {
            MethodSource source;
            if (_methodSources.TryGetValue(t, out source))
            {
                if (ignoreCustomFunctions && source == MethodSource.Custom)
                {
                    return false;
                }
            }
            var function = t.GenerateToStringFunction(out source);
            t.RegisterMethod(function, source);
            return true;
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
            ResetTypeDependencies();
            FullCacheSourceAndSettingsReset();
            ResetNullSettings();
            ResetWrapperSettings();
        }

        public static Func<object, string> GetToStringFunction(this Type t)
        {
            Func<object, string> function;
            if (_typeRebuildQueue.Contains(t) || !_methodCache.TryGetValue(t, out function))
            {
                MethodSource source;
                function = t.GenerateToStringFunction(out source);
                t.RegisterMethod(function, source);
                _typeRebuildQueue.Remove(t);
            }
            return function;
        }

        public static void RebuildToStringFunction(this Type t)
        {
            MethodSource source;
            var function = t.GenerateToStringFunction(out source);
            t.RegisterMethod(function, source);
            _typeRebuildQueue.Remove(t);
        }

        public static string AutoString(this object obj)
        {
            if (obj == null)
            {
                return NullPlaceholder;
            }
            var toStringMethod = obj.GetType().GetToStringFunction();
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
            Type innerType;
            if (objectType.IsNullableType(out innerType))
            {
                innerType.GetToStringFunction();
                source = MethodSource.AutoMethod;
                return NullableAutoStringMethod;
            }
            if (objectType.IsEnumerableType(out innerType))
            {
                innerType.GetToStringFunction();
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
