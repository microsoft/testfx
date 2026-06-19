// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An enum that is used with <see cref="ArchitectureConditionAttribute"/> to control which process architectures a test method or test class supports or doesn't support.
/// </summary>
[Flags]
public enum TestArchitectures
{
    /// <summary>
    /// Represents the x86 architecture.
    /// </summary>
    X86 = 1 << 0,

    /// <summary>
    /// Represents the x64 architecture.
    /// </summary>
    X64 = 1 << 1,

    /// <summary>
    /// Represents the ARM architecture.
    /// </summary>
    Arm = 1 << 2,

    /// <summary>
    /// Represents the ARM64 architecture.
    /// </summary>
    Arm64 = 1 << 3,

    /// <summary>
    /// Represents the WebAssembly platform.
    /// </summary>
    Wasm = 1 << 4,

    /// <summary>
    /// Represents the S390x architecture.
    /// </summary>
    S390x = 1 << 5,

    /// <summary>
    /// Represents the LoongArch64 architecture.
    /// </summary>
    LoongArch64 = 1 << 6,

    /// <summary>
    /// Represents the 32-bit ARMv6 architecture.
    /// </summary>
    Armv6 = 1 << 7,

    /// <summary>
    /// Represents the 64-bit PowerPC little-endian architecture.
    /// </summary>
    Ppc64le = 1 << 8,

    /// <summary>
    /// Represents the 64-bit RISC-V architecture.
    /// </summary>
    RiscV64 = 1 << 9,
}
