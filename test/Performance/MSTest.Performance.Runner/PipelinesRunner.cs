﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileSystemGlobbing;

namespace MSTest.Performance.Runner;

internal class PipelinesRunner
{
    private readonly List<PipelineInfo> _pipelines = [];

    public void AddPipeline(string groupName, string pipelineName, OSPlatform[] oSPlatform, Action<IDictionary<string, object>> func, Action<IDictionary<string, object>>? updatePropertyBag = null, string[]? traits = null) => _pipelines.Add(new PipelineInfo(groupName, pipelineName, oSPlatform, func, updatePropertyBag, traits));

    public int Run(string pipelineNameFilter, IDictionary<string, object>? parametersBag = null)
    {
        parametersBag ??= new Dictionary<string, object>();

        Matcher pipelineNameFilterMatcher = new();
        pipelineNameFilterMatcher.AddInclude(string.IsNullOrEmpty(pipelineNameFilter) ? "*.*" : pipelineNameFilter);

        foreach (PipelineInfo pipeline in _pipelines)
        {
            if (!pipelineNameFilterMatcher.Match(pipeline.PipelineName).HasMatches)
            {
                WriteConsole($"Skip '{pipeline.PipelineName}' for filter '{pipelineNameFilter}'", ConsoleColor.DarkGray);
                continue;
            }

            if (!pipeline.OSPlatform.Any(RuntimeInformation.IsOSPlatform))
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

            foreach ((string key, object value) in parametersBag)
            {
                pipelinePropertyBag.Add(key, value);
            }

            pipeline.UpdatePropertyBag?.Invoke(pipelinePropertyBag);

            pipeline.Func(pipelinePropertyBag);
        }

        return 0;
    }

    private static void WriteConsole(string message, ConsoleColor consoleColor)
    {
        ConsoleColor color = Console.ForegroundColor;
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

    private record PipelineInfo(string GroupName, string PipelineName, OSPlatform[] OSPlatform, Action<IDictionary<string, object>> Func, Action<IDictionary<string, object>>? UpdatePropertyBag = null, string[]? Traits = null);
}
