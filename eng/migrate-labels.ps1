<#
.SYNOPSIS
    Migrates the microsoft/testfx GitHub label taxonomy to the lowercase
    "prefix/name" convention (area/, type/, external/, state/, resolution/,
    needs/, priority/).

    NOTE: 'type/bug' and 'type/feature' are intentionally NOT part of the
    target taxonomy. Bug / feature / task categorization is owned by the
    native GitHub Issue Type field. The migration table deletes those legacy
    labels (and their pre-existing aliases like 'bug', 'enhancement',
    'feature-request'). See .github/copilot-instructions.md >
    "GitHub issue creation guidelines" for the policy.

.DESCRIPTION
    Migration is performed in phases that minimize the risk of losing
    issue/PR associations:

      Preflight   List labels that are neither in the target taxonomy nor
                  mapped in the migration table. Abort unless -Force.
      Phase 1     Atomic 1:1 renames using ``gh label edit --name``. This
                  preserves all associations natively and incidentally
                  creates the target label.
      Phase 2     Many-to-one merges. For each old label that needs to be
                  merged into an already-existing target, list every issue
                  and PR carrying the old label and re-label them. The
                  source label is only deleted after every relabel
                  succeeded AND the source label has zero remaining items.
      Phase 3     Create any target labels that no phase-1 rename created.
                  Update the color/description of any target label whose
                  attributes drifted.
      Phase 4     Pure deletions (legacy labels that have no replacement).
      Postflight  Verify no migrated source label still exists, and no
                  current label is outside the target taxonomy.

    Run with -DryRun to print actions without mutating anything.

.PARAMETER Repo
    GitHub repo in OWNER/NAME form. Default: microsoft/testfx.

.PARAMETER DryRun
    Print actions without calling the GitHub API.

.PARAMETER Force
    Continue even if preflight finds labels that are not covered by the
    migration map. (They will be left untouched.)

.EXAMPLE
    pwsh ./eng/migrate-labels.ps1 -DryRun
    pwsh ./eng/migrate-labels.ps1
#>

[CmdletBinding()]
param(
    [string] $Repo = 'microsoft/testfx',
    [switch] $DryRun,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Color palette (one source of truth per prefix)
# ---------------------------------------------------------------------------
$Color = @{
    Area        = '0052CC'   # blue
    Type        = '1D76DB'   # lighter blue
    External    = 'C5DEF5'   # pale blue
    State       = '0E8A16'   # green
    Resolution  = 'CCCCCC'   # gray
    Needs       = 'FBCA04'   # yellow
    PriorityP0  = 'B60205'   # red
    PriorityP1  = 'D93F0B'   # red-orange
    PriorityP2  = 'FBCA04'   # yellow
    PriorityP3  = '0E8A16'   # green
    Dependencies = '0366D6'
    AutoMerge   = '0E8A16'
    HelpWanted  = '008672'   # GitHub default "help wanted" green
    Breaking    = 'B60205'
    Flaky       = 'B60205'
    AI          = 'BFD4F2'
}

# ---------------------------------------------------------------------------
# Target taxonomy: name -> @{ Color; Description }
# ---------------------------------------------------------------------------
$Targets = [ordered]@{
    # area/*
    'area/agentic-workflows'   = @{ Color = $Color.Area; Description = 'GitHub agentic workflow definitions under .github/workflows/*.md.' }
    'area/analyzers'           = @{ Color = $Color.Area; Description = 'MSTest.Analyzers Roslyn analyzers and code fixes.' }
    'area/assertion'           = @{ Color = $Color.Area; Description = 'Assert / StringAssert / CollectionAssert APIs.' }
    'area/branding'            = @{ Color = $Color.Area; Description = 'Product branding, naming, icons.' }
    'area/deployment-item'     = @{ Color = $Color.Area; Description = '[DeploymentItem] support.' }
    'area/documentation'       = @{ Color = $Color.Area; Description = 'Repository or user-facing documentation.' }
    'area/dump'                = @{ Color = $Color.Area; Description = 'CrashDump / HangDump extensions.' }
    'area/fixtures'            = @{ Color = $Color.Area; Description = 'Test class / assembly initialization & cleanup.' }
    'area/infrastructure'      = @{ Color = $Color.Area; Description = 'Build, CI, repo infrastructure.' }
    'area/localization'        = @{ Color = $Color.Area; Description = 'Localized resources / xlf.' }
    'area/mstest'              = @{ Color = $Color.Area; Description = 'MSTest framework (not analyzers or assertions).' }
    'area/mstest-sdk'          = @{ Color = $Color.Area; Description = 'MSTest.Sdk MSBuild SDK.' }
    'area/mtp'                 = @{ Color = $Color.Area; Description = 'Microsoft.Testing.Platform core library.' }
    'area/mtp-extensions'      = @{ Color = $Color.Area; Description = 'MTP extensions (TrxReport, Retry, HtmlReport, ...).' }
    'area/mtp-migration'       = @{ Color = $Color.Area; Description = 'Tracks the MTP migration campaign (replaces mtp-migration-challenge).' }
    'area/mtp-msbuild'         = @{ Color = $Color.Area; Description = 'MTP MSBuild integration.' }
    'area/mtp-vstest-bridge'   = @{ Color = $Color.Area; Description = 'MTP <-> VSTest bridge.' }
    'area/native-aot'          = @{ Color = $Color.Area; Description = 'Native AOT compatibility.' }
    'area/parameterized-tests' = @{ Color = $Color.Area; Description = 'DataRow / DynamicData / parameterized tests.' }
    'area/performance'         = @{ Color = $Color.Area; Description = 'Runtime / build performance / efficiency.' }
    'area/server-mode-jsonrpc' = @{ Color = $Color.Area; Description = 'MTP server mode - JSON RPC transport.' }
    'area/server-mode-pipe'    = @{ Color = $Color.Area; Description = 'MTP server mode - named pipe transport.' }
    'area/terminal-reporter'   = @{ Color = $Color.Area; Description = 'Console / terminal test reporter.' }
    'area/test-framework'      = @{ Color = $Color.Area; Description = 'TestFramework public API and extensions.' }
    'area/timeout'             = @{ Color = $Color.Area; Description = '[Timeout] handling.' }
    'area/trx'                 = @{ Color = $Color.Area; Description = 'TRX report extension.' }
    'area/uwp'                 = @{ Color = $Color.Area; Description = 'Universal Windows Platform support.' }
    'area/winui'               = @{ Color = $Color.Area; Description = 'WinUI support.' }

    # type/*
    # NOTE: 'type/bug' and 'type/feature' are intentionally absent. They have
    # been replaced by the native GitHub Issue Type field (Bug / Feature /
    # Task) on the repo. The migration map below routes the legacy 'bug',
    # 'enhancement' and 'feature-request' labels (and the now-deprecated
    # 'type/bug' / 'type/feature' labels themselves) to deletion. See
    # .github/copilot-instructions.md > "GitHub issue creation guidelines".
    'type/announcement'        = @{ Color = $Color.Type;     Description = 'Announcement to the community.' }
    'type/automation'          = @{ Color = $Color.Type;     Description = 'Created or maintained by an agentic workflow.' }
    'type/ai-inspected'        = @{ Color = $Color.AI;       Description = 'Reviewed or generated with AI assistance.' }
    'type/breaking-change'     = @{ Color = $Color.Breaking; Description = 'Behavioral or API breaking change.' }
    'type/discussion'          = @{ Color = $Color.Type;     Description = 'Open discussion / brainstorming.' }
    'type/flaky-test'          = @{ Color = $Color.Flaky;    Description = 'Tracks flaky tests in CI.' }
    'type/partner-request'     = @{ Color = $Color.Type;     Description = 'Request from a partner team.' }
    'type/pr-fix'              = @{ Color = $Color.Type;     Description = 'Created by the pr-fix agentic workflow.' }
    'type/qa'                  = @{ Color = $Color.Type;     Description = 'Created by the adhoc-qa agentic workflow.' }
    'type/question'            = @{ Color = $Color.Type;     Description = 'Question or request for clarification.' }
    'type/regression'          = @{ Color = $Color.Breaking; Description = 'Regression from a previous release.' }
    'type/rfc'                 = @{ Color = $Color.Type;     Description = 'Request for comments.' }
    'type/tech-debt'           = @{ Color = $Color.Type;     Description = 'Code health, refactoring, simplification.' }
    'type/test-gap'            = @{ Color = $Color.Type;     Description = 'Missing or insufficient tests.' }

    # external/*
    'external/azdo'            = @{ Color = $Color.External; Description = 'Blocked on Azure DevOps.' }
    'external/code-coverage'   = @{ Color = $Color.External; Description = 'Blocked on the code coverage tooling.' }
    'external/dotnet-sdk'      = @{ Color = $Color.External; Description = 'Blocked on the .NET SDK.' }
    'external/dotnet-test'     = @{ Color = $Color.External; Description = 'Blocked on `dotnet test` integration.' }
    'external/fakes'           = @{ Color = $Color.External; Description = 'Blocked on Microsoft Fakes.' }
    'external/nuget'           = @{ Color = $Color.External; Description = 'Blocked on NuGet.' }
    'external/nunit'           = @{ Color = $Color.External; Description = 'Blocked on NUnit.' }
    'external/other'           = @{ Color = $Color.External; Description = 'Caused by an external issue that needs to be solved first.' }
    'external/test-explorer'   = @{ Color = $Color.External; Description = 'Blocked on the VS Test Explorer.' }
    'external/tunit'           = @{ Color = $Color.External; Description = 'Blocked on TUnit.' }
    'external/vstest'          = @{ Color = $Color.External; Description = 'Blocked on VSTest.' }
    'external/xunit'           = @{ Color = $Color.External; Description = 'Blocked on xUnit.' }

    # state/*
    'state/approved'           = @{ Color = $Color.State; Description = 'Proposal approved; ready for implementation.' }
    'state/blocked'            = @{ Color = $Color.State; Description = 'Cannot progress until a blocker is resolved.' }
    'state/cannot-reproduce'   = @{ Color = $Color.State; Description = 'Maintainers cannot reproduce the issue.' }
    'state/in-pr'              = @{ Color = $Color.State; Description = 'A PR is open for this issue.' }
    'state/needs-approval'     = @{ Color = $Color.State; Description = 'Awaiting maintainer approval to proceed.' }
    'state/stale'              = @{ Color = $Color.State; Description = 'No recent activity.' }

    # resolution/*
    'resolution/by-design'     = @{ Color = $Color.Resolution; Description = 'Closed: behavior is by design.' }
    'resolution/duplicate'     = @{ Color = $Color.Resolution; Description = 'Closed: duplicate of another issue.' }
    'resolution/fixed'         = @{ Color = $Color.Resolution; Description = 'Closed: fix has been merged.' }
    'resolution/pending-release' = @{ Color = $Color.Resolution; Description = 'Fixed; awaiting next release.' }
    'resolution/wont-fix'      = @{ Color = $Color.Resolution; Description = 'Closed: will not be fixed.' }

    # needs/*
    'needs/attention'          = @{ Color = $Color.Needs; Description = 'Needs maintainer attention.' }
    'needs/author-feedback'    = @{ Color = $Color.Needs; Description = 'Waiting on the original author.' }
    'needs/design'             = @{ Color = $Color.Needs; Description = 'Needs design / proposal before implementation.' }
    'needs/info'               = @{ Color = $Color.Needs; Description = 'Needs more information from the reporter.' }
    'needs/triage'             = @{ Color = $Color.Needs; Description = 'Needs triage by a maintainer.' }

    # priority/*
    'priority/0'               = @{ Color = $Color.PriorityP0; Description = 'Critical priority.' }
    'priority/1'               = @{ Color = $Color.PriorityP1; Description = 'High priority.' }
    'priority/2'               = @{ Color = $Color.PriorityP2; Description = 'Medium priority.' }
    'priority/3'               = @{ Color = $Color.PriorityP3; Description = 'Low priority.' }

    # standalone (kept as-is - GitHub UI surfaces have implicit hooks)
    'dependencies'             = @{ Color = $Color.Dependencies; Description = 'Updates to dependency manifests.' }
    'auto-merge'               = @{ Color = $Color.AutoMerge;    Description = 'PR is enabled for auto-merge.' }
    'help wanted'              = @{ Color = $Color.HelpWanted;   Description = 'Up for grabs; can be claimed by commenting.' }

    # explicitly kept (not in the new taxonomy but still in use)
    'sprint'                   = @{ Color = 'BFD4F2'; Description = 'Tracks sprint work items.' }
    'cla-already-signed'       = @{ Color = '009800'; Description = '(Managed by the CLA bot.)' }
    'cla-not-required'         = @{ Color = '009800'; Description = '(Managed by the CLA bot.)' }
    'cla-required'             = @{ Color = 'E11D21'; Description = '(Managed by the CLA bot.)' }
    'cla-signed'               = @{ Color = '207DE5'; Description = '(Managed by the CLA bot.)' }
}

# ---------------------------------------------------------------------------
# Old label -> new label mapping
#   - String value: rename / merge into that target
#   - $null:        delete the old label (no replacement)
# All keys must refer to labels that currently exist in the repo (or are
# silently skipped). Keys with non-null values must point at a key in $Targets.
# ---------------------------------------------------------------------------
$Migration = [ordered]@{
    # Dependabot grouping
    '.NET'                          = 'dependencies'
    'dotnet_sdk_package_manager'    = 'dependencies'
    'javascript'                    = 'dependencies'

    # Area
    'agentic-workflows'             = 'area/agentic-workflows'
    'Area: Analyzers'               = 'area/analyzers'
    'Area: Assertion'               = 'area/assertion'
    'Area: Branding'                = 'area/branding'
    'Area: DeploymentItem'          = 'area/deployment-item'
    'Area: Documentation'           = 'area/documentation'
    'documentation'                 = 'area/documentation'
    'Area: Dump'                    = 'area/dump'
    'Area: Fixtures'                = 'area/fixtures'
    'Area: Infrastructure'          = 'area/infrastructure'
    'Area: Localization'            = 'area/localization'
    'Area: MSTest'                  = 'area/mstest'
    'Area: MSTest.SDK'              = 'area/mstest-sdk'
    'Area: MTP'                     = 'area/mtp'
    'Area: MTP Extensions'          = 'area/mtp-extensions'
    'mtp-migration-challenge'       = 'area/mtp-migration'
    'Area: MTP MSBuild'             = 'area/mtp-msbuild'
    'msbuild'                       = 'area/mtp-msbuild'
    'Area: MTP VSTest Bridge'       = 'area/mtp-vstest-bridge'
    'Area: Native AOT'              = 'area/native-aot'
    'Area: Parameterized tests'     = 'area/parameterized-tests'
    'Area: Performance'             = 'area/performance'
    'performance'                   = 'area/performance'
    'efficiency'                    = 'area/performance'
    'green-software'                = 'area/performance'
    'Area: Server Mode - JSON RPC'  = 'area/server-mode-jsonrpc'
    'Area: Server Mode - Pipe'      = 'area/server-mode-pipe'
    'Area: Terminal reporter'       = 'area/terminal-reporter'
    'Area: TestFramework'           = 'area/test-framework'
    'Area: Timeout'                 = 'area/timeout'
    'Area: TRX'                     = 'area/trx'
    'Area: UWP'                     = 'area/uwp'
    'Area: WinUI'                   = 'area/winui'

    # Type
    # Bug / feature categorization now uses the native GitHub Issue Type field
    # (Bug, Feature, Task) on the repo, so the corresponding legacy labels are
    # deleted rather than remapped. See .github/copilot-instructions.md >
    # "GitHub issue creation guidelines".
    'Announcement'                  = 'type/announcement'
    'automated'                     = 'type/automation'
    'automated-analysis'            = 'type/automation'
    'automation'                    = 'type/automation'
    'ai-inspected'                  = 'type/ai-inspected'
    'Breaking :bangbang:'           = 'type/breaking-change'
    'bug'                           = $null
    'type/bug'                      = $null
    'Discussion'                    = 'type/discussion'
    'enhancement'                   = $null
    'feature-request'               = $null
    'type/feature'                  = $null
    'Flaky-Test'                    = 'type/flaky-test'
    'Partner request'               = 'type/partner-request'
    'pr-fix'                        = 'type/pr-fix'
    'qa'                            = 'type/qa'
    'Question'                      = 'type/question'
    'Regression'                    = 'type/regression'
    'RFC'                           = 'type/rfc'
    'Type: RFC'                     = 'type/rfc'
    'test'                          = 'type/test-gap'
    'Test Gap'                      = 'type/test-gap'
    'testing'                       = 'type/test-gap'
    'code-health'                   = 'type/tech-debt'
    'codehealth'                    = 'type/tech-debt'
    'code-quality'                  = 'type/tech-debt'
    'maintainability'               = 'type/tech-debt'
    'quality'                       = 'type/tech-debt'
    'refactoring'                   = 'type/tech-debt'

    # External
    'External: .NET SDK'            = 'external/dotnet-sdk'
    'External: AzDO'                = 'external/azdo'
    'External: Code Coverage'       = 'external/code-coverage'
    'External: dotnet test'         = 'external/dotnet-test'
    'External: Fakes'               = 'external/fakes'
    'External: NuGet'               = 'external/nuget'
    'External: NUnit'               = 'external/nunit'
    'External: Other'               = 'external/other'
    'External: Test Explorer'       = 'external/test-explorer'
    'External: TUnit'               = 'external/tunit'
    'External: VSTest'              = 'external/vstest'
    'External: xUnit'               = 'external/xunit'

    # State
    'In-PR'                         = 'state/in-pr'
    'State: Approved'               = 'state/approved'
    'State: Blocked'                = 'state/blocked'
    "State: Can't Reproduce"        = 'state/cannot-reproduce'
    'State: In-PR'                  = 'state/in-pr'
    'State: Needs Approval'         = 'state/needs-approval'
    'State: No Recent Activity'     = 'state/stale'

    # Resolution
    'Resolution: By Design'         = 'resolution/by-design'
    'Resolution: Duplicate'         = 'resolution/duplicate'
    'Resolution: Fixed'             = 'resolution/fixed'
    'Resolution: Pending release'   = 'resolution/pending-release'
    "State: Won't Fix"              = 'resolution/wont-fix'

    # Needs
    'need-triage'                   = 'needs/triage'
    'Needs: Additional Info'        = 'needs/info'
    'Needs: Attention :wave:'       = 'needs/attention'
    'Needs: Author Feedback'        = 'needs/author-feedback'
    'Needs: Design'                 = 'needs/design'
    'Needs: Triage'                 = 'needs/triage'
    'Needs: Triage :mag:'           = 'needs/triage'

    # Priority
    'pri:1'                         = 'priority/1'

    # Standalone normalization - canonicalize on the GH default "help wanted"
    'Help-Wanted'                   = 'help wanted'

    # Merge `glossary` (used only by the now-replaced glossary-maintainer workflow)
    # into the broader docs area, preserving discoverability.
    'glossary'                      = 'area/documentation'

    # Pure deletions (legacy / niche / superseded with very few items)
    'Area: Expecto'                 = $null
    'build'                         = $null
    'cross-platform'                = $null
}

# ---------------------------------------------------------------------------
# Labels that should be replaced by the repo's native GitHub Issue Type field
# (Bug / Feature / Task) on every issue that carries them, before the label
# itself is deleted in Phase 4. Pull requests do not have an Issue Type, so
# the label is just removed for PRs without any other action.
#
# See .github/copilot-instructions.md > "GitHub issue creation guidelines".
# ---------------------------------------------------------------------------
$IssueTypeReplacement = [ordered]@{
    'bug'             = 'Bug'
    'type/bug'        = 'Bug'
    'enhancement'     = 'Feature'
    'feature-request' = 'Feature'
    'type/feature'    = 'Feature'
}

# Labels to keep untouched: not in the new taxonomy but kept on purpose.
# (Listing them explicitly so the preflight check doesn't flag them.)
$Keep = @(
    'daily-status'
    'lean-squad'
    'ported-from-ta-repo'
    'report'
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Invoke-Gh {
    param([Parameter(ValueFromRemainingArguments)] [string[]] $Args)
    if ($DryRun) {
        Write-Host "    [dry-run] gh $($Args -join ' ')" -ForegroundColor DarkGray
        return $null
    }
    & gh @Args
    if ($LASTEXITCODE -ne 0) {
        throw "gh command failed (exit $LASTEXITCODE): gh $($Args -join ' ')"
    }
}

function Get-AllLabels {
    $json = & gh label list --repo $Repo --limit 500 --json name,color,description
    if ($LASTEXITCODE -ne 0) { throw "Failed to list labels on $Repo (exit $LASTEXITCODE)" }
    return ($json | ConvertFrom-Json)
}

function Get-LabelByName {
    param($Labels, [string] $Name)
    return ($Labels | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
}

# Lists every issue and PR (state=all) with the given label and returns
# their numbers. Throws if either gh invocation fails.
function Get-ItemsWithLabel {
    param([string] $Label)

    $issueJson = & gh issue list --repo $Repo --label $Label --state all --limit 5000 --json number
    if ($LASTEXITCODE -ne 0) { throw "Failed to list issues with label '$Label'" }

    $prJson = & gh pr list --repo $Repo --label $Label --state all --limit 5000 --json number
    if ($LASTEXITCODE -ne 0) { throw "Failed to list PRs with label '$Label'" }

    $issues = @($issueJson | ConvertFrom-Json | ForEach-Object { $_.number })
    $prs    = @($prJson    | ConvertFrom-Json | ForEach-Object { $_.number })
    return @($issues + $prs | Sort-Object -Unique)
}

function Merge-LabelInto {
    param(
        [string] $From,
        [string] $To
    )

    $items = Get-ItemsWithLabel -Label $From
    $count = $items.Count
    Write-Host "    found $count item(s) with '$From'" -ForegroundColor DarkGray
    if ($count -ge 5000) {
        throw "Label '$From' has >=5000 items - pagination support required, refusing to proceed."
    }

    $failures = @()
    foreach ($n in $items) {
        try {
            Invoke-Gh issue edit $n --repo $Repo --add-label $To --remove-label $From | Out-Null
        }
        catch {
            $failures += [pscustomobject]@{ Number = $n; Error = $_.Exception.Message }
            Write-Host "    ! failed to relabel #${n}: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    if ($failures.Count -gt 0) {
        throw "$($failures.Count) item(s) failed to migrate from '$From' to '$To'; refusing to delete '$From'."
    }

    if (-not $DryRun) {
        # GitHub's label-based issue/PR search is index-backed and may lag a few
        # seconds after edits. Retry a few times before giving up.
        $remaining = @()
        $maxAttempts = 6
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $remaining = Get-ItemsWithLabel -Label $From
            if ($remaining.Count -eq 0) { break }
            if ($attempt -lt $maxAttempts) {
                Start-Sleep -Seconds (2 * $attempt)
            }
        }
        if ($remaining.Count -gt 0) {
            throw "Label '$From' still has $($remaining.Count) item(s) after relabel + $maxAttempts retries; refusing to delete."
        }
    }

    Invoke-Gh label delete $From --repo $Repo --yes | Out-Null
}

# ---------------------------------------------------------------------------
# Issue-type cache (one query per repo). Maps type name (e.g. 'Bug') to its
# global node ID for the GraphQL updateIssueIssueType mutation.
# ---------------------------------------------------------------------------
$script:IssueTypeIdCache = $null

function Get-IssueTypeId {
    param([string] $Name)

    if ($null -eq $script:IssueTypeIdCache) {
        $owner, $name = $Repo -split '/', 2
        $query = 'query($owner:String!,$name:String!){ repository(owner:$owner,name:$name){ issueTypes(first:50){ nodes{ id name } } } }'
        $json = & gh api graphql -f query=$query -F owner=$owner -F name=$name
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to fetch issue types for $Repo (exit $LASTEXITCODE)"
        }
        $data = $json | ConvertFrom-Json
        $script:IssueTypeIdCache = @{}
        foreach ($t in $data.data.repository.issueTypes.nodes) {
            $script:IssueTypeIdCache[$t.name] = $t.id
        }
    }

    if (-not $script:IssueTypeIdCache.ContainsKey($Name)) {
        throw "Issue type '$Name' is not configured on $Repo. Available: $($script:IssueTypeIdCache.Keys -join ', ')"
    }
    return $script:IssueTypeIdCache[$Name]
}

# Returns issue node IDs and current issueType name for every (issue) item with
# the given label. PRs are excluded because they do not have an Issue Type.
function Get-IssuesWithLabel {
    param([string] $Label)

    $owner, $name = $Repo -split '/', 2
    $query = @'
query($owner:String!,$name:String!,$label:String!,$cursor:String){
  repository(owner:$owner,name:$name){
    issues(first:100,filterBy:{labels:[$label],states:[OPEN,CLOSED]},after:$cursor){
      pageInfo{ hasNextPage endCursor }
      nodes{ id number issueType{ name } }
    }
  }
}
'@

    $results = @()
    $cursor = $null
    do {
        if ($cursor) {
            $json = & gh api graphql -f query=$query -F owner=$owner -F name=$name -F label=$Label -F cursor=$cursor
        } else {
            $json = & gh api graphql -f query=$query -F owner=$owner -F name=$name -F label=$Label
        }
        if ($LASTEXITCODE -ne 0) { throw "Failed to query issues with label '$Label' (exit $LASTEXITCODE)" }
        $page = ($json | ConvertFrom-Json).data.repository.issues
        foreach ($n in $page.nodes) { $results += $n }
        $cursor = if ($page.pageInfo.hasNextPage) { $page.pageInfo.endCursor } else { $null }
    } while ($cursor)

    return $results
}

function Set-IssueType {
    param(
        [string] $IssueNodeId,
        [string] $IssueTypeName
    )

    if ($DryRun) {
        Write-Host "    [dry-run] would set issue type '$IssueTypeName' on $IssueNodeId" -ForegroundColor DarkGray
        return
    }

    $typeId = Get-IssueTypeId -Name $IssueTypeName
    $mutation = 'mutation($issue:ID!,$type:ID!){ updateIssueIssueType(input:{issueId:$issue,issueTypeId:$type}){ issue{ number } } }'
    & gh api graphql -f query=$mutation -F issue=$IssueNodeId -F type=$typeId | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set issue type '$IssueTypeName' on $IssueNodeId (exit $LASTEXITCODE)"
    }
}

# Sets the native GitHub Issue Type field on every open/closed issue carrying
# the given $Label (only when the issue does not already have an explicit
# type), then removes the label from every issue and PR. Used to migrate the
# legacy 'type/bug', 'type/feature', 'bug', 'enhancement' and
# 'feature-request' labels into the native Issue Type field.
function Convert-LabelToIssueType {
    param(
        [string] $Label,
        [string] $IssueTypeName
    )

    $issues = Get-IssuesWithLabel -Label $Label
    Write-Host "    found $($issues.Count) issue(s) with '$Label' (will set issue type '$IssueTypeName' where unset)" -ForegroundColor DarkGray

    foreach ($issue in $issues) {
        if ($issue.issueType -and $issue.issueType.name) {
            Write-Host "      = #$($issue.number) already has issue type '$($issue.issueType.name)' - keeping it" -ForegroundColor DarkGray
        } else {
            Write-Host "      + #$($issue.number) -> issue type '$IssueTypeName'" -ForegroundColor Green
            Set-IssueType -IssueNodeId $issue.id -IssueTypeName $IssueTypeName
        }
        Invoke-Gh issue edit $issue.number --repo $Repo --remove-label $Label | Out-Null
    }

    # PRs also carry these labels (no Issue Type on PRs - just strip the label).
    $prJson = & gh pr list --repo $Repo --label $Label --state all --limit 5000 --json number
    if ($LASTEXITCODE -ne 0) { throw "Failed to list PRs with label '$Label'" }
    $prs = @($prJson | ConvertFrom-Json | ForEach-Object { $_.number })
    if ($prs.Count -gt 0) {
        Write-Host "    found $($prs.Count) PR(s) with '$Label' - stripping label (no issue type on PRs)" -ForegroundColor DarkGray
        foreach ($n in $prs) {
            Invoke-Gh pr edit $n --repo $Repo --remove-label $Label | Out-Null
        }
    }
}

function Ensure-Target {
    param(
        [string] $Name,
        [Parameter()] $Existing
    )
    $spec = $Targets[$Name]
    if (-not $spec) { throw "No target spec for '$Name'" }
    $current = Get-LabelByName -Labels $Existing -Name $Name
    if ($current) {
        $colorDrift = $current.color.ToLowerInvariant() -ne $spec.Color.ToLowerInvariant()
        $descDrift  = ($current.description ?? '') -ne $spec.Description
        if ($colorDrift -or $descDrift) {
            Write-Host "  ~ updating '$Name' (color/description)" -ForegroundColor Yellow
            Invoke-Gh label edit $Name --repo $Repo --color $spec.Color --description $spec.Description | Out-Null
        }
    } else {
        Write-Host "  + creating '$Name'" -ForegroundColor Green
        Invoke-Gh label create $Name --repo $Repo --color $spec.Color --description $spec.Description | Out-Null
    }
}

# ---------------------------------------------------------------------------
# Preflight: list labels not covered by the migration map or target taxonomy
# ---------------------------------------------------------------------------
Write-Host "=== Preflight ===" -ForegroundColor Cyan
$existing = Get-AllLabels
$existingNames = @($existing | ForEach-Object { $_.name })

# Validate the script's mapping integrity.
foreach ($old in @($Migration.Keys)) {
    $new = $Migration[$old]
    if ($null -ne $new -and -not $Targets.Contains($new)) {
        throw "Migration target '$new' (for '$old') is not defined in `$Targets."
    }
}

$uncovered = @()
foreach ($name in $existingNames) {
    if ($Targets.Contains($name)) { continue }
    if ($Migration.Contains($name)) { continue }
    if ($Keep -contains $name) { continue }
    $uncovered += $name
}
if ($uncovered.Count -gt 0) {
    Write-Host ("  ! {0} existing label(s) are not covered by the migration:" -f $uncovered.Count) -ForegroundColor Yellow
    foreach ($n in ($uncovered | Sort-Object)) { Write-Host "      - $n" -ForegroundColor Yellow }
    if (-not $Force) {
        throw "Aborting: re-run with -Force to ignore these uncovered labels."
    }
    Write-Host "  (continuing because -Force was specified)" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Phase 1: atomic 1:1 renames (preserves all associations via gh)
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Phase 1: atomic 1:1 renames ===" -ForegroundColor Cyan
foreach ($old in @($Migration.Keys)) {
    $new = $Migration[$old]
    if ($null -eq $new) { continue }
    $oldLabel = Get-LabelByName -Labels $existing -Name $old
    if (-not $oldLabel) { continue }
    $newLabel = Get-LabelByName -Labels $existing -Name $new
    if ($newLabel) { continue }  # target already exists -> merge in phase 2

    $spec = $Targets[$new]
    Write-Host "  > renaming '$old' -> '$new'" -ForegroundColor Magenta
    Invoke-Gh label edit $old --repo $Repo --name $new --color $spec.Color --description $spec.Description | Out-Null

    # Reflect the rename in the in-memory cache so subsequent iterations
    # see the new label as existing (this matters when several old labels
    # all merge into the same target).
    if (-not $DryRun) {
        $oldLabel.name = $new
        $oldLabel.color = $spec.Color
        $oldLabel.description = $spec.Description
    } else {
        # Dry-run simulation: pretend the rename happened.
        $existing += [pscustomobject]@{ name = $new; color = $spec.Color; description = $spec.Description }
        $existing = @($existing | Where-Object { $_.name -ne $old })
    }
}

# ---------------------------------------------------------------------------
# Phase 2: many-to-one merges
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Phase 2: many-to-one merges ===" -ForegroundColor Cyan
if (-not $DryRun) { $existing = Get-AllLabels }
foreach ($old in @($Migration.Keys)) {
    $new = $Migration[$old]
    if ($null -eq $new) { continue }
    $oldLabel = Get-LabelByName -Labels $existing -Name $old
    if (-not $oldLabel) { continue }
    $newLabel = Get-LabelByName -Labels $existing -Name $new
    if (-not $newLabel) { continue }  # shouldn't happen after phase 1

    Write-Host "  m merging '$old' -> '$new'" -ForegroundColor Magenta
    if ($DryRun) {
        Write-Host "    [dry-run] would relabel every issue/PR with '$old' and delete '$old'" -ForegroundColor DarkGray
        $existing = @($existing | Where-Object { $_.name -ne $old })
        continue
    }

    # Make sure the target has the canonical color/description first.
    Ensure-Target -Name $new -Existing $existing
    Merge-LabelInto -From $old -To $new
    $existing = Get-AllLabels
}

# ---------------------------------------------------------------------------
# Phase 3: ensure remaining target labels exist (color/description)
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Phase 3: ensure target taxonomy ===" -ForegroundColor Cyan
if (-not $DryRun) { $existing = Get-AllLabels }
foreach ($name in @($Targets.Keys)) {
    Ensure-Target -Name $name -Existing $existing
}

# ---------------------------------------------------------------------------
# Phase 3.5: convert deprecated bug/feature labels to native Issue Type field
#
# Issues carrying 'type/bug', 'type/feature', 'bug', 'enhancement' or
# 'feature-request' get their native GitHub Issue Type set to Bug or Feature
# (only if no explicit type is already set) and the label is then removed.
# PRs lose the label without any Issue Type change (PRs do not have one).
# Once this phase succeeds, Phase 4 deletes the now-empty labels.
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Phase 3.5: convert deprecated labels to native Issue Types ===" -ForegroundColor Cyan
if (-not $DryRun) { $existing = Get-AllLabels }
foreach ($label in @($IssueTypeReplacement.Keys)) {
    $typeName = $IssueTypeReplacement[$label]
    $labelObj = Get-LabelByName -Labels $existing -Name $label
    if (-not $labelObj) {
        Write-Host "  = '$label' not present - skipping" -ForegroundColor DarkGray
        continue
    }
    Write-Host "  c converting '$label' -> issue type '$typeName'" -ForegroundColor Magenta
    Convert-LabelToIssueType -Label $label -IssueTypeName $typeName
}

# ---------------------------------------------------------------------------
# Phase 4: pure deletions
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Phase 4: pure deletions ===" -ForegroundColor Cyan
if (-not $DryRun) { $existing = Get-AllLabels }
foreach ($old in @($Migration.Keys)) {
    $new = $Migration[$old]
    if ($null -ne $new) { continue }
    $oldLabel = Get-LabelByName -Labels $existing -Name $old
    if (-not $oldLabel) { continue }

    $items = if ($DryRun) { @() } else { Get-ItemsWithLabel -Label $old }
    Write-Host "  - deleting '$old' ($($items.Count) item(s) will lose this label)" -ForegroundColor Red
    Invoke-Gh label delete $old --repo $Repo --yes | Out-Null
}

# ---------------------------------------------------------------------------
# Postflight audit
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Postflight audit ===" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "  (skipped in dry-run)" -ForegroundColor DarkGray
    return
}
$final = Get-AllLabels
$finalNames = @($final | ForEach-Object { $_.name })

$leftover = @($Migration.Keys | Where-Object { $finalNames -contains $_ })
if ($leftover.Count -gt 0) {
    Write-Host "  ! migration source labels still present:" -ForegroundColor Red
    foreach ($n in $leftover) { Write-Host "      - $n" -ForegroundColor Red }
} else {
    Write-Host "  OK no migration source labels remain" -ForegroundColor Green
}

$missing = @($Targets.Keys | Where-Object { $finalNames -notcontains $_ })
if ($missing.Count -gt 0) {
    Write-Host "  ! target labels missing from repo:" -ForegroundColor Red
    foreach ($n in $missing) { Write-Host "      - $n" -ForegroundColor Red }
} else {
    Write-Host "  OK every target label exists" -ForegroundColor Green
}

$orphans = @($finalNames | Where-Object { -not $Targets.Contains($_) })
if ($orphans.Count -gt 0) {
    Write-Host "  i labels outside the target taxonomy (left alone):" -ForegroundColor Yellow
    foreach ($n in ($orphans | Sort-Object)) {
        $tag = if ($Keep -contains $n) { ' (preserved)' } else { '' }
        Write-Host "      - $n$tag" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
