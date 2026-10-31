// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

public sealed class OSConditionAttributeTests : TestContainer
{
    public void IsConditionMet_CurrentPlatformMaskDoesNotInvertExcludeMode()
    {
        OperatingSystems current = GetCurrentPlatform();
        const OperatingSystems All = OperatingSystems.Windows | OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD;
        foreach (ConditionMode mode in new[] { ConditionMode.Include, ConditionMode.Exclude })
        {
            var matching = new OSConditionAttribute(mode, current);
            var nonmatching = new OSConditionAttribute(mode, All & ~current);
            matching.IsConditionMet.Should().BeTrue();
            nonmatching.IsConditionMet.Should().BeFalse();
            matching.Mode.Should().Be(mode);
            matching.GroupName.Should().Be("OSCondition");
        }

        new OSConditionAttribute(current).IsConditionMet.Should().BeTrue();
        new OSConditionAttribute(default).IsConditionMet.Should().BeFalse();
        new OSConditionAttribute((OperatingSystems)(1 << 29)).IsConditionMet.Should().BeFalse();
    }

    public void CombinedAndUnknownMasks_PreservePlatformAndMetadataPolicies()
    {
        var include = new OSConditionAttribute(OperatingSystems.Windows | OperatingSystems.Linux);
        var exclude = new OSConditionAttribute(ConditionMode.Exclude, OperatingSystems.Windows | OperatingSystems.Linux);
        bool expected = GetCurrentPlatform() is OperatingSystems.Windows or OperatingSystems.Linux;
        include.IsConditionMet.Should().Be(expected);
        exclude.IsConditionMet.Should().Be(expected);
        include.IgnoreMessage.Should().Be("Test is only supported on Linux, Windows");
        exclude.IgnoreMessage.Should().Be("Test is not supported on Linux, Windows");
        include.Mode.Should().Be(ConditionMode.Include);

        AttributeUsageAttribute usage = typeof(OSConditionAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    private static OperatingSystems GetCurrentPlatform()
#if NETFRAMEWORK
        => OperatingSystems.Windows;
#else
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OperatingSystems.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OperatingSystems.Linux
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OperatingSystems.OSX
            : RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? OperatingSystems.FreeBSD
            : throw new PlatformNotSupportedException("These policy tests require a documented OSCondition platform.");
#endif
}
