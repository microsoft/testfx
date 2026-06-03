// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Walks two object graphs and reports the first structural difference, if any.
    /// </summary>
    private sealed partial class EquivalenceComparer
    {
        private EquivalenceMismatch? CompareMembers(object expected, object actual, Type expectedType, Type actualType, string path, int depth)
        {
            MemberLookup expectedMembers = GetMembers(expectedType);

            // When strict mode is on AND runtime types differ, ensure the actual side declares no extra
            // members beyond what's present on expected.
            if (_strict && expectedType != actualType)
            {
                MemberLookup actualMembers = GetMembers(actualType);

                List<string>? extras = null;
                foreach (MemberAccessor am in actualMembers.Sorted)
                {
                    if (!expectedMembers.ByName.ContainsKey(am.Name))
                    {
                        (extras ??= []).Add(am.Name);
                    }
                }

                if (extras is { Count: > 0 })
                {
                    return EquivalenceMismatch.ExtraMembers(path, extras);
                }
            }

            foreach (MemberAccessor member in expectedMembers.Sorted)
            {
                MemberAccessor? matchingActual = FindMember(actualType, member.Name);
                string childPath = AppendMember(path, member.Name);

                if (matchingActual is null)
                {
                    return EquivalenceMismatch.MissingMember(childPath, member.Name);
                }

                object? expectedValue;
                object? actualValue;
                try
                {
                    expectedValue = member.GetValue(expected);
                }
                catch (TargetInvocationException ex)
                {
                    ThrowIfAssertException(ex.InnerException);
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: true, ex.InnerException ?? ex);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: true, ex);
                }

                try
                {
                    actualValue = matchingActual.GetValue(actual);
                }
                catch (TargetInvocationException ex)
                {
                    ThrowIfAssertException(ex.InnerException);
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: false, ex.InnerException ?? ex);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not UnitTestAssertException)
                {
                    return EquivalenceMismatch.MemberAccessFailure(childPath, isExpected: false, ex);
                }

                EquivalenceMismatch? nested = Compare(expectedValue, actualValue, member.MemberType, childPath, depth + 1);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static MemberLookup GetMembers(Type type)
            => MemberCache.GetOrAdd(type, static t =>
            {
                // Collect candidates per name, preferring the most-derived declaration so that
                // `new`-shadowed properties/fields are deterministically resolved to the most-derived
                // member regardless of metadata ordering.
#pragma warning disable IDE0028 // Collection initialization can be simplified — target-typed `new` cannot pass the comparer in the same syntactic form expected.
                Dictionary<string, MemberAccessor> byName = new(StringComparer.Ordinal);
                Dictionary<string, Type> declaringTypes = new(StringComparer.Ordinal);
#pragma warning restore IDE0028

                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    MethodInfo? getter = p.GetGetMethod(nonPublic: false);
                    if (getter is null)
                    {
                        continue;
                    }

                    TryRegisterMostDerived(byName, declaringTypes, p, new MemberAccessor(p.Name, p.PropertyType, p));
                }

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.IsStatic)
                    {
                        continue;
                    }

                    TryRegisterMostDerived(byName, declaringTypes, f, new MemberAccessor(f.Name, f.FieldType, f));
                }

                var sorted = new MemberAccessor[byName.Count];
                int i = 0;
                foreach (MemberAccessor accessor in byName.Values)
                {
                    sorted[i++] = accessor;
                }

                Array.Sort(sorted, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

                return new MemberLookup(sorted, byName);
            });

        private static void TryRegisterMostDerived(Dictionary<string, MemberAccessor> byName, Dictionary<string, Type> declaringTypes, MemberInfo member, MemberAccessor accessor)
        {
            if (byName.ContainsKey(member.Name)
                && !IsMoreDerivedThan(member.DeclaringType, declaringTypes[member.Name]))
            {
                return;
            }

            byName[member.Name] = accessor;
            declaringTypes[member.Name] = member.DeclaringType ?? typeof(object);
        }

        private static bool IsMoreDerivedThan(Type? candidate, Type? incumbent)
            => candidate is not null
                && incumbent is not null
                && candidate != incumbent
                && incumbent.IsAssignableFrom(candidate);

        private static MemberAccessor? FindMember(Type type, string name)
            => GetMembers(type).ByName.TryGetValue(name, out MemberAccessor? found) ? found : null;
    }
}
