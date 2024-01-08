// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal interface IExecutionQueue<T>
{
    bool IsEmpty { get; }

    bool TryDequeue(out T item);
}

internal class ConcurrentQueueWrapper<T> : IExecutionQueue<T>
{
    private readonly ConcurrentQueue<T> _testCases;

    public ConcurrentQueueWrapper(IEnumerable<T> testCases)
    {
        _testCases = new(testCases);
    }

    public bool IsEmpty => _testCases.IsEmpty;

    public bool TryDequeue(out T item)
    {
        bool dequeued = _testCases.TryDequeue(out var item2);
        item = item2!;
        return dequeued;
    }
}

internal class MethodLevelScheduleQueue : IExecutionQueue<IEnumerable<TestCase>>
{
    private readonly ConcurrentBag<TestCase> _testCases;

    public MethodLevelScheduleQueue(IEnumerable<TestCase> testCases)
    {
        _testCases = new(testCases);
    }

    public bool IsEmpty => _testCases.IsEmpty;

    public bool TryDequeue(out IEnumerable<TestCase> item)
    {
        if (_testCases.TryTake(out TestCase? result))
        {
            item = new MethodLevelScheduleQueueIEnumerable(result);
            return true;
        }
        else
        {
            item = null!;
            return false;
        }
    }

    private readonly struct MethodLevelScheduleQueueIEnumerable : IEnumerable<TestCase>
    {
        private readonly TestCase _testCase;

        public MethodLevelScheduleQueueIEnumerable(TestCase testCase)
        {
            _testCase = testCase;
        }

        public IEnumerator<TestCase> GetEnumerator() => new MethodLevelScheduleQueueIEnumerator(_testCase);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private struct MethodLevelScheduleQueueIEnumerator : IEnumerator<TestCase>
    {
        private readonly TestCase _testCase;
        private TestCase? _current;

        public MethodLevelScheduleQueueIEnumerator(TestCase testCase)
        {
            _testCase = testCase;
        }

        public readonly TestCase Current => _current!;

        readonly object IEnumerator.Current => _current!;

        public readonly void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_current is null)
            {
                _current = _testCase;
                return true;
            }
            else
            {
                _current = null;
                return false;
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
