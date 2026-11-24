// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class NamedPipeServer
{
    [SupportedOSPlatformGuard("windows")]
    private static bool RequiresExplicitSecurity(IReadOnlyList<string>? authorizedSecurityIdentities)
        => authorizedSecurityIdentities is { Count: > 0 } && NamedPipeServerSecurity.IsSupported;

    /// <summary>
    /// Creates the underlying server stream, using the hardened Windows path only when a caller actually
    /// asked for extra authorization. Every other run — including every run on a non-Windows
    /// operating system — keeps the untouched <c>PipeOptions.CurrentUserOnly</c> pipe.
    /// </summary>
    private static NamedPipeServerStream CreateServerStream(string name, int maxNumberOfServerInstances, IReadOnlyList<string>? authorizedSecurityIdentities)
    {
        if (RequiresExplicitSecurity(authorizedSecurityIdentities))
        {
            // Snapshot before validating. The sequence comes from an extension, so re-enumerating it is not
            // guaranteed to yield the same values; validating one enumeration and composing the descriptor
            // from another would let a value that was never checked reach the SDDL. Everything downstream
            // works off this copy, and NamedPipeServerSecurity.BuildSecurityDescriptor re-validates at the
            // point of concatenation as well.
            string[] securityIdentities = [.. authorizedSecurityIdentities!];

            foreach (string securityIdentity in securityIdentities)
            {
                if (!NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(securityIdentity))
                {
                    throw new ArgumentException(
                        $"'{securityIdentity ?? "<null>"}' does not identify a single sandboxed application and cannot be authorized on the test host controller pipe.",
                        nameof(authorizedSecurityIdentities));
                }
            }

            return NamedPipeServerSecurity.CreateServerStream(name, maxNumberOfServerInstances, securityIdentities);
        }

        return new NamedPipeServerStream(name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, AsyncCurrentUserPipeOptions);
    }
}
