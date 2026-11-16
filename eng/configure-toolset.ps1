# Also enable desktop MSBuild and child CLI invocations; Arcade only passes -mt to dotnet MSBuild.
# Use the soft default so an explicit /mt:false remains effective.
$env:MSBUILDENABLEMULTITHREADED = if ($msbuildMultiThreaded) { '1' } else { '0' }
