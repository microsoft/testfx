#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

run_unit_tests=false
run_integration_tests=false
configuration=Debug
projects=''
ci=false
terminal_build_action=false
build_arguments=()
test_properties=()

while [[ $# -gt 0 ]]; do
  opt="$(echo "${1/#--/-}" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    -test|-t)
      run_unit_tests=true
      ;;
    -integrationtest)
      run_integration_tests=true
      ;;
    -configuration|-c)
      configuration=$2
      build_arguments+=("$1" "$2")
      shift
      ;;
    -projects)
      projects=$2
      build_arguments+=("$1" "$2")
      shift
      ;;
    -ci)
      ci=true
      build_arguments+=("$1")
      ;;
    -clean|-help|-h)
      terminal_build_action=true
      build_arguments+=("$1")
      ;;
    *)
      build_arguments+=("$1")
      if [[ "$1" == /p:* || "$1" == -p:* || "$1" == /bl:* || "$1" == -bl:* ]]; then
        test_properties+=("$1")
      fi
      ;;
  esac

  shift
done

"$scriptroot/eng/common/build.sh" --build --restore "${build_arguments[@]}"
exit_code=$?
if [[ $exit_code -ne 0 || "$terminal_build_action" == true ||
      ("$run_unit_tests" != true && "$run_integration_tests" != true) ]]; then
  exit $exit_code
fi

test_arguments=(--configuration "$configuration")
[[ "$run_unit_tests" == true ]] && test_arguments+=(--unit)
[[ "$run_integration_tests" == true ]] && test_arguments+=(--integration)
[[ "$ci" == true ]] && test_arguments+=(--ci)
[[ -n "$projects" ]] && test_arguments+=(--projects "$projects")
test_arguments+=(-- "${test_properties[@]}")

bash "$scriptroot/eng/run-tests.sh" "${test_arguments[@]}"
