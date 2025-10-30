using System.Reflection;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

// Create the test application builder
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

// Register MSTest as the test framework
builder.AddMSTest(() => [Assembly.GetExecutingAssembly()]);

// Add the retry extension to enable retry functionality
// This must be called to use the --retry-failed-tests command-line option
builder.AddRetryProvider();

// Build and run the test application
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
