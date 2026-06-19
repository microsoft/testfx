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
            TestArchitectures current = MapArchitecture(RuntimeInformation.ProcessArchitecture);
            return (_architectures & current) != 0;
        }
    }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "ArchitectureCondition";

    /// <summary>
    /// Maps a <see cref="Architecture"/> value to the matching <see cref="TestArchitectures"/> flag.
    /// </summary>
    /// <param name="architecture">The current process <see cref="Architecture"/>.</param>
    /// <returns>The matching <see cref="TestArchitectures"/> flag.</returns>
    private static TestArchitectures MapArchitecture(Architecture architecture)
        // The integer value is matched (rather than the named enum members) because some values such as
        // Architecture.RiscV64 don't exist in the net8.0 ref assembly (they were added in .NET 9), yet the
        // same source must compile for every supported .NET TFM.
        => (int)architecture switch
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
            _ => throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null),
        };
}
#endif
