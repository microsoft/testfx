---
description: >-
  Deterministic source inventory, GitHub eligibility resolution, and trusted
  safe-output publication for Unskip Closed Tests.

jobs:
  collect-unskip-candidates:
    name: Collect verified unskip candidates
    runs-on: ubuntu-latest
    timeout-minutes: 15
    permissions:
      contents: read
      issues: read
      pull-requests: read
    outputs:
      eligible-count: ${{ steps.collect.outputs.eligible-count }}
      source-commit: ${{ steps.collect.outputs.source-commit }}
      manifest-digest: ${{ steps.collect.outputs.manifest-digest }}
    steps:
      - name: Checkout trusted source revision
        uses: actions/checkout@v7
        with:
          ref: ${{ github.sha }}
          fetch-depth: 1
          persist-credentials: false

      - name: Set up .NET SDK
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: "8.0.x"

      - name: Restore trusted inventory tool
        working-directory: .github/workflows/unskip-closed-tests-tool
        run: dotnet restore UnskipClosedTests.Tool.csproj --locked-mode

      - name: Inventory source and resolve tracking items
        id: collect
        shell: bash
        working-directory: .github/workflows/unskip-closed-tests-tool
        env:
          GH_TOKEN: ${{ github.token }}
          EXPECTED_REPOSITORY: ${{ github.repository }}
          EXPECTED_COMMIT: ${{ github.sha }}
          RAW_INVENTORY: ${{ runner.temp }}/unskip-closed-tests-inventory.json
          RESOLVED_MANIFEST: ${{ runner.temp }}/unskip-closed-tests-manifest.json
        run: |
          set -euo pipefail

          set +e
          dotnet run --no-restore \
            --project UnskipClosedTests.Tool.csproj \
            -- inventory \
            --repo-root "$GITHUB_WORKSPACE" \
            --repository "$EXPECTED_REPOSITORY" \
            --source-commit "$EXPECTED_COMMIT" \
            --config "$GITHUB_WORKSPACE/.github/workflows/unskip-closed-tests.config.json" \
            --output "$RAW_INVENTORY"
          INVENTORY_EXIT=$?
          set -e
          if [ "$INVENTORY_EXIT" -ne 0 ] && [ "$INVENTORY_EXIT" -ne 10 ]; then
            exit "$INVENTORY_EXIT"
          fi

          set +e
          dotnet run --no-restore \
            --project UnskipClosedTests.Tool.csproj \
            -- resolve \
            --manifest "$RAW_INVENTORY" \
            --output "$RESOLVED_MANIFEST"
          RESOLVE_EXIT=$?
          set -e
          if [ "$RESOLVE_EXIT" -ne 0 ] && [ "$RESOLVE_EXIT" -ne 10 ]; then
            exit "$RESOLVE_EXIT"
          fi

          ELIGIBLE_COUNT=$(jq -r '[.candidates[] | select(.decision.eligible == true)] | length' "$RESOLVED_MANIFEST")
          MANIFEST_DIGEST=$(jq -r '.manifest_digest' "$RESOLVED_MANIFEST")
          test "$MANIFEST_DIGEST" != "null"
          cp "$RESOLVED_MANIFEST" "$GITHUB_WORKSPACE/manifest.json"
          {
            echo "eligible-count=$ELIGIBLE_COUNT"
            echo "source-commit=$EXPECTED_COMMIT"
            echo "manifest-digest=$MANIFEST_DIGEST"
          } >> "$GITHUB_OUTPUT"

      - name: Upload trusted candidate manifest
        uses: actions/upload-artifact@v7
        with:
          name: unskip-closed-tests-manifest-${{ github.run_id }}-${{ github.run_attempt }}
          path: manifest.json
          if-no-files-found: error
          retention-days: 1

  verify-selected-unskips:
    name: Verify selected unskips without write credentials
    needs: [agent, detection]
    if: >-
      needs.agent.result == 'success' &&
      needs.detection.result == 'success' &&
      needs.detection.outputs.detection_success == 'true' &&
      contains(needs.agent.outputs.output_types, 'apply_verified_unskips')
    runs-on: ubuntu-latest
    timeout-minutes: 45
    permissions:
      contents: read
      issues: read
      pull-requests: read
    steps:
      - name: Download agent output artifact
        uses: actions/download-artifact@v8.0.1
        with:
          pattern: "{agent,agent-output-fallback}"
          merge-multiple: true
          path: ${{ runner.temp }}/gh-aw/verify-job

      - name: Checkout exact analyzed revision without credentials
        uses: actions/checkout@v7
        with:
          ref: ${{ github.sha }}
          fetch-depth: 0
          persist-credentials: false

      - name: Set up .NET SDK
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: "8.0.x"

      - name: Restore trusted apply tool
        working-directory: .github/workflows/unskip-closed-tests-tool
        run: dotnet restore UnskipClosedTests.Tool.csproj --locked-mode

      - name: Download original trusted manifest
        uses: actions/download-artifact@v8.0.1
        with:
          name: unskip-closed-tests-manifest-${{ github.run_id }}-${{ github.run_attempt }}
          path: ${{ runner.temp }}/unskip-closed-tests-manifest

      - name: Revalidate, edit, verify, and package selected candidates
        shell: bash
        working-directory: .github/workflows/unskip-closed-tests-tool
        env:
          GH_TOKEN: ${{ github.token }}
          EXPECTED_REPOSITORY: ${{ github.repository }}
          EXPECTED_COMMIT: ${{ github.sha }}
          AGENT_OUTPUT: ${{ runner.temp }}/gh-aw/verify-job/agent_output.json
          ORIGINAL_MANIFEST: ${{ runner.temp }}/unskip-closed-tests-manifest/manifest.json
          RESULT_PATH: ${{ runner.temp }}/unskip-closed-tests-result.json
          PACKAGE_DIRECTORY: ${{ runner.temp }}/unskip-closed-tests-verification
        run: |
          set -euo pipefail

          rm -rf "$PACKAGE_DIRECTORY"
          mkdir -p "$PACKAGE_DIRECTORY/files"

          set +e
          timeout --kill-after=30s 40m dotnet run --no-restore \
            --project UnskipClosedTests.Tool.csproj \
            -- apply \
            --repo-root "$GITHUB_WORKSPACE" \
            --config "$GITHUB_WORKSPACE/.github/workflows/unskip-closed-tests.config.json" \
            --manifest "$ORIGINAL_MANIFEST" \
            --agent-output "$AGENT_OUTPUT" \
            --output "$RESULT_PATH"
          APPLY_EXIT=$?
          set -e

          if [ "$APPLY_EXIT" -ne 0 ] && [ "$APPLY_EXIT" -ne 10 ]; then
            rm -rf bin obj
            exit "$APPLY_EXIT"
          fi

          test -f "$RESULT_PATH"
          MANIFEST_DIGEST=$(jq -r '.manifest_digest' "$ORIGINAL_MANIFEST")
          test "$MANIFEST_DIGEST" != "null"
          jq -e \
            --arg source "$EXPECTED_COMMIT" \
            --arg digest "$MANIFEST_DIGEST" \
            '.schema_version == "1" and
             .source_commit == $source and
             .manifest_digest == $digest' \
            "$RESULT_PATH" >/dev/null

          rm -rf bin obj
          git diff --cached --quiet

          if [ "$APPLY_EXIT" -eq 10 ]; then
            jq -e \
              '.has_changes == false and
               (.retained_candidates | length) == 0 and
               (.changed_files | length) == 0 and
               (.changed_paths | length) == 0' \
              "$RESULT_PATH" >/dev/null
            git diff --quiet
          else
            jq -e \
              '.has_changes == true and
               (.retained_candidates | length) > 0 and
               (.changed_files | length) > 0 and
               ([.changed_files[].path] | length) == ([.changed_files[].path] | unique | length) and
               ([.changed_files[].path] | sort) == (.changed_paths | sort) and
               all(.changed_files[];
                 (.path | type == "string") and
                 (.content_sha256 | type == "string" and test("^[0-9a-f]{64}$")))' \
              "$RESULT_PATH" >/dev/null

            EXPECTED_FILES="$RUNNER_TEMP/unskip-closed-tests-expected-files.bin"
            EXPECTED_PATHS="$RUNNER_TEMP/unskip-closed-tests-expected-paths.bin"
            ACTUAL_PATHS="$RUNNER_TEMP/unskip-closed-tests-actual-paths.bin"
            jq -j '.changed_files[] | .path, "\u0000", .content_sha256, "\u0000"' \
              "$RESULT_PATH" > "$EXPECTED_FILES"
            jq -j '.changed_files[].path, "\u0000"' "$RESULT_PATH" | sort -z > "$EXPECTED_PATHS"
            git diff --name-only --no-renames -z | sort -z > "$ACTUAL_PATHS"
            cmp "$EXPECTED_PATHS" "$ACTUAL_PATHS"

            while IFS= read -r -d '' path && IFS= read -r -d '' expected_sha; do
                test -n "$path"
                test "${path#/}" = "$path"
                case "/$path/" in
                  *"/../"*|*"/./"*) exit 20 ;;
                esac
                case "$path" in
                  *.cs) ;;
                  *) echo "::error::Unexpected changed path: $path"; exit 20 ;;
                esac
                actual_sha=$(sha256sum -- "$GITHUB_WORKSPACE/$path")
                actual_sha=${actual_sha%% *}
                test "$actual_sha" = "$expected_sha" ||
                  { echo "::error::Verified content changed for $path"; exit 20; }
                blob="$PACKAGE_DIRECTORY/files/$expected_sha"
                if [ -e "$blob" ]; then
                  cmp "$GITHUB_WORKSPACE/$path" "$blob"
                else
                  cp -- "$GITHUB_WORKSPACE/$path" "$blob"
                fi
            done < "$EXPECTED_FILES"
          fi

          cp "$RESULT_PATH" "$PACKAGE_DIRECTORY/result.json"

      - name: Upload verified unskip package
        uses: actions/upload-artifact@v7
        with:
          name: unskip-closed-tests-verification-${{ github.run_id }}-${{ github.run_attempt }}
          path: ${{ runner.temp }}/unskip-closed-tests-verification
          if-no-files-found: error
          retention-days: 1

safe-outputs:
  needs: [verify-selected-unskips]
  jobs:
    apply-verified-unskips:
      description: >-
        Publish the exact read-only verified Ignore-removal package in a fresh
        authenticated checkout and open at most one draft pull request.
      needs: safe_outputs
      if: >-
        needs.agent.result == 'success' &&
        needs.detection.result == 'success' &&
        needs.detection.outputs.detection_success == 'true' &&
        contains(needs.agent.outputs.output_types, 'apply_verified_unskips')
      runs-on: ubuntu-latest
      permissions:
        contents: write
        issues: read
        pull-requests: write
      inputs:
        manifest_digest:
          description: "Exact trusted manifest digest."
          required: true
          type: string
        candidate_ids_json:
          description: "JSON array of exact candidate IDs copied from the manifest."
          required: true
          type: string
      steps:
        - name: Download original trusted manifest
          uses: actions/download-artifact@v8.0.1
          with:
            name: unskip-closed-tests-manifest-${{ github.run_id }}-${{ github.run_attempt }}
            path: ${{ runner.temp }}/unskip-closed-tests-manifest

        - name: Download verified unskip package
          uses: actions/download-artifact@v8.0.1
          with:
            name: unskip-closed-tests-verification-${{ github.run_id }}-${{ github.run_attempt }}
            path: ${{ runner.temp }}/unskip-closed-tests-verification

        - name: Checkout exact analyzed revision with publisher credentials
          uses: actions/checkout@v7
          with:
            ref: ${{ github.sha }}
            fetch-depth: 0
            persist-credentials: true

        - name: Publish one verified draft pull request
          shell: bash
          env:
            GH_TOKEN: ${{ github.token }}
            EXPECTED_REPOSITORY: ${{ github.repository }}
            EXPECTED_COMMIT: ${{ github.sha }}
            ORIGINAL_MANIFEST: ${{ runner.temp }}/unskip-closed-tests-manifest/manifest.json
            PACKAGE_DIRECTORY: ${{ runner.temp }}/unskip-closed-tests-verification
            RESULT_PATH: ${{ runner.temp }}/unskip-closed-tests-verification/result.json
          run: |
            set -euo pipefail

            test -f "$RESULT_PATH"
            test -d "$PACKAGE_DIRECTORY/files"
            MANIFEST_DIGEST=$(jq -r '.manifest_digest' "$ORIGINAL_MANIFEST")
            test "$MANIFEST_DIGEST" != "null"
            jq -e \
              --arg source "$EXPECTED_COMMIT" \
              --arg digest "$MANIFEST_DIGEST" \
              --slurpfile manifest "$ORIGINAL_MANIFEST" \
              '.schema_version == "1" and
               .source_commit == $source and
               .manifest_digest == $digest and
               (.has_changes | type == "boolean") and
               if .has_changes then
                 (.retained_candidates | length) > 0 and
                 ([.retained_candidates[].candidate_id] | length) ==
                   ([.retained_candidates[].candidate_id] | unique | length) and
                 all(.retained_candidates[];
                   . as $retained |
                   any($manifest[0].candidates[];
                     .candidate_id == $retained.candidate_id and
                     .path == $retained.path and
                     .decision.eligible == true and
                     ((.owner.test_fqns | sort) == ($retained.test_fqns | sort)))) and
                 (.changed_files | length) > 0 and
                 ([.changed_files[].path] | length) ==
                   ([.changed_files[].path] | unique | length) and
                 ([.changed_files[].path] | sort) == (.changed_paths | sort) and
                 ([.retained_candidates[].path] | unique | sort) == (.changed_paths | sort) and
                 all(.changed_files[];
                   (.path | type == "string") and
                   (.content_sha256 | type == "string" and test("^[0-9a-f]{64}$"))) and
                 (.pr_title | type == "string" and
                   startswith("[unskip-closed-tests] ") and length <= 256) and
                 (.pr_body | type == "string" and
                   startswith("<!-- unskip-closed-tests:v1;source=\($source);manifest=\($digest) -->"))
               else
                 (.retained_candidates | length) == 0 and
                 (.changed_files | length) == 0 and
                 (.changed_paths | length) == 0 and
                 .pr_title == "" and
                 .pr_body == ""
               end' \
              "$RESULT_PATH" >/dev/null

            if [ "$(jq -r '.has_changes' "$RESULT_PATH")" = "false" ]; then
              test -z "$(find "$PACKAGE_DIRECTORY/files" -mindepth 1 -print -quit)"
              git diff --quiet
              echo "::notice::No selected candidate passed verification."
              exit 0
            fi

            DEFAULT_BRANCH=$(gh api "repos/${EXPECTED_REPOSITORY}" --jq '.default_branch')
            CURRENT_HEAD=$(gh api "repos/${EXPECTED_REPOSITORY}/commits/${DEFAULT_BRANCH}" --jq '.sha')
            test "$CURRENT_HEAD" = "$EXPECTED_COMMIT" ||
              { echo "::notice::Default branch advanced; leaving verified changes unpublished."; exit 0; }

            EXISTING=$(gh pr list \
              --repo "$EXPECTED_REPOSITORY" \
              --state open \
              --search 'in:title "[unskip-closed-tests]"' \
              --json number \
              --jq 'length')
            test "$EXISTING" -eq 0 ||
              { echo "::notice::An unskip pull request is already open."; exit 0; }

            TITLE=$(jq -r '.pr_title' "$RESULT_PATH")
            BODY_FILE="$RUNNER_TEMP/unskip-closed-tests-pr-body.md"
            EXPECTED_FILES="$RUNNER_TEMP/unskip-closed-tests-expected-files.bin"
            EXPECTED_PATHS="$RUNNER_TEMP/unskip-closed-tests-expected-paths.bin"
            ACTUAL_PATHS="$RUNNER_TEMP/unskip-closed-tests-actual-paths.bin"
            EXPECTED_BLOBS="$RUNNER_TEMP/unskip-closed-tests-expected-blobs.txt"
            ACTUAL_BLOBS="$RUNNER_TEMP/unskip-closed-tests-actual-blobs.txt"
            jq -r '.pr_body' "$RESULT_PATH" > "$BODY_FILE"
            jq -j '.changed_files[] | .path, "\u0000", .content_sha256, "\u0000"' \
              "$RESULT_PATH" > "$EXPECTED_FILES"
            jq -j '.changed_files[].path, "\u0000"' "$RESULT_PATH" | sort -z > "$EXPECTED_PATHS"
            jq -r '[.changed_files[].content_sha256] | unique | sort | .[]' \
              "$RESULT_PATH" > "$EXPECTED_BLOBS"
            find "$PACKAGE_DIRECTORY/files" -mindepth 1 -maxdepth 1 -type f \
              -printf '%f\n' | sort > "$ACTUAL_BLOBS"
            cmp "$EXPECTED_BLOBS" "$ACTUAL_BLOBS"
            test -z "$(find "$PACKAGE_DIRECTORY/files" -mindepth 1 -not -type f -print -quit)"

            git diff --cached --quiet
            while IFS= read -r -d '' path && IFS= read -r -d '' expected_sha; do
                test -n "$path"
                test "${path#/}" = "$path"
                case "/$path/" in
                  *"/../"*|*"/./"*) exit 20 ;;
                esac
                case "$path" in
                  *.cs) ;;
                  *) echo "::error::Unexpected changed path: $path"; exit 20 ;;
                esac
                git ls-files --error-unmatch -- "$path" >/dev/null
                test -f "$path"
                test ! -L "$path"
                SOURCE_SHA=$(jq -er \
                  --arg path "$path" \
                  '[.candidates[] | select(.path == $path) | .source_sha256] |
                   unique | select(length == 1) | .[0]' \
                  "$ORIGINAL_MANIFEST")
                ACTUAL_SOURCE_SHA=$(sha256sum -- "$path")
                ACTUAL_SOURCE_SHA=${ACTUAL_SOURCE_SHA%% *}
                test "$ACTUAL_SOURCE_SHA" = "$SOURCE_SHA" ||
                  { echo "::error::Source content changed for $path"; exit 20; }
                BLOB="$PACKAGE_DIRECTORY/files/$expected_sha"
                test -f "$BLOB"
                test ! -L "$BLOB"
                ACTUAL_BLOB_SHA=$(sha256sum -- "$BLOB")
                ACTUAL_BLOB_SHA=${ACTUAL_BLOB_SHA%% *}
                test "$ACTUAL_BLOB_SHA" = "$expected_sha" ||
                  { echo "::error::Verified package content changed for $path"; exit 20; }
                cp -- "$BLOB" "$path"
                git add -- "$path"
            done < "$EXPECTED_FILES"

            git diff --quiet
            git diff --cached --name-only --no-renames -z | sort -z > "$ACTUAL_PATHS"
            cmp "$EXPECTED_PATHS" "$ACTUAL_PATHS"
            while IFS= read -r -d '' path && IFS= read -r -d '' expected_sha; do
                staged_sha=$(git show ":$path" | sha256sum)
                staged_sha=${staged_sha%% *}
                test "$staged_sha" = "$expected_sha" ||
                  { echo "::error::Staged content does not match verified content for $path"; exit 20; }
            done < "$EXPECTED_FILES"

            rm -rf .github/workflows/unskip-closed-tests-tool/bin \
              .github/workflows/unskip-closed-tests-tool/obj
            git config user.name "github-actions[bot]"
            git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
            git commit -m "Re-enable tests with resolved tracking items"

            BRANCH="automation/unskip-closed-tests-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}"
            git push origin "HEAD:refs/heads/$BRANCH"

            CURRENT_HEAD=$(gh api "repos/${EXPECTED_REPOSITORY}/commits/${DEFAULT_BRANCH}" --jq '.sha')
            if [ "$CURRENT_HEAD" != "$EXPECTED_COMMIT" ]; then
              git push origin --delete "$BRANCH"
              echo "::notice::Default branch advanced before PR creation; removed the unpublished branch."
              exit 0
            fi
            EXISTING=$(gh pr list \
              --repo "$EXPECTED_REPOSITORY" \
              --state open \
              --search 'in:title "[unskip-closed-tests]"' \
              --json number \
              --jq 'length')
            if [ "$EXISTING" -ne 0 ]; then
              git push origin --delete "$BRANCH"
              echo "::notice::Another unskip pull request opened; removed the duplicate branch."
              exit 0
            fi

            PR_URL=$(gh pr create \
              --repo "$EXPECTED_REPOSITORY" \
              --base "$DEFAULT_BRANCH" \
              --head "$BRANCH" \
              --draft \
              --title "$TITLE" \
              --body-file "$BODY_FILE")

            LIVE=$(gh pr view "$PR_URL" \
              --repo "$EXPECTED_REPOSITORY" \
              --json baseRefName,baseRefOid,body,isDraft,headRefName,number,state,url)
            test "$(printf '%s' "$LIVE" | jq -r '.isDraft')" = "true"
            test "$(printf '%s' "$LIVE" | jq -r '.headRefName')" = "$BRANCH"
            test "$(printf '%s' "$LIVE" | jq -r '.baseRefName')" = "$DEFAULT_BRANCH"
            printf '%s' "$LIVE" | jq -r '.body' | grep -F '<!-- unskip-closed-tests:v1;'
            if [ "$(printf '%s' "$LIVE" | jq -r '.baseRefOid')" != "$EXPECTED_COMMIT" ]; then
              PR_NUMBER=$(printf '%s' "$LIVE" | jq -r '.number')
              gh pr close "$PR_NUMBER" --repo "$EXPECTED_REPOSITORY"
              test "$(gh pr view "$PR_NUMBER" --repo "$EXPECTED_REPOSITORY" --json state --jq '.state')" = "CLOSED"
              git push origin --delete "$BRANCH"
              echo "::notice::Default branch advanced during PR creation; closed the draft and removed its branch."
              exit 0
            fi

---
