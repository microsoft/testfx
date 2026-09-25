// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class TestExecutionActivityContextStore
{
#pragma warning disable IDE0028 // A collection expression cannot pass the reference-equality comparer.
    private readonly Dictionary<TestNodeUpdateMessage, Queue<PlatformActivityContext>> _contexts = new(MessageReferenceComparer.Instance);
#pragma warning restore IDE0028
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif

    public void Set(TestNodeUpdateMessage message, PlatformActivityContext context)
    {
        lock (_syncRoot)
        {
            if (!_contexts.TryGetValue(message, out Queue<PlatformActivityContext>? contexts))
            {
                contexts = new Queue<PlatformActivityContext>();
                _contexts.Add(message, contexts);
            }

            contexts.Enqueue(context);
        }
    }

    public bool TryTake(TestNodeUpdateMessage message, [NotNullWhen(true)] out PlatformActivityContext? context)
    {
        lock (_syncRoot)
        {
            context = null;
            if (!_contexts.TryGetValue(message, out Queue<PlatformActivityContext>? contexts) || contexts.Count == 0)
            {
                return false;
            }

            context = contexts.Dequeue();
            if (contexts.Count == 0)
            {
                _contexts.Remove(message);
            }

            return true;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _contexts.Clear();
        }
    }

    private sealed class MessageReferenceComparer : IEqualityComparer<TestNodeUpdateMessage>
    {
        public static MessageReferenceComparer Instance { get; } = new();

        public bool Equals(TestNodeUpdateMessage? x, TestNodeUpdateMessage? y)
            => ReferenceEquals(x, y);

        public int GetHashCode(TestNodeUpdateMessage obj)
            => RuntimeHelpers.GetHashCode(obj);
    }
}
