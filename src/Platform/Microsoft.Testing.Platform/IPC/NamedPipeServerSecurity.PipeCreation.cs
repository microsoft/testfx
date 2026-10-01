// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO.Pipes;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.Testing.Platform.IPC;

internal static partial class NamedPipeServerSecurity
{
    private const uint SddlRevision1 = 1;
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint PipeTypeByte = 0x00000000;
    private const uint PipeReadModeByte = 0x00000000;
    private const uint PipeWait = 0x00000000;
    private const uint PipeRejectRemoteClients = 0x00000008;
    private const uint PipeUnlimitedInstances = 255;

    private static readonly IntPtr InvalidHandleValue = new(-1);

    /// <summary>
    /// Creates a named pipe server stream whose DACL grants the current token's owner full control and each
    /// identity in <paramref name="authorizedSecurityIdentities"/> the minimum rights required to connect
    /// and exchange messages. Every identity is validated here, at the point it is composed into the
    /// security descriptor.
    /// </summary>
    /// <param name="pipeName">The pipe name, without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="maxNumberOfServerInstances">The maximum number of concurrent server instances.</param>
    /// <param name="authorizedSecurityIdentities">The identities to authorize.</param>
    /// <returns>An asynchronous, not-yet-connected server stream.</returns>
    [SupportedOSPlatform("windows")]
    internal static NamedPipeServerStream CreateServerStream(string pipeName, int maxNumberOfServerInstances, IReadOnlyList<string> authorizedSecurityIdentities)
        => CreateServerStreamCore(
            GetNativePipePath(pipeName, authorizedSecurityIdentities),
            maxNumberOfServerInstances,
            BuildSecurityDescriptor(GetCurrentProcessOwnerSid(), authorizedSecurityIdentities));

    /// <summary>
    /// Creates a named pipe server stream protected by a caller-supplied SDDL security descriptor.
    /// </summary>
    /// <remarks>
    /// <strong>This overload performs no validation</strong> — the descriptor is passed to Windows verbatim.
    /// It exists so tests can exercise arbitrary descriptors, including deliberately malformed ones. Product
    /// code must use <see cref="CreateServerStream(string, int, IReadOnlyList{string})"/>, which validates
    /// every identity at the point it is composed. The name is deliberately unlike the validating overload
    /// so this one can never be selected by overload-resolution accident.
    /// </remarks>
    /// <param name="pipeName">The pipe name, without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="maxNumberOfServerInstances">The maximum number of concurrent server instances.</param>
    /// <param name="securityDescriptorSddl">The security descriptor in SDDL form.</param>
    /// <returns>An asynchronous, not-yet-connected server stream.</returns>
    [SupportedOSPlatform("windows")]
    internal static NamedPipeServerStream CreateServerStreamWithExplicitSecurityDescriptor(string pipeName, int maxNumberOfServerInstances, string securityDescriptorSddl)
        => CreateServerStreamCore($@"\\.\pipe\{pipeName}", maxNumberOfServerInstances, securityDescriptorSddl);

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateServerStreamCore(string nativePipePath, int maxNumberOfServerInstances, string securityDescriptorSddl)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(securityDescriptorSddl, SddlRevision1, out IntPtr securityDescriptor, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to build the security descriptor for the named pipe '{nativePipePath}'.");
        }

        try
        {
            SecurityAttributes securityAttributes = new()
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = securityDescriptor,

                // The controller must never leak the listening handle into the test host (or any other
                // child), which would bypass the DACL entirely.
                InheritHandle = 0,
            };

            uint openMode = PipeAccessDuplex
                | FileFlagOverlapped
                // Mirrors what NamedPipeServerStream does: for a single-instance pipe, refuse to attach to a
                // name somebody else already created (anti-squatting).
                | (maxNumberOfServerInstances == 1 ? FileFlagFirstPipeInstance : 0);

            uint pipeMode = PipeTypeByte | PipeReadModeByte | PipeWait | PipeRejectRemoteClients;
            uint maxInstances = maxNumberOfServerInstances == -1 ? PipeUnlimitedInstances : (uint)maxNumberOfServerInstances;

            IntPtr handle = CreateNamedPipe(
                nativePipePath,
                openMode,
                pipeMode,
                maxInstances,
                nOutBufferSize: 0,
                nInBufferSize: 0,
                nDefaultTimeOut: 0,
                ref securityAttributes);

            if (handle == InvalidHandleValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to create the named pipe '{nativePipePath}'.");
            }

            var safePipeHandle = new SafePipeHandle(handle, ownsHandle: true);
            try
            {
                return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, safePipeHandle);
            }
            catch
            {
                safePipeHandle.Dispose();
                throw;
            }
        }
        finally
        {
            LocalFree(securityDescriptor);
        }
    }

    [SupportedOSPlatform("windows")]
    private static string GetNativePipePath(string pipeName, IReadOnlyList<string> authorizedSecurityIdentities)
    {
        if (!pipeName.StartsWith(SandboxedApplicationPipeNamePrefix, StringComparison.Ordinal))
        {
            return $@"\\.\pipe\{pipeName}";
        }

        if (authorizedSecurityIdentities.Count != 1)
        {
            throw new InvalidOperationException(
                $"AppContainer-local pipe '{pipeName}' requires exactly one authorized package SID, but received {authorizedSecurityIdentities.Count}.");
        }

        if (!ConvertStringSidToSid(authorizedSecurityIdentities[0], out SafeLocalAllocHandle appContainerSid))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Failed to parse the AppContainer SID '{authorizedSecurityIdentities[0]}'.");
        }

        using (appContainerSid)
        {
            _ = GetAppContainerNamedObjectPath(
                IntPtr.Zero,
                appContainerSid.DangerousGetHandle(),
                objectPathLength: 0,
                objectPath: null,
                out uint requiredLength);
            if (requiredLength == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"Failed to query the named-object path for AppContainer SID '{authorizedSecurityIdentities[0]}'.");
            }

            var objectPath = new StringBuilder((int)requiredLength);
            if (!GetAppContainerNamedObjectPath(
                IntPtr.Zero,
                appContainerSid.DangerousGetHandle(),
                requiredLength,
                objectPath,
                out _))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"Failed to resolve the named-object path for AppContainer SID '{authorizedSecurityIdentities[0]}'.");
            }

            string unqualifiedPipeName = pipeName[SandboxedApplicationPipeNamePrefix.Length..];
            string namedObjectPath = objectPath.ToString().Trim('\\');
            if (!namedObjectPath.StartsWith("Sessions\\", StringComparison.OrdinalIgnoreCase))
            {
                if (!ProcessIdToSessionId(GetCurrentProcessId(), out uint sessionId))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to resolve the current Windows session.");
                }

                namedObjectPath = $@"Sessions\{sessionId}\{namedObjectPath}";
            }

            return $@"\\.\pipe\{namedObjectPath}\{unqualifiedPipeName}";
        }
    }

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the platform wrapper for the current process ID.")]
    private static uint GetCurrentProcessId()
#if NETCOREAPP
        => unchecked((uint)Environment.ProcessId);
#else
        => GetCurrentProcessIdNative();
#endif
}
