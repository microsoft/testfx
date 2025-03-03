// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace TestFramework.ForTestingMSTest;

internal sealed class TestFrameworkExtension : IExtension
{
    public string Uid => "TestFrameworkForTestingMSTest";

    public string Version => "internal";

    public string DisplayName => "Internal Framework for MSTest";

    public string Description => "An internal test framework made to test MSTest";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
