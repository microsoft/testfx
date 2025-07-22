// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to conditionally control whether a test class or a test method will run or be ignored based on the .NET framework being used.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class FrameworkConditionAttribute : ConditionBaseAttribute
{
    private readonly Frameworks _frameworks;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameworkConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the frameworks will be included or excluded.</param>
    /// <param name="frameworks">The .NET frameworks that this test includes/excludes.</param>
    public FrameworkConditionAttribute(ConditionMode mode, Frameworks frameworks)
        : base(mode)
    {
        _frameworks = frameworks;
        IgnoreMessage = mode == ConditionMode.Include
            ? $"Test is only supported on {frameworks}"
            : $"Test is not supported on {frameworks}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameworkConditionAttribute"/> class.
    /// </summary>
    /// <param name="frameworks">The .NET frameworks that this test supports.</param>
    public FrameworkConditionAttribute(Frameworks frameworks)
        : this(ConditionMode.Include, frameworks)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should run.
    /// </summary>
    public override bool ShouldRun => IsCurrentFrameworkSupported();

    /// <summary>
    /// Gets the ignore message (in case <see cref="ShouldRun"/> returns <see langword="false"/>).
    /// </summary>
    public override string? IgnoreMessage { get; }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => nameof(FrameworkConditionAttribute);

    private bool IsCurrentFrameworkSupported()
    {
        Frameworks currentFramework = GetCurrentFramework();
        return (_frameworks & currentFramework) != 0;
    }

    private static Frameworks GetCurrentFramework()
    {
        string frameworkDescription = RuntimeInformation.FrameworkDescription;

        // Check for UWP first as it may also have .NET Core or .NET in its description
        if (IsRunningOnUwp())
        {
            return Frameworks.Uwp;
        }

        // Check for .NET Framework
        if (frameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
        {
            return Frameworks.NetFramework;
        }

        // Check for .NET Core
        if (frameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
        {
            return Frameworks.NetCore;
        }

        // Check for .NET 5+ (includes .NET 5, 6, 7, 8, 9, etc.)
        if (frameworkDescription.StartsWith(".NET ", StringComparison.OrdinalIgnoreCase))
        {
            return Frameworks.Net;
        }

        // Default to .NET for unknown cases
        return Frameworks.Net;
    }

    private static bool IsRunningOnUwp()
    {
        try
        {
            // Try to access Windows.ApplicationModel.Package.Current
            // This is only available in UWP applications
            var packageType = Type.GetType("Windows.ApplicationModel.Package, Windows.Runtime");
            if (packageType is not null)
            {
                var currentProperty = packageType.GetProperty("Current");
                if (currentProperty is not null)
                {
                    var current = currentProperty.GetValue(null);
                    return current is not null;
                }
            }
        }
        catch
        {
            // If we can't access the UWP APIs, we're not running on UWP
        }

        return false;
    }
}