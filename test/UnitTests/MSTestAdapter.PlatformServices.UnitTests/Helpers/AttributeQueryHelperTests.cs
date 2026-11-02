// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

/// <summary>
/// Direct tests for <see cref="AttributeQueryHelper"/>, the single source of truth for the attribute-query
/// semantics shared by every <c>IReflectionOperations</c> implementation and by <c>ReflectHelper</c>.
/// </summary>
public sealed class AttributeQueryHelperTests : TestContainer
{
    private static readonly Attribute[] EmptyAttributes = [];

    public void IsAttributeDefinedReturnsFalseForEmptyAttributes()
        => AttributeQueryHelper.IsAttributeDefined<BaseAttribute>(EmptyAttributes).Should().BeFalse();

    public void IsAttributeDefinedMatchesDerivedAttribute()
    {
        Attribute[] attributes = [new UnrelatedAttribute(), new DerivedAttribute()];

        AttributeQueryHelper.IsAttributeDefined<BaseAttribute>(attributes).Should().BeTrue();
    }

    public void IsAttributeDefinedReturnsFalseWhenNoAttributeMatches()
    {
        Attribute[] attributes = [new UnrelatedAttribute()];

        AttributeQueryHelper.IsAttributeDefined<BaseAttribute>(attributes).Should().BeFalse();
    }

    public void GetFirstAttributeOrDefaultReturnsNullForEmptyAttributes()
        => AttributeQueryHelper.GetFirstAttributeOrDefault<DerivedAttribute>(EmptyAttributes).Should().BeNull();

    public void GetFirstAttributeOrDefaultReturnsFirstMatchInDeclarationOrder()
    {
        var first = new DerivedAttribute();
        var second = new DerivedAttribute();
        Attribute[] attributes = [new UnrelatedAttribute(), first, second];

        AttributeQueryHelper.GetFirstAttributeOrDefault<DerivedAttribute>(attributes).Should().BeSameAs(first);
    }

    public void GetSingleAttributeOrDefaultReturnsNullForEmptyAttributes()
        => AttributeQueryHelper.GetSingleAttributeOrDefault<BaseAttribute>(EmptyAttributes).Should().BeNull();

    public void GetSingleAttributeOrDefaultMatchesDerivedAttribute()
    {
        var derived = new DerivedAttribute();
        Attribute[] attributes = [new UnrelatedAttribute(), derived];

        AttributeQueryHelper.GetSingleAttributeOrDefault<BaseAttribute>(attributes).Should().BeSameAs(derived);
    }

    public void GetSingleAttributeOrDefaultThrowsLocalizedErrorWhenMultipleAttributesMatch()
    {
        Attribute[] attributes = [new DerivedAttribute(), new DerivedAttribute()];

        Action action = () => AttributeQueryHelper.GetSingleAttributeOrDefault<BaseAttribute>(attributes);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage(string.Format(CultureInfo.InvariantCulture, Resource.DuplicateAttributeError, typeof(BaseAttribute)));
    }

    public void GetSingleAttributeOrDefaultThrowsOnSecondMatchEvenWhenMoreFollow()
    {
        Attribute[] attributes = [new DerivedAttribute(), new DerivedAttribute(), new DerivedAttribute()];

        Action action = () => AttributeQueryHelper.GetSingleAttributeOrDefault<BaseAttribute>(attributes);

        action.Should().Throw<InvalidOperationException>();
    }

    public void GetAttributesReturnsEmptyForEmptyAttributes()
        => AttributeQueryHelper.GetAttributes<BaseAttribute>(EmptyAttributes).Should().BeEmpty();

    public void GetAttributesReturnsMatchesInDeclarationOrderIncludingDerived()
    {
        var derived = new DerivedAttribute();
        var @base = new BaseAttribute();
        Attribute[] attributes = [new UnrelatedAttribute(), derived, new UnrelatedAttribute(), @base];

        AttributeQueryHelper.GetAttributes<BaseAttribute>(attributes).Should().Equal(derived, @base);
    }

    public void PerformActionOnAttributeInvokesActionForEachMatchWithState()
    {
        var derived = new DerivedAttribute();
        var @base = new BaseAttribute();
        Attribute[] attributes = [new UnrelatedAttribute(), derived, @base];
        List<BaseAttribute> seen = [];

        AttributeQueryHelper.PerformActionOnAttribute<BaseAttribute, List<BaseAttribute>>(
            attributes,
            static (attribute, state) => state!.Add(attribute),
            seen);

        seen.Should().Equal(derived, @base);
    }

    public void PerformActionOnAttributeDoesNotInvokeActionWhenNoAttributeMatches()
    {
        Attribute[] attributes = [new UnrelatedAttribute()];
        int callCount = 0;

        AttributeQueryHelper.PerformActionOnAttribute<BaseAttribute, object>(
            attributes,
            (_, _) => callCount++,
            null);

        callCount.Should().Be(0);
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    private class BaseAttribute : Attribute;

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    private sealed class DerivedAttribute : BaseAttribute;

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    private sealed class UnrelatedAttribute : Attribute;
}
