// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ConsoleTests : AcceptanceTestBase<ConsoleTests.TestAssetFixture>
{
    private const string AssetName = "ConsoleTests";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task StartUpdateShouldNotDeadLockWithDifferentThreadWritingToReplacedConsoleInterceptor(string tfm)
        => await ConsoleTestsCoreAsync(tfm, "REPLACE_CONSOLE_WITH_WRAPPER");

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [Ignore("""
        This currently doesn't pass because SystemConsole is writing to StreamWriter directly. It's not synchronized AT ALL.
        We can fix later if it started to show issues.
        """)]
    public async Task StartUpdateShouldNotBeInterruptedConsoleIsReplaced(string tfm)
        => await ConsoleTestsCoreAsync(tfm, "REPLACE_CONSOLE");

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [Ignore("""
        This currently doesn't pass because SystemConsole is writing to StreamWriter directly. It's not synchronized AT ALL.
        We can fix later if it started to show issues.
        """)]
    public async Task StartUpdateShouldNotBeInterruptedConsoleIsNotReplaced(string tfm)
        => await ConsoleTestsCoreAsync(tfm, null);

    private async Task ConsoleTestsCoreAsync(string tfm, string? environmentVariableToSet)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        Dictionary<string, string?>? environmentVariables = null;
        if (environmentVariableToSet is not null)
        {
            environmentVariables = new Dictionary<string, string?>
            {
                { environmentVariableToSet, "1" },
            };
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync("--no-ansi --ignore-exit-code 8", environmentVariables);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("ABCDEF123");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file ConsoleTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Services;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new Capabilities(), (_, sp) => new DummyTestFramework(sp));
        using ITestApplication app = await builder.BuildAsync();

        return await app.RunAsync();
    }
}

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    private sealed class ConsoleInterceptor : TextWriter
    {
        private TextWriter _originalOut;

        public ConsoleInterceptor()
        {
            _originalOut = Console.Out;
        }

        public override Encoding Encoding => RedirectedOut?.Encoding ?? Encoding.UTF8;

        public TextWriter RedirectedOut { get; } = TextWriter.Synchronized(new StringWriter());

        public override void Flush()
        {
            _originalOut.Flush();
        }

        public override void Write(bool value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(char[] buffer)
        {
            _originalOut.Write(buffer);
            RedirectedOut?.Write(buffer);
        }

        public override void Write(decimal value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(double value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(int value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(long value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(object value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(float value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(string format, object arg0)
        {
            _originalOut.Write(format, arg0);
            RedirectedOut?.Write(format, arg0);
        }

        public override void Write(string format, object arg0, object arg1)
        {
            _originalOut.Write(format, arg0, arg1);
            RedirectedOut?.Write(format, arg0, arg1);
        }

        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            _originalOut.Write(format, arg0, arg1, arg2);
            RedirectedOut?.Write(format, arg0, arg1, arg2);
        }

        public override void Write(string format, params object[] arg)
        {
            _originalOut.Write(format, arg);
            RedirectedOut?.Write(format, arg);
        }

        public override void Write(uint value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(ulong value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void WriteLine()
        {
            _originalOut.WriteLine();
            RedirectedOut?.WriteLine();
        }

        public override void WriteLine(bool value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(char value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(char[] buffer)
        {
            _originalOut.WriteLine(buffer);
            RedirectedOut?.WriteLine(buffer);
        }

        public override void WriteLine(char[] buffer, int index, int count)
        {
            _originalOut.WriteLine(buffer, index, count);
            RedirectedOut?.WriteLine(buffer, index, count);
        }

        public override void WriteLine(decimal value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(double value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(int value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(long value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(object value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(float value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(string value)
        {
            Thread.Sleep(1000);
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(string format, object arg0)
        {
            _originalOut.WriteLine(format, arg0);
            RedirectedOut?.WriteLine(format, arg0);
        }

        public override void WriteLine(string format, object arg0, object arg1)
        {
            _originalOut.WriteLine(format, arg0, arg1);
            RedirectedOut?.WriteLine(format, arg0, arg1);
        }

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            _originalOut.WriteLine(format, arg0, arg1, arg2);
            RedirectedOut?.WriteLine(format, arg0, arg1, arg2);
        }

        public override void WriteLine(string format, params object[] arg)
        {
            _originalOut.WriteLine(format, arg);
            RedirectedOut?.WriteLine(format, arg);
        }

        public override void WriteLine(uint value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override void WriteLine(ulong value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override async Task WriteLineAsync()
        {
            await _originalOut.WriteLineAsync();
            await (RedirectedOut?.WriteLineAsync() ?? Task.CompletedTask);
        }

        public override IFormatProvider FormatProvider => _originalOut.FormatProvider;

        public override string NewLine
        {
            get => _originalOut.NewLine;
            set
            {
                _originalOut.NewLine = value;

                if (RedirectedOut != null)
                {
                    RedirectedOut.NewLine = value;
                }
            }
        }

        public override void Close()
        {
        }

        public override Task FlushAsync()
        {
            return _originalOut.FlushAsync();
        }

        public override void Write(char value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            _originalOut.Write(buffer, index, count);
            RedirectedOut?.Write(buffer, index, count);
        }

        public override void Write(string value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override async Task WriteAsync(char value)
        {
            await _originalOut.WriteAsync(value);
            await (RedirectedOut?.WriteAsync(value) ?? Task.CompletedTask);
        }
        public override async Task WriteAsync(char[] buffer, int index, int count)
        {
            await _originalOut.WriteAsync(buffer, index, count);
            await (RedirectedOut?.WriteAsync(buffer, index, count) ?? Task.CompletedTask);
        }
        public override async Task WriteAsync(string value)
        {
            await _originalOut.WriteAsync(value);
            await (RedirectedOut?.WriteAsync(value) ?? Task.CompletedTask);
        }
#if NET
        public override void Write(ReadOnlySpan<char> buffer)
        {
            _originalOut.Write(buffer);
            RedirectedOut?.Write(buffer);
        }

        public override void Write(StringBuilder value)
        {
            _originalOut.Write(value);
            RedirectedOut?.Write(value);
        }

        public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = new())
        {
            await _originalOut.WriteAsync(buffer, cancellationToken);
            await (RedirectedOut?.WriteAsync(buffer, cancellationToken) ?? Task.CompletedTask);
        }

        public override async Task WriteAsync(StringBuilder value, CancellationToken cancellationToken = new())
        {
            await _originalOut.WriteAsync(value, cancellationToken);
            await (RedirectedOut?.WriteAsync(value, cancellationToken) ?? Task.CompletedTask);
        }

        public override void WriteLine(ReadOnlySpan<char> buffer)
        {
            _originalOut.WriteLine(buffer);
            RedirectedOut?.WriteLine(buffer);
        }

        public override void WriteLine(StringBuilder value)
        {
            _originalOut.WriteLine(value);
            RedirectedOut?.WriteLine(value);
        }

        public override async Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = new())
        {
            await _originalOut.WriteLineAsync(buffer, cancellationToken);
            await (RedirectedOut?.WriteLineAsync(buffer, cancellationToken) ?? Task.CompletedTask);
        }

        public override async Task WriteLineAsync(StringBuilder value, CancellationToken cancellationToken = new())
        {
            await _originalOut.WriteLineAsync(value, cancellationToken);
            await (RedirectedOut?.WriteLineAsync(value, cancellationToken) ?? Task.CompletedTask);
        }
#endif
        public override async Task WriteLineAsync(char value)
        {
            await _originalOut.WriteLineAsync(value);
            await (RedirectedOut?.WriteLineAsync(value) ?? Task.CompletedTask);
        }

        public override async Task WriteLineAsync(char[] buffer, int index, int count)
        {
            await _originalOut.WriteLineAsync(buffer, index, count);
            await (RedirectedOut?.WriteLineAsync(buffer, index, count) ?? Task.CompletedTask);
        }

        public override async Task WriteLineAsync(string value)
        {
            await _originalOut.WriteLineAsync(value);
            await (RedirectedOut?.WriteLineAsync(value) ?? Task.CompletedTask);
        }
    }

    private readonly IServiceProvider _serviceProvider;

    public DummyTestFramework(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task ReplaceConsoleWithWrapper(ExecuteRequestContext context)
    {
        var interceptor = new ConsoleInterceptor();
        Console.SetOut(interceptor);

        // This goes through the interceptor after acquiring lock for ConsoleInterceptor.
        var thread = new Thread(() => Console.WriteLine("ABCDEF123"));
        thread.Start();

        var mtpConsole = typeof(ServiceProviderExtensions).GetMethod("GetConsole", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { _serviceProvider });
        var writeMethod = mtpConsole.GetType().GetMethods().Single(m => m.Name == "Write" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

        Thread.Sleep(300);

        // This will acquire the lock for the original Console.Out, before being replaced by ConsoleInterceptor.
        writeMethod.Invoke(mtpConsole, new object[] { "" });
        context.Complete();
        return Task.CompletedTask;
    }

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (Environment.GetEnvironmentVariable("REPLACE_CONSOLE_WITH_WRAPPER") == "1")
        {
            return ReplaceConsoleWithWrapper(context);
        }

        var replaceConsole = Environment.GetEnvironmentVariable("REPLACE_CONSOLE") == "1";

        StringWriter newConsole = null;
        var originalConsole = Console.Out;
        if (replaceConsole)
        {
            newConsole = new StringWriter();
            Console.SetOut(newConsole);
        }

        var mtpConsole = typeof(ServiceProviderExtensions).GetMethod("GetConsole", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { _serviceProvider });
        var terminal = Activator.CreateInstance(typeof(ExecuteRequestContext).Assembly.GetType("Microsoft.Testing.Platform.OutputDevice.Terminal.NonAnsiTerminal"), new object[] { mtpConsole });
        terminal.GetType().GetMethod("StartUpdate").Invoke(terminal, null);
        var appendMethod = terminal.GetType().GetMethods().Single(m => m.Name == "Append" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        var writeMethod = mtpConsole.GetType().GetMethods().Single(m => m.Name == "Write" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        appendMethod.Invoke(terminal, new object[] { "ABC" });

        // This thread shouldn't be able to write to the console until StopUpdate is called.
        var thread = new Thread(() => writeMethod.Invoke(mtpConsole, new object[] { "123" }));
        thread.Start();

        Thread.Sleep(1000);
        appendMethod.Invoke(terminal, new object[] { "DEF" });
        terminal.GetType().GetMethod("StopUpdate").Invoke(terminal, null);

        context.Complete();

        if (replaceConsole)
        {
            Console.SetOut(originalConsole);
            Console.Write(newConsole.ToString());
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }
}
