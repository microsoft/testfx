// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to ignore a test class or a test method based on the current process architecture.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not cause derived classes to be ignored.
/// It can be combined with <see cref="OSConditionAttribute"/> (or any other condition attribute) to gate a test
/// on both the operating system and the process architecture, because condition attributes that expose a different
/// <see cref="ConditionBaseAttribute.GroupName"/> are combined with a logical AND.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ArchitectureConditionAttribute : ConditionBaseAttribute
{
#if NET462
    // Cache the detected architecture to avoid repeated reflection calls.
    private static readonly Architectures? DetectedArchitecture = DetectCurrentArchitecture();
#endif

    private readonly Architectures _architectures;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the architectures will be included or excluded.</param>
    /// <param name="architectures">The process architectures that this test includes/excludes.</param>
    public ArchitectureConditionAttribute(ConditionMode mode, Architectures architectures)
        : base(mode)
    {
        _architectures = architectures;
        IgnoreMessage = mode == ConditionMode.Include
            ? $"Test is only supported on {architectures}"
            : $"Test is not supported on {architectures}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureConditionAttribute"/> class.
    /// </summary>
    /// <param name="architectures">The process architectures that this test supports.</param>
    public ArchitectureConditionAttribute(Architectures architectures)
        : this(ConditionMode.Include, architectures)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should be ignored.
    /// </summary>
    public override bool IsConditionMet
    {
        get
        {
#if NET462
            Architectures? current = DetectedArchitecture;
#else
            Architectures? current = MapArchitecture((int)RuntimeInformation.ProcessArchitecture);
#endif
            return current is not null && (_architectures & current.Value) != 0;
        }
    }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "ArchitectureCondition";

    /// <summary>
    /// Maps the integer value of a <c>System.Runtime.InteropServices.Architecture</c> to the matching
    /// <see cref="Architectures"/> flag. The integer value is used (rather than the named enum members) so that the
    /// code compiles against ref assemblies that predate the newer architecture values.
    /// </summary>
    /// <param name="value">The integer value of the <c>System.Runtime.InteropServices.Architecture</c>.</param>
    /// <returns>The matching <see cref="Architectures"/> flag, or <see langword="null"/> if the value is unknown.</returns>
    private static Architectures? MapArchitecture(int value)
        => value switch
        {
            0 => Architectures.X86,
            1 => Architectures.X64,
            2 => Architectures.Arm,
            3 => Architectures.Arm64,
            4 => Architectures.Wasm,
            5 => Architectures.S390x,
            6 => Architectures.LoongArch64,
            7 => Architectures.Armv6,
            8 => Architectures.Ppc64le,
            9 => Architectures.RiscV64,
            _ => null,
        };

#if NET462
    /// <summary>
    /// Detects the current process architecture using reflection to maintain compatibility with .NET Framework 4.6.2,
    /// where <c>System.Runtime.InteropServices.RuntimeInformation</c> may not be present.
    /// </summary>
    /// <returns>
    /// The detected process architecture, or <see langword="null"/> if it could not be determined.
    /// </returns>
    private static Architectures? DetectCurrentArchitecture()
    {
        // RuntimeInformation.ProcessArchitecture is available in .NET Framework 4.7.1+.
        // For older .NET Framework versions or environments where the API is not available, we return null.
        Type? runtimeInformationType = Type.GetType("System.Runtime.InteropServices.RuntimeInformation, System.Runtime.InteropServices.RuntimeInformation")
            ?? Type.GetType("System.Runtime.InteropServices.RuntimeInformation, mscorlib");
        if (runtimeInformationType is null)
        {
            return null;
        }

        PropertyInfo? processArchitectureProperty = runtimeInformationType.GetProperty("ProcessArchitecture", BindingFlags.Public | BindingFlags.Static);
        if (processArchitectureProperty is null)
        {
            return null;
        }

        object? processArchitecture = processArchitectureProperty.GetValue(null);
        return processArchitecture is null ? null : MapArchitecture((int)processArchitecture);
    }
#endif
}
