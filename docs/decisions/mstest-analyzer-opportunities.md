# MSTest analyzer opportunities: research and decisions

## Problem and contract

The requested analyzer improvements cover fixture synchronization attributes,
resource-lock modes, derived test attributes, open generic test classes,
combinatorial data, async-void exception assertions, and invalid attribute
constants. The separately identified `Assert.Scope()` disposal opportunity is
excluded because it overlaps CA2000.

The compiler or IDE loads `MSTest.Analyzers` alongside a consumer's referenced
`MSTest.TestFramework`. These diagnostics must describe behavior of that
framework, honor existing severity/configuration and work when newer framework
symbols are absent. They do not execute tests, change framework runtime APIs,
or require a process handshake. Do not offer a code fix when a safe equivalent
cannot be established. In particular, do not move fixture locks: their
lifetime can change.

The initial source is commit `8599eaa8f335b040bfab8d2eae3fe8e079af6964`,
which already contains the MSTEST0007 fixture-attribute extension. The
analyzer producer targets `netstandard2.0`; its unit-test consumer targets
`net472`, `net10.0`, and `net11.0`. The pinned SDK is
`12.0.100-alpha.1.26570.125`. Packed-consumer validation must identify the
actual `MSTest.Analyzers` and `MSTest.TestFramework` package versions and the
resolved analyzer binary, not just a local feed.

## Research and experiments

### 2026-11-25: runtime contracts and existing diagnostics

- `TypeEnumerator` and `ResourceLockInfo` merge matching resource keys using
  ordinal comparison; `ReadWrite` wins and unknown modes are normalized to
  exclusive. A method-level lock on a fixture is ignored. A read lock alone
  cannot protect a mutation.
- Runtime discovery accepts attributes derived from `TestClassAttribute` and
  `TestMethodAttribute`; concrete open generic test classes cannot be
  instantiated. Abstract generic base classes and closed derived classes are
  valid.
- `CombinatorialValuesUtilities` accepts custom
  `ICombinatorialValuesProvider` implementations, infers only specific types,
  and rejects multiple providers. A trailing unprovided `CancellationToken`
  is not a data parameter. Range and random attributes enforce additional
  constant-value constraints.
- A synchronous exception assertion cannot observe an exception raised after
  an async-void delegate has yielded. MSTEST0064 already identifies a
  different blocking pattern; its existing fix is not necessarily safe for
  explicitly cast async delegates.
- `TimeoutAttribute`, `RetryAttribute`, and `ExpectedDurationAttribute` enforce
  value constraints at different points in discovery or execution. Only
  provably invalid, relevant values should be diagnosed.
- A pre-implementation analyzer selection ran 90 cases: 70 passed and 20
  failed on missing resource-lock diagnostics, not compilation or discovery.
  The same tests must pass after the correction. This initial run was not an
  isolated counterfactual and does not by itself establish the required paired
  evidence.
- The lock-mode correction retains the existing ordinal key and inheritance
  walks, but continues past shared-read declarations looking for an exclusive
  declaration. On the changed source, the same net10.0 selection passed all
  90 cases, including the read-lock code-fix and no-fix checks. The command
  used `.\.dotnet\dotnet.exe test` with `--no-restore`, the three
  `UndeclaredProcessGlobalStateMutation`, `CurrentDirectoryMutation`, and
  `CultureMutation` analyzer test filters, and a binary log; exit was 0.
  The pre-change run is useful defect evidence; the isolated comparison
  below provides the paired evidence.
- Derived test attributes and open generic classes are now analyzed by
  MSTEST0002/0003. MSTEST0064 also reports inline async-void delegates with a
  separate message and no automatic fix. An initial run exposed that the
  verifier's unqualified markup assumes a single descriptor; the verifier now
  selects the first descriptor for these two existing analyzer suites while
  the new cases assert their specific messages. The resulting net10.0
  selection passed 82 cases with no skips, exit 0.
- MSTEST0092 checks only combinatorial test methods and recognizes custom
  providers by interface rather than attribute name. Invalid built-in range
  arguments are checked using widened arithmetic. Random-data bounds are
  compared only when explicitly supplied, because defaults belong to the
  referenced framework version. Unknown parameter types are not guessed.
- MSTEST0093 checks positive timeout, retry-count, and expected-duration
  arguments at applicable test and fixture sites. Retry delays less than -1
  cause `Task.Delay` to throw when a retry occurs; -1 is accepted by
  `Task.Delay`, even though it waits indefinitely. Undefined backoff enum
  values are checked against the referenced enum members. Attribute types
  unavailable in older frameworks are skipped.

### 2026-11-25: first review round

Review identified an unsafe MSTEST0003 fix for inferable generic test methods,
an inherited scheduling attribute on an overridable fixture, misleading
fixture guidance for global fixtures, and async-void method groups passed to
synchronous exception assertions. Preserve inferable generic parameters; only
remove uninferable parameters when they are unused and have no constraints.
Suppress fixture scheduling diagnostics on overridable methods because an
override can inherit the attribute as a test method. Replace the class-specific
guidance with coordination of all affected tests or explicit synchronization.
Recognize nonvirtual async-void method groups without assuming a virtual
override has the same behavior.

The six focused cases were copied unchanged to the task-owned comparison
checkout before these production changes. Running the `net10.0` filtered test
command with the pre-fix source exited 2: six executed, six failed, none
skipped. Failures were the expected diagnostic/message or code-fix mismatches,
not build failures. The post-fix `net10.0` run passed 111/111 cases across the
three affected analyzer suites; the same six-case selection subsequently passed
6/6 with `--no-build`, exit 0.
Both runs used the pinned SDK, `--no-restore`, unique binary logs, and TRX
reports. The command shape was:

```powershell
.\.dotnet\dotnet.exe test --project .\test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj --framework net10.0 --configuration Debug --no-restore "-bl:$out\test-{}.binlog" --filter $filter --report-trx --report-trx-filename $report --results-directory $out --minimum-expected-tests 1 --no-ansi --no-progress
```

The red filter selected `DerivedStaticGenericTest`, `UninferableGenericTest_Referenced`,
`OverridableFixtureSchedulingAttributes`, `WhenFixtureMethodIsMarkedWithResourceLock_Message`,
and `AsyncVoidMethodGroupPassed` by fully qualified name. The green filter
selected `TestMethodShouldBeValidAnalyzerTests`, `UseAttributeOnTestMethodAnalyzerTests`,
and `PreferAsyncAssertionAnalyzerTests`. The final six-case run used the red
filter with `--no-build` instead of `--no-restore`. Regenerating XLF with the
analyzer project's `UpdateXlf` target exited 0; the localization guard's 41
self-tests and all 364 catalogs passed.

### 2026-11-25: second review round

Removing an unused generic parameter can still break callers that explicitly
specify it, including callers outside the current solution. The MSTEST0003
fix therefore does not remove uninferable type parameters; it retains the
fix for inferable parameters when another part of the signature is invalid.
For async-void test methods, the fix uses the framework `Task` type even
when no import exists or a user type has the same name. A later review
superseded that fix because changing the return type can break callers and
overrides. MSTEST0064 also
recognizes sealed async-void overrides, while leaving overridable method
groups unreported because dispatch can select a different implementation.
With the same five selected tests and unchanged assertions, the pre-fix
comparison exited 2 with four expected diagnostic/code-fix mismatches and
one passing overridable-method case; the fixed run exited 0 with all five
passing. Both used `net10.0` Debug, the pinned SDK, isolated outputs, and the
unit-test command shape above with a filter selecting
`UninferableGenericTest_CalledWithTypeArguments`,
`DerivedAsyncVoidTest_WithoutTaskImport`, `SealedOverrideAsyncVoidMethodGroup`,
`OverridableAsyncVoidMethodGroup`, and `WhenTestMethodIsGeneric_Diagnostic`.
The broader `TestMethodShouldBeValidAnalyzerTests` and
`PreferAsyncAssertionAnalyzerTests` selection then passed 65/65 on `net10.0`,
65/65 on `net11.0`, and 63/63 on `net472`; each exited 0 with no skips.

### 2026-11-25: third review round

The test-method fixer now refuses return-type changes that could drop
observable calls or break consumers, and honors the no-fix marker on
non-method declarations. Async-void assertions also recognize the async
state-machine marker on compiled method references. Abstract fixture methods
are excluded from the fixture-lock warning when an override could inherit
their attributes. An unprovided method-generic combinatorial parameter is
diagnosed because data generation precedes method type inference; a type
parameter on a containing class remains unresolved until that class is closed.

The same seven selected cases ran in an isolated pre-fix comparison and on
the updated code with `net10.0` Debug. The pre-fix run exited 2 with six
diagnostic/code-fix failures and one passing class-generic negative case;
the updated run exited 0 with seven passing and no skips. The first
comparison attempt failed to compile because a test was inserted inside a
raw string; it was corrected before the paired runs and is not red evidence.
Selectors were `DerivedTestWithReturnValue`, `DerivedTestAttributeOnOperator`,
`MetadataAsyncVoidMethodGroup`, `AbstractFixtureSchedulingAttributes`,
`MethodGenericParameterWithoutProvider`, `ClassGenericAndUnresolvedParameterTypes`,
and `WhenTestMethodReturnTypeIsNotValid_Diagnostic`. Both used the unit-test
command shape above with isolated outputs and unique binary logs.

### 2026-11-25: final review follow-up

Removing `static` or `abstract` or changing `async void` to `Task` can break
existing callers, overrides, and interface implementations, including callers
outside the solution. MSTEST0003 therefore no longer offers those automatic
signature changes; accessibility fixes remain available where safe.
MSTEST0064 also recognizes an async-void override declared in a sealed class,
where further virtual dispatch cannot change its behavior.
With the same seven selected cases, the pre-fix comparison exited 2 with six
diagnostic/code-fix failures and one passing accessibility-fix case. The
updated `net10.0` Debug run exited 0 with seven passing and no skips. The
selection covered `DerivedSignatureChangesWithCallersOrOverrides`,
`DerivedNonPublicGenericTest`, `WhenTestMethodIsStatic_Diagnostic`,
`WhenTestMethodIsAbstract_Diagnostic`, `WhenTestMethodIsAsyncVoid_Diagnostic`,
`DerivedAsyncVoidTest_WithoutTaskImport`, and
`OverrideInSealedClassAsyncVoidMethodGroup`; both used the command shape above
with isolated outputs and unique binary logs. Earlier review results are
historical snapshots: tests for previously offered unsafe fixes were updated
to assert that no fix is now offered.

After this follow-up, the four affected suites
`TestMethodShouldBeValidAnalyzerTests`, `UseAttributeOnTestMethodAnalyzerTests`,
`PreferAsyncAssertionAnalyzerTests`, and
`CombinatorialDataShouldBeValidAnalyzerTests` were run on the final production
source using the unit-test command shape above. On `net10.0` and `net11.0`,
153 tests passed per target; on `net472`, 151 passed. Each run exited 0 with
no failures or skips. This supersedes the earlier suite counts for these cases.

## Alternatives and decision

Leaving the existing rules unchanged misses configurations that discovery
ignores or executes incorrectly. Exact-name or syntax-only checks are simpler,
but miss inherited attributes, constant expressions, or custom data providers.
Use semantic symbols and constant values, mirroring runtime behavior where it
is determinable. Avoid speculative dataflow and do not infer unspecified
random-data defaults from a potentially different framework version.

Extend the existing class, method, async-assertion, and lock diagnostics where
their established subject applies. Use separate diagnostics for combinatorial
configuration and invalid framework attribute values rather than changing the
meaning of unrelated rules. No automatic fix is planned for an open generic
class, invalid data configuration, or async-void assertion; removing type
parameters or changing delegate/fixture lifetimes can change program behavior.

## Validation and demonstration

| Requirement | Automated evidence | Surface / limitation |
| --- | --- | --- |
| Read versus exclusive locks, inheritance, fixture placement | MSTEST0074/0075 verifier cases, including code fixes, and existing MSTEST0076/0077 cases. | Compiler diagnostics and existing code fixes. |
| Derived attributes and open generic classes | MSTEST0002/0003 verifier cases, including false-positive and no-fix boundaries. | Abstract and closed types remain accepted. |
| Invalid combinatorial configuration | Provider, inferred-type, cancellation-token, range, random, and compatibility cases. | Custom providers remain supported. |
| Async-void exception assertions | Direct lambdas/delegates, casts, valid async assertions, and no-fix cases. | Arbitrary variable/dataflow analysis is excluded. |
| Invalid attribute constants | Positive and boundary cases for relevant test/fixture contexts and older framework references. | Nonconstant or unresolved values remain unreported. |
| Shipping consumer | `AnalyzersTests.PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases` restores the packed analyzer and framework, checks 11 associated diagnostics, and rejects diagnostics on valid input. | Compiler output is captured; IDE presentation is not established. |

### Focused and complete analyzer tests

The implementation was validated as uncommitted changes on
`8599eaa8f335b040bfab8d2eae3fe8e079af6964`. The all-framework focused run's
tracked patch SHA-256 was
`84BBB7451A3C88E725BBB9F59E037D680E2D7550D6DBC4F7F88638EE443F94D2`;
the later comparison patch was
`788285C3560459875042F4C38F86379289C25854ADEA7E4083F2E40097A0C1E1`.
Comparing tracked blob identities showed only the acceptance test changed
between those snapshots. The four new analyzer and unit-test files were
identical. The later edit corrected an expected diagnostic, not production
or unit-test behavior.

The focused selector was the disjunction of
`FullyQualifiedName~<class>` for these classes:
`UndeclaredProcessGlobalStateMutationAnalyzerTests`,
`CurrentDirectoryMutationUnderParallelizationAnalyzerTests`,
`CultureMutationUnderParallelizationAnalyzerTests`,
`TestClassShouldBeValidAnalyzerTests`, `TestMethodShouldBeValidAnalyzerTests`,
`PreferAsyncAssertionAnalyzerTests`, `CombinatorialDataShouldBeValidAnalyzerTests`,
`TestAttributeValuesShouldBeValidAnalyzerTests`,
`UseAttributeOnTestMethodAnalyzerTests`, and `AnalyzerReleaseTrackingTests`.
For each target, with `$out` a fresh results directory and `$filter` that
selector, the command was:

```powershell
.\.dotnet\dotnet.exe test --project .\test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj --framework $tfm --configuration Debug --no-restore "-bl:$out\test-{}.binlog" --filter $filter --report-trx --report-trx-filename focused.trx --results-directory $out --minimum-expected-tests 1 --no-ansi --no-progress
```

`net10.0` and `net11.0` each executed and passed 278 tests; `net472` executed
and passed 276. All exited 0 with no skips. TRX, CTRF, and JUnit results
agreed for each target. The same command without `--filter`, with
`--report-trx-filename full.trx` on `net10.0`, exited 0: 3,183 passed, none
failed, and one existing Unix-only crash-dump test was skipped on Windows.
After copying the reports to matching module filenames, their three formats
agreed on all 3,184 results. An initial report comparison failed only because
the explicit TRX filename did not match the other reports' module name.

### Isolated behavior-removal comparison

An isolated checkout retained all 118 analyzer unit-test source/support files
byte-for-byte and used a separate NuGet cache and outputs. The identical
`net10.0` selector and command above passed 278/278 initially, then ran
against seven simultaneous, buildable behavior-removal mutations: fixture
attribute reporting, exclusive lock-mode detection, derived class/method
attribute matching, open-generic detection, async-void reporting, and the
MSTEST0092/0093 analyses were disabled or reverted. This was a targeted
mutation comparison, not a pristine-base run.

The mutant run exited 2 with 196 passed, 82 failed, and no skips. Every
failure was a missing-diagnostic count assertion, not a compiler or setup
failure: 9 fixture-attribute cases, 20 lock-mutation cases, 4 derived/open
generic cases, 4 async-void cases, 24 combinatorial cases, and 21 attribute
constant cases. Restoring the seven source files, rebuilding with
`dotnet build ... --framework net10.0 --configuration Debug --no-restore
--no-incremental`, and rerunning the same selection exited 0 with 278/278
passed and no skips. Test identities and multiplicities matched across all
three runs. The mutant analyzer DLL SHA-256 was
`80AC24D425BA4E6CC4286D70548048E58A12D9A493EAF0913520E3F123D6364F`;
the restored DLL SHA-256 was
`51965BC4CEE4836D8C87EDCCA38168ADFD9907AC39846B09624D6D648AA93401`.
Binary logs, reports, and mutation patches were retained in session evidence.

### Final packed-consumer validation

After the review fixes, `.\build.cmd -pack -c Release /bl:<unique-binlog>`
exited 0 with no warnings or errors. The Release acceptance host was rebuilt
with `.\.dotnet\dotnet.exe build
.\test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj
-c Release --no-restore -bl:<unique-binlog>` and exited 0. Discovery selected
exactly `PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases`.
The packed-consumer command below exited 0; the execution gate verified one
planned, selected, executed, and passed case with no skips.

```powershell
.\.dotnet\dotnet.exe test --project .\test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~AnalyzersTests.PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases" -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$out" "-bl:$out\execution.binlog" --show-test-results all --no-ansi --no-progress
```

The `net12.0` outer host built the `net11.0` consumer against the isolated
cache. The test matched the current shipping `5.0.0-dev` package hashes:
analyzer `86ED3AD2BB3FC819C55014B959C264CFEE050871A1DDD1E58F402AE35837A966`
and framework `D762EF77EC5E75C4591E83C516A74C5CEFA670A8B9F603183C49A5A333DCC951`.
The restored analyzer DLL matched the package at SHA-256
`4F676C1BD364C1B6015629946E0343C3DE3469E6C45FACBF66974C528578F96B`;
the consumer binlog recorded that exact path as an executed Csc analyzer input.
The test checked 11 associated diagnostics and the absence of selected
diagnostics in valid input. The consumer binlog, assets file, and outer
execution results were retained as session evidence.

### Earlier packed-consumer comparison and localization

`.\build.cmd -pack -c Release /bl:<unique-binlog>` exited 0 with no warnings
or errors. The test host used `net12.0`; its generated consumer built for
`net11.0` with `MSTest.Analyzers` and `MSTest.TestFramework` `5.0.0-dev`.
Discovery using `--list-tests text` selected exactly the named acceptance
test. The following execution, after the configuration-matched pack and
build, passed one test with no skips and exit 0:

```powershell
.\.dotnet\dotnet.exe test --project .\test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~AnalyzersTests.PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases" -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$out" "-bl:$out\execution-{}.binlog" --show-test-results all --no-ansi --no-progress
```

The isolated consumer cache matched the freshly packed shipping packages:
`MSTest.Analyzers.5.0.0-dev.nupkg` SHA-256
`A9A8F5F688BAFDC103B6358DB1F68AB1FCA0DFD684C115116CF048197288713C`,
`MSTest.TestFramework.5.0.0-dev.nupkg` SHA-256
`D25248FC1BCB2BF99E698C1A153E14D11C545F208DACC9A9EE3539D16C013237`,
and the packaged and restored analyzer DLL SHA-256
`6B38B0D095D3544F2AF10716FB1E0177A6121B86E4A3253126CE225AD1A23916`.
The consumer binlog showed that DLL as a compiler analyzer input. For example,
the compiler reported MSTEST0092 for the unsupported `string` data parameter
and MSTEST0064 for the inline async-void delegate. The test associates each
expected rule with its source and message and verifies that the valid source
does not receive any of the selected diagnostics.

The packed-consumer test was also run in the isolated comparison checkout
with the same seven behavior-removal mutations as the unit comparison. The
test source SHA-256 was
`5EA0841987C3210C3E8F12EA2541A951BCAB25C96815031DE883C0A83A1587C8`
in both checkouts; all ten generated consumer inputs and their two parent
build imports also matched byte for byte. The Release-compatible open-generic
mutation patch SHA-256 was
`B7559CB62B66A278DEE94AD774A034952DBAB8CCD90F03CB2FD2431CE46BE723`.
The seven mutant and restored source hashes are retained in a local manifest
with SHA-256
`0D82A4F71FAC10394817A05CC343D576453936EFAA5EC54A61F49099B052EA8A`.

The comparison used the repository script to pack the framework and analyzer
projects separately in Release. The local clone's remote was a filesystem path,
so both commands supplied the same `PackageProjectUrl` and `RepositoryUrl` as
the green shipping package. Unchanged packages from the successful full pack
were copied into the comparison feed for the repository's package-shape check
and acceptance-host initialization; the framework and analyzer under test were
freshly packed in the comparison. Every copied package was hash-verified.

```powershell
.\build.cmd -pack -c Release -projects src\MSTest\TestFramework\TestFramework.csproj /p:PackageProjectUrl=https://github.com/microsoft/testfx.git /p:RepositoryUrl=https://github.com/microsoft/testfx.git /bl:<unique-binlog>
.\build.cmd -pack -c Release -projects src\MSTest\MSTest.Analyzers.Package\MSTest.Analyzers.Package.csproj /p:PackageProjectUrl=https://github.com/microsoft/testfx.git /p:RepositoryUrl=https://github.com/microsoft/testfx.git /bl:<unique-binlog>
```

Both targeted packs exited 0. The comparison host was built with `dotnet build`
in Release for `net12.0` and `--no-restore`; discovery selected exactly the
same single acceptance test. Executing the acceptance command from the
preceding section with the mutant package produced one executed failure, no
skips, and exit 2: the generated
`net11.0` consumer build succeeded, but its diagnostic-count assertion found
one diagnostic instead of eleven. This is an intentional behavior-removal
failure, not a pristine-base comparison.

After restoring all seven files byte-for-byte, the analyzer package project
was rebuilt with `--no-incremental` and repacked using the second command.
The host was rebuilt, discovery again selected the same case, and the same
acceptance command passed one test with no skips and exit 0. The framework
package was identical across the two states, SHA-256
`F85A93774587F846B522C1A71F50DF5C7A54E9F4D5F9EB3CBA87A92E3ACBB529`.
The mutant analyzer package and DLL SHA-256 values were
`21B89AD27A1B1BA0143AA1C5B8666195F0F85914D5FA7910D4484A1841F044CA`
and `D3DFED49204FE89C5655EB62B6D8FB7E9F57264A1DDEA44EEA186ACF47861E37`;
the restored values were
`E5C4D371A84BCA33BA4B58049F04961110FD790CF90BDCB014A827CBA5FC9EA6`
and `83EB022C6BC48E3C3636909FAE2D5481070AF814733EB193505916EFA47BCE89`.
Both consumers restored their respective packages to distinct caches, and
their binary logs show the matching analyzer DLL as an executed `Csc` input.
The packages, generated inputs, logs, and reports are retained locally, not
published.

Earlier comparison setup attempts are not red-phase evidence: the first
mutant pack failed on an IDE0022 style error in the comparison-only no-op;
the full-pack retry failed with an F# missing-reference error for a file that
was present when checked afterward; and initial targeted-pack attempts
lacked clone metadata or the other packages required by the shape check.
The first acceptance execution also failed during host initialization before
the missing platform packages were copied. None reached the intended
diagnostic assertion.

An earlier acceptance attempt omitted the existing MSTEST0003 diagnostic on
the hidden method; its expectation was corrected before the successful run.
Updating existing localization descriptions initially conflicted with locked
tokens in old translations, so new resource IDs were used instead and the
catalogs were regenerated from the neutral RESX. The localization guard's 41
self-tests passed, and all 364 catalogs passed structural, neutral-resource,
placeholder, and locked-token checks. These checks do not prove translation
quality.

Both affected test assemblies retain method-level parallelization with the
default CPU-count workers. Analyzer verifier inputs are compiled, not executed.
The acceptance case builds a unique generated asset with its own NuGet cache
and passes environment overrides to the child process rather than mutating the
test host's process state; no scheduling declaration was changed.

The Windows pipeline's Release solution includes both test projects when its
change and `SkipTests` conditions permit execution; PR affected-test selection
may further narrow the run. No CI execution for this local revision has been
observed.

## Open questions

IDE presentation and Visual Basic consumer execution were not validated.
Release-branch servicing and downstream rollout have not been validated.
