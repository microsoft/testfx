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
#if NETFRAMEWORK
        => IsConditionMetNetFramework();
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

#if NETFRAMEWORK
    private bool IsConditionMetNetFramework()
    {
        // RuntimeInformation.IsOSPlatform is available in .NET Framework 4.7.1+.
        // For older .NET Framework versions or environments where the API is not available,
        // we fall back to assuming Windows.
        // This also handles Mono which supports RuntimeInformation API and can run on non-Windows platforms.
        Type? runtimeInformationType = Type.GetType("System.Runtime.InteropServices.RuntimeInformation, System.Runtime.InteropServices.RuntimeInformation, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
            ?? Type.GetType("System.Runtime.InteropServices.RuntimeInformation, mscorlib");

        if (runtimeInformationType is null)
        {
            // API not available, assume Windows
            return (_operatingSystems & OperatingSystems.Windows) != 0;
        }

        MethodInfo? isOSPlatformMethod = runtimeInformationType.GetMethod("IsOSPlatform", BindingFlags.Public | BindingFlags.Static);
        if (isOSPlatformMethod is null)
        {
            // API not available, assume Windows
            return (_operatingSystems & OperatingSystems.Windows) != 0;
        }

        Type? osPlatformType = Type.GetType("System.Runtime.InteropServices.OSPlatform, System.Runtime.InteropServices.RuntimeInformation, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
            ?? Type.GetType("System.Runtime.InteropServices.OSPlatform, mscorlib");

        if (osPlatformType is null)
        {
            // API not available, assume Windows
            return (_operatingSystems & OperatingSystems.Windows) != 0;
        }

        if (IsOSPlatformViaReflection(isOSPlatformMethod, osPlatformType, "Windows"))
        {
            return (_operatingSystems & OperatingSystems.Windows) != 0;
        }
        else if (IsOSPlatformViaReflection(isOSPlatformMethod, osPlatformType, "Linux"))
        {
            return (_operatingSystems & OperatingSystems.Linux) != 0;
        }
        else if (IsOSPlatformViaReflection(isOSPlatformMethod, osPlatformType, "OSX"))
        {
            return (_operatingSystems & OperatingSystems.OSX) != 0;
        }
        else if (IsOSPlatformViaReflection(isOSPlatformMethod, osPlatformType, "FreeBSD"))
        {
            return (_operatingSystems & OperatingSystems.FreeBSD) != 0;
        }

        return false;
    }

    private static bool IsOSPlatformViaReflection(MethodInfo isOSPlatformMethod, Type osPlatformType, string osName)
    {
        MethodInfo? createMethod = osPlatformType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        if (createMethod is null)
        {
            return false;
        }

        object? osPlatform = createMethod.Invoke(null, new object[] { osName });
        if (osPlatform is null)
        {
            return false;
        }

        object? result = isOSPlatformMethod.Invoke(null, new object[] { osPlatform });
        return result is true;
    }
#endif

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "OSCondition";
}
