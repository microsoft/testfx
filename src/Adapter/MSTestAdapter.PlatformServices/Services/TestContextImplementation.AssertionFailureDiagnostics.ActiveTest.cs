// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    private const int MaximumCapturesPerAttempt = 3;

    private sealed class ActiveTestScope : IDisposable
    {
        private readonly ActiveTest _activeTest;
        private readonly ActiveTest? _previous;
        private int _disposed;

        public ActiveTestScope(ActiveTest activeTest, ActiveTest? previous)
        {
            _activeTest = activeTest;
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            ActiveTests.TryRemove(_activeTest.Id, out _);
            foreach (string path in _activeTest.CloseAndDrain())
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogWarning("Failed to delete unfinalized assertion failure diagnostics artifact '{0}': {1}", path, ex);
                }
            }

            if (ReferenceEquals(CurrentActiveTest.Value, _activeTest))
            {
                CurrentActiveTest.Value = _previous;
            }
        }
    }

    private sealed class ActiveTest
    {
#if NET9_0_OR_GREATER
        private readonly Lock _captureLock = new();
#else
        private readonly object _captureLock = new();
#endif
        private readonly Queue<string> _pendingPaths = new();
        private bool _closed;

        public ActiveTest(
            long id,
            TestContextImplementation context,
            string testClassName,
            string testName,
            string? displayName,
            int attempt,
            DateTimeOffset startedAtUtc,
            long startTimestamp,
            ResourceBaseline startResources)
        {
            Id = id;
            Context = context;
            FullyQualifiedName = $"{testClassName}.{testName}";
            DisplayName = displayName ?? testName;
            Attempt = attempt;
            StartedAtUtc = startedAtUtc;
            StartTimestamp = startTimestamp;
            StartResources = startResources;
        }

        public long Id { get; }

        public TestContextImplementation Context { get; }

        public string FullyQualifiedName { get; }

        public string DisplayName { get; }

        public int Attempt { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public long StartTimestamp { get; }

        public ResourceBaseline StartResources { get; }

        public void Capture(string message, string? expected, string? actual)
        {
            lock (_captureLock)
            {
                if (_closed || !Context._assertionFailureCaptureBudget.TryReserve(out int captureIndex))
                {
                    return;
                }

                _pendingPaths.Enqueue(WriteAssertionFailureDiagnostics(this, captureIndex, message, expected, actual));
            }
        }

        public string[] CloseAndDrain()
        {
            lock (_captureLock)
            {
                if (_closed)
                {
                    return [];
                }

                _closed = true;
                string[] paths = _pendingPaths.ToArray();
                _pendingPaths.Clear();
                return paths;
            }
        }
    }

    private sealed class AssertionFailureCaptureBudget
    {
        private int _captureCount;

        public bool TryReserve(out int captureIndex)
        {
            captureIndex = Interlocked.Increment(ref _captureCount);
            return captureIndex <= MaximumCapturesPerAttempt;
        }

        public void Reset()
            => Interlocked.Exchange(ref _captureCount, 0);
    }
}
#endif
