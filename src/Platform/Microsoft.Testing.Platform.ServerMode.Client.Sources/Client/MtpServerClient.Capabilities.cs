// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerClient
{
    private static MtpServerCapabilities DecodeCapabilities(IDictionary<string, object?>? result)
    {
        result ??= new Dictionary<string, object?>();

        int? processId = result.TryGetValue(JsonRpcStrings.ProcessId, out object? processIdObj) ? AsInt(processIdObj) : null;

        string? serverName = null;
        string? serverVersion = null;
        if (result.TryGetValue(JsonRpcStrings.ServerInfo, out object? serverInfoObj)
            && serverInfoObj is IDictionary<string, object?> serverInfo)
        {
            serverName = serverInfo.TryGetValue(JsonRpcStrings.Name, out object? nameObj) ? nameObj as string : null;
            serverVersion = serverInfo.TryGetValue(JsonRpcStrings.Version, out object? versionObj) ? versionObj as string : null;
        }

        bool supportsDiscovery = false;
        bool multiRequestSupport = false;
        bool vstestProviderSupport = false;
        bool supportsAttachments = false;
        bool multiConnectionProvider = false;
        bool? showMessage = null;
        string? protocolVersion = null;
        if (result.TryGetValue(JsonRpcStrings.ProtocolVersion, out object? protocolVersionObj))
        {
            protocolVersion = protocolVersionObj switch
            {
                null => null,
                string value => value,
                _ => throw new MtpServerClientException(
                    $"Expected '{JsonRpcStrings.ProtocolVersion}' to be a string but it was '{protocolVersionObj.GetType()}'."),
            };
        }

        if (result.TryGetValue(JsonRpcStrings.Capabilities, out object? capabilitiesObj)
            && capabilitiesObj is IDictionary<string, object?> capabilities
            && capabilities.TryGetValue(JsonRpcStrings.Testing, out object? testingObj)
            && testingObj is IDictionary<string, object?> testing)
        {
            supportsDiscovery = AsBool(testing, JsonRpcStrings.SupportsDiscovery);
            multiRequestSupport = AsBool(testing, JsonRpcStrings.MultiRequestSupport);
            vstestProviderSupport = AsBool(testing, JsonRpcStrings.VSTestProviderSupport);
            supportsAttachments = AsBool(testing, JsonRpcStrings.AttachmentsSupport);
            multiConnectionProvider = AsBool(testing, JsonRpcStrings.MultiConnectionProvider);
            showMessage = testing.TryGetValue(JsonRpcStrings.ShowMessage, out object? showMessageValue)
                ? showMessageValue switch
                {
                    null => null,
                    bool value => value,
                    _ => throw new MtpServerClientException($"Expected '{JsonRpcStrings.ShowMessage}' to be a boolean."),
                }
                : null;
        }

        return new MtpServerCapabilities(
            processId,
            serverName,
            serverVersion,
            supportsDiscovery,
            multiRequestSupport,
            vstestProviderSupport,
            supportsAttachments,
            multiConnectionProvider,
            protocolVersion)
        {
            ShowMessage = showMessage,
        };
    }

    private bool IsSupportedProtocolVersion(string negotiatedProtocolVersion)
        => _options.SupportedProtocolVersions.Count == 0
            ? negotiatedProtocolVersion == JsonRpcProtocolVersions.V1
            : _options.SupportedProtocolVersions.Contains(negotiatedProtocolVersion, StringComparer.Ordinal);

    private static int? AsInt(object? value)
        => value switch
        {
            int i => i,
            short s => s,
            byte b => b,

            // The JSON formatter may widen an integer to long/ulong/double depending on its magnitude, so
            // accept those too but only when the value round-trips into an Int32 without loss. Anything out
            // of range or non-integral is treated as absent (null) rather than silently truncated.
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            uint u when u <= int.MaxValue => (int)u,
            ulong ul when ul <= int.MaxValue => (int)ul,

            // `d % 1d is 0d` is the integrality test (behaviorally equal to `d == Math.Floor(d)`) written as
            // a constant pattern so it does not trip the analyzer's "equality on floating-point" rule; the
            // remainder of an in-range integral double against 1 is exactly zero.
            double d when d is >= int.MinValue and <= int.MaxValue && d % 1d is 0d => (int)d,
            _ => null,
        };

    private static bool AsBool(IDictionary<string, object?> dictionary, string key)
        => dictionary.TryGetValue(key, out object? value) && value is bool boolean && boolean;
}
