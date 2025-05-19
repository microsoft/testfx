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
    public override bool ShouldRun
#if NET462
        // On .NET Framework, we are sure we are running on Windows.
        => (_operatingSystems & OperatingSystems.Windows) != 0;
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

    /// <summary>
    /// Gets the ignore message (in case <see cref="ShouldRun"/> returns <see langword="false"/>).
    /// </summary>
    public override string? IgnoreMessage { get; }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => "OSCondition";
}
