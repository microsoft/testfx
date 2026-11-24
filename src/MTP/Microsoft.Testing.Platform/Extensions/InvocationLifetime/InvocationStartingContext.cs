// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// The context passed to <see cref="IInvocationLifetimeHandler.OnInvocationStartingAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// The platform creates the instances. There is no public constructor so that publishing values stays under the
/// platform's control; test a handler through a test application rather than by constructing this type.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class InvocationStartingContext
{
    private readonly Action<string, string?> _setEnvironmentVariable;

    internal InvocationStartingContext(Action<string, string?> setEnvironmentVariable)
        => _setEnvironmentVariable = setEnvironmentVariable;

    /// <summary>
    /// Publishes an environment variable to the test processes of the invocation.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <param name="value">The value of the environment variable, or <see langword="null"/> to remove it.</param>
    /// <remarks>
    /// The variable is set on the current process, so the test processes launched afterward inherit it. When the
    /// same name is written more than once, the last write wins. The invocation lifetime never logs the values, but
    /// they are ordinary environment variables of the process: other extensions can read, log, or replace them.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, contains <c>=</c> or a null character, or uses the reserved
    /// <c>TESTINGPLATFORM_INVOCATIONLIFETIME_</c> prefix.
    /// </exception>
    public void SetEnvironmentVariable(string name, string? value)
    {
        if (RoslynString.IsNullOrWhiteSpace(name)
            || name.IndexOf('=') >= 0
            || name.IndexOf('\0') >= 0
            || name.StartsWith(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, PlatformResources.InvocationLifetimeInvalidEnvironmentVariableName, name, EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_PREFIX),
                nameof(name));
        }

        _setEnvironmentVariable(name, value);
    }
}
