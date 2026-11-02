// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class RedundantTestMethodAttributeAnalyzer
{
    private sealed class KnownAttributeSymbols
    {
        public KnownAttributeSymbols(
            Compilation compilation,
            INamedTypeSymbol testMethodAttributeSymbol,
            INamedTypeSymbol testClassAttributeSymbol)
        {
            TestMethod = testMethodAttributeSymbol;
            TestClass = testClassAttributeSymbol;
            ArchitectureCondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingArchitectureConditionAttribute);
            CICondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCIConditionAttribute);
            DeploymentItem = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDeploymentItemAttribute);
            DependsOn = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDependsOnAttribute);
            DoNotParallelize = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);
            Ignore = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute);
            OSCondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOSConditionAttribute);
            ResourceLock = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute);
            Retry = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingRetryAttribute);
            TestCategory = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCategoryAttribute);
            TestProperty = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute);
        }

        public INamedTypeSymbol? ArchitectureCondition { get; }

        public INamedTypeSymbol? CICondition { get; }

        public INamedTypeSymbol? DeploymentItem { get; }

        public INamedTypeSymbol? DependsOn { get; }

        public INamedTypeSymbol? DoNotParallelize { get; }

        public INamedTypeSymbol? Ignore { get; }

        public INamedTypeSymbol? OSCondition { get; }

        public INamedTypeSymbol? ResourceLock { get; }

        public INamedTypeSymbol? Retry { get; }

        public INamedTypeSymbol TestClass { get; }

        public INamedTypeSymbol? TestCategory { get; }

        public INamedTypeSymbol TestMethod { get; }

        public INamedTypeSymbol? TestProperty { get; }
    }
}
