// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class ProcessHelper : IProcessHelper
{
    public IProcessHandle Start(ProcessStartInfo startInfo, bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false, int timeoutInSeconds = 300)
    {
        string fullPath = startInfo.FileName; // Path.GetFullPath(startInfo.FileName);
        string workingDirectory = startInfo.WorkingDirectory
            .OrDefault(Path.GetDirectoryName(startInfo.FileName).OrDefault(Directory.GetCurrentDirectory()));

        var processStartInfo = new System.Diagnostics.ProcessStartInfo()
        {
            FileName = fullPath,
            Arguments = startInfo.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        if (startInfo.EnvironmentVariables is not null)
        {
            if (cleanDefaultEnvironmentVariableIfCustomAreProvided)
            {
                processStartInfo.Environment.Clear();
                processStartInfo.EnvironmentVariables.Clear();
            }

            foreach (KeyValuePair<string, string> kvp in startInfo.EnvironmentVariables)
            {
                if (kvp.Value is null)
                {
                    continue;
                }

                processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        var process = new Process()
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };

        // ToolName and Pid are not populated until we start the process,
        // and once we stop the process we cannot retrieve the info anymore
        // so we start the process, try to grab the needed info and set it.
        // And then we give the call reference to ProcessHandle, but not to ProcessHandleInfo
        // so they can easily get the info, but cannot change it.
        var processHandleInfo = new ProcessHandleInfo();
        var processHandle = new ProcessHandle(process, processHandleInfo);

        process.Exited += (s, e) => startInfo?.OnExit?.Invoke(processHandle, process.ExitCode);

        if (startInfo.OnStandardOutput != null)
        {
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    startInfo.OnStandardOutput(processHandle, e.Data);
                }
            };
        }

        if (startInfo.OnErrorOutput != null)
        {
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    startInfo.OnErrorOutput(processHandle, e.Data);
                }
            };
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Process failed to start");
        }

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

        if (startInfo.OnStandardOutput != null)
        {
            process.BeginOutputReadLine();
        }

        if (startInfo.OnErrorOutput != null)
        {
            process.BeginErrorReadLine();
        }

        return processHandle;
    }
}
