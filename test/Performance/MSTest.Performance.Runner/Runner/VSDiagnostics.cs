// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Management;
using System.Runtime.InteropServices;

using MSTest.Performance.Runner.Runners;

namespace MSTest.Performance.Runner.Runner;

internal class VSDiagnostics : IStep<BuildArtifact, Files>
{
    private readonly string _agentConfigName;
    private readonly string _reportFileName;

    public string Description => "Run under VSDiagnostics.exe";

    public VSDiagnostics(string agentConfigName, string reportFileName)
    {
        _agentConfigName = agentConfigName;
        _reportFileName = reportFileName;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Skip run, not supported in Windows");
            return new Files(Array.Empty<string>());
        }

        string vsProgramFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        string? vSDiagnostics = Directory.GetFiles(vsProgramFile, "VSDiagnostics.exe", SearchOption.AllDirectories).SingleOrDefault();
        if (vSDiagnostics is null)
        {
            throw new Exception("VSDiagnostics.exe not found");
        }

        string agentConfig = Path.Combine(Path.GetDirectoryName(vSDiagnostics)!, "AgentConfigs", _agentConfigName);
        if (!File.Exists(agentConfig))
        {
            throw new Exception($"'{_agentConfigName}' not found");
        }

        string sessionID = Guid.NewGuid().ToString();
        ProcessStartInfo startCollection =
           new(vSDiagnostics, $"start {sessionID} /launch:\"{payload.TestHost.FullName}\" /loadConfig:\"{agentConfig}\"")
           {
               UseShellExecute = false,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               RedirectStandardInput = true,
           };

        ManualResetEventSlim profiledProcessExited = new(false);
        WindowsProcessWatcher processWatcher = new(Path.GetFileName(payload.TestHost.FullName));
        processWatcher.Start();
        processWatcher.ProcessDeleted += (object? _, ManagementBaseObject _) =>
        {
            Console.WriteLine($"Process '{Path.GetFileName(payload.TestHost.FullName)}' exited.");
            profiledProcessExited.Set();
        };

        Console.WriteLine($"VSDiagnostics start command: '{startCollection.FileName} {startCollection.Arguments}'");
        Process process = Process.Start(startCollection)!;
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.OutputDataReceived += (sender, args) =>
        {
            Console.WriteLine(args.Data);
        };
        await process.WaitForExitAsync();

        // Wait for process exit
        profiledProcessExited.Wait();

        string diagSessionFileNames = Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "session.diagsession");
        File.Delete(diagSessionFileNames);
        ProcessStartInfo stopCollection =
          new(vSDiagnostics, $"stop {sessionID} /output:{diagSessionFileNames}")
          {
              UseShellExecute = false,
              RedirectStandardOutput = true,
              RedirectStandardError = true,
              RedirectStandardInput = true,
          };
        Console.WriteLine($"VSDiagnostics start command: '{startCollection.FileName} {startCollection.Arguments}'");
        process = Process.Start(stopCollection)!;
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.OutputDataReceived += (sender, args) =>
        {
            Console.WriteLine(args.Data);
        };
        await process.WaitForExitAsync();

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, CompressionLevel.SmallestSize, includeBaseDirectory: true);

        return new Files(new[] { sample });
    }
}
