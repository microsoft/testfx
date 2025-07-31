// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.PlatformServices.ObjectModel;

namespace MSTest.PlatformServices.Discovery;

/// <summary>
/// Helps us communicate results that were created inside of AppDomain, when AppDomains are available and enabled.
/// </summary>
/// <param name="TestElements">The test elements that were discovered.</param>
/// <param name="Warnings">Warnings that happened during discovery.</param>
[Serializable]
internal sealed record AssemblyEnumerationResult(List<UnitTestElement> TestElements, List<string> Warnings);
