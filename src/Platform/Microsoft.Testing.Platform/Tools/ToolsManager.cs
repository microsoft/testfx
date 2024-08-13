// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Tools;

internal sealed class ToolsManager : IToolsManager
{
    private readonly List<Func<IServiceProvider, ITool>> _toolsFactories = [];

    public void AddTool(Func<IServiceProvider, ITool> toolFactory)
    {
        Guard.NotNull(toolFactory);
        _toolsFactories.Add(toolFactory);
    }

    internal async Task<IReadOnlyList<ITool>> BuildAsync(IServiceProvider serviceProvider)
    {
        List<ITool> tools = [];
        foreach (Func<IServiceProvider, ITool> toolFactory in _toolsFactories)
        {
            ITool tool = toolFactory(serviceProvider);
            if (!await tool.IsEnabledAsync())
            {
                continue;
            }

            await tool.TryInitializeAsync();

            tools.Add(tool);
        }

        return tools;
    }
}
