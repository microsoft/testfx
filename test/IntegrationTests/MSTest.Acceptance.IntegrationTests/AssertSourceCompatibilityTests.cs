// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Compiles manually maintained consumer call shapes and requires every public <see cref="Assert"/> method family
/// to have a representative scenario. This complements binary API compatibility tooling with source coverage.
/// </summary>
[TestClass]
public sealed class AssertSourceCompatibilityTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "AssertSourceCompatibility";

    private const string ConsumerSource = """
        #file AssertSourceCompatibility.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>$TargetFrameworks$</TargetFramework>
            <OutputType>Library</OutputType>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <!-- C# 12 remains a supported consumer language version. Later compilers can change
                 overload resolution, so using the SDK default would not preserve this regression test. -->
            <LangVersion>12.0</LangVersion>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
          </ItemGroup>
        </Project>

        #file AssertCallShapes.cs
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Globalization;
        using System.Text.RegularExpressions;
        using System.Threading.Tasks;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace AssertSourceCompatibility;

        // This code is compiled but never executed. Except for ExplicitGenericArrayCalls, keep the calls
        // implicit: casts or explicit type arguments would hide the overload-resolution regressions this
        // asset is intended to catch.
        internal static class AssertCallShapes
        {
            internal static void ArrayAndEnumerableCalls(
                int[] values,
                int[] other,
                object?[] objects,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct(values);
                Assert.AreAllDistinct(values, comparer);
                Assert.AreAllNotNull(objects);
                Assert.AreAllOfType<int>(values);
                Assert.AreAllOfType(typeof(int), values);

                Assert.AreEquivalent(values, other);
                Assert.AreEquivalent(values, other, true);
                Assert.AreNotEquivalent(values, other);
                Assert.AreNotEquivalent(values, other, true);

                Assert.AreSequenceEqual(values, other);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, other, SequenceOrder.InAnyOrder);
                Assert.AreSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);

                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain(1, values);
                Assert.DoesNotContain(1, values, comparer);
                Assert.DoesNotContain(predicate, values);

                Assert.ContainsAll(values, other);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(values);
                _ = Assert.ContainsSingle(predicate, values);
                Assert.HasCount(1, values);
                Assert.IsEmpty(values);
                Assert.IsNotEmpty(values);
            }

            // General constrained collection forwarders have additional generic parameters, so explicit
            // one-type-argument calls require the exact-array overloads to remain source compatible.
            internal static void ExplicitGenericArrayCalls(
                int[] values,
                int[] other,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct<int>(values);
                Assert.AreAllDistinct<int>(values, comparer);
                Assert.AreAllNotNull<int>(values);
                Assert.AreSequenceEqual<int>(values, other);
                Assert.AreSequenceEqual<int>(values, other, SequenceOrder.InAnyOrder);
                Assert.AreSequenceEqual<int>(values, other, comparer);
                Assert.AreSequenceEqual<int>(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual<int>(values, other);
                Assert.AreNotSequenceEqual<int>(values, other, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual<int>(values, other, comparer);
                Assert.AreNotSequenceEqual<int>(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains<int>(1, values);
                Assert.Contains<int>(1, values, comparer);
                Assert.Contains<int>(predicate, values);
                Assert.DoesNotContain<int>(1, values);
                Assert.DoesNotContain<int>(1, values, comparer);
                Assert.DoesNotContain<int>(predicate, values);
                Assert.ContainsAll<int>(values, other);
                Assert.ContainsAll<int>(values, other, comparer);
                Assert.DoesNotContainAll<int>(values, other);
                Assert.DoesNotContainAll<int>(values, other, comparer);
                _ = Assert.ContainsSingle<int>(values);
                _ = Assert.ContainsSingle<int>(predicate, values);
                Assert.HasCount<int>(1, values);
                Assert.IsEmpty<int>(values);
                Assert.IsNotEmpty<int>(values);
                _ = Assert.ContainsSingle<int>(values, $"contains single {values.Length}");
                Assert.HasCount<int>(1, values, $"has count {values.Length}");
                Assert.IsEmpty<int>(values, $"is empty {values.Length}");
                Assert.IsNotEmpty<int>(values, $"is not empty {values.Length}");

                Assert.AreAllDistinct<int>(default!);
                Assert.AreAllDistinct<int>(default!, comparer);
                Assert.AreAllNotNull<int>(default!);
                Assert.AreSequenceEqual<int>(default!, default!);
                Assert.AreSequenceEqual<int>(default!, default!, SequenceOrder.InAnyOrder);
                Assert.AreSequenceEqual<int>(default!, default!, comparer);
                Assert.AreSequenceEqual<int>(default!, default!, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual<int>(default!, default!);
                Assert.AreNotSequenceEqual<int>(default!, default!, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual<int>(default!, default!, comparer);
                Assert.AreNotSequenceEqual<int>(default!, default!, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains<int>(1, default!);
                Assert.Contains<int>(1, default!, comparer);
                Assert.Contains<int>(predicate, default!);
                Assert.DoesNotContain<int>(1, default!);
                Assert.DoesNotContain<int>(1, default!, comparer);
                Assert.DoesNotContain<int>(predicate, default!);
                Assert.ContainsAll<int>(default!, default!);
                Assert.ContainsAll<int>(default!, default!, comparer);
                Assert.DoesNotContainAll<int>(default!, default!);
                Assert.DoesNotContainAll<int>(default!, default!, comparer);
                _ = Assert.ContainsSingle<int>(default!);
                _ = Assert.ContainsSingle<int>(predicate, default!);
                Assert.HasCount<int>(1, default!);
                Assert.IsEmpty<int>(default!);
                Assert.IsNotEmpty<int>(default!);
            }

            internal static void GenericEnumerableCalls(
                IEnumerable<int> values,
                IEnumerable<int> other,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct(values);
                Assert.AreAllDistinct(values, comparer);
                Assert.AreAllNotNull(values);
                Assert.AreAllOfType<int>(values);
                Assert.AreAllOfType(typeof(int), values);
                Assert.AreSequenceEqual(values, other);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, other, SequenceOrder.InAnyOrder);
                Assert.AreSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain(1, values);
                Assert.DoesNotContain(1, values, comparer);
                Assert.DoesNotContain(predicate, values);
                Assert.ContainsAll(values, other);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(values);
                _ = Assert.ContainsSingle(predicate, values);
                Assert.HasCount(1, values);
                Assert.IsEmpty(values);
                Assert.IsNotEmpty(values);
            }

            internal static void NonNullableReferenceCollectionCalls(
                List<string> list,
                HashSet<string> set,
                string[] array,
                IEnumerable<string> enumerable,
                IEqualityComparer<string> comparer)
            {
                Assert.Contains("Name", list);
                Assert.DoesNotContain("Missing", list);
                Assert.Contains("Name", list, comparer);
                Assert.DoesNotContain("Missing", list, comparer);
                Assert.Contains("Name", set, comparer);
                Assert.DoesNotContain("Missing", set, comparer);
                Assert.Contains("Name", array, comparer);
                Assert.DoesNotContain("Missing", array, comparer);
                Assert.Contains("Name", enumerable, comparer);
                Assert.DoesNotContain("Missing", enumerable, comparer);
            }

            internal static void NullableReferenceCollectionCalls(
                List<string?> list,
                HashSet<string?> set,
                string?[] array,
                IEnumerable<string?> enumerable,
                ICollection<string?> collection,
                IReadOnlyCollection<string?> readOnlyCollection,
                ArraySegment<string?> segment,
                ImmutableArray<string?> immutableArray,
                DualConvertibleEnumerable<string?> dualConvertible,
                IEqualityComparer<string?> comparer)
            {
                string? nullValue = null;
                Func<string?, bool> predicate = value => value is null;

                Assert.Contains("Name", list);
                Assert.DoesNotContain("Missing", list);
                Assert.Contains(nullValue, list);
                Assert.DoesNotContain(nullValue, list);
                Assert.Contains("Name", list, comparer);
                Assert.DoesNotContain("Missing", list, comparer);
                Assert.Contains("NAME", list, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain("Missing", list, StringComparer.OrdinalIgnoreCase);
                Assert.Contains(predicate, list);
                Assert.DoesNotContain(predicate, list);

                Assert.Contains("Name", set);
                Assert.DoesNotContain("Missing", set);
                Assert.Contains(nullValue, set);
                Assert.DoesNotContain(nullValue, set);
                Assert.Contains("Name", set, comparer);
                Assert.DoesNotContain("Missing", set, comparer);
                Assert.Contains(predicate, set);
                Assert.DoesNotContain(predicate, set);

                Assert.Contains("Name", array);
                Assert.DoesNotContain("Missing", array);
                Assert.Contains(nullValue, array);
                Assert.DoesNotContain(nullValue, array);
                Assert.Contains("Name", array, comparer);
                Assert.DoesNotContain("Missing", array, comparer);
                Assert.Contains(predicate, array);
                Assert.DoesNotContain(predicate, array);

                Assert.Contains("Name", enumerable);
                Assert.DoesNotContain("Missing", enumerable);
                Assert.Contains(nullValue, enumerable);
                Assert.DoesNotContain(nullValue, enumerable);
                Assert.Contains("Name", enumerable, comparer);
                Assert.DoesNotContain("Missing", enumerable, comparer);
                Assert.Contains(predicate, enumerable);
                Assert.DoesNotContain(predicate, enumerable);

                Assert.Contains("Name", collection);
                Assert.DoesNotContain("Missing", collection);
                Assert.Contains(nullValue, collection);
                Assert.DoesNotContain(nullValue, collection);
                Assert.Contains("Name", readOnlyCollection);
                Assert.DoesNotContain("Missing", readOnlyCollection);
                Assert.Contains(nullValue, readOnlyCollection);
                Assert.DoesNotContain(nullValue, readOnlyCollection);

                Assert.Contains("Name", segment);
                Assert.DoesNotContain("Missing", segment);
                Assert.Contains(nullValue, segment);
                Assert.DoesNotContain(nullValue, segment);
                Assert.Contains("Name", immutableArray);
                Assert.DoesNotContain("Missing", immutableArray);
                Assert.Contains(nullValue, immutableArray);
                Assert.DoesNotContain(nullValue, immutableArray);

                Assert.Contains("Name", dualConvertible);
                Assert.DoesNotContain("Missing", dualConvertible);
                Assert.Contains(nullValue, dualConvertible);
                Assert.DoesNotContain(nullValue, dualConvertible);
                Assert.Contains("Name", dualConvertible, comparer);
                Assert.DoesNotContain("Missing", dualConvertible, comparer);
                Assert.Contains(predicate, dualConvertible);
                Assert.DoesNotContain(predicate, dualConvertible);

                Assert.AreAllDistinct(list, comparer);
                Assert.AreSequenceEqual(list, set, comparer);
                Assert.AreSequenceEqual(list, array, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(list, set, comparer);
                Assert.AreNotSequenceEqual(list, array, comparer, SequenceOrder.InAnyOrder);
                Assert.ContainsAll(list, set);
                Assert.ContainsAll(list, set, comparer);
                Assert.DoesNotContainAll(list, set);
                Assert.DoesNotContainAll(list, set, comparer);
                _ = Assert.ContainsSingle(predicate, list);
                Assert.HasCount(1, list);
                Assert.IsEmpty(list);
                Assert.IsNotEmpty(list);
            }

            internal static void NullableValueCollectionCalls(
                List<int?> list,
                HashSet<int?> set,
                int?[] array,
                IEnumerable<int?> enumerable)
            {
                int? nullValue = null;

                Assert.Contains(1, list);
                Assert.DoesNotContain(0, list);
                Assert.Contains(nullValue, list);
                Assert.DoesNotContain(nullValue, list);
                Assert.Contains(1, set);
                Assert.DoesNotContain(0, set);
                Assert.Contains(nullValue, set);
                Assert.DoesNotContain(nullValue, set);
                Assert.Contains(1, array);
                Assert.DoesNotContain(0, array);
                Assert.Contains(nullValue, array);
                Assert.DoesNotContain(nullValue, array);
                Assert.Contains(1, enumerable);
                Assert.DoesNotContain(0, enumerable);
                Assert.Contains(nullValue, enumerable);
                Assert.DoesNotContain(nullValue, enumerable);
            }

            internal static void StringCharacterCollectionCalls(
                string values,
                string other,
                IEqualityComparer<char> comparer)
            {
                Func<char, bool> predicate = value => value == 'a';

                Assert.AreAllDistinct(values, comparer);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains('a', values);
                Assert.Contains('a', values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain('z', values);
                Assert.DoesNotContain('z', values, comparer);
                Assert.DoesNotContain(predicate, values);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(predicate, values);
            }

            internal static void ArraySegmentCalls(
                ArraySegment<int> values,
                ArraySegment<int> other,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct(values, comparer);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain(0, values);
                Assert.DoesNotContain(0, values, comparer);
                Assert.DoesNotContain(predicate, values);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(predicate, values);
            }

            internal static void ImmutableArrayCalls(
                ImmutableArray<int> values,
                ImmutableArray<int> other,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct(values, comparer);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain(0, values);
                Assert.DoesNotContain(0, values, comparer);
                Assert.DoesNotContain(predicate, values);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(predicate, values);
            }

            internal static void CollectionExpressionCalls(IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct([1, 2, 3], comparer);
                Assert.AreSequenceEqual([1, 2], [1, 2], comparer);
                Assert.AreSequenceEqual([1, 2], [2, 1], comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual([1, 2], [1, 3], comparer);
                Assert.AreNotSequenceEqual([1, 2], [2, 1], comparer, SequenceOrder.InAnyOrder);
                Assert.Contains(1, [1, 2, 3]);
                Assert.Contains(1, [1, 2, 3], comparer);
                Assert.Contains(predicate, [1, 2, 3]);
                Assert.DoesNotContain(0, [1, 2, 3]);
                Assert.DoesNotContain(0, [1, 2, 3], comparer);
                Assert.DoesNotContain(predicate, [1, 2, 3]);
                Assert.ContainsAll([1, 2], [1, 2, 3], comparer);
                Assert.DoesNotContainAll([0, 1], [1, 2, 3], comparer);
                _ = Assert.ContainsSingle(predicate, [1]);
            }

            internal static void CustomDualConvertibleCollectionCalls(
                DualConvertibleEnumerable<int> values,
                DualConvertibleEnumerable<int> other,
                ArraySegment<int> segment,
                IEqualityComparer<int> comparer)
            {
                Func<int, bool> predicate = value => value > 0;

                Assert.AreAllDistinct(values, comparer);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreSequenceEqual(values, segment, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(segment, values, comparer);
                Assert.AreNotSequenceEqual(values, segment, comparer, SequenceOrder.InAnyOrder);
                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains(predicate, values);
                Assert.DoesNotContain(0, values);
                Assert.DoesNotContain(0, values, comparer);
                Assert.DoesNotContain(predicate, values);
                Assert.ContainsAll(values, segment, comparer);
                Assert.DoesNotContainAll(segment, values, comparer);
                _ = Assert.ContainsSingle(predicate, values);
            }

            internal static void NonGenericEnumerableCalls(
                IEnumerable values,
                IEnumerable other,
                IEqualityComparer comparer)
            {
                Assert.AreAllDistinct(values);
                Assert.AreAllDistinct(values, comparer);
                Assert.AreAllNotNull(values);
                Assert.AreAllOfType(typeof(int), values);
                Assert.AreAllOfType<int>(values);
                Assert.AreSequenceEqual(values, other);
                Assert.AreSequenceEqual(values, other, comparer);
                Assert.AreNotSequenceEqual(values, other);
                Assert.AreNotSequenceEqual(values, other, comparer);
                Assert.Contains(1, values);
                Assert.Contains(1, values, comparer);
                Assert.Contains((object? value) => value is int, values);
                Assert.DoesNotContain(1, values);
                Assert.DoesNotContain(1, values, comparer);
                Assert.DoesNotContain((object? value) => value is string, values);
                Assert.ContainsAll(values, other);
                Assert.ContainsAll(values, other, comparer);
                Assert.DoesNotContainAll(values, other);
                Assert.DoesNotContainAll(values, other, comparer);
                _ = Assert.ContainsSingle(values);
                _ = Assert.ContainsSingle((object? value) => value is int, values);
                Assert.HasCount(1, values);
                Assert.IsEmpty(values);
                Assert.IsNotEmpty(values);
            }

            internal static void SpanAndMemoryCalls(
                Span<int> span,
                ReadOnlySpan<int> readOnlySpan,
                Memory<int> memory,
                ReadOnlyMemory<int> readOnlyMemory,
                IEqualityComparer<int> comparer)
            {
                Assert.AreAllDistinct(span);
                Assert.AreAllDistinct(readOnlySpan);
                Assert.AreAllDistinct(memory);
                Assert.AreAllDistinct(readOnlyMemory, comparer);
                Assert.AreAllNotNull(span);
                Assert.AreAllNotNull(readOnlySpan);
                Assert.AreAllNotNull(memory);
                Assert.AreAllNotNull(readOnlyMemory);
                Assert.AreAllOfType<int, int>(span);
                Assert.AreAllOfType<int, int>(readOnlySpan);
                Assert.AreAllOfType<int, int>(memory);
                Assert.AreAllOfType(typeof(int), readOnlyMemory);

                Assert.AreEquivalent(span, span);
                Assert.AreEquivalent(readOnlySpan, readOnlySpan, true);
                Assert.AreEquivalent(memory, memory);
                Assert.AreEquivalent(readOnlyMemory, readOnlyMemory, true);
                Assert.AreNotEquivalent(span, span);
                Assert.AreNotEquivalent(readOnlySpan, readOnlySpan, true);
                Assert.AreNotEquivalent(memory, memory);
                Assert.AreNotEquivalent(readOnlyMemory, readOnlyMemory, true);

                Assert.AreSequenceEqual(span, span);
                Assert.AreSequenceEqual(readOnlySpan, readOnlySpan, comparer);
                Assert.AreSequenceEqual(memory, memory, SequenceOrder.InAnyOrder);
                Assert.AreSequenceEqual(readOnlyMemory, readOnlyMemory, comparer, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(span, span);
                Assert.AreNotSequenceEqual(readOnlySpan, readOnlySpan, comparer);
                Assert.AreNotSequenceEqual(memory, memory, SequenceOrder.InAnyOrder);
                Assert.AreNotSequenceEqual(readOnlyMemory, readOnlyMemory, comparer, SequenceOrder.InAnyOrder);

                Assert.Contains(1, span);
                Assert.Contains(1, span, comparer);
                Assert.Contains((int value) => value > 0, span);
                Assert.Contains(1, readOnlySpan);
                Assert.Contains(1, readOnlySpan, comparer);
                Assert.Contains((int value) => value > 0, readOnlySpan);
                Assert.Contains(1, memory);
                Assert.Contains(1, memory, comparer);
                Assert.Contains((int value) => value > 0, memory);
                Assert.Contains(1, readOnlyMemory);
                Assert.Contains(1, readOnlyMemory, comparer);
                Assert.Contains((int value) => value > 0, readOnlyMemory);
                Assert.DoesNotContain(1, span);
                Assert.DoesNotContain(1, span, comparer);
                Assert.DoesNotContain((int value) => value < 0, span);
                Assert.DoesNotContain(1, readOnlySpan);
                Assert.DoesNotContain(1, readOnlySpan, comparer);
                Assert.DoesNotContain((int value) => value < 0, readOnlySpan);
                Assert.DoesNotContain(1, memory);
                Assert.DoesNotContain(1, memory, comparer);
                Assert.DoesNotContain((int value) => value < 0, memory);
                Assert.DoesNotContain(1, readOnlyMemory);
                Assert.DoesNotContain(1, readOnlyMemory, comparer);
                Assert.DoesNotContain((int value) => value < 0, readOnlyMemory);

                Assert.ContainsAll(span, span);
                Assert.ContainsAll(span, span, comparer);
                Assert.ContainsAll(readOnlySpan, readOnlySpan);
                Assert.ContainsAll(readOnlySpan, readOnlySpan, comparer);
                Assert.ContainsAll(memory, memory);
                Assert.ContainsAll(memory, memory, comparer);
                Assert.ContainsAll(readOnlyMemory, readOnlyMemory);
                Assert.ContainsAll(readOnlyMemory, readOnlyMemory, comparer);
                Assert.DoesNotContainAll(span, span);
                Assert.DoesNotContainAll(span, span, comparer);
                Assert.DoesNotContainAll(readOnlySpan, readOnlySpan);
                Assert.DoesNotContainAll(readOnlySpan, readOnlySpan, comparer);
                Assert.DoesNotContainAll(memory, memory);
                Assert.DoesNotContainAll(memory, memory, comparer);
                Assert.DoesNotContainAll(readOnlyMemory, readOnlyMemory);
                Assert.DoesNotContainAll(readOnlyMemory, readOnlyMemory, comparer);

                _ = Assert.ContainsSingle(span);
                _ = Assert.ContainsSingle((int value) => value > 0, readOnlySpan);
                _ = Assert.ContainsSingle(memory);
                _ = Assert.ContainsSingle((int value) => value > 0, readOnlyMemory);
                Assert.HasCount(1, span);
                Assert.HasCount(1, readOnlySpan);
                Assert.HasCount(1, memory);
                Assert.HasCount(1, readOnlyMemory);
                Assert.IsEmpty(span);
                Assert.IsEmpty(readOnlySpan);
                Assert.IsEmpty(memory);
                Assert.IsEmpty(readOnlyMemory);
                Assert.IsNotEmpty(span);
                Assert.IsNotEmpty(readOnlySpan);
                Assert.IsNotEmpty(memory);
                Assert.IsNotEmpty(readOnlyMemory);
            }

            internal static void ScalarAndStringCalls(object? value, object? other)
            {
                Assert.AreEqual(1, 1);
                Assert.AreEqual(1, 1, EqualityComparer<int>.Default);
                Assert.AreEqual(1m, 1m, 0.1m);
                Assert.AreEqual(1d, 1d, 0.1d);
                Assert.AreEqual(1f, 1f, 0.1f);
                Assert.AreEqual(1L, 1L, 1L);
                Assert.AreEqual("a", "A", true);
                Assert.AreEqual("a", "A", true, CultureInfo.InvariantCulture);
                Assert.AreNotEqual(1, 2);
                Assert.AreNotEqual(1, 2, EqualityComparer<int>.Default);
                Assert.AreNotEqual(1m, 2m, 0.1m);
                Assert.AreNotEqual(1d, 2d, 0.1d);
                Assert.AreNotEqual(1f, 2f, 0.1f);
                Assert.AreNotEqual(1L, 2L, 1L);
                Assert.AreNotEqual("a", "b", true);
                Assert.AreNotEqual("a", "b", true, CultureInfo.InvariantCulture);
                Assert.AreSame(value, other);
                Assert.AreNotSame(value, other);

                Assert.IsTrue(true);
                Assert.IsFalse(false);
                Assert.IsNull(value);
                Assert.IsNotNull(value);
                _ = Assert.IsInstanceOfType<string>(value);
                Assert.IsInstanceOfType(value, typeof(string));
                Assert.IsNotInstanceOfType<string>(value);
                Assert.IsNotInstanceOfType(value, typeof(string));
                _ = Assert.IsExactInstanceOfType<string>(value);
                Assert.IsExactInstanceOfType(value, typeof(string));
                Assert.IsNotExactInstanceOfType<string>(value);
                Assert.IsNotExactInstanceOfType(value, typeof(string));

                Assert.IsGreaterThan(0, 1);
                Assert.IsGreaterThanOrEqualTo(1, 1);
                Assert.IsLessThan(2, 1);
                Assert.IsLessThanOrEqualTo(1, 1);
                Assert.IsInRange(0, 2, 1);
                Assert.IsPositive(1);
                Assert.IsNegative(-1);

                Assert.Contains("b", "abc");
                Assert.Contains("B", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("z", "abc");
                Assert.DoesNotContain("Z", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("a", "abc");
                Assert.StartsWith("A", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotStartWith("z", "abc");
                Assert.DoesNotStartWith("Z", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith("c", "abc");
                Assert.EndsWith("C", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotEndWith("z", "abc");
                Assert.DoesNotEndWith("Z", "abc", StringComparison.OrdinalIgnoreCase);
                Assert.MatchesRegex("a.c", "abc");
                Assert.MatchesRegex(new Regex("a.c"), "abc");
                Assert.DoesNotMatchRegex("z", "abc");
                Assert.DoesNotMatchRegex(new Regex("z"), "abc");
            }

            internal static async Task ExceptionCallsAsync()
            {
                _ = Assert.Throws<InvalidOperationException>(() => Throw());
                _ = Assert.Throws<InvalidOperationException>(() => ReturnValue());
                _ = Assert.Throws<InvalidOperationException>(() => Throw(), exception => exception?.Message ?? "");
                _ = Assert.ThrowsExactly<InvalidOperationException>(() => Throw());
                _ = Assert.ThrowsExactly<InvalidOperationException>(() => ReturnValue());
                _ = Assert.ThrowsExactly<InvalidOperationException>(() => Throw(), exception => exception?.Message ?? "");
                _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.CompletedTask);
                _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.CompletedTask, exception => exception?.Message ?? "");
                _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Task.CompletedTask);
                _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Task.CompletedTask, exception => exception?.Message ?? "");
            }

            internal static void TerminalCalls(bool fail)
            {
                if (fail)
                {
                    Assert.Fail();
                }
                else
                {
                    Assert.Inconclusive();
                }
            }

            internal static void ExperimentalCalls()
            {
        #pragma warning disable MSTESTEXP
                using IDisposable genericFormatter = Assert.AddValueFormatter<int>(value => value.ToString(CultureInfo.InvariantCulture));
                using IDisposable formatterFactory = Assert.AddValueFormatter(next => value => next(value));
                using IDisposable scope = Assert.Scope();
        #pragma warning restore MSTESTEXP
            }

            private static void Throw() => throw new InvalidOperationException();

            private static object ReturnValue() => new object();
        }

        internal sealed class DualConvertibleEnumerable<T> : IEnumerable<T>
        {
            private readonly T[] _items;

            internal DualConvertibleEnumerable(params T[] items)
                => _items = items;

            public static implicit operator Span<T>(DualConvertibleEnumerable<T> collection)
                => collection._items;

            public static implicit operator ReadOnlySpan<T>(DualConvertibleEnumerable<T> collection)
                => collection._items;

            public IEnumerator<T> GetEnumerator()
                => ((IEnumerable<T>)_items).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();
        }
        """;

    private static readonly Regex PublicAssertMethodRegex = new(
        @"^(?:\[[^\]]+\])*\s*static Microsoft\.VisualStudio\.TestTools\.UnitTesting\.Assert\.(?<name>[A-Za-z0-9]+)(?:<|\()",
        RegexOptions.CultureInvariant);

    private static readonly Regex ConsumerAssertCallRegex = new(
        @"^(?!\s*//)[^\r\n]*\bAssert\.(?<name>[A-Za-z0-9]+)\s*(?:<[^;\r\n]+>)?\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(GetSourceCompatibilityTargetFrameworks))]
    public async Task PublicAssertCallShapes_CompileWithCSharp12(string targetFramework)
    {
        VerifyEveryPublicAssertMethodFamilyHasAConsumerCall();

        string source = ConsumerSource
            .PatchTargetFrameworks(targetFramework)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(AssetName, source);

        string isolatedPackages = Path.Combine(testAsset.TargetAssetPath, ".nuget-packages");
        var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = isolatedPackages,
        };

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build -c Release {testAsset.TargetAssetPath}",
            environmentVariables: environmentVariables,
            workingDirectory: testAsset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
    }

    private static IEnumerable<object[]> GetSourceCompatibilityTargetFrameworks()
    {
        yield return [TargetFrameworks.NetCurrent];
        yield return ["net8.0"];
    }

    private static void VerifyEveryPublicAssertMethodFamilyHasAConsumerCall()
    {
        // This guard catches entirely new Assert families. It cannot detect a new overload in an existing
        // family, so every overload change must also add representative implicit consumer call shapes above.
        string publicApiDirectory = Path.Combine(
            RootFinder.Find(),
            "src",
            "TestFramework",
            "TestFramework",
            "PublicAPI");

        string[] publicApiFiles =
        [
            Path.Combine(publicApiDirectory, "PublicAPI.Shipped.txt"),
            Path.Combine(publicApiDirectory, "PublicAPI.Unshipped.txt"),
            Path.Combine(publicApiDirectory, "net", "PublicAPI.Shipped.txt"),
            Path.Combine(publicApiDirectory, "net", "PublicAPI.Unshipped.txt"),
            Path.Combine(publicApiDirectory, "net9.0", "PublicAPI.Shipped.txt"),
            Path.Combine(publicApiDirectory, "net9.0", "PublicAPI.Unshipped.txt"),
        ];

        var publicMethodFamilies = publicApiFiles
            .SelectMany(File.ReadLines)
            .Select(line => PublicAssertMethodRegex.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["name"].Value)
            .Where(name => name is not nameof(object.Equals) and not nameof(object.ReferenceEquals))
            .ToHashSet(StringComparer.Ordinal);

        var coveredMethodFamilies = ConsumerAssertCallRegex
            .Matches(ConsumerSource)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] missingMethodFamilies = publicMethodFamilies
            .Except(coveredMethodFamilies)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            missingMethodFamilies.Length,
            $"Add representative C# 12 consumer calls for: {string.Join(", ", missingMethodFamilies)}");
    }
}
