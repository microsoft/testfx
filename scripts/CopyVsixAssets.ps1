# Copyright (c) Microsoft. All rights reserved.
# Copy script for the MSTest Test Framework templates and Wizards.

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [Alias("c")]
    [System.String] $Configuration = "Release",

    [Parameter(Mandatory=$true)]
    [Alias("d")]
    [System.String] $Destination = ""
)

#
# Environment Variables
#
$env:MSTEST_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$env:MSTEST_TEMPLATES_DIR = Join-Path $env:MSTEST_ROOT_DIR "Templates"
$env:MSTEST_WIZARDS_DIR = Join-Path $env:MSTEST_ROOT_DIR "WizardExtensions"

function Copy-VsixAssets()
{    
    # Copy over the template vsixs.
    $sources = @( $env:MSTEST_TEMPLATES_DIR, $env:MSTEST_WIZARDS_DIR );

    foreach($location in $sources)
    {
        $vsixs = (Get-ChildItem -File $location -Filter *.vsix -Recurse).FullName
        
        foreach($vsix in $vsixs)
        {
            if($vsix -Match $Configuration)
            {
                Write-Output "Copying $vsix to $Destination"
                Copy-Item $vsix $Destination -Force
            }
        }
    }
}

Write-Output("Copying all the vsix assets to $Destination.")

Copy-VsixAssets

Write-Output("Done copying vsix assets.")