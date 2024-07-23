// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Tools;

internal sealed class ToolsManager : IToolsManager
{
    private readonly List<Func<IServiceProvider, ITool>> _toolsFactories = [];

    public void AddTool(Func<IServiceProvider, ITool> toolFactory)
    {
        ArgumentGuard.IsNotNull(toolFactory);
        _toolsFactories.Add(toolFactory);
    }

    internal async Task<ToolsInformation> BuildAsync(IServiceProvider serviceProvider)
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

        return new ToolsInformation(tools);
    }
}
