using Android.Util;

namespace BlankAndroid;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private const string TAG = "BlankAndroid";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Log.Info(TAG, "Starting test execution...");

        // Run tests when the app starts
        Task.Run(async () =>
        {
            try
            {
                Log.Info(TAG, "Invoking test entry point...");

                // Configure test results directory to a writable location
                var filesDir = FilesDir?.AbsolutePath ?? "/data/local/tmp";
                var testResultsDir = Path.Combine(filesDir, "TestResults");
                var args = new[] { "--results-directory", testResultsDir };

                Log.Info(TAG, $"Test results directory: {testResultsDir}");

                int exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);

                Log.Info(TAG, $"Tests completed with exit code: {exitCode}");

                // Exit the app with the test result exit code
                Java.Lang.JavaSystem.Exit(exitCode);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Test error: {ex}");
                Java.Lang.JavaSystem.Exit(1);
            }
        });
    }
}