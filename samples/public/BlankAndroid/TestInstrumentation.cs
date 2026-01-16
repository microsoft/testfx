using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace BlankAndroid;

/// <summary>
/// Android Instrumentation class for running tests.
/// This class is invoked via: adb shell am instrument -w com.companyname.BlankAndroid/blankandroid.TestInstrumentation
/// </summary>
[Instrumentation(Name = "blankandroid.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    private const string TAG = "TestInstrumentation";

    // Required constructor for Android .NET interop
    public TestInstrumentation(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Log.Info(TAG, "TestInstrumentation.OnCreate called");
        
        // Start the instrumentation - this will call OnStart
        Start();
    }

    public override async void OnStart()
    {
        base.OnStart();
        Log.Info(TAG, "TestInstrumentation.OnStart called - running tests");

        int exitCode = 1;
        Bundle results = new Bundle();

        try
        {
            // Get writable directory for test results
            var context = TargetContext;
            var filesDir = context?.FilesDir?.AbsolutePath ?? "/data/local/tmp";
            var testResultsDir = Path.Combine(filesDir, "TestResults");
            
            Directory.CreateDirectory(testResultsDir);
            
            Log.Info(TAG, $"Test results directory: {testResultsDir}");

            // Configure test arguments
            var args = new[]
            {
                "--results-directory", testResultsDir,
                "--report-trx"
            };

            Log.Info(TAG, "Starting test execution via MicrosoftTestingPlatformEntryPoint.Main");
            
            // Run the tests
            exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);
            
            Log.Info(TAG, $"Tests completed with exit code: {exitCode}");
            
            results.PutInt("exitCode", exitCode);
            results.PutString("testResultsDir", testResultsDir);
            
            if (exitCode == 0)
            {
                results.PutString("status", "SUCCESS");
            }
            else
            {
                results.PutString("status", "FAILURE");
            }
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Test execution error: {ex}");
            results.PutString("status", "ERROR");
            results.PutString("error", ex.ToString());
            exitCode = 1;
        }
        finally
        {
            Log.Info(TAG, $"Finishing instrumentation with exit code: {exitCode}");
            
            // Signal completion - exitCode 0 = success, non-zero = failure
            // The second parameter is the result code
            Finish(exitCode == 0 ? Result.Ok : Result.Canceled, results);
        }
    }
}
