# Acceptance fixture performance: reproduction appendix

See the [decision and results](acceptance-fixture-performance.md). These are the
scratch harness sources used for the experiment, preserved as documentation
rather than installed benchmark infrastructure. Reflection intentionally binds
to this revision's private fixture fields; adjust and disclose changes if those
fields or the framework matrix change in a later revision.

## Build and run

From the worktree root, first build/pack using its pinned SDK:

```powershell
.\build.cmd -c Release -pack -bl /p:FastAcceptanceTest=true /p:OfficialBuildId=20261125.91
```

Save the two source blocks below as `FixtureProbe.csproj` and `Program.cs` in an
experiment-owned scratch directory **outside any repository**. Keep C# UTF-8 BOM
encoding. An external source directory avoids inheriting TestFx's analyzer/API
policies; the executable stays under its `artifacts` tree so the existing
infrastructure can resolve the repository and its Shipping feeds.

Set `$probe` to that project and `$root` to the worktree root:

```powershell
$root = (Get-Location).Path
.\.dotnet\dotnet.exe build $probe -c Release --configfile "$root\NuGet.config" `
  "-p:AcceptanceDirectory=$root\artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net12.0" `
  "-p:PlatformDirectory=$root\artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net12.0" `
  "-p:OutputPath=$root\artifacts\bin\FixtureProbe\Release\net12.0\" `
  "-bl:$root\artifacts\log\fixture-probe.binlog"

# A NEW evidence directory also means a NEW experiment-owned package cache.
$evidence = Join-Path $root 'artifacts\fixture-performance\new-comparison'
.\.dotnet\dotnet.exe artifacts\bin\FixtureProbe\Release\net12.0\FixtureProbe.dll $evidence

# Separately measure the real fixture: prime once, then three timed samples.
$evidence = Join-Path $root 'artifacts\fixture-performance\new-actual'
.\.dotnet\dotnet.exe artifacts\bin\FixtureProbe\Release\net12.0\FixtureProbe.dll $evidence actual
```

The experiment's initial restore/build and subsequent `--no-restore` harness
rebuilds succeeded. Rebuild the harness after rebuilding acceptance assemblies:
its references are copied, not loaded live from the source directory.
Repack each compared source state and use different evidence directories;
never clear a shared/global cache. For original actual-fixture measurements,
use the starting revision recorded in the decision document in a separate
task-owned checkout. The build-only arms use identical generated inputs in one
process, independent of the fixture's current orchestration.

One untimed sequential build set primes the cache. Fresh output directories are
used thereafter. Trial order is sequential/aggregate/concurrent, then
concurrent/aggregate/sequential, then sequential/aggregate/concurrent.
The actual-fixture before/after samples were collected in separate time blocks.
No build/pack or other measurement ran concurrently with a timed sample.

## FixtureProbe.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net12.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultItems>true</EnableDefaultItems>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="$(AcceptanceDirectory)\*.dll" />
    <Reference Include="$(PlatformDirectory)\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll" />
  </ItemGroup>
</Project>
```

## Program.cs

```csharp
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Testing.TestInfrastructure;

string evidence = Path.GetFullPath(args[0]);
Directory.CreateDirectory(evidence);
Environment.SetEnvironmentVariable("Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup", "0");
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
Environment.SetEnvironmentVariable("NUGET_PACKAGES", Path.Combine(evidence, "packages"));
DotnetCli.UseMultithreadedMSBuild = true;
using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(30));
var rows = new List<object>();
Assembly mstest = Assembly.Load("MSTest.Acceptance.IntegrationTests");
Type fixtureType = mstest.GetType("MSTest.Acceptance.IntegrationTests.ParallelExecutionTests+TestAssetFixture", throwOnError: true)!;
Type baseType = mstest.GetType("Microsoft.Testing.Platform.Acceptance.IntegrationTests.AcceptanceTestBase", throwOnError: true)!;
string mstestVersion = (string)baseType.GetProperty("MSTestVersion")!.GetValue(null)!;
string[] names = ["ParallelMethodsTestProject", "ParallelClassesTestProject", "DoNotParallelizeTestProject", "DoNotParallelizeOrderingTestProject"];
string[] fields = ["MethodParallelSourceCode", "ClassParallelSourceCode", "DoNotParallelizeSourceCode", "DoNotParallelizeOrderingSourceCode"];
string[] sources = fields.Select(field => ((string)fixtureType.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!)
    .PatchTargetFrameworks(TargetFrameworks.All)
    .PatchCodeWithReplace("$MSTestVersion$", mstestVersion)).ToArray();
File.WriteAllText(Path.Combine(evidence, "provenance.json"), JsonSerializer.Serialize(new
{
    Frameworks = TargetFrameworks.All,
    MSTestVersion = mstestVersion,
    AcceptanceAssembly = mstest.Location,
    InfrastructureAssembly = typeof(TestAsset).Assembly.Location,
    ProcessorCount = Environment.ProcessorCount,
    PackageCache = Environment.GetEnvironmentVariable("NUGET_PACKAGES"),
}, new JsonSerializerOptions { WriteIndented = true }));

async Task Record(string label, string arm, int trial, Func<Task> action)
{
    int processStart = CommandLine.TotalProcessesAttempt;
    var watch = Stopwatch.StartNew();
    try
    {
        await action();
        rows.Add(new { Label = label, Arm = arm, Trial = trial, Seconds = watch.Elapsed.TotalSeconds, Processes = CommandLine.TotalProcessesAttempt - processStart, Status = "passed" });
    }
    catch (Exception exception)
    {
        rows.Add(new { Label = label, Arm = arm, Trial = trial, Seconds = watch.Elapsed.TotalSeconds, Processes = CommandLine.TotalProcessesAttempt - processStart, Status = "failed", Error = exception.ToString() });
        throw;
    }
    finally
    {
        File.WriteAllText(Path.Combine(evidence, "measurements.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{label}/{arm}/{trial}: {watch.Elapsed.TotalSeconds:F3}s");
    }
}

async Task BuildSet(string arm, int trial)
{
    using TempDirectory temporary = new();
    var assets = new List<TestAsset>();
    await Record("generation", arm, trial, async () =>
    {
        for (int index = 0; index < names.Length; index++)
        {
            assets.Add(await TestAsset.GenerateAssetAsync(names[index], sources[index], temporary));
        }
    });
    string solution = Path.Combine(temporary.Path, "ParallelAssets.slnx");
    await File.WriteAllTextAsync(solution, "<Solution>\n" + string.Join("\n", names.Select(name => $"  <Project Path=\"{name}/{name}.csproj\" />")) + "\n</Solution>\n");
    File.WriteAllText(Path.Combine(evidence, $"assets-{arm}-{trial}.json"), JsonSerializer.Serialize(new { temporary.Path, names }, new JsonSerializerOptions { WriteIndented = true }));
    await Record("build", arm, trial, async () =>
    {
        if (arm == "aggregate")
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build \"{solution}\" -c Release", callerMemberName: $"probe_{arm}_{trial}", cancellationToken: cancellation.Token);
            File.WriteAllText(Path.Combine(evidence, $"build-{arm}-{trial}.log"), result.ToString());
        }
        else if (arm == "concurrent")
        {
            DotnetMuxerResult[] results = await Task.WhenAll(assets.Select(asset => DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: $"probe_{arm}_{trial}_{asset.AssetId}", cancellationToken: cancellation.Token)));
            for (int index = 0; index < results.Length; index++)
            {
                File.WriteAllText(Path.Combine(evidence, $"build-{arm}-{trial}-{index}.log"), results[index].ToString());
            }
        }
        else
        {
            for (int index = 0; index < assets.Count; index++)
            {
                DotnetMuxerResult result = await DotnetCli.RunAsync($"build \"{assets[index].TargetAssetPath}\" -c Release", callerMemberName: $"probe_{arm}_{trial}_{assets[index].AssetId}", cancellationToken: cancellation.Token);
                File.WriteAllText(Path.Combine(evidence, $"build-{arm}-{trial}-{index}.log"), result.ToString());
            }
        }
    });
    foreach (TestAsset asset in assets)
    {
        foreach (string framework in TargetFrameworks.All)
        {
            TestHost host = TestHost.LocateFrom(asset.TargetAssetPath, asset.AssetId, framework);
            if (!File.Exists(host.FullName))
            {
                throw new InvalidOperationException($"Missing output {host.FullName}");
            }
        }
    }
}

if (args.Length > 1 && args[1] == "actual")
{
    for (int trial = 0; trial <= 3; trial++)
    {
        using var fixture = (ITestAssetFixture)Activator.CreateInstance(fixtureType)!;
        await Record("actual-fixture", trial == 0 ? "prime" : "measured", trial, () => fixture.InitializeAsync(cancellation.Token));
        foreach (string name in names)
        {
            foreach (string framework in TargetFrameworks.All)
            {
                _ = fixtureType.GetMethod("GetTestHost")!.Invoke(fixture, [name, framework]);
            }
        }
    }
    return;
}

await BuildSet("prime", 0);
for (int trial = 1; trial <= 3; trial++)
{
    string[] arms = trial % 2 == 1 ? ["sequential", "aggregate", "concurrent"] : ["concurrent", "aggregate", "sequential"];
    foreach (string arm in arms)
    {
        await BuildSet(arm, trial);
    }
}

foreach (string assemblyName in new[] { "MSTest.Acceptance.IntegrationTests", "Microsoft.Testing.Platform.Acceptance.IntegrationTests" })
{
    Assembly assembly = Assembly.Load(assemblyName);
    string typeName = assemblyName == "MSTest.Acceptance.IntegrationTests"
        ? "MSTest.Acceptance.IntegrationTests.HelpInfoTests+TestAssetFixture"
        : "Microsoft.Testing.Platform.Acceptance.IntegrationTests.HelpInfoTests+TestAssetFixture";
    Type type = assembly.GetType(typeName, throwOnError: true)!;
    for (int trial = 1; trial <= 3; trial++)
    {
        using var fixture = (ITestAssetFixture)Activator.CreateInstance(type)!;
        await Record("fixture", assemblyName, trial, () => fixture.InitializeAsync(cancellation.Token));
    }
}
```

## Acceptance execution commands

The baseline and optimized fast commands below exited 0: independently
discovered ten cases, then executed/passed ten with zero failed/skipped. The
full-matrix pack command uses `/p:FastAcceptanceTest=false`; the same selector
then discovers/executes/passes fifteen cases. Keep a fresh results directory for
each source state.

```powershell
$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$results = Join-Path (Get-Location).Path ('artifacts\fixture-performance\acceptance-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
.\.dotnet\dotnet.exe test --project $project -c Release --no-build `
  --list-tests text --filter 'FullyQualifiedName~ParallelExecutionTests' `
  -p:UsingDotNetTest=true -p:TestingPlatformCommandLineArguments= `
  -p:TestRunnerAdditionalArguments= "-bl:$results\discovery.binlog"

$methods = @(
  'AllMethodsShouldRunInParallel',
  'AllClassesShouldRunInParallel',
  'NothingShouldRunInParallel',
  'DoNotParallelizeTestRunsAfterAllParallelizableTests',
  'ParallelizationBannerIsHiddenWithMinimalOutput'
)
# From TargetFrameworks.All and the five source DynamicData declarations:
$frameworks = @('net11.0', 'net462') # Add net10.0 for the full-matrix build.
$expected = @(foreach ($method in $methods) {
  foreach ($framework in $frameworks) { '{0} ("{1}")' -f $method, $framework }
})
ConvertTo-Json -InputObject $expected | Set-Content "$results\expected-tests.json"
$started = [DateTimeOffset]::UtcNow.ToString('o')
.\.dotnet\dotnet.exe test --project $project -c Release --no-build `
  --filter 'FullyQualifiedName~ParallelExecutionTests' -p:UsingDotNetTest=true `
  "-p:ArtifactsTestResultsDir=$results" "-bl:$results\test.binlog" --show-test-results all
$testExit = $LASTEXITCODE
$reports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx' -File)
if ($reports.Count -ne 1) { throw "Expected one outer TRX, found $($reports.Count)" }
python .github\skills\testfx-acceptance-validation\scripts\verify_execution.py `
  --trx $reports[0].FullName --expected-tests "$results\expected-tests.json" `
  --started-after $started --process-exit-code $testExit
```

The execution gate exited 0 in each recorded green run. Reconcile discovery
against the source-derived plan before execution. Capture command exits and
logs before proceeding; a failed launch or discovery is not a performance sample.
