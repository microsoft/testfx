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

        Serializers[typeof(TestNodeUpdateMessage)] = new ObjectSerializer<TestNodeUpdateMessage>(ev =>
        {
            // TODO: Fill in the node properties
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Node] = Serialize(ev.TestNode),
                [JsonRpcStrings.Parent] = ev.ParentTestNodeUid?.Value,
            };

            return values;
        });

        // Serialize event types.
        Serializers[typeof(TestNodeStateChangedEventArgs)] = new ObjectSerializer<TestNodeStateChangedEventArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.RunId] = ev.RunId,
                [JsonRpcStrings.Changes] = ev.Changes?.Select(ch => Serialize(ch)).ToList<object>(),
            };

            return values;
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
                                    if (failedTestNodeStateProperty.Exception != null)
                                    {
                                        Exception exception = failedTestNodeStateProperty.Exception;
                                        properties["error.stacktrace"] = exception.StackTrace ?? string.Empty;
                                        properties["assert.actual"] = exception.Data["assert.actual"] ?? string.Empty;
                                        properties["assert.expected"] = exception.Data["assert.expected"] ?? string.Empty;
                                    }

                                    break;
                                }

                            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "timed-out";
                                    properties["error.message"] = timeoutTestNodeStateProperty.Explanation ?? timeoutTestNodeStateProperty.Exception?.Message;
                                    if (timeoutTestNodeStateProperty.Exception != null)
                                    {
                                        properties["error.stacktrace"] = timeoutTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "error";
                                    properties["error.message"] = errorTestNodeStateProperty.Explanation ?? errorTestNodeStateProperty.Exception?.Message;
                                    if (errorTestNodeStateProperty.Exception != null)
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
                                    if (canceledTestNodeStateProperty.Exception != null)
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
