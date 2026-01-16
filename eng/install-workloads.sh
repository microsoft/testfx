#!/usr/bin/env bash

# Script to install dotnet workloads for device testing (Android, iOS, etc.)
# This script should be run after the SDK is installed in .dotnet folder.
#
# The script reads workload versions from eng/Versions.props to ensure
# consistent versions across the repository.
#
# Usage:
#   ./eng/install-workloads.sh [workloads...]
#
# Examples:
#   ./eng/install-workloads.sh                    # Install default workloads (android)
#   ./eng/install-workloads.sh android            # Install android workload with dependencies
#   ./eng/install-workloads.sh android ios maui   # Install specific workloads

set -e

source="${BASH_SOURCE[0]}"

# Resolve script directory
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
repo_root="$( cd -P "$scriptroot/.." && pwd )"

# Default workloads to install if none specified
default_workloads="android"

# Parse arguments
if [[ $# -eq 0 ]]; then
  workloads="$default_workloads"
else
  workloads="$*"
fi

# Find the dotnet installation
dotnet_root="${repo_root}/.dotnet"

if [[ ! -d "$dotnet_root" ]]; then
  echo "Error: .dotnet folder not found at $dotnet_root"
  echo "Please run ./restore.sh or ./build.sh first to install the SDK."
  exit 1
fi

dotnet_exe="$dotnet_root/dotnet"

if [[ ! -f "$dotnet_exe" ]]; then
  echo "Error: dotnet executable not found at $dotnet_exe"
  exit 1
fi

# Read versions from eng/Versions.props
versions_props="${scriptroot}/Versions.props"

get_version_from_props() {
  local property_name=$1
  if [[ -f "$versions_props" ]]; then
    # Extract the value between the XML tags using sed (more portable than grep -P)
    local value=$(sed -n "s/.*<${property_name}>\([^<]*\)<\/${property_name}>.*/\1/p" "$versions_props" 2>/dev/null | head -1)
    echo "$value"
  fi
}

# Get workload versions from Versions.props
android_version=$(get_version_from_props "MicrosoftAndroidSdkWindowsPackageVersion")
ios_version=$(get_version_from_props "MicrosoftiOSSdkPackageVersion")
# If the version references another property, try to resolve it
if [[ "$ios_version" == *'$('* ]]; then
  # Extract the referenced property name
  ref_prop=$(echo "$ios_version" | sed 's/.*\$(\([^)]*\)).*/\1/')
  if [[ -n "$ref_prop" && "$ref_prop" != "$ios_version" ]]; then
    ios_version=$(get_version_from_props "$ref_prop")
  fi
fi
maccatalyst_version=$(get_version_from_props "MicrosoftMacCatalystSdkPackageVersion")
if [[ "$maccatalyst_version" == *'$('* ]]; then
  ref_prop=$(echo "$maccatalyst_version" | sed 's/.*\$(\([^)]*\)).*/\1/')
  if [[ -n "$ref_prop" && "$ref_prop" != "$maccatalyst_version" ]]; then
    maccatalyst_version=$(get_version_from_props "$ref_prop")
  fi
fi

echo "Using dotnet from: $dotnet_exe"
echo "SDK version: $("$dotnet_exe" --version)"
echo ""
echo "Workload versions from eng/Versions.props:"
[[ -n "$android_version" ]] && echo "  Android: $android_version"
[[ -n "$ios_version" ]] && echo "  iOS: $ios_version"
[[ -n "$maccatalyst_version" ]] && echo "  Mac Catalyst: $maccatalyst_version"
echo ""

# Expand workload aliases to include dependencies and versions
# Build a space-separated list of "workload[@version]"
expanded_workloads=""
for workload in $workloads; do
  case "$workload" in
    android)
      # Android workload requires wasm-tools for some scenarios
      expanded_workloads="$expanded_workloads wasm-tools-net10"
      if [[ -n "$android_version" ]]; then
        expanded_workloads="$expanded_workloads android@${android_version}"
      else
        expanded_workloads="$expanded_workloads android"
      fi
      ;;
    ios)
      if [[ -n "$ios_version" ]]; then
        expanded_workloads="$expanded_workloads ios@${ios_version}"
      else
        expanded_workloads="$expanded_workloads ios"
      fi
      ;;
    maccatalyst)
      if [[ -n "$maccatalyst_version" ]]; then
        expanded_workloads="$expanded_workloads maccatalyst@${maccatalyst_version}"
      else
        expanded_workloads="$expanded_workloads maccatalyst"
      fi
      ;;
    maui)
      # MAUI includes android, ios, and other dependencies
      expanded_workloads="$expanded_workloads wasm-tools-net10 maui"
      ;;
    *)
      expanded_workloads="$expanded_workloads $workload"
      ;;
  esac
done

# Remove duplicates while preserving order (compare base workload name without version)
unique_workloads=""
seen_workloads=""
for workload_spec in $expanded_workloads; do
  base_workload="${workload_spec%%@*}"
  if [[ ! " $seen_workloads " =~ " $base_workload " ]]; then
    seen_workloads="$seen_workloads $base_workload"
    unique_workloads="$unique_workloads $workload_spec"
  fi
done

# Install each workload
for workload_spec in $unique_workloads; do
  # Parse workload@version format
  workload="${workload_spec%%@*}"
  version="${workload_spec#*@}"
  if [[ "$version" == "$workload_spec" ]]; then
    version=""
  fi
  
  echo "Installing workload: $workload"
  if [[ -n "$version" ]]; then
    echo "  Version: $version"
  fi
  
  # Build install command
  if [[ -n "$version" ]]; then
    "$dotnet_exe" workload install "$workload" --skip-sign-check --version "$version" || {
      echo "Warning: Failed to install workload '$workload' with version $version. Trying without specific version..."
      "$dotnet_exe" workload install "$workload" --skip-sign-check || {
        echo "Warning: Failed to install workload '$workload'. It may not be available for this SDK version."
      }
    }
  else
    "$dotnet_exe" workload install "$workload" --skip-sign-check || {
      echo "Warning: Failed to install workload '$workload'. It may not be available for this SDK version."
    }
  fi
  echo ""
done

echo "Workload installation complete."
echo ""
echo "Installed workloads:"
"$dotnet_exe" workload list
