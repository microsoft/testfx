// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Walks two object graphs and reports the first structural difference, if any.
    /// </summary>
    private sealed partial class EquivalenceComparer
    {
        private static IEquatableOutcome InvokeIEquatable(object expected, object actual, Type declaredType, out Exception? thrown)
        {
            thrown = null;
            MethodInfo? equalsMethod = IEquatableEqualsCache.GetOrAdd(declaredType, GetIEquatableEqualsMethod);
            if (equalsMethod is null)
            {
                return IEquatableOutcome.NotApplicable;
            }

            // Both sides must be assignable to declaredType for the call to be safe. Since the method
            // came in via the static type, this is true at the public entry point. For nested members
            // (and collection elements), we resolve the method against the *declared* property/field/element
            // type, but the runtime values can be derived types. As long as actual is also assignable, we
            // can still call.
            if (!declaredType.IsInstanceOfType(expected) || !declaredType.IsInstanceOfType(actual))
            {
                return IEquatableOutcome.NotApplicable;
            }

            try
            {
                bool isEqual = (bool)equalsMethod.Invoke(expected, [actual])!;
                return isEqual ? IEquatableOutcome.Equal : IEquatableOutcome.NotEqual;
            }
            catch (TargetInvocationException ex)
            {
                ThrowIfAssertException(ex.InnerException);
                thrown = ex.InnerException ?? ex;
                return IEquatableOutcome.Threw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
            {
                thrown = ex;
                return IEquatableOutcome.Threw;
            }
        }

        private static MethodInfo? GetIEquatableEqualsMethod(Type declaredType)
        {
            if (declaredType == typeof(object) || declaredType == typeof(string))
            {
                // string is already handled via the primitive-like path; skip the IEquatable lookup.
                return null;
            }

            Type ieq;
            try
            {
                ieq = typeof(IEquatable<>).MakeGenericType(declaredType);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!ieq.IsAssignableFrom(declaredType))
            {
                return null;
            }

            // Resolve the IEquatable<T>.Equals(T) method on the interface itself. Invoking via reflection
            // performs virtual/interface dispatch, so this finds both implicit and explicit interface
            // implementations on the actual instance.
            return ieq.GetMethod("Equals", [declaredType]);
        }

        /// <summary>
        /// Rethrows a framework assertion exception (e.g., from a user-defined property getter or
        /// IEquatable.Equals that called <c>Assert.Fail</c>) unwrapped from <see cref="TargetInvocationException"/>,
        /// so user assertions propagate untouched instead of being rewritten as a structured equivalence failure.
        /// Uses <see cref="ExceptionDispatchInfo"/> to preserve the original throw site in the stack trace.
        /// </summary>
        private static void ThrowIfAssertException(Exception? inner)
        {
            if (inner is UnitTestAssertException)
            {
                ExceptionDispatchInfo.Capture(inner).Throw();
            }
        }

        private static Type GetEnumerableElementType(Type declaredType, Type expectedRuntimeType, Type actualRuntimeType)
            => TryGetEnumerableElementType(declaredType)
                ?? TryGetEnumerableElementType(expectedRuntimeType)
                ?? TryGetEnumerableElementType(actualRuntimeType)
                ?? typeof(object);

        private static Type? TryGetEnumerableElementType(Type type)
            => EnumerableElementTypeCache.GetOrAdd(type, static t =>
            {
                if (t.IsArray)
                {
                    return t.GetElementType();
                }

                if (t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return t.GetGenericArguments()[0];
                }

                Type? best = null;
                foreach (Type i in t.GetInterfaces())
                {
                    if (i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        Type elem = i.GetGenericArguments()[0];

                        // If multiple IEnumerable<T> are implemented, prefer the most specific (non-object) one.
                        if (best is null || best == typeof(object))
                        {
                            best = elem;
                        }
                    }
                }

                return best;
            });

        private static Type GetDictionaryValueType(Type declaredType, Type expectedRuntimeType, Type actualRuntimeType)
            => TryGetDictionaryValueType(declaredType)
                ?? TryGetDictionaryValueType(expectedRuntimeType)
                ?? TryGetDictionaryValueType(actualRuntimeType)
                ?? typeof(object);

        private static Type? TryGetDictionaryValueType(Type type)
            => DictionaryValueTypeCache.GetOrAdd(type, static t =>
            {
                if (t.IsConstructedGenericType)
                {
                    Type def = t.GetGenericTypeDefinition();
                    if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                    {
                        return t.GetGenericArguments()[1];
                    }
                }

                foreach (Type i in t.GetInterfaces())
                {
                    if (i.IsConstructedGenericType)
                    {
                        Type def = i.GetGenericTypeDefinition();
                        if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                        {
                            return i.GetGenericArguments()[1];
                        }
                    }
                }

                return null;
            });

        private static bool IsPrimitiveLike(Type type)
            => IsPrimitiveLikeCache.GetOrAdd(type, static t =>
            {
                if (t.IsPrimitive || t.IsEnum)
                {
                    return true;
                }

                if (t == typeof(string) || t == typeof(decimal) ||
                    t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
                    t == typeof(TimeSpan) || t == typeof(Guid) ||
                    t == typeof(Uri) || t == typeof(Type) ||
                    t == typeof(Version))
                {
                    return true;
                }

                // DateOnly / TimeOnly / Half / Int128 exist only on newer TFMs; match by full name to avoid #if.
                string? fullName = t.FullName;
                return fullName is "System.DateOnly" or "System.TimeOnly" or "System.Half" or "System.Int128" or "System.UInt128";
            });

        private static string AppendMember(string parent, string name)
            => parent.Length == 0 ? name : $"{parent}.{name}";

        private static string AppendIndex(string parent, int index)
            => $"{parent}[{index.ToString(CultureInfo.InvariantCulture)}]";

        private static string AppendDictionaryKey(string parent, object key)
        {
            string keyPart = $"[{AssertionValueRenderer.RenderValue(key)}]";
            return parent.Length == 0 ? keyPart : parent + keyPart;
        }
    }
}
