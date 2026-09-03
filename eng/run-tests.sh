#!/usr/bin/env bash

set -u

scriptroot="$(cd -P "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$scriptroot/.." && pwd)"
configuration=Debug
run_unit_tests=false
run_integration_tests=false
ci=false
projects=''
properties=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      configuration=$2
      shift 2
      ;;
    --unit)
      run_unit_tests=true
      shift
      ;;
    --integration)
      run_integration_tests=true
      shift
      ;;
    --ci)
      ci=true
      shift
      ;;
    --projects)
      projects=$2
      shift 2
      ;;
    --)
      shift
      properties+=("$@")
      break
      ;;
    *)
      echo "Unknown test-runner argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ "$run_unit_tests" != true && "$run_integration_tests" != true ]]; then
  echo "Specify --unit, --integration, or both." >&2
  exit 1
fi

dotnet="$repo_root/.dotnet/dotnet"
if [[ ! -x "$dotnet" ]]; then
  echo "The repository-local .NET SDK was not found. Run ./build.sh before running tests." >&2
  exit 1
fi

if [[ "$ci" == true ]]; then
  export DOTNET_ROOT="$repo_root/.dotnet"
  export NUGET_PACKAGES="$repo_root/.packages"
fi

test_results_directory="$repo_root/artifacts/TestResults/$configuration"
mkdir -p "$test_results_directory"

forwarded_properties=()
requested_binary_log=''
for property in "${properties[@]}"; do
  case "$property" in
    /bl:*|-bl:*)
      requested_binary_log="${property#*:}"
      requested_binary_log="${requested_binary_log%\"}"
      requested_binary_log="${requested_binary_log#\"}"
      ;;
    /bl|-bl)
      ;;
    *)
      forwarded_properties+=("$property")
      ;;
  esac
done

run_native_test() {
  local selection_option=$1
  local selection=$2
  local binary_log_name=$3
  local use_unique_binary_log=${4:-false}
  local binary_log="${requested_binary_log:-$test_results_directory/$binary_log_name}"
  if [[ -n "$requested_binary_log" && "$use_unique_binary_log" == true ]]; then
    local requested_directory requested_file requested_stem requested_extension selection_name unique_file
    requested_directory="$(dirname "$requested_binary_log")"
    requested_file="$(basename "$requested_binary_log")"
    requested_stem="${requested_file%.*}"
    requested_extension=''
    [[ "$requested_file" == *.* ]] && requested_extension=".${requested_file##*.}"
    selection_name="${binary_log_name%.*}"
    unique_file="$requested_stem.$selection_name$requested_extension"
    binary_log="$unique_file"
    [[ "$requested_directory" != "." ]] && binary_log="$requested_directory/$unique_file"
  fi

  "$dotnet" test "$selection_option" "$selection" \
    --configuration "$configuration" \
    --no-build \
    "-bl:$binary_log" \
    "${forwarded_properties[@]}"
}

if [[ -n "$projects" ]]; then
  IFS=';' read -ra selected_projects <<< "$projects"
  use_unique_binary_log=false
  [[ ${#selected_projects[@]} -gt 1 ]] && use_unique_binary_log=true
  for index in "${!selected_projects[@]}"; do
    project="${selected_projects[$index]}"
    if [[ "$project" != /* ]]; then
      project="$repo_root/$project"
    fi
    project_file="${project##*[\\/]}"
    project_name="${project_file%.*}"
    binary_log_name="$project_name.binlog"
    if [[ "$use_unique_binary_log" == true ]]; then
      printf -v binary_log_name '%02d.%s.binlog' "$((index + 1))" "$project_name"
    fi
    case "$project" in
      *.sln|*.slnf|*.slnx)
        run_native_test --solution "$project" "$binary_log_name" "$use_unique_binary_log" || exit $?
        continue
        ;;
    esac

    is_unit_test=false
    is_integration_test=false
    [[ "$project_name" == *.IntegrationTests ]] && is_integration_test=true
    [[ "$is_integration_test" != true && ("$project_name" == *.UnitTests || "$project_name" == *.Tests) ]] && is_unit_test=true
    if { [[ "$run_unit_tests" == true && "$is_unit_test" == true ]]; } ||
       { [[ "$run_integration_tests" == true && "$is_integration_test" == true ]]; }; then
      run_native_test --project "$project" "$binary_log_name" "$use_unique_binary_log" || exit $?
    fi
  done

  exit 0
fi

if [[ "$run_unit_tests" == true && "$run_integration_tests" == true ]]; then
  solution=NonWindowsTests.slnf
elif [[ "$run_unit_tests" == true ]]; then
  solution=NonWindowsUnitTests.slnf
else
  solution=NonWindowsIntegrationTests.slnf
fi

run_native_test --solution "$repo_root/$solution" TestStep.binlog
