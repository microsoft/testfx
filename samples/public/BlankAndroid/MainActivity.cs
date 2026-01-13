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

                // Redirect console to logcat
                Console.SetOut(new LogcatTextWriter());

                // Configure test results directory to a writable location
                var filesDir = FilesDir?.AbsolutePath ?? "/data/local/tmp";
                var testResultsDir = Path.Combine(filesDir, "TestResults");
                
                // Pass arguments to MTP
                var args = new[] 
                { 
                    "--results-directory", testResultsDir
                };
                Log.Info(TAG, $"Test results directory: {testResultsDir}");

                int exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);

                Log.Info(TAG, $"Tests completed with exit code: {exitCode}");

                RunOnUiThread(() =>
                {
                    var textView = new Android.Widget.TextView(this)
                    {
                        Text = $"Tests completed with exit code: {exitCode}"
                    };
                    SetContentView(textView);
                });

                // Exit the app with the test result exit code
                Java.Lang.JavaSystem.Exit(exitCode);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Test error: {ex}");
                RunOnUiThread(() =>
                {
                    var textView = new Android.Widget.TextView(this)
                    {
                        Text = $"Test error: {ex.Message}"
                    };
                    SetContentView(textView);
                });
            }
        });
    }
}

internal sealed class LogcatTextWriter : System.IO.TextWriter
{
    private const string TAG = "BlankAndroid.Tests";

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public override void Write(char value)
    {
        // Single chars are not useful for logcat
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Log.Info(TAG, value);
        }
    }

    public override void WriteLine(string? value)
    {
        Log.Info(TAG, value ?? string.Empty);
    }
}