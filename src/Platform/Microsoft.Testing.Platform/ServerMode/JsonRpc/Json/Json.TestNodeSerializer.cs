// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static (string Name, object? Value)[] BuildTestNodeProperties(TestNode message)
    {
        List<(string Name, object? Value)> properties =
        [
            with(capacity: 16),
            (JsonRpcStrings.Uid, message.Uid.Value),
            (JsonRpcStrings.DisplayName, message.DisplayName)
        ];

        List<KeyValuePair<string, string>>? traits = null;
        bool hasActionNodeType = false;

        int attachmentIndex = 0;
        foreach (IProperty property in message.Properties)
        {
            if (property is TestMetadataProperty metadataProperty)
            {
                (traits ??= []).Add(new KeyValuePair<string, string>(metadataProperty.Key, metadataProperty.Value));
                continue;
            }

            if (property is SerializableKeyValuePairStringProperty keyValuePairProperty)
            {
                properties.Add((keyValuePairProperty.Key, keyValuePairProperty.Value));
                continue;
            }

            if (property is TestFileLocationProperty fileLocationProperty)
            {
                properties.Add(("location.file", fileLocationProperty.FilePath));
                properties.Add(("location.line-start", fileLocationProperty.LineSpan.Start.Line));
                properties.Add(("location.line-end", fileLocationProperty.LineSpan.End.Line));
                continue;
            }

            if (property is TestMethodIdentifierProperty testMethodIdentifierProperty)
            {
                properties.Add(("location.type", RoslynString.IsNullOrEmpty(testMethodIdentifierProperty.Namespace)
                    ? testMethodIdentifierProperty.TypeName
                    : $"{testMethodIdentifierProperty.Namespace}.{testMethodIdentifierProperty.TypeName}"));

                properties.Add(("location.method", testMethodIdentifierProperty.ParameterTypeFullNames.Length > 0
                    ? $"{testMethodIdentifierProperty.MethodName}({string.Join(',', testMethodIdentifierProperty.ParameterTypeFullNames)})"
                    : testMethodIdentifierProperty.MethodName));

                properties.Add(("location.method-arity", testMethodIdentifierProperty.MethodArity));

                continue;
            }

            if (property is StandardOutputProperty standardOutputProperty)
            {
                properties.Add(("standardOutput", standardOutputProperty.StandardOutput));
            }

            if (property is StandardErrorProperty standardErrorProperty)
            {
                properties.Add(("standardError", standardErrorProperty.StandardError));
            }

            if (property is TestNodeStateProperty testNodeStateProperty)
            {
                properties.Add(("node-type", "action"));
                hasActionNodeType = true;
                switch (property)
                {
                    case DiscoveredTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "discovered"));
                            break;
                        }

                    case InProgressTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "in-progress"));
                            break;
                        }

                    case PassedTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "passed"));
                            break;
                        }

                    case SkippedTestNodeStateProperty skippedTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "skipped"));
                            if (!RoslynString.IsNullOrEmpty(skippedTestNodeStateProperty.Explanation))
                            {
                                properties.Add(("error.message", skippedTestNodeStateProperty.Explanation));
                            }

                            break;
                        }

                    case FailedTestNodeStateProperty failedTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "failed"));
                            Exception? exception = failedTestNodeStateProperty.Exception;
                            properties.Add(("error.message", failedTestNodeStateProperty.Explanation ?? exception?.Message));
                            if (exception is not null)
                            {
                                properties.Add(("error.stacktrace", exception.StackTrace ?? string.Empty));
                                properties.Add(("assert.actual", exception.Data["assert.actual"] ?? string.Empty));
                                properties.Add(("assert.expected", exception.Data["assert.expected"] ?? string.Empty));
                            }

                            break;
                        }

                    case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "timed-out"));
                            Exception? exception = timeoutTestNodeStateProperty.Exception;
                            properties.Add(("error.message", timeoutTestNodeStateProperty.Explanation ?? exception?.Message));
                            if (exception is not null)
                            {
                                properties.Add(("error.stacktrace", exception.StackTrace ?? string.Empty));
                            }

                            break;
                        }

                    case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                        {
                            properties.Add(("execution-state", "error"));
                            Exception? exception = errorTestNodeStateProperty.Exception;
                            properties.Add(("error.message", errorTestNodeStateProperty.Explanation ?? exception?.Message));
                            if (exception is not null)
                            {
                                properties.Add(("error.stacktrace", exception.StackTrace ?? string.Empty));
                            }

                            break;
                        }

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                    case CancelledTestNodeStateProperty canceledTestNodeStateProperty:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                        {
                            properties.Add(("execution-state", "canceled"));
                            Exception? exception = canceledTestNodeStateProperty.Exception;
                            properties.Add(("error.message", canceledTestNodeStateProperty.Explanation ?? exception?.Message));
                            if (exception is not null)
                            {
                                properties.Add(("error.stacktrace", exception.StackTrace ?? string.Empty));
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
                properties.Add(("time.duration-ms", timingProperty.GlobalTiming.Duration.TotalMilliseconds));
                continue;
            }

            if (property is FileArtifactProperty artifact)
            {
                properties.Add(($"attachments.{attachmentIndex}.uri", artifact.FileInfo.FullName));
                properties.Add(($"attachments.{attachmentIndex}.display-name", artifact.DisplayName));
                properties.Add(($"attachments.{attachmentIndex}.description", artifact.Description));
                attachmentIndex++;
                continue;
            }
        }

        if (traits is not null)
        {
            // Insert "traits" right after "uid" and "display-name" to preserve the
            // original wire format ordering.
            properties.Insert(2, ("traits", traits));
        }

        if (!hasActionNodeType)
        {
            properties.Add(("node-type", "group"));
        }

        return [.. properties];
    }
}
