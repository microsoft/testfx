// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Anti-drift guard for the source-only <c>Microsoft.Testing.Platform.ServerClient.Source</c> package.
/// The package ships the MTP server-mode client as <c>contentFiles</c> (compiled into each consumer),
/// reusing the exact protocol + serialization source the platform server compiles. These tests inspect
/// the produced <c>.nupkg</c> and assert the properties that make it a correct source-only client:
/// <list type="bullet">
///   <item>it carries no compiled output (no <c>lib/</c>, <c>ref/</c>, <c>runtimes/</c>) — source only;</item>
///   <item>every packed <c>.cs</c> is declared in the nuspec <c>&lt;contentFiles&gt;</c> with
///   <c>buildAction="Compile"</c>, and the manifest matches the packed files exactly (no orphan either way);</item>
///   <item>the netstandard2.0 JSON path is Jsonite only, while net8.0 additionally carries the in-box
///   System.Text.Json engine — so a net462/netstandard consumer never compiles the STJ files;</item>
///   <item>the client API ships in every target framework, and every client source file on disk is packed;</item>
///   <item>every packed linked/client file is a pack-time transformed copy — it starts with the
///   <c>// &lt;auto-generated/&gt;</c> header and declares no column-0 <c>public</c> type (the injected
///   source adds no public API to the consumer);</item>
///   <item>the platform polyfills ship (a hostile net462/netstandard2.0 consumer needs them) while no
///   build-generated source (GlobalUsings.g.cs, AssemblyInfo, …) leaks.</item>
/// </list>
/// The package must have been produced first (build with <c>-pack</c>); otherwise the tests fail with a hint
/// rather than passing vacuously.
/// </summary>
[TestClass]
public sealed class MtpServerClientSourcePackageTests
{
    private const string PackageId = "Microsoft.Testing.Platform.ServerClient.Source";

    // The two target frameworks the package project multi-targets. netstandard2.0 covers net462 consumers
    // (Jsonite JSON path); net8.0 covers modern .NET consumers (in-box System.Text.Json JSON path).
    private const string NetStandard = "netstandard2.0";
    private const string Net = "net8.0";

    // Logical path prefix (under contentFiles/cs/<tfm>/) of the shared JSON engine folder.
    private const string JsonFolder = "Linked/ServerMode/JsonRpc/Json/";

    // Client API entry points that must be shipped for every target framework.
    private static readonly string[] ClientApiFiles =
    [
        "Client/IMtpServerClient.cs",
        "Client/MtpServerClient.cs",
        "Client/MtpServerProcess.cs",
    ];

    private static readonly SourcePackage Package = SourcePackage.Load();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SourcePackage_ContainsNoCompiledOutput()
    {
        string[] forbiddenRoots = ["lib/", "ref/", "runtimes/"];
        var offenders = Package.AllEntries
            .Where(e => forbiddenRoots.Any(root => e.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.IsEmpty(
            offenders,
            $"'{PackageId}' is a source-only package and must not carry compiled output. Found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders));
    }

    [TestMethod]
    public void SourcePackage_EveryCsFileIsCompileContentFile_AndManifestMatchesPackedFiles()
    {
        // Every .cs actually in the zip must live under contentFiles/cs/<tfm>/ and be declared in the
        // nuspec <contentFiles> with buildAction="Compile". Anything else means a consumer would either
        // fail to compile the injected source or receive a stray file.
        List<string> notDeclaredAsCompile = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (string logical in Package.PackedCsByTfm[tfm])
            {
                if (!Package.CompileManifestByTfm[tfm].Contains(logical))
                {
                    notDeclaredAsCompile.Add($"{tfm}: {logical}");
                }
            }
        }

        Assert.IsEmpty(
            notDeclaredAsCompile,
            $"These packed .cs files are not declared as buildAction=\"Compile\" contentFiles (consumers would not compile them):{Environment.NewLine}" +
            string.Join(Environment.NewLine, notDeclaredAsCompile));

        // Conversely, every contentFiles entry the manifest declares must exist as a packed file, so the
        // manifest can never advertise a source file that isn't actually shipped.
        List<string> declaredButMissing = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (string logical in Package.CompileManifestByTfm[tfm])
            {
                if (!Package.PackedCsByTfm[tfm].Contains(logical))
                {
                    declaredButMissing.Add($"{tfm}: {logical}");
                }
            }
        }

        Assert.IsEmpty(
            declaredButMissing,
            $"The nuspec declares these contentFiles that are not actually packed:{Environment.NewLine}" +
            string.Join(Environment.NewLine, declaredButMissing));

        Assert.IsNotEmpty(
            Package.PackedCsByTfm[Net],
            $"Expected '{PackageId}' to ship source files, but none were found. Build with '-pack' first.");
    }

    [TestMethod]
    public void SourcePackage_NetStandardJsonPath_IsJsoniteOnly_AndNetIsSuperset()
    {
        // netstandard2.0 must carry ONLY the Jsonite JSON path: every file under the JSON folder must be
        // under Json/Jsonite/ or be JsoniteProperties.cs. If a System.Text.Json engine file (e.g.
        // Json.Serializers.cs) leaks in, a net462 consumer would try to compile the STJ path.
        var stjOnNetStandard = Package.PackedCsByTfm[NetStandard]
            .Where(IsJsonEngineFile)
            .Where(logical => !IsJsonitePath(logical))
            .ToList();

        Assert.IsEmpty(
            stjOnNetStandard,
            $"The netstandard2.0 JSON path must be Jsonite only, but these non-Jsonite JSON files were packed:{Environment.NewLine}" +
            string.Join(Environment.NewLine, stjOnNetStandard));

        // Sanity: the Jsonite parser itself must be present on netstandard2.0.
        Assert.Contains(
            "Linked/ServerMode/JsonRpc/Json/Jsonite/Json.cs",
            Package.PackedCsByTfm[NetStandard],
            "Expected the Jsonite parser (Json/Jsonite/Json.cs) to be packed for netstandard2.0.");

        // net8.0 must carry the in-box System.Text.Json engine that netstandard2.0 does not.
        var stjOnlyOnNet = Package.PackedCsByTfm[Net]
            .Where(IsJsonEngineFile)
            .Where(logical => !IsJsonitePath(logical))
            .Where(logical => !Package.PackedCsByTfm[NetStandard].Contains(logical))
            .ToList();

        Assert.IsNotEmpty(
            stjOnlyOnNet,
            "Expected net8.0 to add System.Text.Json engine files that netstandard2.0 does not carry, but found none.");

        // net8.0 is a superset of netstandard2.0: the STJ path is additive, it never drops the shared source.
        var missingOnNet = Package.PackedCsByTfm[NetStandard]
            .Where(logical => !Package.PackedCsByTfm[Net].Contains(logical))
            .ToList();

        Assert.IsEmpty(
            missingOnNet,
            $"net8.0 must ship every source file netstandard2.0 ships (plus the STJ engine), but these are missing on net8.0:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missingOnNet));
    }

    [TestMethod]
    public void SourcePackage_ShipsClientApi_InEveryTargetFramework()
    {
        List<string> missing = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (string api in ClientApiFiles)
            {
                if (!Package.PackedCsByTfm[tfm].Contains(api))
                {
                    missing.Add($"{tfm}: {api}");
                }
            }
        }

        Assert.IsEmpty(
            missing,
            $"The client API must ship for every target framework, but these are missing:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing));

        // Every client source file on disk must be packed for every target framework. This is the direct
        // guard against adding a Client/*.cs to the project and forgetting to ship it (or vice versa).
        string clientDir = Path.Combine(
            Constants.Root, "src", "Platform", "Microsoft.Testing.Platform.ServerClient", "Client");
        Assert.IsTrue(Directory.Exists(clientDir), $"Expected the client source folder to exist at '{clientDir}'.");

        List<string> ownedButNotPacked = [];
        foreach (string file in Directory.EnumerateFiles(clientDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string logical = "Client/" + Path.GetFileName(file);
            foreach (string tfm in Package.TargetFrameworks)
            {
                if (!Package.PackedCsByTfm[tfm].Contains(logical))
                {
                    ownedButNotPacked.Add($"{tfm}: {logical}");
                }
            }
        }

        Assert.IsEmpty(
            ownedButNotPacked,
            $"These client source files exist on disk but are not packed (add them to the package or exclude them intentionally):{Environment.NewLine}" +
            string.Join(Environment.NewLine, ownedButNotPacked));
    }

    [TestMethod]
    public void SourcePackage_ShippedLinkedAndClientSource_IsAutoGeneratedAndInternal()
    {
        // Every packed non-polyfill .cs is a pack-time transformed copy. It must start with the
        // // <auto-generated/> header (so a consumer's analyzers + TreatWarningsAsErrors skip it) and must
        // declare NO column-0 `public` type: the injected source adds no public API to the consumer
        // (vstest enforces this with PublicApiAnalyzers). Polyfills also go through the transform (the
        // public->internal flip is a no-op on them, but they gain the self-contained BCL using preamble) and
        // are checked separately below.
        Regex publicType = new(
            @"^public\s+(?:(?:sealed|abstract|static|partial|unsafe|readonly|ref|file|new)\s+)*(?:class|struct|interface|enum|record|delegate)\b",
            RegexOptions.Multiline);

        List<string> missingHeader = [];
        List<string> publicLeaks = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (KeyValuePair<string, string> file in Package.PackedTextByTfm[tfm])
            {
                if (file.Key.Contains("Polyfills/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!file.Value.StartsWith("// <auto-generated/>", StringComparison.Ordinal))
                {
                    missingHeader.Add($"{tfm}: {file.Key}");
                }

                foreach (Match match in publicType.Matches(file.Value))
                {
                    publicLeaks.Add($"{tfm}: {file.Key} :: {match.Value}");
                }
            }
        }

        Assert.IsEmpty(
            missingHeader,
            $"Every packed linked/client source file must start with the // <auto-generated/> header:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missingHeader));

        Assert.IsEmpty(
            publicLeaks,
            $"The injected source must add no public API (no column-0 public type declaration), but these were found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, publicLeaks));
    }

    [TestMethod]
    public void SourcePackage_ShipsPolyfills_AndDoesNotLeakBuildGeneratedSource()
    {
        // The package SHIPS the platform polyfills: a hostile net462 / netstandard2.0 consumer with no
        // down-level polyfills of its own needs init/required, Index/Range, EmbeddedAttribute,
        // ExperimentalAttribute, UnreachableException, the OperatingSystem shim (OperatingSystem.IsBrowser),
        // and helpers like Ensure. They go through the same pack-time transform as the linked source (so they
        // gain the self-contained BCL using preamble; they are already internal + auto-generated). Ensure and
        // the OperatingSystem shim are required for every target framework.
        foreach (string tfm in Package.TargetFrameworks)
        {
            Assert.Contains(
                "Polyfills/Ensure.cs",
                Package.PackedCsByTfm[tfm],
                $"Expected the package to ship the platform polyfills for '{tfm}' (Polyfills/Ensure.cs).");
            Assert.Contains(
                "Polyfills/OperatingSystem.cs",
                Package.PackedCsByTfm[tfm],
                $"Expected the OperatingSystem polyfill (OperatingSystem.IsBrowser shim) to ship for '{tfm}'.");
        }

        // But it must NOT leak build-generated source (GlobalUsings.g.cs, *.AssemblyInfo.cs, …) — each
        // consumer generates its own.
        List<string> generatedLeaks = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (string logical in Package.PackedCsByTfm[tfm])
            {
                string fileName = logical[(logical.LastIndexOf('/') + 1)..];
                bool isGenerated = fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith("AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains("GlobalUsings", StringComparison.OrdinalIgnoreCase);

                if (isGenerated)
                {
                    generatedLeaks.Add($"{tfm}: {logical}");
                }
            }
        }

        Assert.IsEmpty(
            generatedLeaks,
            $"The package must not ship build-generated source (each consumer generates its own). Found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, generatedLeaks));
    }

    [TestMethod]
    public void SourcePackage_ShipsBuildTargets_AndNet462SafetyGuardsSurviveTransform()
    {
        // The package ships a build/*.targets that customizes the consumer's compilation of the injected
        // source (defines IS_CORE_MTP and NoWarns the benign down-level polyfill collision CS0436). Without
        // it a .NET Framework / netstandard2.0 consumer would not compile the source clean.
        const string TargetsEntry = "build/Microsoft.Testing.Platform.ServerClient.Source.targets";
        Assert.Contains(
            TargetsEntry,
            Package.AllEntries,
            $"Expected the package to ship '{TargetsEntry}' so consumers get the required build customization.");

        // net462-safety regression guard: the injected source is authored for netstandard2.0, but a net462
        // consumer compiles it WITHOUT System.Runtime.InteropServices.RuntimeInformation / OSPlatform (those
        // do not exist on net462 and no facade forwards them). The two source sites that used them are guarded
        // with #if NETFRAMEWORK. If the guard is ever dropped, net462 consumers (e.g. vstest CrossPlatEngine)
        // break with CS0103 — assert the guard survives the pack transform on the netstandard2.0 (net462) leg.
        var missingGuard = new List<string>();
        foreach (string guarded in new[] { "Polyfills/OperatingSystem.cs", "Client/MtpServerProcess.cs" })
        {
            if (!Package.PackedTextByTfm[NetStandard].TryGetValue(guarded, out string? text))
            {
                missingGuard.Add($"{guarded} (not packed for {NetStandard})");
            }
            else if (!text.Contains("#if NETFRAMEWORK", StringComparison.Ordinal))
            {
                missingGuard.Add($"{guarded} (no '#if NETFRAMEWORK' net462 guard)");
            }
        }

        Assert.IsEmpty(
            missingGuard,
            $"The net462-safety guards must survive the pack transform so a net462 consumer compiles clean:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missingGuard));
    }

    [TestMethod]
    public void SourcePackage_JsoniteNamespace_IsPackageQualified_NotTopLevel()
    {
        // The pack-time transform rewrites the vendored top-level `namespace Jsonite` to the
        // package-qualified `Microsoft.Testing.Platform.ServerMode.JsonRpc.Json.Jsonite`. This is the
        // regression guard for the collision that broke vstest: two `namespace Jsonite` type sets compiled
        // into one assembly (the package's + vstest CrossPlatEngine's own vendored Jsonite) collide with
        // CS0436 on net462/netstandard2.0. If anyone drops the pack-time rename (or the platform is packed
        // raw), this fails. Namespaces have no wire effect, so the "Jsonite" formatter Id literal (wire
        // identity) must be preserved untouched — asserted below.
        const string QualifiedNs = "namespace Microsoft.Testing.Platform.ServerMode.JsonRpc.Json.Jsonite";
        Regex topLevelNamespace = new(@"^namespace Jsonite\s*$", RegexOptions.Multiline);
        Regex bareUsing = new(@"^using Jsonite;\s*$", RegexOptions.Multiline);

        List<string> leaks = [];
        bool sawQualifiedNs = false;
        bool sawWireIdLiteral = false;
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (KeyValuePair<string, string> file in Package.PackedTextByTfm[tfm])
            {
                if (topLevelNamespace.IsMatch(file.Value))
                {
                    leaks.Add($"{tfm}: {file.Key} :: top-level 'namespace Jsonite'");
                }

                if (bareUsing.IsMatch(file.Value))
                {
                    leaks.Add($"{tfm}: {file.Key} :: bare 'using Jsonite;'");
                }

                if (file.Value.Contains(QualifiedNs, StringComparison.Ordinal))
                {
                    sawQualifiedNs = true;
                }

                // The formatter Id is the wire identity of the Jsonite path and must survive verbatim.
                if (file.Value.Contains("\"Jsonite\"", StringComparison.Ordinal))
                {
                    sawWireIdLiteral = true;
                }
            }
        }

        Assert.IsEmpty(
            leaks,
            $"The packed source must not carry the top-level 'Jsonite' namespace (it would collide with a " +
            $"consumer's own vendored Jsonite). These leaks were found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, leaks));

        Assert.IsTrue(
            sawQualifiedNs,
            $"Expected the packed Jsonite parser to declare the package-qualified '{QualifiedNs}'. " +
            "The pack-time namespace rewrite did not run.");

        Assert.IsTrue(
            sawWireIdLiteral,
            "Expected the packed formatter to preserve the \"Jsonite\" Id literal (wire identity). " +
            "The namespace rewrite must not touch the quoted token.");
    }

    private static bool IsJsonEngineFile(string logical)
        => logical.Contains(JsonFolder, StringComparison.Ordinal);

    private static bool IsJsonitePath(string logical)
        => logical.Contains(JsonFolder + "Jsonite/", StringComparison.Ordinal)
            || logical.EndsWith(JsonFolder + "JsoniteProperties.cs", StringComparison.Ordinal);

    /// <summary>
    /// Snapshot of the produced source-only package: the raw zip entries, the packed <c>.cs</c> files per
    /// target framework, and the <c>buildAction="Compile"</c> contentFiles declared in the nuspec.
    /// </summary>
    private sealed class SourcePackage
    {
        private const string ContentPrefix = "contentFiles/cs/";

        private SourcePackage(
            IReadOnlyList<string> allEntries,
            IReadOnlyList<string> targetFrameworks,
            IReadOnlyDictionary<string, HashSet<string>> packedCsByTfm,
            IReadOnlyDictionary<string, HashSet<string>> compileManifestByTfm,
            IReadOnlyDictionary<string, Dictionary<string, string>> packedTextByTfm)
        {
            AllEntries = allEntries;
            TargetFrameworks = targetFrameworks;
            PackedCsByTfm = packedCsByTfm;
            CompileManifestByTfm = compileManifestByTfm;
            PackedTextByTfm = packedTextByTfm;
        }

        public IReadOnlyList<string> AllEntries { get; }

        public IReadOnlyList<string> TargetFrameworks { get; }

        public IReadOnlyDictionary<string, HashSet<string>> PackedCsByTfm { get; }

        public IReadOnlyDictionary<string, HashSet<string>> CompileManifestByTfm { get; }

        /// <summary>Gets the full text of each packed <c>.cs</c> file, keyed by target framework then logical path.</summary>
        public IReadOnlyDictionary<string, Dictionary<string, string>> PackedTextByTfm { get; }

        public static SourcePackage Load()
        {
            string nupkg = FindPackage();
            using ZipArchive archive = ZipFile.OpenRead(nupkg);

            var allEntries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

            var packedCsByTfm = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var packedTextByTfm = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (ZipArchiveEntry archiveEntry in archive.Entries)
            {
                string entry = archiveEntry.FullName.Replace('\\', '/');
                if (!entry.StartsWith(ContentPrefix, StringComparison.Ordinal)
                    || !entry.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rest = entry[ContentPrefix.Length..];
                int slash = rest.IndexOf('/');
                string tfm = rest[..slash];
                string logical = rest[(slash + 1)..];
                if (!packedCsByTfm.TryGetValue(tfm, out HashSet<string>? set))
                {
                    set = [];
                    packedCsByTfm[tfm] = set;
                }

                set.Add(logical);

                using StreamReader reader = new(archiveEntry.Open());
                if (!packedTextByTfm.TryGetValue(tfm, out Dictionary<string, string>? textMap))
                {
                    textMap = [];
                    packedTextByTfm[tfm] = textMap;
                }

                textMap[logical] = reader.ReadToEnd();
            }

            Dictionary<string, HashSet<string>> compileManifestByTfm = ReadCompileManifest(archive);

            // Fail early and clearly if the expected target frameworks were not produced.
            foreach (string tfm in new[] { NetStandard, Net })
            {
                Assert.IsTrue(
                    packedCsByTfm.ContainsKey(tfm),
                    $"Expected '{PackageId}' to ship contentFiles for '{tfm}', but none were found. Build with '-pack' first.");
                if (!compileManifestByTfm.ContainsKey(tfm))
                {
                    compileManifestByTfm[tfm] = [];
                }

                if (!packedTextByTfm.ContainsKey(tfm))
                {
                    packedTextByTfm[tfm] = [];
                }
            }

            return new SourcePackage(
                allEntries,
                packedCsByTfm.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                packedCsByTfm,
                compileManifestByTfm,
                packedTextByTfm);
        }

        private static Dictionary<string, HashSet<string>> ReadCompileManifest(ZipArchive archive)
        {
            var manifest = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            ZipArchiveEntry nuspecEntry = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

            XDocument nuspec;
            using (Stream stream = nuspecEntry.Open())
            {
                nuspec = XDocument.Load(stream);
            }

            foreach (XElement file in nuspec.Descendants().Where(e => e.Name.LocalName == "files"))
            {
                if ((string?)file.Attribute("include") is not { } include
                    || !string.Equals((string?)file.Attribute("buildAction"), "Compile", StringComparison.Ordinal))
                {
                    continue;
                }

                string normalized = include.Replace('\\', '/');
                if (!normalized.StartsWith("cs/", StringComparison.Ordinal))
                {
                    continue;
                }

                string rest = normalized["cs/".Length..];
                int slash = rest.IndexOf('/');
                string tfm = rest[..slash];
                string logical = rest[(slash + 1)..];
                if (!manifest.TryGetValue(tfm, out HashSet<string>? set))
                {
                    set = [];
                    manifest[tfm] = set;
                }

                set.Add(logical);
            }

            return manifest;
        }

        private static string FindPackage()
        {
            string folder = Constants.ArtifactsPackagesShipping;
            string? nupkg = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, PackageId + ".*.nupkg", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .LastOrDefault()
                : null;

            Assert.IsNotNull(
                nupkg,
                $"Could not find '{PackageId}.*.nupkg' in '{folder}'. Build with '-pack' before running this test.");
            return nupkg;
        }
    }
}
