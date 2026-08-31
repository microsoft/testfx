// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Property bag, result files and lifecycle-property capture of <see cref="TestContextImplementation"/>.
/// </summary>
internal sealed partial class TestContextImplementation
{
#if NET9_0_OR_GREATER
    private readonly Lock _propertiesLock = new();
#else
    private readonly object _propertiesLock = new();
#endif

    /// <summary>
    /// List of result files associated with the test.
    /// </summary>
    private List<string>? _testResultFiles;

    /// <inheritdoc/>
    public override IDictionary<string, object?> Properties => _properties;

    /// <summary>
    /// Returns whether property with parameter name is present or not.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <returns>True if found.</returns>
    public bool TryGetPropertyValue(string propertyName, out object? propertyValue)
        => _properties.TryGetValue(propertyName, out propertyValue);

    /// <summary>
    /// Adds the parameter name/value pair to property bag.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    public void AddProperty(string propertyName, string propertyValue)
        => _properties.Add(propertyName, propertyValue);

    /// <summary>
    /// Merges the given properties into this context's property bag using indexer semantics
    /// (existing keys are overwritten, except the per-context labels
    /// <see cref="TestContext.FullyQualifiedTestClassNameLabel"/> and
    /// <see cref="TestContext.TestNameLabel"/>, which are preserved).
    /// Used to flow properties set during <c>AssemblyInitialize</c> / <c>ClassInitialize</c>
    /// into subsequent contexts.
    /// <para>
    /// Merge precedence: keys in <paramref name="propertiesToMerge"/> WIN over keys already
    /// present in this context's bag. This is intentional — lifecycle snapshots typically
    /// flow on top of the seeded source-level parameters (e.g. <c>TestRunParameters</c> from
    /// <c>.runsettings</c>), so a user's explicit assignment in <c>AssemblyInitialize</c> /
    /// <c>ClassInitialize</c> overrides any same-named runsettings value for the rest of
    /// the lifecycle (class init, tests, class cleanup, assembly cleanup).
    /// </para>
    /// </summary>
    /// <param name="propertiesToMerge">The properties to merge in. May be <see langword="null"/>.</param>
    internal void MergeProperties(IReadOnlyDictionary<string, object?>? propertiesToMerge)
    {
        if (propertiesToMerge is null or { Count: 0 })
        {
            return;
        }

        // Take the same internal lock as CaptureLifecycleProperties so a snapshot capture
        // cannot race with a merge on the same context (which would otherwise corrupt the
        // Dictionary iterator or cause a missed write). Writes via the public Properties
        // indexer still bypass this lock - see the remarks on CaptureLifecycleProperties.
        lock (_propertiesLock)
        {
            foreach (KeyValuePair<string, object?> kvp in propertiesToMerge)
            {
                // Never overwrite the per-context labels.
                if (kvp.Key == FullyQualifiedTestClassNameLabel || kvp.Key == TestNameLabel)
                {
                    continue;
                }

                _properties[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Captures a snapshot of the current property bag, excluding the per-context labels
    /// (<see cref="TestContext.FullyQualifiedTestClassNameLabel"/> and
    /// <see cref="TestContext.TestNameLabel"/>). The returned dictionary is intended to be
    /// stored on a <c>TestAssemblyInfo</c> / <c>TestClassInfo</c> and later merged into other
    /// contexts via <see cref="MergeProperties(IReadOnlyDictionary{string, object?}?)"/>.
    /// <para>
    /// Returns <see langword="null"/> when there are no non-label properties to capture
    /// (the common case when <c>AssemblyInitialize</c> / <c>ClassInitialize</c> do not set
    /// properties on <c>TestContext</c>). <see cref="MergeProperties"/> already handles a
    /// <see langword="null"/> argument as a no-op, so callers need not special-case this.
    /// </para>
    /// <para>
    /// The snapshot is shallow: keys and value references are copied as-is. Reference-type
    /// values stored in the bag (e.g. a mocked file system, a connection pool, a list) are
    /// shared across every context the snapshot is later merged into. Mutations of those
    /// reference-type instances are visible everywhere.
    /// </para>
    /// <para>
    /// Enumeration is performed under a private synchronization lock so that snapshot
    /// capture is safe against concurrent calls to this method or <see cref="MergeProperties"/>
    /// on the same context. Note: writes made via the public <see cref="Properties"/> indexer
    /// do NOT take this lock, so a lifecycle method that spawns a background thread which
    /// keeps mutating <see cref="Properties"/> past method return can still race with the
    /// capture - that is treated as user error and is consistent with the pre-existing
    /// thread-affinity expectation of <c>AssemblyInitialize</c> / <c>ClassInitialize</c>.
    /// </para>
    /// </summary>
    /// <returns>
    /// A read-only snapshot of the current properties (excluding per-context labels), or
    /// <see langword="null"/> if there are no such properties to snapshot.
    /// </returns>
    internal IReadOnlyDictionary<string, object?>? CaptureLifecycleProperties()
    {
        Dictionary<string, object?>? snapshot = null;
        lock (_propertiesLock)
        {
            foreach (KeyValuePair<string, object?> kvp in _properties)
            {
                if (kvp.Key == FullyQualifiedTestClassNameLabel || kvp.Key == TestNameLabel)
                {
                    continue;
                }

#pragma warning disable IDE0028 // Collection initialization can be simplified - capacity hint is intentional.
                snapshot ??= new Dictionary<string, object?>(_properties.Count);
#pragma warning restore IDE0028
                snapshot[kvp.Key] = kvp.Value;
            }
        }

        return snapshot is null ? null : new ReadOnlyDictionary<string, object?>(snapshot);
    }

    /// <inheritdoc/>
    public override void AddResultFile(string fileName)
    {
        if (StringEx.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, nameof(fileName));
        }

        string fullPath = Path.GetFullPath(fileName);
        (_testResultFiles ??= []).Add(fullPath);
#if !WINDOWS_UWP && !WIN_UI
        // Remember when a registered result file lives inside the per-test temp directory. The
        // framework consumes the result-file list during execution (before this context is
        // disposed), so this flag — not the list — is what cleanup consults to avoid deleting a
        // directory whose file the host still reports as an attachment. See ShouldRetainTestTempDirectory.
        if (Volatile.Read(ref _testTempDirectoryCreated)
            && _testTempDirectory is { Length: > 0 } tempDir
            && IsPathUnderDirectory(fullPath, tempDir))
        {
            _hasResultFileUnderTestTempDirectory = true;
        }
#endif
    }

    /// <summary>
    /// Result files attached.
    /// </summary>
    /// <returns>Results files generated in run.</returns>
    public IList<string>? GetResultFiles()
    {
#if !WINDOWS_UWP && !WIN_UI
        // This is called once per execution attempt, and the last call before disposal reflects the
        // reportable attempt's result files. Recompute (not accumulate) whether any of *this*
        // attempt's result files live under the per-test temp directory, so a sticky value from an
        // earlier retry attempt cannot force retention of an otherwise-passing final attempt.
        _hasResultFileUnderTestTempDirectory = HasResultFileUnderTestTempDirectory();
#endif
        if (_testResultFiles is null || _testResultFiles.Count == 0)
        {
            return null;
        }

        // Hand over the existing list to the caller (callers only enumerate it) and reset the field
        // so data driven tests start with a fresh list on the next AddResultFile call.
        // This avoids the copy that ToList() would do.
        List<string> results = _testResultFiles;
        _testResultFiles = null;

        return results;
    }
}
