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
///   <item>no polyfill or build-generated source leaks into the package.</item>
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
    public void SourcePackage_DoesNotLeakPolyfillsOrGeneratedSource()
    {
        List<string> leaks = [];
        foreach (string tfm in Package.TargetFrameworks)
        {
            foreach (string logical in Package.PackedCsByTfm[tfm])
            {
                string fileName = logical[(logical.LastIndexOf('/') + 1)..];
                bool isPolyfill = logical.Contains("Polyfills/", StringComparison.OrdinalIgnoreCase);
                bool isGenerated = fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith("AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains("GlobalUsings", StringComparison.OrdinalIgnoreCase);

                if (isPolyfill || isGenerated)
                {
                    leaks.Add($"{tfm}: {logical}");
                }
            }
        }

        Assert.IsEmpty(
            leaks,
            $"The package must not ship polyfills (consumers have their own) or build-generated source. Found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, leaks));
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
            IReadOnlyDictionary<string, HashSet<string>> compileManifestByTfm)
        {
            AllEntries = allEntries;
            TargetFrameworks = targetFrameworks;
            PackedCsByTfm = packedCsByTfm;
            CompileManifestByTfm = compileManifestByTfm;
        }

        public IReadOnlyList<string> AllEntries { get; }

        public IReadOnlyList<string> TargetFrameworks { get; }

        public IReadOnlyDictionary<string, HashSet<string>> PackedCsByTfm { get; }

        public IReadOnlyDictionary<string, HashSet<string>> CompileManifestByTfm { get; }

        public static SourcePackage Load()
        {
            string nupkg = FindPackage();
            using ZipArchive archive = ZipFile.OpenRead(nupkg);

            var allEntries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

            var packedCsByTfm = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string entry in allEntries)
            {
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
            }

            return new SourcePackage(
                allEntries,
                packedCsByTfm.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                packedCsByTfm,
                compileManifestByTfm);
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
