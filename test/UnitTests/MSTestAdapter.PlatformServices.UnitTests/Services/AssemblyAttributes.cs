// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Services;

[assembly: ReflectionOperationsTests.DummyA("a1")]
[assembly: ReflectionOperationsTests.DummyA("a2")]

// Marker consumed by TypeCacheAssemblyFixtureProviderTests. The discovery path opportunistically
// reads AssemblyFixtureProvider markers from the test assembly itself; pointing at a type defined
// in the same assembly is the "consumer-side" escape hatch supported by the feature.
[assembly: AssemblyFixtureProvider(typeof(TypeCacheAssemblyFixtureProviderTests.DummyFixtureProvider))]
