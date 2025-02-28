// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration;

/*
 * IMPORTANT: Keep the constants, properties and constructor properties assignments in alphabetical order.
 */
internal sealed class WellKnownTypes
{
    private const string MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute = "Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute";
    private const string MicrosoftVisualStudioTestToolsUnitTestingDynamicDataAttribute = "Microsoft.VisualStudio.TestTools.UnitTesting.DynamicDataAttribute";
    private const string MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute = "Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute";
    private const string MicrosoftTestingFrameworkTestArgumentsEntry1 = "Microsoft.Testing.Framework.InternalUnsafeTestArgumentsEntry`1";
    private const string MicrosoftTestingFrameworkTestExecutionTimeoutAttribute = "Microsoft.Testing.Framework.TestExecutionTimeoutAttribute";
    private const string MicrosoftTestingFrameworkTestPropertyAttribute = "Microsoft.Testing.Framework.TestPropertyAttribute";
    private const string SystemCollectionsGenericIEnumerable1 = "System.Collections.Generic.IEnumerable`1";
    private const string SystemIAsyncDisposable = "System.IAsyncDisposable";
    private const string SystemIDisposable = "System.IDisposable";
    private const string SystemObsoleteAttribute = "System.ObsoleteAttribute";
    private const string SystemThreadingTasksTask = "System.Threading.Tasks.Task";
    private const string SystemThreadingTasksValueTask = "System.Threading.Tasks.ValueTask";
    private const string SystemTimeSpan = "System.TimeSpan";
    private const string MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute";

    public WellKnownTypes(Compilation compilation)
    {
        DataRowAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute);
        DynamicDataAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftVisualStudioTestToolsUnitTestingDynamicDataAttribute);
        IAsyncDisposableSymbol = compilation.GetTypeByMetadataName(SystemIAsyncDisposable);
        IDisposableSymbol = compilation.GetTypeByMetadataName(SystemIDisposable);
        IEnumerable1Symbol = compilation.GetTypeByMetadataName(SystemCollectionsGenericIEnumerable1);
        IgnoreAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute);
        SystemObsoleteAttributeSymbol = compilation.GetTypeByMetadataName(SystemObsoleteAttribute);
        TaskSymbol = compilation.GetTypeByMetadataName(SystemThreadingTasksTask);
        TestArgumentsEntrySymbol = compilation.GetTypeByMetadataName(MicrosoftTestingFrameworkTestArgumentsEntry1);
        TestExecutionTimeoutAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftTestingFrameworkTestExecutionTimeoutAttribute);
        TestPropertyAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftTestingFrameworkTestPropertyAttribute);
        TimeSpanSymbol = compilation.GetTypeByMetadataName(SystemTimeSpan);
        ValueTaskSymbol = compilation.GetTypeByMetadataName(SystemThreadingTasksValueTask);
        TestMethodAttributeSymbol = compilation.GetTypeByMetadataName(MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
    }

    public INamedTypeSymbol? DataRowAttributeSymbol { get; }

    public INamedTypeSymbol? DynamicDataAttributeSymbol { get; }

    public INamedTypeSymbol? IAsyncDisposableSymbol { get; }

    public INamedTypeSymbol? IDisposableSymbol { get; }

    public INamedTypeSymbol? IEnumerable1Symbol { get; }

    public INamedTypeSymbol? IgnoreAttributeSymbol { get; }

    public INamedTypeSymbol? SystemObsoleteAttributeSymbol { get; }

    public INamedTypeSymbol? TaskSymbol { get; }

    public INamedTypeSymbol? TestArgumentsEntrySymbol { get; }

    public INamedTypeSymbol? TestExecutionTimeoutAttributeSymbol { get; }

    public INamedTypeSymbol? TestPropertyAttributeSymbol { get; }

    public INamedTypeSymbol? TimeSpanSymbol { get; }

    public INamedTypeSymbol? ValueTaskSymbol { get; }

    public INamedTypeSymbol? TestMethodAttributeSymbol { get; }
}
