# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Portable to Full PDB conversion script for Test Platform.

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [System.String] $Configuration = "Release"
)

. $PSScriptRoot\common.lib.ps1

#
# Variables
#
Write-Verbose "Setup environment variables."
$TF_PortablePdbs = @("PlatformServices.NetCore\netstandard1.5\Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.pdb")

$PdbConverterToolVersion = Get-PackageVersion -PackageName "MicrosoftDiaSymReaderPdb2PdbVersion"

function Locate-PdbConverterTool
{
    $pdbConverter = Join-Path -path $TF_PACKAGES_DIR -ChildPath "Microsoft.DiaSymReader.Pdb2Pdb\$PdbConverterToolVersion\tools\Pdb2Pdb.exe"

    if (!(Test-Path -path $pdbConverter))
    {
       throw "Unable to locate Microsoft.DiaSymReader.Pdb2Pdb converter exe in path '$pdbConverter'."
    }

    Write-Verbose "Microsoft.DiaSymReader.Pdb2Pdb converter path is : $pdbConverter"
    return $pdbConverter

}

function ConvertPortablePdbToWindowsPdb
{
    foreach($TF_PortablePdb in $TF_PortablePdbs)
    {
        $portablePdbs += Join-Path -path $TF_OUT_DIR\$Configuration -childPath $TF_PortablePdb
    }

    $pdbConverter = Locate-PdbConverterTool

    foreach($portablePdb in $portablePdbs)
    {
	# First check if corresponding dll exists
        $dllOrExePath = $portablePdb -replace ".pdb",".dll"

		if(!(Test-Path -path $dllOrExePath))
		{
			# If no corresponding dll found, check if exe exists
			$dllOrExePath = $portablePdb -replace ".pdb",".exe"

			if(!(Test-Path -path $dllOrExePath))
            		{
			    throw "Unable to locate dll/exe corresponding to $portablePdb"
            		}
		}

        $fullpdb = $portablePdb -replace ".pdb",".pdbfull"

        Write-Verbose "$pdbConverter $dll /pdb $portablePdb /out $fullpdb"
        & $pdbConverter $dllOrExePath /pdb $portablePdb /out $fullpdb
    }
}

Write-Verbose "Converting Portable pdbs to Windows(Full) Pdbs..."
ConvertPortablePdbToWindowsPdb

