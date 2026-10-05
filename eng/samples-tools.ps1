function Get-SampleRelativePath {
    param(
        [string]$FullPath,
        [string]$SamplesFolder
    )

    $samplesFolderWithTrailingSeparator = [System.IO.Path]::GetFullPath($SamplesFolder).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $normalizedFullPath = [System.IO.Path]::GetFullPath($FullPath)

    if (!$normalizedFullPath.StartsWith($samplesFolderWithTrailingSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$FullPath' is not under the public samples folder '$SamplesFolder'."
    }

    return $normalizedFullPath.Substring($samplesFolderWithTrailingSeparator.Length)
}

function Get-SampleBinlogArgument {
    param(
        [string]$BinaryLogDirectory,
        [string]$LogName,
        [string]$ArgumentPrefix = "-bl:"
    )

    $binlogPath = Join-Path $BinaryLogDirectory "$LogName.binlog"
    return "$ArgumentPrefix$binlogPath"
}
