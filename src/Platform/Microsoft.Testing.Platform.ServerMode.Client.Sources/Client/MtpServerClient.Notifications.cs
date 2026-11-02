// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerClient
{
    private void OnNotificationReceived(NotificationMessage notification)
    {
        switch (notification.Method)
        {
            case JsonRpcMethods.TestingTestUpdatesTests:
                RaiseTestNodesUpdated(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.ClientLog:
            case JsonRpcMethods.ClientShowMessage:
                RaiseLog(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.TelemetryUpdate:
                RaiseTelemetry(notification.Params as IDictionary<string, object?>);
                break;

            case JsonRpcMethods.TestingTestUpdatesAttachments:
                RaiseAttachments(notification.Params as IDictionary<string, object?>);
                break;

            default:
                break;
        }
    }

    private void RaiseTestNodesUpdated(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpTestNodeUpdateEventArgs>? handler = TestNodesUpdated;
        if (@params is null || handler is null)
        {
            return;
        }

        // A 'changes: null' payload is the completion sentinel the server sends right before the run/discover
        // response; there is nothing to report, so skip it (do not raise an empty update for it).
        if (!@params.TryGetValue(JsonRpcStrings.Changes, out object? changesObj) || changesObj is null)
        {
            return;
        }

        Guid runId = Guid.Empty;
        if (@params.TryGetValue(JsonRpcStrings.RunId, out object? runIdObj) && runIdObj is string runIdString
            && Guid.TryParse(runIdString, out Guid parsedRunId))
        {
            runId = parsedRunId;
        }

        var changes = new List<MtpTestNodeUpdate>();
        if (changesObj is ICollection<object> changeItems)
        {
            foreach (IDictionary<string, object?> change in changeItems.OfType<IDictionary<string, object?>>())
            {
                if (!change.TryGetValue(JsonRpcStrings.Node, out object? nodeObj)
                    || nodeObj is not IDictionary<string, object?> node)
                {
                    continue;
                }

                string? parentUid = change.TryGetValue(JsonRpcStrings.Parent, out object? parentObj) ? parentObj as string : null;
                changes.Add(new MtpTestNodeUpdate(node, parentUid));
            }
        }

        handler(this, new MtpTestNodeUpdateEventArgs(runId, changes));
    }

    private void RaiseLog(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpLogEventArgs>? handler = LogReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        string level = (@params.TryGetValue(JsonRpcStrings.Level, out object? levelObj) ? levelObj as string : null) ?? string.Empty;
        string message = (@params.TryGetValue(JsonRpcStrings.Message, out object? messageObj) ? messageObj as string : null) ?? string.Empty;
        handler(this, new MtpLogEventArgs(level, message));
    }

    private void RaiseTelemetry(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpTelemetryEventArgs>? handler = TelemetryReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        string eventName = (@params.TryGetValue(JsonRpcStrings.EventName, out object? eventNameObj) ? eventNameObj as string : null) ?? string.Empty;

        var metrics = new Dictionary<string, object?>();
        if (@params.TryGetValue(JsonRpcStrings.Metrics, out object? metricsObj) && metricsObj is IDictionary<string, object?> metricsDict)
        {
            foreach (KeyValuePair<string, object?> pair in metricsDict)
            {
                metrics[pair.Key] = pair.Value;
            }
        }

        handler(this, new MtpTelemetryEventArgs(eventName, metrics));
    }

    private void RaiseAttachments(IDictionary<string, object?>? @params)
    {
        EventHandler<MtpAttachmentsEventArgs>? handler = AttachmentsReceived;
        if (@params is null || handler is null)
        {
            return;
        }

        var attachments = new List<MtpAttachment>();
        if (@params.TryGetValue(JsonRpcStrings.Attachments, out object? attachmentsObj)
            && attachmentsObj is ICollection<object> attachmentItems)
        {
            foreach (IDictionary<string, object?> attachment in attachmentItems.OfType<IDictionary<string, object?>>())
            {
                attachments.Add(new MtpAttachment(
                    attachment.TryGetValue(JsonRpcStrings.Uri, out object? uriObj) ? uriObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Producer, out object? producerObj) ? producerObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Type, out object? typeObj) ? typeObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.DisplayName, out object? displayNameObj) ? displayNameObj as string : null,
                    attachment.TryGetValue(JsonRpcStrings.Description, out object? descriptionObj) ? descriptionObj as string : null));
            }
        }

        handler(this, new MtpAttachmentsEventArgs(attachments));
    }
}
