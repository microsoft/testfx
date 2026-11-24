// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerDictionaryAndPrimitiveTypeHelpersTests : TestContainer
{
    public AreEquivalentComparerDictionaryAndPrimitiveTypeHelpersTests()
        => EquivalenceComparerCaches.Reset();

    protected override void Dispose(bool disposing)
    {
        try
        {
            EquivalenceComparerCaches.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void AreEquivalent_DictionaryDeclaredIDictionaryValueType_IsUsed()
    {
        IDictionary<string, DeclaredEquatable> expected = new Dictionary<string, DeclaredEquatable>
        {
            ["item"] = new("same-key", "expected-only"),
        };
        IDictionary<string, DeclaredEquatable> actual = new Dictionary<string, DeclaredEquatable>
        {
            ["item"] = new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_DictionaryDeclaredIReadOnlyDictionaryValueType_IsUsed()
    {
        IReadOnlyDictionary<string, DeclaredEquatable> expected = new Dictionary<string, DeclaredEquatable>
        {
            ["item"] = new("same-key", "expected-only"),
        };
        IReadOnlyDictionary<string, DeclaredEquatable> actual = new Dictionary<string, DeclaredEquatable>
        {
            ["item"] = new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void GetDictionaryValueType_DeclaredTypePrecedesRuntimeTypes()
    {
        Type comparerType = typeof(Assert).GetNestedType("EquivalenceComparer", BindingFlags.NonPublic)!;
        MethodInfo method = comparerType.GetMethod("GetDictionaryValueType", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(
            null,
            [
                typeof(IDictionary<string, DeclaredEquatable>),
                typeof(Dictionary<string, RuntimeEquatable>),
                typeof(Dictionary<string, RuntimeEquatable>),
            ]).Should().Be(typeof(DeclaredEquatable));
    }

    public void TryGetDictionaryValueType_DictionaryDefinitions_ReturnsDeclaredValueType()
    {
        Type comparerType = typeof(Assert).GetNestedType("EquivalenceComparer", BindingFlags.NonPublic)!;
        MethodInfo method = comparerType.GetMethod("TryGetDictionaryValueType", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(null, [typeof(IDictionary<string, DeclaredEquatable>)]).Should().Be(typeof(DeclaredEquatable));
        method.Invoke(null, [typeof(IReadOnlyDictionary<string, DeclaredEquatable>)]).Should().Be(typeof(DeclaredEquatable));
    }

    public void GetDictionaryValueType_ExpectedRuntimeTypePrecedesActualRuntimeType()
    {
        Type comparerType = typeof(Assert).GetNestedType("EquivalenceComparer", BindingFlags.NonPublic)!;
        MethodInfo method = comparerType.GetMethod("GetDictionaryValueType", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(
            null,
            [
                typeof(object),
                typeof(Dictionary<string, DeclaredEquatable>),
                typeof(Dictionary<string, RuntimeEquatable>),
            ]).Should().Be(typeof(DeclaredEquatable));
    }

    public void GetDictionaryValueType_ActualRuntimeTypeIsUsedAsLastFallback()
    {
        Type comparerType = typeof(Assert).GetNestedType("EquivalenceComparer", BindingFlags.NonPublic)!;
        MethodInfo method = comparerType.GetMethod("GetDictionaryValueType", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(
            null,
            [
                typeof(object),
                typeof(object),
                typeof(Dictionary<string, DeclaredEquatable>),
            ]).Should().Be(typeof(DeclaredEquatable));
    }

    public void AreEquivalent_DictionaryExpectedRuntimeValueType_IsUsed()
    {
        IDictionary expected = new Dictionary<string, RuntimeEquatable>
        {
            ["item"] = new("same-key", "expected-only"),
        };
        IDictionary actual = new Hashtable
        {
            ["item"] = new RuntimeEquatable("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_DictionaryActualRuntimeValueType_IsUsed()
    {
        IDictionary expected = new Hashtable
        {
            ["item"] = new RuntimeEquatable("same-key", "expected-only"),
        };
        IDictionary actual = new Dictionary<string, RuntimeEquatable>
        {
            ["item"] = new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_DictionaryReadOnlyInterfaceWalk_UsesRuntimeValueType()
    {
        object expected = new ReadOnlyRuntimeDictionary(new RuntimeEquatable("same-key", "expected-only"));
        object actual = new ReadOnlyRuntimeDictionary(new RuntimeEquatable("same-key", "actual-only"));

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_DictionaryRuntimeValueType_IsResolvedWhenDeclaredAsObject()
    {
        object expected = new Dictionary<string, RuntimeEquatable>
        {
            ["item"] = new("same-key", "expected-only"),
        };
        object actual = new Dictionary<string, RuntimeEquatable>
        {
            ["item"] = new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_PrimitiveLikeValues_UseValueEqualityAtTheRoot()
    {
        (object Expected, object Actual)[] cases =
        [
            (42, 43),
            ("expected", "actual"),
            (DayOfWeek.Monday, DayOfWeek.Tuesday),
            (1.25m, 2.5m),
            (new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2024, 1, 2, 3, 4, 6, DateTimeKind.Utc)),
            (new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), new DateTimeOffset(2024, 1, 2, 3, 4, 6, TimeSpan.Zero)),
            (TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")),
            (new Uri("https://example.test/first"), new Uri("https://example.test/second")),
            (new Version(1, 2), new Version(1, 3)),
#if NET6_0_OR_GREATER
            (new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 3)),
            (new TimeOnly(1, 2, 3), new TimeOnly(1, 2, 4)),
            ((Half)1, (Half)2),
#endif
#if NET7_0_OR_GREATER
            ((Int128)1, (Int128)2),
            ((UInt128)1, (UInt128)2),
#endif
        ];

        foreach ((object expected, object actual) in cases)
        {
            Action compare = () => Assert.AreEquivalent<object>(expected, actual);

            AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
            failure.Message.Should().Contain("Mismatch at '<root>'");
            failure.Message.Should().Contain("values are not equal");
        }
    }

    public void AreEquivalent_UserDefinedObject_IsComparedStructurally()
    {
        object expected = new Person("same");
        object actual = new Person("same");

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_NestedDictionariesMismatch_ReportsComposedKeyPathAndValues()
    {
        Dictionary<string, Dictionary<int, int>> expected = new()
        {
            ["outer"] = new Dictionary<int, int> { [42] = 7 },
        };
        Dictionary<string, Dictionary<int, int>> actual = new()
        {
            ["outer"] = new Dictionary<int, int> { [42] = 11 },
        };
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at '[\"outer\"][42]'");
        failure.ExpectedText.Should().Be("7");
        failure.ActualText.Should().Be("11");
    }

    public void AreEquivalent_DictionaryMemberMismatch_ReportsMemberAndKeyPath()
    {
        var expected = new DictionaryHolder(new Dictionary<string, int> { ["inner"] = 7 });
        var actual = new DictionaryHolder(new Dictionary<string, int> { ["inner"] = 11 });
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at 'Values[\"inner\"]'");
        failure.ExpectedText.Should().Be("7");
        failure.ActualText.Should().Be("11");
    }

    private sealed class DeclaredEquatable(string key, string visibleValue) : IEquatable<DeclaredEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        public bool Equals(DeclaredEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class DictionaryHolder(Dictionary<string, int> values)
    {
        public Dictionary<string, int> Values { get; } = values;
    }

    private sealed class ReadOnlyRuntimeDictionary : IReadOnlyDictionary<string, RuntimeEquatable>
    {
        private readonly Dictionary<string, RuntimeEquatable> _inner;

        public ReadOnlyRuntimeDictionary(RuntimeEquatable value)
            => _inner = new Dictionary<string, RuntimeEquatable> { ["item"] = value };

        public RuntimeEquatable this[string key] => _inner[key];

        public IEnumerable<string> Keys => _inner.Keys;

        public IEnumerable<RuntimeEquatable> Values => _inner.Values;

        public int Count => _inner.Count;

        public bool ContainsKey(string key)
            => _inner.ContainsKey(key);

        public bool TryGetValue(string key, out RuntimeEquatable value)
            => _inner.TryGetValue(key, out value!);

        public IEnumerator<KeyValuePair<string, RuntimeEquatable>> GetEnumerator()
            => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class Person(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class RuntimeEquatable(string key, string visibleValue) : IEquatable<RuntimeEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        public bool Equals(RuntimeEquatable? other)
            => other is not null && Key == other.Key;
    }
}
