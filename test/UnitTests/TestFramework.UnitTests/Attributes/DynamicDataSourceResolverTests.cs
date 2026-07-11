// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class DynamicDataSourceResolverTests : TestContainer
{
    private readonly MethodInfo _testMethodInfo;

    public DynamicDataSourceResolverTests()
        => _testMethodInfo = typeof(ResolverDummyClass).GetTypeInfo().GetDeclaredMethod(nameof(ResolverDummyClass.TestMethod))!;

    public void GetDataUsesRegisteredProviderInsteadOfReflection()
    {
        // "NonExistentSource" is not a member on the declaring type, so reflection would throw. The registered
        // provider must be used instead, proving the resolver short-circuits reflection.
        object[][] expected = [[1, 2], [3, 4]];
        DynamicDataSourceResolver.RegisterDataProvider(typeof(ResolverDummyClass), "NonExistentSource", _ => expected);

        var attribute = new DynamicDataAttribute("NonExistentSource", typeof(ResolverDummyClass));
        IEnumerable<object[]> data = attribute.GetData(_testMethodInfo);

        data.Should().BeSameAs(expected);
    }

    public void GetDataPassesSourceArgumentsToRegisteredProvider()
    {
        object?[]? capturedArguments = null;
        DynamicDataSourceResolver.RegisterDataProvider(
            typeof(ResolverDummyClass),
            "MethodSourceWithArgs",
            args =>
            {
                capturedArguments = args;
                return new object[][] { [42] };
            });

        var attribute = new DynamicDataAttribute("MethodSourceWithArgs", typeof(ResolverDummyClass), "a", 7);
        _ = attribute.GetData(_testMethodInfo).ToList();

        capturedArguments.Should().Equal("a", 7);
    }

    public void GetDataFallsBackToReflectionWhenNoProviderRegistered()
    {
        // No registration for this source name -> reflection reads the real property.
        var attribute = new DynamicDataAttribute(nameof(ResolverDummyClass.ReflectionData), typeof(ResolverDummyClass));

        IEnumerable<object[]> data = attribute.GetData(_testMethodInfo);

        data.ToList().Should().HaveCount(2);
    }

    public void GetDisplayNameUsesRegisteredProviderInsteadOfReflection()
    {
        // "NonExistentDisplayNameMethod" does not exist on the declaring type, so reflection would throw. The
        // registered provider must be used instead.
        DynamicDataSourceResolver.RegisterDisplayNameProvider(
            typeof(ResolverDummyClass),
            "NonExistentDisplayNameMethod",
            (methodInfo, data) => $"custom:{methodInfo.Name}:{data?.Length ?? 0}");

        var attribute = new DynamicDataAttribute("NonExistentSource2", typeof(ResolverDummyClass))
        {
            DynamicDataDisplayName = "NonExistentDisplayNameMethod",
            DynamicDataDisplayNameDeclaringType = typeof(ResolverDummyClass),
        };

        string? displayName = attribute.GetDisplayName(_testMethodInfo, [1, 2, 3]);

        displayName.Should().Be($"custom:{nameof(ResolverDummyClass.TestMethod)}:3");
    }

    public void GetDisplayNameFallsBackToReflectionWhenNoProviderRegistered()
    {
        var attribute = new DynamicDataAttribute("Source", typeof(ResolverDummyClass))
        {
            DynamicDataDisplayName = nameof(ResolverDummyClass.ReflectionDisplayName),
            DynamicDataDisplayNameDeclaringType = typeof(ResolverDummyClass),
        };

        string? displayName = attribute.GetDisplayName(_testMethodInfo, [1]);

        displayName.Should().Be("reflection-display-name");
    }

    private sealed class ResolverDummyClass
    {
        public static IEnumerable<object[]> ReflectionData => [[1, 2, 3], [4, 5, 6]];

        public void TestMethod()
        {
        }

        public static string ReflectionDisplayName(MethodInfo methodInfo, object[] data)
            => "reflection-display-name";
    }
}
