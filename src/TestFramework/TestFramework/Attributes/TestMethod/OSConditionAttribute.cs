// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to ignore a test class or a test method, with an optional message.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not cause derived classes to be ignored.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OSConditionAttribute : ConditionBaseAttribute
{
#if !NETFRAMEWORK
    private static readonly OSPlatform FreeBSD =
#if NETSTANDARD
        OSPlatform.Create("FreeBSD");
#else
        OSPlatform.FreeBSD;
#endif
#endif

#if NET462
    // Cache the detected OS to avoid repeated reflection calls
    private static readonly OperatingSystems? DetectedOS = DetectCurrentOS();
#endif

    private readonly OperatingSystems _operatingSystems;

    /// <summary>
    /// Initializes a new instance of the <see cref="OSConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the OSes will be included or excluded.</param>
    /// <param name="operatingSystems">The operating systems that this test includes/excludes.</param>
    public OSConditionAttribute(ConditionMode mode, OperatingSystems operatingSystems)
        : base(mode)
    {
        _operatingSystems = operatingSystems;
        IgnoreMessage = mode == ConditionMode.Include
            ? $"Test is only supported on {operatingSystems}"
            : $"Test is not supported on {operatingSystems}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OSConditionAttribute"/> class.
    /// </summary>
    /// <param name="operatingSystems">The operating systems that this test supports.</param>
    public OSConditionAttribute(OperatingSystems operatingSystems)
        : this(ConditionMode.Include, operatingSystems)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should be ignored.
    /// </summary>
    public override bool IsConditionMet
#if NET462
        => DetectedOS is not null && (_operatingSystems & DetectedOS) != 0;
#else
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return (_operatingSystems & OperatingSystems.Windows) != 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return (_operatingSystems & OperatingSystems.Linux) != 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return (_operatingSystems & OperatingSystems.OSX) != 0;
            }
            else if (RuntimeInformation.IsOSPlatform(FreeBSD))
            {
                return (_operatingSystems & OperatingSystems.FreeBSD) != 0;
            }

            return false;
        }
    }
#endif

#if NET462
    /// <summary>
    /// Detects the current operating system using reflection to maintain compatibility with .NET Framework 4.6.2.
    /// </summary>
    /// <returns>
    /// The detected operating system, or null if the OS could not be determined.
    /// </returns>
    private static OperatingSystems? DetectCurrentOS()
    {
        // RuntimeInformation.IsOSPlatform is available in .NET Framework 4.7.1+.
        // For older .NET Framework versions or environments where the API is not available,
        // we return null to fall back to assuming Windows.
        // This also handles Mono which supports RuntimeInformation API and can run on non-Windows platforms.
        Type? runtimeInformationType = Type.GetType("System.Runtime.InteropServices.RuntimeInformation, System.Runtime.InteropServices.RuntimeInformation")
            ?? Type.GetType("System.Runtime.InteropServices.RuntimeInformation, mscorlib");
        if (runtimeInformationType is null)
        {
            return OperatingSystems.Windows;
        }

        MethodInfo? isOSPlatformMethod = runtimeInformationType.GetMethod("IsOSPlatform", BindingFlags.Public | BindingFlags.Static);
        if (isOSPlatformMethod is null)
        {
            // Fallback to Windows if the method is not found
            return OperatingSystems.Windows;
        }

        Type? osPlatformType = Type.GetType("System.Runtime.InteropServices.OSPlatform, System.Runtime.InteropServices.RuntimeInformation")
            ?? Type.GetType("System.Runtime.InteropServices.OSPlatform, mscorlib")
            ?? throw ApplicationStateGuard.Unreachable();

        // Use the predefined static properties instead of Create() method
        // On Mono, the static properties use uppercase strings (e.g., "LINUX") while Create() uses the provided casing,
        // and IsOSPlatform performs case-sensitive comparison against the predefined values.
        PropertyInfo? windowsProp = osPlatformType.GetProperty("Windows", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo? linuxProp = osPlatformType.GetProperty("Linux", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo? osxProp = osPlatformType.GetProperty("OSX", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo? freebsdProp = osPlatformType.GetProperty("FreeBSD", BindingFlags.Public | BindingFlags.Static);

        if (windowsProp != null && IsOSPlatformViaProperty(isOSPlatformMethod, windowsProp))
        {
            return OperatingSystems.Windows;
        }
        else if (linuxProp != null && IsOSPlatformViaProperty(isOSPlatformMethod, linuxProp))
        {
            return OperatingSystems.Linux;
        }
        else if (osxProp != null && IsOSPlatformViaProperty(isOSPlatformMethod, osxProp))
        {
            return OperatingSystems.OSX;
        }
        else if (freebsdProp != null && IsOSPlatformViaProperty(isOSPlatformMethod, freebsdProp))
        {
            return OperatingSystems.FreeBSD;
        }

        // Unknown OS
        return null;
    }

    private static bool IsOSPlatformViaProperty(MethodInfo isOSPlatformMethod, PropertyInfo osPlatformProperty)
    {
        object? osPlatform = osPlatformProperty.GetValue(null);
        object? result = isOSPlatformMethod.Invoke(null, [osPlatform]);
        return result is true;
    }
#endif

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "OSCondition";
}
