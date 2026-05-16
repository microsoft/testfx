// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region AreEquivalent — primitives, nulls, strings

    public void AreEquivalent_BothNull_Passes()
        => Assert.AreEquivalent<object>(null, null);

    public void AreEquivalent_SameReference_Passes()
    {
        var o = new Person("a", 1);
        Assert.AreEquivalent(o, o);
    }

    public void AreEquivalent_ExpectedNull_ActualNotNull_Fails()
    {
        Action act = () => Assert.AreEquivalent<object?>(null, new object());
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*structurally equivalent*one side is null*");
    }

    public void AreEquivalent_ExpectedNotNull_ActualNull_Fails()
    {
        Action act = () => Assert.AreEquivalent<object?>(new object(), null);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*one side is null*");
    }

    public void AreEquivalent_EqualPrimitives_Passes()
    {
        Assert.AreEquivalent(42, 42);
        Assert.AreEquivalent("abc", "abc");
        Assert.AreEquivalent(3.14, 3.14);
        Assert.AreEquivalent(true, true);
        Assert.AreEquivalent('z', 'z');
    }

    public void AreEquivalent_BoxedTypeValues_UseTypeEquality()
    {
        object expected = typeof(string);
        object actual = typeof(string);
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_BoxedPrimitiveLikeAndReferenceType_FailsWithTypeMismatch()
    {
        object expected = 42;
        object actual = new Person("Ada", 36);
        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("incompatible types");
    }

    public void AreEquivalent_DifferentInts_Fails()
    {
        Action act = () => Assert.AreEquivalent(1, 2);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at '<root>'");
        ex.Message.Should().Contain("values are not equal");
        ex.ExpectedText.Should().Be("1");
        ex.ActualText.Should().Be("2");
    }

    public void AreEquivalent_Strings_DifferentValues_Fails()
    {
        Action act = () => Assert.AreEquivalent("foo", "bar");
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("values are not equal");
        ex.ExpectedText.Should().Be("\"foo\"");
        ex.ActualText.Should().Be("\"bar\"");
    }

    public void AreEquivalent_Enums_Equal_Passes()
        => Assert.AreEquivalent(DayOfWeek.Tuesday, DayOfWeek.Tuesday);

    public void AreEquivalent_Enums_Different_Fails()
    {
        Action act = () => Assert.AreEquivalent(DayOfWeek.Monday, DayOfWeek.Tuesday);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreEquivalent_DateTime_Equal_Passes()
        => Assert.AreEquivalent(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));

    public void AreEquivalent_Guid_Different_Fails()
    {
        Action act = () => Assert.AreEquivalent(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"));
        act.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region AreEquivalent — POCOs

    public void AreEquivalent_IdenticalPocos_Passes()
        => Assert.AreEquivalent(new Person("Ada", 36), new Person("Ada", 36));

    public void AreEquivalent_PocoWithDifferentField_Fails()
    {
        Action act = () => Assert.AreEquivalent(new Person("Ada", 36), new Person("Ada", 37));
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at 'Age'");
        ex.ExpectedText.Should().Be("36");
        ex.ActualText.Should().Be("37");
    }

    public void AreEquivalent_NestedPoco_DeepDifference_ReportedWithDottedPath()
    {
        Order expected = new("o-1", new Address("street1", "city1"));
        Order actual = new("o-1", new Address("street1", "different-city"));
        Action act = () => Assert.AreEquivalent(expected, actual);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at 'ShippingAddress.City'");
        ex.ExpectedText.Should().Be("\"city1\"");
        ex.ActualText.Should().Be("\"different-city\"");
    }

    public void AreEquivalent_PublicFieldsAreCompared()
    {
        WithPublicField a = new() { Number = 1 };
        WithPublicField b = new() { Number = 2 };
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Mismatch at 'Number'*");
    }

    public void AreEquivalent_AnonymousTypes_SameShape_Passes()
        => Assert.AreEquivalent(new { Name = "Ada", Age = 36 }, new { Name = "Ada", Age = 36 });

    public void AreEquivalent_PrivateFieldsAreIgnored()
    {
        WithPrivateField a = new(privateValue: "secret-1", publicValue: 1);
        WithPrivateField b = new(privateValue: "secret-2", publicValue: 1);
        Assert.AreEquivalent(a, b);
    }

    #endregion

    #region AreEquivalent — collections

    public void AreEquivalent_EqualLists_Passes()
        => Assert.AreEquivalent<List<int>>([1, 2, 3], [1, 2, 3]);

    public void AreEquivalent_Lists_DifferentLengths_Fails()
    {
        Action act = () => Assert.AreEquivalent<List<int>>([1, 2, 3], [1, 2]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Mismatch at '<root>'*collections differ in length*expected 3*actual 2*");
    }

    public void AreEquivalent_Lists_DifferentElement_ReportsIndex()
    {
        Action act = () => Assert.AreEquivalent<int[]>([10, 20, 30], [10, 25, 30]);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at '[1]'");
        ex.ExpectedText.Should().Be("20");
        ex.ActualText.Should().Be("25");
    }

    public void AreEquivalent_OrderSensitive_Fails()
    {
        Action act = () => Assert.AreEquivalent<int[]>([1, 2], [2, 1]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Mismatch at '[0]'*");
    }

    public void AreEquivalent_NestedCollectionInsidePoco()
    {
        Order expected = new("o", new Address("s", "c")) { Items = { 1, 2, 3 } };
        Order actual = new("o", new Address("s", "c")) { Items = { 1, 999, 3 } };
        Action act = () => Assert.AreEquivalent(expected, actual);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at 'Items[1]'");
        ex.ExpectedText.Should().Be("2");
        ex.ActualText.Should().Be("999");
    }

    #endregion

    #region AreEquivalent — dictionaries

    public void AreEquivalent_EqualDictionaries_Passes()
    {
        var a = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var b = new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_Dictionary_MissingKey_Fails()
    {
        var a = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var b = new Dictionary<string, int> { ["a"] = 1 };
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("key \"b\"").And.Contain("missing from actual");
    }

    public void AreEquivalent_Dictionary_DifferentValue_Fails()
    {
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 2 };
        Action act = () => Assert.AreEquivalent(a, b);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Mismatch at '[\"a\"]'");
        ex.ExpectedText.Should().Be("1");
        ex.ActualText.Should().Be("2");
    }

    public void AreEquivalent_Dictionary_ExtraKeyOnActual_NonStrict_Passes()
    {
        // Non-strict mode (default): extra keys on actual are tolerated.
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_Dictionary_ExtraKeyOnActual_Strict_Fails()
    {
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        Action act = () => Assert.AreEquivalent(a, b, strict: true);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("key \"b\"").And.Contain("not on expected");
    }

    public void AreEquivalent_Dictionary_NonStringKey_Equal_Passes()
    {
        var a = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        var b = new Dictionary<int, string> { [2] = "two", [1] = "one" };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_Dictionary_NonStringKey_DifferentValue_FailsWithKeyInPath()
    {
        var a = new Dictionary<int, string> { [42] = "x" };
        var b = new Dictionary<int, string> { [42] = "y" };
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Mismatch at '[42]'");
    }

    public void AreEquivalent_Dictionary_KeyPath_UsesRenderedValue()
    {
        string key = "line1\nline2";
        var a = new Dictionary<string, int> { [key] = 1 };
        var b = new Dictionary<string, int> { [key] = 2 };
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Mismatch at '[\"line1\\nline2\"]'");
    }

    public void AreEquivalent_Dictionary_CaseInsensitiveComparer_RespectsSourceComparer()
    {
        // The source dictionaries use OrdinalIgnoreCase. The comparer must defer to the source's
        // own key lookup so "Hello" == "hello" is honored.
        var a = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Hello"] = 1 };
        var b = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["hello"] = 1 };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_ReadOnlyDictionaryOnlyType_TreatedAsDictionary()
    {
        ReadOnlyDictionaryOnly<string, int> a = new() { ["a"] = 1, ["b"] = 2 };
        ReadOnlyDictionaryOnly<string, int> b = new() { ["b"] = 2, ["a"] = 1 };
        // Both implement only IReadOnlyDictionary<,> (not IDictionary<,>); should be detected as a
        // dictionary and compared by key set, not as an ordered KeyValuePair sequence.
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_MixedDictionaryAndNonDictionary_TypeMismatch()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1 };
        var list = new List<KeyValuePair<string, int>> { new("a", 1) };
        Action act = () => Assert.AreEquivalent<object>(dict, list);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("incompatible types");
    }

    public void AreEquivalent_MemberGetterThrows_FailsWithExpectedDiagnostic()
    {
        ThrowingGetter a = new();
        ThrowingGetter b = new();
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Mismatch at 'BadProperty'").And.Contain("InvalidOperationException");
    }

    public void AreEquivalent_MemberGetterCallsAssertFail_PropagatesAsAssertFailedException()
    {
        // A property getter that calls Assert.Fail() throws AssertFailedException wrapped in a
        // TargetInvocationException by Reflection. The equivalence comparer must NOT rewrite the
        // framework exception as a structured "comparison failure" — it must propagate so the user's
        // assertion surfaces with its original message.
        AssertFailingGetter a = new();
        AssertFailingGetter b = new();
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*nested-assert-fail*")
            .And.Message.Should().NotContain("Mismatch at");
    }

    public void AreEquivalent_IEquatableThrows_FailsWithExpectedDiagnostic()
    {
        ThrowingEquatable a = new();
        ThrowingEquatable b = new();
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("IEquatable").And.Contain("InvalidOperationException");
    }

    public void AreEquivalent_Strict_MultipleExtraMembers_RenderedSortedAndCommaSeparated()
    {
        Person expected = new("Ada", 36);
        PersonWithMultipleExtras actual = new("Ada", 36, "ada@example.com", "+44");
        Action act = () => Assert.AreEquivalent<object>(expected, actual, strict: true);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        // Extras (Email, Phone) are sorted alphabetically by Ordinal comparer, comma-separated.
        ex.Message.Should().Contain("Email, Phone");
    }

    public void AreEquivalent_TopologyMismatch_SharedSubobjectVsDistinctCopies()
    {
        // expected: A → shared → both A.Other and A.OtherAlso point to the *same* node.
        SharedHolder shared = new() { Value = "x" };
        TopologyHolder expectedRoot = new() { Other = shared, OtherAlso = shared };
        // actual: two distinct nodes that compare equal field-by-field.
        TopologyHolder actualRoot = new() { Other = new() { Value = "x" }, OtherAlso = new() { Value = "x" } };

        Action act = () => Assert.AreEquivalent(expectedRoot, actualRoot);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("graph topology differs");
    }

    public void AreEquivalent_ValueTypeFieldsInSharedAndDistinctPositions_NoFalseTopologyFailure()
    {
        // Value types skip topology tracking, so an int (or struct) appearing in two field positions
        // on expected vs two distinct copies on actual must still compare as equivalent.
        TwoIntsHolder expected = new() { First = 7, Second = 7 };
        TwoIntsHolder actual = new() { First = 7, Second = 7 };
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_ValueTypeIEquatableThrows_FailsWithExpectedDiagnostic()
    {
        // Boxing a struct that throws from IEquatable<T> must still be caught and surfaced as a
        // structured failure, not propagated.
        ThrowingEquatableStruct a = default;
        ThrowingEquatableStruct b = default;
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("IEquatable").And.Contain("InvalidOperationException");
    }

    public void AreEquivalent_OnlyActualGetterThrows_FailsWithActualSideDiagnostic()
    {
        // Only the actual side throws (expected uses the safe base type). Exercises the
        // isExpected: false branch of MemberAccessFailure.
        SafeGetter expected = new() { Value = 1 };
        ActualThrowingGetter actual = new();
        Action act = () => Assert.AreEquivalent<SafeGetter>(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Mismatch at 'Value'").And.Contain("reading the actual member threw");
    }

    public void AreEquivalent_DictionaryThrowsFromTryGetValue_FailsWithExpectedDiagnostic()
    {
        var a = new Dictionary<string, int> { ["a"] = 1 };
        ThrowingDictionary actual = new();
        Action act = () => Assert.AreEquivalent<IDictionary<string, int>>(a, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("reading the actual dictionary threw").And.Contain("InvalidOperationException");
    }

    public void AreEquivalent_EnumerableThrows_FailsWithExpectedDiagnostic()
    {
        IEnumerable<int> expected = [1];
        IEnumerable<int> actual = new ThrowingEnumerable();
        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("enumerating the actual collection threw").And.Contain("InvalidOperationException");
    }

    public void AreEquivalent_EnumerableMismatch_FailsFastWithoutEnumeratingPastMismatch()
    {
        IEnumerable<int> expected = [1, 2, 3];
        IEnumerable<int> actual = new FailIfEnumeratedPastIndexEnumerable(1, 1, 999, 3);
        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Mismatch at '[1]'").And.NotContain("enumerating the actual collection threw");
    }

    public void AreEquivalent_ReadOnlyDictionaryOnly_RespectsSourceComparer()
    {
        // Custom IReadOnlyDictionary<,>-only type backed by a case-insensitive Dictionary should
        // honor the source's comparer through GenericDictionaryAccessors.TryGetValue.
        var a = ReadOnlyDictionaryOnly<string, int>.CaseInsensitive();
        var b = ReadOnlyDictionaryOnly<string, int>.CaseInsensitive();
        a["Hello"] = 1;
        b["hello"] = 1;
        Assert.AreEquivalent(a, b);
    }

    #endregion

    #region AreNotEquivalent — broader mirroring

    public void AreNotEquivalent_DifferentLists_Passes()
        => Assert.AreNotEquivalent<int[]>([1, 2, 3], [1, 2, 4]);

    public void AreNotEquivalent_EqualLists_Fails()
    {
        Action act = () => Assert.AreNotEquivalent<int[]>([1, 2, 3], [1, 2, 3]);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreNotEquivalent_DifferentDictionaries_Passes()
    {
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 2 };
        Assert.AreNotEquivalent(a, b);
    }

    public void AreNotEquivalent_EqualDictionaries_Fails()
    {
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 1 };
        Action act = () => Assert.AreNotEquivalent(a, b);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreNotEquivalent_TopologicallyIdenticalCycles_Fails()
    {
        Node a = new("v");
        a.Next = a;
        Node b = new("v");
        b.Next = b;
        Action act = () => Assert.AreNotEquivalent(a, b);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreNotEquivalent_IEquatableSemantics_Fails()
    {
        EquatableMoney a = new(100m, "USD");
        EquatableMoney b = new(100m, "EUR");
        Action act = () => Assert.AreNotEquivalent(a, b);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreNotEquivalent_IEquatableThrows_FailsWithComparisonFailure()
    {
        ThrowingEquatable a = new();
        ThrowingEquatable b = new();
        Action act = () => Assert.AreNotEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Could not complete structural comparison").And.Contain("IEquatable").And.Contain("InvalidOperationException");
    }

    public void AreNotEquivalent_CallSiteExpression_IsRendered()
    {
        int x = 1;
        int y = 1;
        Action act = () => Assert.AreNotEquivalent(x, y);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Assert.AreNotEquivalent(x, y)");
    }

    #endregion

    #region AreEquivalent — IEquatable shortcut

    public void AreEquivalent_IEquatableType_UsesEquals_NotMembers()
    {
        // EquatableMoney has IEquatable<EquatableMoney> that ignores Currency.
        // So 100 USD vs 100 EUR are considered equivalent by IEquatable.
        EquatableMoney a = new(100m, "USD");
        EquatableMoney b = new(100m, "EUR");
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_IEquatableType_DifferentValues_Fails()
    {
        EquatableMoney a = new(100m, "USD");
        EquatableMoney b = new(200m, "USD");
        Action act = () => Assert.AreEquivalent(a, b);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        // IEquatableOutcome.NotEqual must surface the rendered values via the structured failure.
        ex.ExpectedText.Should().NotBeNull();
        ex.ActualText.Should().NotBeNull();
    }

    public void AreEquivalent_PlainEqualsOverride_DoesNotShortCircuit_RecursesIntoMembers()
    {
        // OnlyEqualsOverride.Equals always returns true, but we should ignore that override
        // and recurse into the Value property, which differs.
        OnlyEqualsOverride a = new(1);
        OnlyEqualsOverride b = new(2);
        Action act = () => Assert.AreEquivalent(a, b);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Mismatch at 'Value'*");
    }

    #endregion

    #region AreEquivalent — cycles

    public void AreEquivalent_Cycles_DoNotStackOverflow()
    {
        Node a = new("x");
        a.Next = a;
        Node b = new("x");
        b.Next = b;
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_CrossCycles_Detected_Pass()
    {
        Node a = new("v");
        Node b = new("v");
        a.Next = b;
        b.Next = a;
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_DeepGraph_AtMaximumSupportedDepth_Passes()
    {
        DeepNode expected = CreateDeepNodeChain(256);
        DeepNode actual = CreateDeepNodeChain(256);
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_DeepGraph_ReportsMaxDepthExceeded()
    {
        DeepNode expected = CreateDeepNodeChain(257);
        DeepNode actual = CreateDeepNodeChain(257);
        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("maximum supported depth of 256");
    }

    #endregion

    #region AreEquivalent — strict mode

    public void AreEquivalent_Strict_DifferentRuntimeTypes_ExtraMemberOnActual_Fails()
    {
        Person expected = new("Ada", 36);
        PersonWithEmail actual = new("Ada", 36, "ada@example.com");
        // Non-strict: passes (Email is ignored).
        Assert.AreEquivalent<object>(expected, actual);

        // Strict: fails because actual has extra member 'Email'.
        Action act = () => Assert.AreEquivalent<object>(expected, actual, strict: true);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*structurally equivalent (strict mode)*Email*");
    }

    public void AreEquivalent_Strict_SameRuntimeType_NotAffected()
        => Assert.AreEquivalent(new Person("Ada", 36), new Person("Ada", 36), strict: true);

    public void AreEquivalent_Strict_Dictionary_ExtraKey_Fails()
    {
        // Strict: still fails the same way.
        var a = new Dictionary<string, int> { ["a"] = 1 };
        var b = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        Action act = () => Assert.AreEquivalent(a, b, strict: true);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreEquivalent_IEquatableElementInList_UsesIEquatable()
    {
        // List<EquatableMoney> recursion preserves the declared element type, so the IEquatable<EquatableMoney>
        // shortcut applies per element. EUR vs USD differ via members but are equal via IEquatable.
        var a = new List<EquatableMoney> { new(100m, "USD") };
        var b = new List<EquatableMoney> { new(100m, "EUR") };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_IEquatableElementInArray_UsesIEquatable()
    {
        EquatableMoney[] a = [new(50m, "USD")];
        EquatableMoney[] b = [new(50m, "EUR")];
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_IEquatableValueInDictionary_UsesIEquatable()
    {
        var a = new Dictionary<string, EquatableMoney> { ["price"] = new(10m, "USD") };
        var b = new Dictionary<string, EquatableMoney> { ["price"] = new(10m, "EUR") };
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_ExplicitIEquatableImpl_Honored()
    {
        // Even though the Equals(T) is implemented explicitly (not as a public concrete method),
        // reflection-driven interface-method dispatch still finds it.
        ExplicitEquatable a = new(1);
        ExplicitEquatable b = new(2);
        Assert.AreEquivalent(a, b);
    }

    public void AreEquivalent_TopologyMismatch_Detected()
    {
        // expected: a -> b -> a (two distinct nodes in a 2-cycle)
        Node a = new("x");
        Node b = new("x");
        a.Next = b;
        b.Next = a;

        // actual: c -> c (single self-cycle)
        Node c = new("x");
        c.Next = c;

        Action act = () => Assert.AreEquivalent(a, c);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("graph topology differs");
    }

    public void AreEquivalent_SelfCycleThroughField()
    {
        NodeWithField a = new() { Label = "x" };
        a.Next = a;
        NodeWithField b = new() { Label = "x" };
        b.Next = b;
        Assert.AreEquivalent(a, b);
    }

    #endregion

    #region AreEquivalent — user message and call-site expression

    public void AreEquivalent_UserMessage_IsIncluded()
    {
        Action act = () => Assert.AreEquivalent(1, 2, "boom");
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("boom");
    }

    public void AreEquivalent_CallSiteExpression_IsRendered()
    {
        int x = 1;
        int y = 2;
        Action act = () => Assert.AreEquivalent(x, y);
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Assert.AreEquivalent(x, y)");
    }

    public void AreEquivalent_FailureMessage_FollowsRfc012Layout()
    {
        int expected = 1;
        int actual = 2;
        Action act = () => Assert.AreEquivalent(expected, actual, "numbers should match");
        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be structurally equivalent.
                Mismatch at '<root>': values are not equal.
                numbers should match

                expected: 1
                actual:   2

                Assert.AreEquivalent(expected, actual)
                """);
    }

    #endregion

    #region AreNotEquivalent

    public void AreNotEquivalent_DifferentValues_Passes()
        => Assert.AreNotEquivalent(new Person("Ada", 36), new Person("Ada", 37));

    public void AreNotEquivalent_EqualValues_Fails()
    {
        Action act = () => Assert.AreNotEquivalent(new Person("Ada", 36), new Person("Ada", 36));
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*structurally different*");
    }

    public void AreNotEquivalent_BothNull_Fails()
    {
        Action act = () => Assert.AreNotEquivalent<object>(null, null);
        act.Should().Throw<AssertFailedException>();
    }

    public void AreNotEquivalent_PlainEqualsOverride_StillRecurses_PassesOnDifferentValue()
        => Assert.AreNotEquivalent(new OnlyEqualsOverride(1), new OnlyEqualsOverride(2));

    public void AreNotEquivalent_StrictMode_ExtraMemberOnActual_Passes()
    {
        Person expected = new("Ada", 36);
        PersonWithEmail actual = new("Ada", 36, "ada@example.com");
        // Strict mode: extra member on actual makes them NOT equivalent → AreNotEquivalent passes.
        Assert.AreNotEquivalent<object>(expected, actual, strict: true);
    }

    public void AreNotEquivalent_UserMessage_IsIncluded()
    {
        Action act = () => Assert.AreNotEquivalent(1, 1, "boom");
        act.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("boom");
    }

    #endregion

    #region Test helpers

    private static DeepNode CreateDeepNodeChain(int length)
    {
        DeepNode root = new();
        DeepNode current = root;
        for (int i = 1; i < length; i++)
        {
            current.Next = new DeepNode();
            current = current.Next!;
        }

        return root;
    }

    public void AreEquivalent_NewShadowedProperty_UsesMostDerivedDeclaration()
    {
        // The derived class shadows the base `int Value` with `string Value`.
        // If the comparer ever picked the base accessor, it would compare base.Value (always 0 on both
        // sides) and silently pass — masking the genuine derived-value mismatch. The expected outcome
        // is a failure that mentions the derived string values.
        var expected = new ShadowedDerived { Value = "derived-expected" };
        var actual = new ShadowedDerived { Value = "derived-actual" };

        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Value*derived-expected*derived-actual*");
    }

    public void AreEquivalent_NewShadowedProperty_SameType_DetectsDerivedMismatch()
    {
        // Same-type `new` shadowing (int → int with different defaults). If the comparer picked the
        // base accessor, both sides would resolve to the base default (100) and the mismatch on the
        // derived value (200 vs 999) would be silently swallowed. The expected outcome is a failure
        // that mentions the derived integers.
        var expected = new SameTypeShadowDerived { Value = 200 };
        var actual = new SameTypeShadowDerived { Value = 999 };

        Action act = () => Assert.AreEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Value*200*999*");
    }

    private class ShadowedBase
    {
        public int Value { get; set; }
    }

    private sealed class ShadowedDerived : ShadowedBase
    {
        public new string Value { get; set; } = "default";
    }

    private class SameTypeShadowBase
    {
        public int Value { get; set; } = 100;
    }

    private sealed class SameTypeShadowDerived : SameTypeShadowBase
    {
        public new int Value { get; set; } = 200;
    }

    private sealed class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }

    private sealed class PersonWithEmail
    {
        public PersonWithEmail(string name, int age, string email)
        {
            Name = name;
            Age = age;
            Email = email;
        }

        public string Name { get; }

        public int Age { get; }

        public string Email { get; }
    }

    private sealed class Address
    {
        public Address(string street, string city)
        {
            Street = street;
            City = city;
        }

        public string Street { get; }

        public string City { get; }
    }

    private sealed class Order
    {
        public Order(string id, Address shippingAddress)
        {
            Id = id;
            ShippingAddress = shippingAddress;
        }

        public string Id { get; }

        public Address ShippingAddress { get; }

        public List<int> Items { get; } = [];
    }

    private sealed class WithPublicField
    {
#pragma warning disable SA1401 // Field should be private - intentional: this type tests public-field comparison.
        public int Number;
#pragma warning restore SA1401
    }

    private sealed class WithPrivateField
    {
#pragma warning disable IDE0052 // Remove unread private members - intentional for the test
        private readonly string _privateValue;
#pragma warning restore IDE0052

        public WithPrivateField(string privateValue, int publicValue)
        {
            _privateValue = privateValue;
            PublicValue = publicValue;
        }

        public int PublicValue { get; }
    }

    private sealed class EquatableMoney : IEquatable<EquatableMoney>
    {
        public EquatableMoney(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }

        public string Currency { get; }

        // Intentionally ignores Currency to demonstrate that IEquatable<T>.Equals takes precedence.
        public bool Equals(EquatableMoney? other) => other is not null && other.Amount == Amount;

        public override bool Equals(object? obj) => Equals(obj as EquatableMoney);

        public override int GetHashCode() => Amount.GetHashCode();
    }

    private sealed class OnlyEqualsOverride
    {
        public OnlyEqualsOverride(int value) => Value = value;

        public int Value { get; }

        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 0;
    }

    private sealed class Node
    {
        public Node(string label) => Label = label;

        public string Label { get; }

        public Node? Next { get; set; }
    }

    private sealed class DeepNode
    {
        public DeepNode? Next { get; set; }
    }

    private sealed class NodeWithField
    {
#pragma warning disable SA1401 // Field should be private - intentional: this type tests public-field cycle handling.
        public string? Label;
        public NodeWithField? Next;
#pragma warning restore SA1401
    }

    private sealed class ExplicitEquatable : IEquatable<ExplicitEquatable>
    {
#pragma warning disable IDE0052 // Remove unread private members - intentional: ensures the type is non-trivial without exposing public members.
        private readonly int _value;
#pragma warning restore IDE0052

        public ExplicitEquatable(int value) => _value = value;

        // Explicit interface implementation: Equals always returns true so we can detect that the
        // comparer dispatched through IEquatable<T> rather than recursing into _value via reflection.
        bool IEquatable<ExplicitEquatable>.Equals(ExplicitEquatable? other) => true;
    }

    private sealed class PersonWithMultipleExtras
    {
        public PersonWithMultipleExtras(string name, int age, string email, string phone)
        {
            Name = name;
            Age = age;
            Email = email;
            Phone = phone;
        }

        public string Name { get; }

        public int Age { get; }

        // Out-of-order declarations to confirm extras are sorted alphabetically in the message.
        public string Phone { get; }

        public string Email { get; }
    }

    private sealed class ThrowingGetter
    {
        public string BadProperty => throw new InvalidOperationException("nope");
    }

    private sealed class AssertFailingGetter
    {
        public string Value
        {
            get
            {
                Assert.Fail("nested-assert-fail");
                return string.Empty;
            }
        }
    }

    private sealed class ThrowingEquatable : IEquatable<ThrowingEquatable>
    {
        public bool Equals(ThrowingEquatable? other) => throw new InvalidOperationException("equality boom");

        public override bool Equals(object? obj) => Equals(obj as ThrowingEquatable);

        public override int GetHashCode() => 0;
    }

    private sealed class SharedHolder
    {
        public string? Value { get; set; }
    }

    private sealed class TopologyHolder
    {
        public SharedHolder? Other { get; set; }

        public SharedHolder? OtherAlso { get; set; }
    }

    /// <summary>
    /// Custom dictionary-shaped type that implements ONLY <see cref="IReadOnlyDictionary{TKey, TValue}"/>
    /// (not <see cref="IDictionary{TKey, TValue}"/> nor <see cref="IDictionary"/>).
    /// </summary>
    private sealed class ReadOnlyDictionaryOnly<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _inner;

        public ReadOnlyDictionaryOnly() => _inner = [];

#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer.
        private ReadOnlyDictionaryOnly(IEqualityComparer<TKey> comparer) => _inner = new(comparer);
#pragma warning restore IDE0028

        public TValue this[TKey key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public IEnumerable<TKey> Keys => _inner.Keys;

        public IEnumerable<TValue> Values => _inner.Values;

        public int Count => _inner.Count;

        internal static ReadOnlyDictionaryOnly<string, TValue> CaseInsensitive()
            => new ReadOnlyDictionaryOnly<string, TValue>(StringComparer.OrdinalIgnoreCase);

        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member. - net48 / netstandard target ships TryGetValue without [MaybeNullWhen]; net9.0 does. The mismatch is harmless for the test.
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _inner.TryGetValue(key, out value);
#pragma warning restore CS8767

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TwoIntsHolder
    {
        public int First { get; set; }

        public int Second { get; set; }
    }

    private readonly struct ThrowingEquatableStruct : IEquatable<ThrowingEquatableStruct>
    {
        public bool Equals(ThrowingEquatableStruct other) => throw new InvalidOperationException("struct equality boom");

        public override bool Equals(object? obj) => obj is ThrowingEquatableStruct other && Equals(other);

        public override int GetHashCode() => 0;
    }

    private class SafeGetter
    {
        public virtual int Value { get; set; }
    }

    private sealed class ActualThrowingGetter : SafeGetter
    {
        public override int Value
        {
            get => throw new InvalidOperationException("actual side boom");
            set { }
        }
    }

    private sealed class ThrowingEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("enumeration boom");

        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("enumeration boom");
    }

    private sealed class FailIfEnumeratedPastIndexEnumerable : IEnumerable<int>
    {
        private readonly int[] _items;
        private readonly int _maxIndex;

        public FailIfEnumeratedPastIndexEnumerable(int maxIndex, params int[] items)
        {
            _maxIndex = maxIndex;
            _items = items;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                yield return i > _maxIndex
                    ? throw new InvalidOperationException("enumerated past mismatch")
                    : _items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingDictionary : IDictionary<string, int>
    {
        public int this[string key]
        {
            get => throw new InvalidOperationException("indexer boom");
            set => throw new InvalidOperationException("indexer boom");
        }

        public ICollection<string> Keys => throw new InvalidOperationException("Keys boom");

        public ICollection<int> Values => throw new InvalidOperationException("Values boom");

        public int Count => 0;

        public bool IsReadOnly => true;

        public void Add(string key, int value) => throw new NotSupportedException();

        public void Add(KeyValuePair<string, int> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(KeyValuePair<string, int> item) => throw new InvalidOperationException("Contains boom");

        public bool ContainsKey(string key) => throw new InvalidOperationException("ContainsKey boom");

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => throw new NotSupportedException();

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            yield break;
        }

        public bool Remove(string key) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<string, int> item) => throw new NotSupportedException();

        public bool TryGetValue(string key, out int value) => throw new InvalidOperationException("TryGetValue boom");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
