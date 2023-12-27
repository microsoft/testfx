// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

using MSTest.Performance.Runner.Runners;

namespace MSTest.Performance.Runner.Runner;

internal class PerfviewRunner : IStep<BuildArtifact, Files>
{
    private const string PrefViewDownload = "https://github.com/microsoft/perfview/releases/download/v3.1.7/PerfView.exe";
    private readonly string _argument;
    private readonly string _reportFileName;

    public string Description => "run under PerfView";

    public PerfviewRunner(string argument, string reportFileName)
    {
        _argument = argument;
        _reportFileName = reportFileName;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Skip run, not supported in Windows");
            return new Files(Array.Empty<string>());
        }

        string perfViewExecutable = await PerfviewExecutable();
        StringBuilder commandLine = new();
        commandLine.Append(CultureInfo.InvariantCulture, $"\"/DataFile:{Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "DataFile.etl")}\" /NoGui {_argument} ");
        commandLine.Append(CultureInfo.InvariantCulture, $"run {payload.TestHost.FullName} ");

        ProcessStartInfo processStartInfo =
        new(await PerfviewExecutable(), commandLine.ToString())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        Console.WriteLine($"Perfview command: '{processStartInfo.FileName} {processStartInfo.Arguments}'");

        ManualResetEvent killTheProcess = new(false);
        Process process = Process.Start(processStartInfo)!;
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.OutputDataReceived += (sender, args) =>
        {
            Console.WriteLine(args.Data);
            if (args.Data == "Press enter to close window.")
            {
                process.StandardInput.WriteLine();
                killTheProcess.Set();
            }
        };

        _ = Task.Run(() =>
        {
            killTheProcess.WaitOne();
            process.Kill();
        });

        process.WaitForExit();

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);
        Console.WriteLine($"Compressing to '{sample}'");
        ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, CompressionLevel.SmallestSize, includeBaseDirectory: true);

        return new Files(new[] { sample });
    }

    private async Task<string> PerfviewExecutable()
    {
        string localPath = Path.Combine(Path.GetTempPath(), "PerfView", "PerfView.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        using (HttpClient client = new())
        {
            using HttpResponseMessage response = await client.GetAsync(PrefViewDownload);
            using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
            using Stream streamToWriteTo = File.Open(localPath, FileMode.Create);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
        }

        return localPath;
    }
}
