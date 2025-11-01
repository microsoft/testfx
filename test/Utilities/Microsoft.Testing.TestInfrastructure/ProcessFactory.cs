// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class ProcessFactory
{
    public static (IProcessHandle Handle, Task OutputAndErrorTask) Start(ProcessConfiguration config, bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false)
    {
        string fullPath = config.FileName; // Path.GetFullPath(startInfo.FileName);
        string workingDirectory = config.WorkingDirectory
            .OrDefault(Path.GetDirectoryName(config.FileName).OrDefault(Directory.GetCurrentDirectory()));

        ProcessStartInfo processStartInfo = new()
        {
            FileName = fullPath,
            Arguments = config.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        if (config.EnvironmentVariables is not null)
        {
            if (cleanDefaultEnvironmentVariableIfCustomAreProvided)
            {
                processStartInfo.Environment.Clear();
                processStartInfo.EnvironmentVariables.Clear();
            }

            foreach ((string key, string? value) in config.EnvironmentVariables)
            {
                if (value is null)
                {
                    continue;
                }

                processStartInfo.EnvironmentVariables[key] = value;
            }
        }

        Process process = new()
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };

        // ToolName and Pid are not populated until we start the process,
        // and once we stop the process we cannot retrieve the info anymore
        // so we start the process, try to grab the needed info and set it.
        // And then we give the call reference to ProcessHandle, but not to ProcessHandleInfo
        // so they can easily get the info, but cannot change it.
        ProcessHandleInfo processHandleInfo = new();
        ProcessHandle processHandle = new(process, processHandleInfo);

        if (config.OnExit != null)
        {
            process.Exited += (s, e) => config.OnExit.Invoke(processHandle, process.ExitCode);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Process failed to start");
        }

        Task outputTask = Task.Factory.StartNew(
            () =>
            {
                while (process.StandardOutput.ReadLine() is string line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        config.OnStandardOutput?.Invoke(processHandle, line);
                    }
                }
            }, TaskCreationOptions.LongRunning);

        Task errorTask = Task.Factory.StartNew(
            () =>
            {
                while (process.StandardError.ReadLine() is string line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        config.OnErrorOutput?.Invoke(processHandle, line);
                    }
                }
            }, TaskCreationOptions.LongRunning);

        try
        {
            processHandleInfo.ProcessName = process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            // The associated process has exited.
            // https://learn.microsoft.com/dotnet/api/system.diagnostics.process.processname?view=net-7.0
        }

        processHandleInfo.Id = process.Id;

        return (processHandle, Task.WhenAll(outputTask, errorTask));
    }
}
