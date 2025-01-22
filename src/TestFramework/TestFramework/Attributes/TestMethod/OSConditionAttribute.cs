// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to ignore a test class or a test method, with an optional message.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not cause derived classes to be ignored.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class OSConditionAttribute : ConditionBaseAttribute
{
    private readonly OperatingSystems _operatingSystems;

    /// <summary>
    /// Initializes a new instance of the <see cref="OSConditionAttribute"/> class.
    /// </summary>
    /// <param name="operatingSystems">The operating systems that this test supports.</param>
    public OSConditionAttribute(OperatingSystems operatingSystems, Mode mode)
        : base(mode)
    {
        _operatingSystems = operatingSystems;
        ConditionalIgnoreMessage = $"Test is only supported on {operatingSystems}";
    }

    public OSConditionAttribute(OperatingSystems operatingSystems)
        : this(operatingSystems, Mode.Include)
    {
    }

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
                return (_operatingSystems & OperatingSystems.MacOSX) != 0;
            }

            return false;
        }
    }
#endif

    public override string? ConditionalIgnoreMessage { get; }

    public override string GroupName => nameof(OSConditionAttribute);
}
