// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

internal sealed partial class MSTestDiscoveryCache
{
    private sealed class CachedDiscoverer : UnitTestDiscoverer
    {
        private readonly MSTestDiscoveryCache _cache;
        private readonly ITestSourceHandler _sourceHandler;
        private readonly Assembly[] _assemblies;
        private readonly TestNodeUid[] _selectedUids;
        private readonly CancellationToken _cancellationToken;

        internal CachedDiscoverer(
            MSTestDiscoveryCache cache,
            ITestSourceHandler sourceHandler,
            Assembly[] assemblies,
            TestNodeUid[] selectedUids,
            CancellationToken cancellationToken)
            : base(sourceHandler)
        {
            _cache = cache;
            _sourceHandler = sourceHandler;
            _assemblies = assemblies;
            _selectedUids = selectedUids;
            _cancellationToken = cancellationToken;
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "Single-file sources do not match a physical assembly path and fall back to regular discovery.")]
        internal override ICollection<UnitTestElement>? GetTestElements(
            string source,
            string? settingsXml,
            bool isMTP,
            out List<string> warnings,
            out int discoveredTestCount)
        {
            Assembly? assembly = _assemblies.FirstOrDefault(candidate => candidate.Location == source);
            return isMTP && assembly is not null && IsSupported(assembly) && !_cancellationToken.IsCancellationRequested
                ? _cache.GetTests(source, settingsXml, _sourceHandler, assembly, _selectedUids, _cancellationToken, out warnings, out discoveredTestCount)
                : base.GetTestElements(source, settingsXml, isMTP, out warnings, out discoveredTestCount);
        }
    }
}
