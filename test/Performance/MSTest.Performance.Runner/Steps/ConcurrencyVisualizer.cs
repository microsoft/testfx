// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace MSTest.Performance.Runner.Steps;

internal class ConcurrencyVisualizer : IStep<BuildArtifact, Files>
{
    private readonly string _reportFileName;
    private readonly string _symbols;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run under ConcurrencyVisualizer";

    public ConcurrencyVisualizer(string reportFileName, string symbols = "https://msdl.microsoft.com/download/symbols", CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _reportFileName = reportFileName;
        _symbols = symbols;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Skip run, not supported in Windows");
            return new Files(Array.Empty<string>());
        }

        string vsProgramFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        string? cVCollectionCmd = Directory.GetFiles(vsProgramFile, "CVCollectionCmd.exe", SearchOption.AllDirectories).SingleOrDefault()
            ?? throw new InvalidOperationException("CVCollectionCmd.exe not found, https://learn.microsoft.com/visualstudio/profiling/concurrency-visualizer-command-line-utility-cvcollectioncmd");

        // https://learn.microsoft.com/visualstudio/profiling/concurrency-visualizer-command-line-utility-cvcollectioncmd for options and markers
        string config = $"""
<?xml version="1.0"?>
<LocalConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" MajorVersion="1" MinorVersion="0">
  <IncludeEnvSymbolPath>true</IncludeEnvSymbolPath>
  <DeleteEtlsAfterAnalysis>true</DeleteEtlsAfterAnalysis>
  <TraceLocation>{Path.GetDirectoryName(payload.TestHost.FullName)}</TraceLocation>
  <SymbolPath>{_symbols}</SymbolPath>

  <FilterConfig>
    <CollectClrEvents>true</CollectClrEvents>
    <ClrCollectionOptions>CollectForNative DisableNGenRundown</ClrCollectionOptions>
    <CollectSampleEvents>true</CollectSampleEvents>
    <CollectGpuEvents>true</CollectGpuEvents>
    <CollectFileIO>true</CollectFileIO>
  </FilterConfig>

  <UserBufferSettings>
    <BufferFlushTimer>0</BufferFlushTimer>
    <BufferSize>256</BufferSize>
    <MinimumBuffers>512</MinimumBuffers>
    <MaximumBuffers>1024</MaximumBuffers>
  </UserBufferSettings>

  <KernelBufferSettings>
    <BufferFlushTimer>0</BufferFlushTimer>
    <BufferSize>256</BufferSize>
    <MinimumBuffers>512</MinimumBuffers>
    <MaximumBuffers>1024</MaximumBuffers>
  </KernelBufferSettings>

  <!-- List of MyCodeDirectory directories -->
  <JustMyCode>
    <MyCodeDirectory>{Path.GetDirectoryName(payload.TestHost.FullName)}</MyCodeDirectory>
    <MyCodeDirectory>{payload.TestAsset.TargetAssetPath}</MyCodeDirectory>
  </JustMyCode>
</LocalConfig>
""";

        string configFilePath = Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "Config.xml");
        File.WriteAllText(configFilePath, config);
        StringBuilder commandLine = new();
        commandLine.Append(CultureInfo.InvariantCulture, $"/Config \"{configFilePath}\" /launch \"{payload.TestHost.FullName}\" /outdir \"{Path.GetDirectoryName(payload.TestHost.FullName)!}\"");

        ManualResetEventSlim profiledProcessExited = new(false);
        WindowsProcessWatcher processWatcher = new(Path.GetFileName(payload.TestHost.FullName));
        processWatcher.Start();
        processWatcher.ProcessDeleted += (_, _) =>
        {
            Console.WriteLine($"Process '{Path.GetFileName(payload.TestHost.FullName)}' exited.");
            profiledProcessExited.Set();
        };

        ProcessStartInfo startCollection =
        new(cVCollectionCmd, commandLine.ToString())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        Console.WriteLine($"CVCollectionCmd command: '{startCollection.FileName} {startCollection.Arguments}'");
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

        ProcessStartInfo stopCollection =
         new(cVCollectionCmd, "/Detach")
         {
             UseShellExecute = false,
             RedirectStandardOutput = true,
             RedirectStandardError = true,
             RedirectStandardInput = true,
         };
        Console.WriteLine($"VSDiagnostics stop command: '{stopCollection.FileName} {stopCollection.Arguments}'");
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
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);

        return new Files(new[] { sample });
    }
}
