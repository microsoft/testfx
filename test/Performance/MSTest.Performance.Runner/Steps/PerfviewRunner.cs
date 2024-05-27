// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace MSTest.Performance.Runner.Steps;

internal class PerfviewRunner : IStep<BuildArtifact, Files>
{
    private const string PrefViewDownload = "https://github.com/microsoft/perfview/releases/download/v3.1.7/PerfView.exe";
    private readonly string _argument;
    private readonly string _reportFileName;
    private readonly bool _includeScenario;
    private readonly CompressionLevel _compressionLevel;

    public string Description => "run under PerfView";

    public PerfviewRunner(string argument, string reportFileName, bool includeScenario = false, CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _argument = argument;
        _reportFileName = reportFileName;
        _includeScenario = includeScenario;
        _compressionLevel = compressionLevel;
    }

    public async Task<Files> ExecuteAsync(BuildArtifact payload, IContext context)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Skip run, not supported in Windows");
            return new Files(Array.Empty<string>());
        }

        await PerfviewExecutable();
        StringBuilder commandLine = new();
        commandLine.Append(CultureInfo.InvariantCulture, $" \"/DataFile:{Path.Combine(Path.GetDirectoryName(payload.TestHost.FullName)!, "DataFile.etl")}\" /AcceptEULA /NoGui {_argument} ");
        commandLine.Append(CultureInfo.InvariantCulture, $"run \"{payload.TestHost.FullName}\" ");

        ProcessStartInfo processStartInfo =
        new(await PerfviewExecutable(), commandLine.ToString())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        Console.WriteLine($"Perfview command: '{processStartInfo.FileName} {processStartInfo.Arguments}'");

        bool succeded = false;
        ManualResetEvent killTheProcess = new(false);
        Process process = Process.Start(processStartInfo)!;
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.OutputDataReceived += (sender, args) =>
        {
            Console.WriteLine(args.Data);
            if (args?.Data?.Contains("SUCCESS: PerfView") == true)
            {
                succeded = true;
            }

            if (args?.Data == "Press enter to close window.")
            {
                killTheProcess.Set();
            }
        };

        _ = Task.Run(() =>
        {
            killTheProcess.WaitOne();
            process.Kill();
        });

        process.WaitForExit();

        if (!succeded)
        {
            throw new InvalidOperationException("Perview command didn't succeed.");
        }

        string sample = Path.Combine(Path.GetTempPath(), _reportFileName);
        File.Delete(sample);

        Console.WriteLine($"Compressing to '{sample}'");
        if (_includeScenario)
        {
            ZipFile.CreateFromDirectory(payload.TestAsset.TargetAssetPath, sample, _compressionLevel, includeBaseDirectory: true);
        }
        else
        {
            string reportDirectory = Path.GetDirectoryName(payload.TestHost.FullName)!;
            string dataFileDirectory = Path.Combine(reportDirectory, "DataFile");
            Directory.CreateDirectory(dataFileDirectory);
            foreach (string item in Directory.GetFiles(reportDirectory, "DataFile.*"))
            {
                File.Move(item, Path.Combine(dataFileDirectory, Path.GetFileName(item)!));
            }

            ZipFile.CreateFromDirectory(dataFileDirectory, sample, _compressionLevel, includeBaseDirectory: true);
        }

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

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(PrefViewDownload);
        using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
        using Stream streamToWriteTo = File.Open(localPath, FileMode.Create);
        await streamToReadFrom.CopyToAsync(streamToWriteTo);

        return localPath;
    }
}
