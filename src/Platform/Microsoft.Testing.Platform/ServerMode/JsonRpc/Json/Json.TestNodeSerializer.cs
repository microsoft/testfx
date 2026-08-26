// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static (string Name, object? Value)[] BuildTestNodeProperties(TestNode message)
    {
        // IDE0028 would rewrite this to a collection expression [with(capacity: 16), ...], but the
        // 'with(...)' collection-expression-argument is a C# *preview* feature. The source-package
        // consumer compiles this file with LangVersion=latest (supplied by build/*.targets), where
        // preview features are unsupported, so keep the explicit capacity-ctor + initializer form.
#pragma warning disable IDE0028 // Collection initialization can be simplified
        List<(string Name, object? Value)> properties = new(capacity: 16)
        {
            (JsonRpcStrings.Uid, message.Uid.Value),
            (JsonRpcStrings.DisplayName, message.DisplayName),
        };
#pragma warning restore IDE0028 // Collection initialization can be simplified

        List<KeyValuePair<string, string>>? traits = null;
        bool hasActionNodeType = false;

        // Collected up-front because the assertion values are rendered inside the failed-state branch below,
        // and property order inside the bag is not guaranteed. The state lookup is O(1) (it is a dedicated
        // PropertyBag field), so non-failed nodes — discovery, in-progress, passed, which are the vast
        // majority — never pay for the linked-list walk.
        AssertionFailureProperty? assertionFailure = message.Properties.SingleOrDefault<TestNodeStateProperty>() is FailedTestNodeStateProperty
            ? message.Properties.SingleOrDefault<AssertionFailureProperty>()
            : null;

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

            if (property is TestNodeExecutionCompletedProperty)
            {
                // The server protocol has no outcome-less terminal state. Return the action to discovered
                // so clients clear in-progress state without recording a test outcome.
                properties.Add(("node-type", "action"));
                properties.Add(("execution-state", "discovered"));
                hasActionNodeType = true;
                continue;
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
                            }

                            // AssertionFailureProperty is the supported channel; Exception.Data is the legacy
                            // fallback for producers that have not been updated yet. The choice is
                            // all-or-nothing so the two halves of a diff always come from the same producer.
                            if (assertionFailure is not null)
                            {
                                properties.Add(("assert.actual", assertionFailure.Actual ?? string.Empty));
                                properties.Add(("assert.expected", assertionFailure.Expected ?? string.Empty));
                            }
                            else if (exception is not null)
                            {
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

            // In-process retry attribution (MSTest's [Retry], ...). Kept in sync with
            // SerializerUtilities.TestNodeSerializers.cs: an unhandled property falls through this chain and is
            // silently dropped, which would leave a server-mode client unable to tell repeated updates for the same
            // test node uid apart, or to know which one is the test's final outcome.
            if (property is RetryAttemptProperty retryAttemptProperty)
            {
                properties.Add(("retry.attempt", retryAttemptProperty.AttemptNumber));
                properties.Add(("retry.is-superseded", retryAttemptProperty.IsSuperseded));
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
