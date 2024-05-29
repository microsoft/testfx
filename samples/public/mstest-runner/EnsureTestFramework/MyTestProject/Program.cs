// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Contoso.EnsureTestFramework;

using Microsoft.Testing.Platform.Builder;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddEnsureTestFramework();
        var app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

