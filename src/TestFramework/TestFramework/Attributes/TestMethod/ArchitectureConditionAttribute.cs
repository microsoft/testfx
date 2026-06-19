// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#if NET
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
    private readonly TestArchitectures _architectures;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the architectures will be included or excluded.</param>
    /// <param name="architectures">The process architectures that this test includes/excludes.</param>
    public ArchitectureConditionAttribute(ConditionMode mode, TestArchitectures architectures)
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
    public ArchitectureConditionAttribute(TestArchitectures architectures)
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
            TestArchitectures? current = MapArchitecture((int)RuntimeInformation.ProcessArchitecture);
            return current is not null && (_architectures & current.Value) != 0;
        }
    }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "ArchitectureCondition";

    /// <summary>
    /// Maps the integer value of a <c>System.Runtime.InteropServices.Architecture</c> to the matching
    /// <see cref="TestArchitectures"/> flag. The integer value is used (rather than the named enum members) so that the
    /// code compiles against ref assemblies that predate the newer architecture values.
    /// </summary>
    /// <param name="value">The integer value of the <c>System.Runtime.InteropServices.Architecture</c>.</param>
    /// <returns>The matching <see cref="TestArchitectures"/> flag, or <see langword="null"/> if the value is unknown.</returns>
    private static TestArchitectures? MapArchitecture(int value)
        => value switch
        {
            0 => TestArchitectures.X86,
            1 => TestArchitectures.X64,
            2 => TestArchitectures.Arm,
            3 => TestArchitectures.Arm64,
            4 => TestArchitectures.Wasm,
            5 => TestArchitectures.S390x,
            6 => TestArchitectures.LoongArch64,
            7 => TestArchitectures.Armv6,
            8 => TestArchitectures.Ppc64le,
            9 => TestArchitectures.RiscV64,
            _ => null,
        };
}
#endif
