#!/usr/bin/env bash

# Keep child CLI invocations consistent with Arcade's multithreading option.
# Use the soft default so an explicit /mt:false remains effective.
if [[ "$msbuild_multi_threaded" == true ]]; then
  export MSBUILDENABLEMULTITHREADED=1
else
  export MSBUILDENABLEMULTITHREADED=0
fi
