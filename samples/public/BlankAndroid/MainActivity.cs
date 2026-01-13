namespace BlankAndroid;

/// <summary>
/// Main activity - kept minimal since tests are run via TestInstrumentation.
/// This activity is only needed for app manifest requirements.
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
    }
}