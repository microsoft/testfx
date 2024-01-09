$projects = Get-ChildItem $PSScriptRoot/*/*.csproj

$projects | Foreach-Object { dotnet build $_ ; if ($LASTEXITCODE -ne 0) { throw "Build failed."} }

$user = git config user.name
$table = ""
foreach ($project in $projects) { 
    $dir = $project.Directory
    $name = $project.BaseName 
    $exe = $name + ".exe"
    
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & "$PSScriptRoot\..\..\artifacts\bin\$name\Debug\net8.0\$exe" --report-trx # --coverage
    $runner = $sw.ElapsedMilliseconds; 
    
    $sw.Restart()
    dotnet test $dir --no-restore --no-build --logger trx # --collect "Code Coverage"
    $vstest = $sw.ElapsedMilliseconds

    $entry = "| $name | $user | $vstest | $runner | $([math]::round($vstest/$runner * 100 , 0))% |`n"
    $entry
    $table += $entry
}


$table
