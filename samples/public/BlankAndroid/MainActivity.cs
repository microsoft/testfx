using Android.Util;

namespace BlankAndroid;

/// <summary>
/// Main activity that runs tests when the app is launched via 'dotnet run --device'.
/// Tests are executed using Microsoft.Testing.Platform and results are output to logcat.
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private const string TAG = "DeviceTests";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Log.Info(TAG, "MainActivity.OnCreate - starting test execution");
        
        // Run tests on a background thread to avoid blocking the UI thread
        _ = Task.Run(RunTestsAsync);
    }

    private async Task RunTestsAsync()
    {
        int exitCode = 1;
        
        try
        {
            // Get writable directory for test results
            var filesDir = FilesDir?.AbsolutePath ?? "/data/local/tmp";
            var testResultsDir = Path.Combine(filesDir, "TestResults");
            
            Directory.CreateDirectory(testResultsDir);
            
            Log.Info(TAG, $"╔══════════════════════════════════════════════════════════════╗");
            Log.Info(TAG, $"║               DEVICE TEST EXECUTION STARTING                 ║");
            Log.Info(TAG, $"╠══════════════════════════════════════════════════════════════╣");
            Log.Info(TAG, $"║  Results Directory: {testResultsDir}");
            Log.Info(TAG, $"╚══════════════════════════════════════════════════════════════╝");

            // Configure test arguments
            var args = new[]
            {
                "--results-directory", testResultsDir,
                "--report-trx"
            };

            Log.Info(TAG, "Calling MicrosoftTestingPlatformEntryPoint.Main...");
            
            // Run the tests via the generated entry point
            exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);
            
            Log.Info(TAG, $"╔══════════════════════════════════════════════════════════════╗");
            if (exitCode == 0)
            {
                Log.Info(TAG, $"║  ✓ TESTS PASSED (exit code: {exitCode})                        ║");
            }
            else
            {
                Log.Error(TAG, $"║  ✗ TESTS FAILED (exit code: {exitCode})                        ║");
            }
            Log.Info(TAG, $"╚══════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Test execution error: {ex}");
            exitCode = 1;
        }
        finally
        {
            Log.Info(TAG, $"Test execution complete. Finishing activity with exit code: {exitCode}");
            
            // Finish the activity and exit the process
            // This signals to 'dotnet run' that the app has completed
            RunOnUiThread(() =>
            {
                FinishAffinity();
                Java.Lang.JavaSystem.Exit(exitCode);
            });
        }
    }
}