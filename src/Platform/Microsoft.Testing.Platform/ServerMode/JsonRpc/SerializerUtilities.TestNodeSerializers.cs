// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
#if !NETCOREAPP
using Jsonite;
#endif
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode;

internal static partial class SerializerUtilities
{
    private static void RegisterTestNodeSerializers()
    {
        Serializers[typeof(Artifact)] = new ObjectSerializer<Artifact>(res => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Uri] = res.Uri,
            [JsonRpcStrings.Producer] = res.Producer,
            [JsonRpcStrings.Type] = res.Type,
            [JsonRpcStrings.DisplayName] = res.DisplayName,
            [JsonRpcStrings.Description] = res.Description,
        });

        Serializers[typeof(DiscoverResponseArgs)] = new ObjectSerializer<DiscoverResponseArgs>(_ => new Dictionary<string, object?>());

        Serializers[typeof(RunResponseArgs)] = new ObjectSerializer<RunResponseArgs>(res => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Attachments] = res.Artifacts.Select(f => Serialize(f)).ToList<object>(),
        });

        Serializers[typeof(TestNodeUpdateMessage)] = new ObjectSerializer<TestNodeUpdateMessage>(ev => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Node] = Serialize(ev.TestNode),
            [JsonRpcStrings.Parent] = ev.ParentTestNodeUid?.Value,
        });

        // Serialize event types.
        Serializers[typeof(TestNodeStateChangedEventArgs)] = new ObjectSerializer<TestNodeStateChangedEventArgs>(ev =>
        {
            List<object>? changes = null;
            if (ev.Changes is not null)
            {
#pragma warning disable IDE0028 // Collection initialization can be simplified - capacity hint is intentional.
                changes = new(ev.Changes.Length);
#pragma warning restore IDE0028
                for (int i = 0; i < ev.Changes.Length; i++)
                {
                    changes.Add(Serialize(ev.Changes[i]));
                }
            }

            return new Dictionary<string, object?>
            {
                [JsonRpcStrings.RunId] = ev.RunId,
                [JsonRpcStrings.Changes] = changes,
            };
        });

        Serializers[typeof(TestNode)] = new ObjectSerializer<TestNode>(
            n =>
            {
                // RECALL TO UPDATE TESTS INSIDE FormatterUtilitiesTests.cs
                Dictionary<string, object?> properties = new()
                {
                    [JsonRpcStrings.Uid] = n.Uid.Value,
                    [JsonRpcStrings.DisplayName] = n.DisplayName,
                };

                // Reserve the "traits" slot up-front so it appears immediately after
                // "display-name" in the serialized output (preserving the original wire
                // format). The placeholder is either assigned with the collected traits
                // below, or removed when no TestMetadataProperty is found.
                properties["traits"] = null;

                int attachmentIndex = 0;
#if NETCOREAPP
                List<KeyValuePair<string, string>>? traits = null;
#else
                JsonArray? traits = null;
#endif

                // Collected up-front because the assertion values are rendered inside the failed-state branch
                // below, and property order inside the bag is not guaranteed. The state lookup is O(1) (it is a
                // dedicated PropertyBag field), so non-failed nodes — discovery, in-progress, passed, which are
                // the vast majority — never pay for the linked-list walk.
                AssertionFailureProperty? assertionFailure = n.Properties.SingleOrDefault<TestNodeStateProperty>() is FailedTestNodeStateProperty
                    ? n.Properties.SingleOrDefault<AssertionFailureProperty>()
                    : null;

                foreach (IProperty property in n.Properties)
                {
                    if (property is TestMetadataProperty metadataProperty)
                    {
#if NETCOREAPP
                        (traits ??= []).Add(new KeyValuePair<string, string>(metadataProperty.Key, metadataProperty.Value));
#else
                        (traits ??= []).Add(new JsonObject { { metadataProperty.Key, metadataProperty.Value } });
#endif
                        continue;
                    }

                    if (property is SerializableKeyValuePairStringProperty keyValuePairProperty)
                    {
                        properties[keyValuePairProperty.Key] = keyValuePairProperty.Value;
                        continue;
                    }

                    if (property is TestFileLocationProperty fileLocationProperty)
                    {
                        properties["location.file"] = fileLocationProperty.FilePath;
                        properties["location.line-start"] = fileLocationProperty.LineSpan.Start.Line;
                        properties["location.line-end"] = fileLocationProperty.LineSpan.End.Line;
                        continue;
                    }

                    if (property is TestMethodIdentifierProperty testMethodIdentifierProperty)
                    {
                        properties["location.type"] = RoslynString.IsNullOrEmpty(testMethodIdentifierProperty.Namespace)
                            ? testMethodIdentifierProperty.TypeName
                            : $"{testMethodIdentifierProperty.Namespace}.{testMethodIdentifierProperty.TypeName}";

                        properties["location.method"] = testMethodIdentifierProperty.ParameterTypeFullNames.Length > 0
                            ? $"{testMethodIdentifierProperty.MethodName}({string.Join(",", testMethodIdentifierProperty.ParameterTypeFullNames)})"
                            : testMethodIdentifierProperty.MethodName;

                        properties["location.method-arity"] = testMethodIdentifierProperty.MethodArity;
                        continue;
                    }

                    if (property is StandardOutputProperty consoleStandardOutputProperty)
                    {
                        properties["standardOutput"] = consoleStandardOutputProperty.StandardOutput;
                        continue;
                    }

                    if (property is StandardErrorProperty standardErrorProperty)
                    {
                        properties["standardError"] = standardErrorProperty.StandardError;
                        continue;
                    }

                    if (property is TestNodeExecutionCompletedProperty)
                    {
                        // The server protocol has no outcome-less terminal state. Return the action to discovered
                        // so clients clear in-progress state without recording a test outcome.
                        properties["node-type"] = "action";
                        properties["execution-state"] = "discovered";
                        continue;
                    }

                    if (property is TestNodeStateProperty testNodeStateProperty)
                    {
                        properties["node-type"] = "action";
                        switch (property)
                        {
                            case DiscoveredTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "discovered";
                                    break;
                                }

                            case InProgressTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "in-progress";
                                    break;
                                }

                            case PassedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "passed";
                                    break;
                                }

                            case SkippedTestNodeStateProperty skippedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "skipped";

                                    if (!RoslynString.IsNullOrEmpty(skippedTestNodeStateProperty.Explanation))
                                    {
                                        properties["error.message"] = skippedTestNodeStateProperty.Explanation;
                                    }

                                    break;
                                }

                            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "failed";
                                    properties["error.message"] = failedTestNodeStateProperty.Explanation ?? failedTestNodeStateProperty.Exception?.Message;
                                    Exception? exception = failedTestNodeStateProperty.Exception;
                                    if (exception is not null)
                                    {
                                        properties["error.stacktrace"] = exception.StackTrace ?? string.Empty;
                                    }

                                    // AssertionFailureProperty is the supported channel; Exception.Data is the
                                    // legacy fallback for producers that have not been updated yet. The choice is
                                    // all-or-nothing so the two halves of a diff always come from the same producer.
                                    if (assertionFailure is not null)
                                    {
                                        properties["assert.actual"] = assertionFailure.Actual ?? string.Empty;
                                        properties["assert.expected"] = assertionFailure.Expected ?? string.Empty;
                                    }
                                    else if (exception is not null)
                                    {
                                        properties["assert.actual"] = exception.Data["assert.actual"] ?? string.Empty;
                                        properties["assert.expected"] = exception.Data["assert.expected"] ?? string.Empty;
                                    }

                                    break;
                                }

                            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "timed-out";
                                    properties["error.message"] = timeoutTestNodeStateProperty.Explanation ?? timeoutTestNodeStateProperty.Exception?.Message;
                                    if (timeoutTestNodeStateProperty.Exception is not null)
                                    {
                                        properties["error.stacktrace"] = timeoutTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "error";
                                    properties["error.message"] = errorTestNodeStateProperty.Explanation ?? errorTestNodeStateProperty.Exception?.Message;
                                    if (errorTestNodeStateProperty.Exception is not null)
                                    {
                                        properties["error.stacktrace"] = errorTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                            case CancelledTestNodeStateProperty canceledTestNodeStateProperty:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                                {
                                    properties["execution-state"] = "canceled";
                                    properties["error.message"] = canceledTestNodeStateProperty.Explanation ?? canceledTestNodeStateProperty.Exception?.Message;
                                    if (canceledTestNodeStateProperty.Exception is not null)
                                    {
                                        properties["error.stacktrace"] = canceledTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            default:
                                throw new NotSupportedException($"Unsupported TestNodeStateProperty '{testNodeStateProperty.GetType()}'");
                        }

                        continue;
                    }

                    if (property is TimingProperty timingProperty)
                    {
                        properties["time.duration-ms"] = timingProperty.GlobalTiming.Duration.TotalMilliseconds;
                        continue;
                    }

                    // In-process retry attribution (MSTest's [Retry], ...). Without this the property would fall
                    // through the chain and be silently dropped, so a server-mode client (an IDE) would see several
                    // updates for the same test node uid with no way to tell the attempts apart or to know which
                    // one is the test's final outcome.
                    if (property is RetryAttemptProperty retryAttemptProperty)
                    {
                        properties["retry.attempt"] = retryAttemptProperty.AttemptNumber;
                        properties["retry.is-superseded"] = retryAttemptProperty.IsSuperseded;
                        continue;
                    }

                    if (property is FileArtifactProperty artifact)
                    {
                        properties[$"attachments.{attachmentIndex}.uri"] = artifact.FileInfo.FullName;
                        properties[$"attachments.{attachmentIndex}.display-name"] = artifact.DisplayName;
                        properties[$"attachments.{attachmentIndex}.description"] = artifact.Description;
                        attachmentIndex++;
                        continue;
                    }
                }

                if (traits is not null)
                {
                    properties["traits"] = traits;
                }
                else
                {
                    properties.Remove("traits");
                }

                if (!properties.ContainsKey("node-type"))
                {
                    properties.Add("node-type", "group");
                }

                return properties;
            });
    }
}
