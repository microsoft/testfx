# Copyright (c) Microsoft. All rights reserved.
# Portable to Full PDB conversion script for Test Platform.

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [System.String] $Configuration = "Release"
)

#
# Variables
#
Write-Verbose "Setup environment variables."
$TF_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$TF_PACKAGES_DIR = Join-Path $TF_ROOT_DIR "packages"
$TF_OUT_DIR = Join-Path $TF_ROOT_DIR "artifacts"
$TF_PortablePdbs =@("PlatformServices.NetCore\netstandard1.5\Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.pdb")

$PdbConverterToolVersion = "1.1.0-beta1-62316-01"

function Locate-PdbConverterTool
{
    $pdbConverter = Join-Path -path $TF_PACKAGES_DIR -ChildPath "Pdb2Pdb.$PdbConverterToolVersion\tools\Pdb2Pdb.exe"

    if (!(Test-Path -path $pdbConverter)) 
    {
       throw "Unable to locate Pdb2Pdb converter exe in path '$pdbConverter'."
    }

    Write-Verbose "Pdb2Pdb converter path is : $pdbConverter"
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

