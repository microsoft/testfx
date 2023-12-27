// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MSTest.Performance.Runner;

internal class PipelinesRunner
{
    private readonly List<PipelineInfo> _pipelines = new();

    public void AddPipeline(string groupName, string pipelineName, OSPlatform oSPlatform, Action<IDictionary<string, object>> func, Action<IDictionary<string, object>>? updatePropertyBag = null, string[]? traits = null)
    {
        _pipelines.Add(new PipelineInfo(groupName, pipelineName, oSPlatform, func, updatePropertyBag, traits));
    }

    public int Run(IDictionary<string, object>? parametersBag = null)
    {
        parametersBag ??= new Dictionary<string, object>();

        foreach (PipelineInfo pipeline in _pipelines)
        {
            if (!RuntimeInformation.IsOSPlatform(pipeline.OSPlatform))
            {
                WriteConsole($"Skip '{pipeline.PipelineName}', OS expected: '{pipeline.OSPlatform}', current OS: '{RuntimeInformation.OSDescription}'", ConsoleColor.Yellow);
                continue;
            }

            WriteConsole(string.Empty, Console.ForegroundColor);
            WriteConsole($"=== Starting pipeline '{pipeline.PipelineName}', group '{pipeline.GroupName}' === ", ConsoleColor.Cyan);

            Dictionary<string, object> pipelinePropertyBag = new()
            {
                { "GroupName", pipeline.GroupName },
                { "PipelineName", pipeline.PipelineName },
            };

            foreach (var item in parametersBag)
            {
                pipelinePropertyBag.Add(item.Key, item.Value);
            }

            if (pipeline.UpdatePropertyBag is not null)
            {
                pipeline.UpdatePropertyBag(pipelinePropertyBag);
            }

            pipeline.Func(pipelinePropertyBag);
        }

        return 0;
    }

    private static void WriteConsole(string message, ConsoleColor consoleColor)
    {
        var color = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = color;
        }
    }

    private record class PipelineInfo(string GroupName, string PipelineName, OSPlatform OSPlatform, Action<IDictionary<string, object>> Func, Action<IDictionary<string, object>>? UpdatePropertyBag = null, string[]? Traits = null);
}
