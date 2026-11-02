// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal static partial class NamedPipeServerSecurity
{
    /// <summary>
    /// Builds the SDDL security descriptor for the controller pipe.
    /// </summary>
    /// <remarks>
    /// This is the sink where caller-supplied strings are concatenated into a security descriptor, so it
    /// validates every identity itself rather than trusting that a caller already did. That is deliberate:
    /// a caller that validates one enumeration of a sequence and then hands the sequence on cannot
    /// guarantee the second enumeration yields the same values, and an unvalidated value containing
    /// <c>)</c> or <c>(</c> would break out of its ACE and inject additional ones. Validating at the point
    /// of concatenation makes that class of bug impossible regardless of how the sequence behaves.
    /// </remarks>
    /// <param name="ownerSid">The current token's owner SID, which owns the pipe and gets full control.</param>
    /// <param name="authorizedSecurityIdentities">
    /// The identities that additionally get the minimum connect/exchange rights.
    /// </param>
    /// <returns>The SDDL string.</returns>
    /// <exception cref="ArgumentException">
    /// An entry does not identify a single sandboxed application.
    /// </exception>
    internal static string BuildSecurityDescriptor(string ownerSid, IReadOnlyList<string> authorizedSecurityIdentities)
    {
        string owner = Normalize(ownerSid);

        // Copy to an array first. The sequence can come from an extension, so its Count and indexer are not
        // guaranteed to be stable or even self-consistent; once the values are in an array, the value that
        // is validated below is provably the same value that is appended.
        //
        // The snapshot alone is NOT sufficient, and the validation below must not be removed on the grounds
        // that a caller already validated: when the runtime type also implements ICollection<T>, this spread
        // lowers to ICollection<T>.CopyTo, so a hostile implementation still chooses what lands in the
        // array. Validating here — against the very local that is then appended — is what actually closes
        // the hole, because string is immutable and Normalize is pure.
        string[] securityIdentities = [.. authorizedSecurityIdentities];

        var builder = new StringBuilder();

        // Owner and group mirror what PipeOptions.CurrentUserOnly does; 'P' protects the DACL from
        // inheritance so nothing can widen it behind our back.
        builder.Append(CultureInfo.InvariantCulture, $"O:{owner}G:{owner}D:P");
        builder.Append(CultureInfo.InvariantCulture, $"(A;;0x{PipeAccessRightsFullControl:x};;;{owner})");

        foreach (string securityIdentity in securityIdentities)
        {
            if (!IsAuthorizableSandboxedApplicationIdentity(securityIdentity))
            {
                throw new ArgumentException(
                    $"'{securityIdentity ?? "<null>"}' does not identify a single sandboxed application and cannot be authorized on the test host controller pipe.",
                    nameof(authorizedSecurityIdentities));
            }

            builder.Append(CultureInfo.InvariantCulture, $"(A;;0x{PipeAccessRightsReadWriteSynchronize:x};;;{Normalize(securityIdentity)})");
        }

        // No mandatory label is emitted: the pipe keeps the implicit (medium) integrity level of the
        // controller, so Mandatory Integrity Control stays a second gate behind the DACL. An AppContainer
        // client is admitted by its package-SID ACE alone — a lowbox token's access check is satisfied by
        // that ACE and is not blocked by the object's medium label.
        return builder.ToString();
    }
}
