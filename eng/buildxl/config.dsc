// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

config({
    disableDefaultSourceResolver: true,
    disableInBoxSdkSourceResolver: true,
    mounts: [
        {
            name: a`TestFx`,
            path: Environment.getPathValue("TESTFX_BUILDXL_ROOT"),
            isReadable: true,
            // The native resolver declares the checkout as a catch-all shared output.
            isWritable: true,
            isScrubbable: false,
            trackSourceFileChanges: true
        },
        {
            name: a`DotNet`,
            path: Environment.getPathValue("DOTNET_ROOT"),
            isReadable: true,
            isWritable: false,
            isScrubbable: false,
            trackSourceFileChanges: true
        },
        {
            name: a`NuGetPackages`,
            path: Environment.getPathValue("NUGET_PACKAGES"),
            isReadable: true,
            isWritable: false,
            isScrubbable: false,
            trackSourceFileChanges: true
        }
    ],
    resolvers: [{
        kind: "MsBuild",
        moduleName: "TestFx",
        root: Environment.getDirectoryValue("TESTFX_BUILDXL_ROOT"),
        fileNameEntryPoints: [
            RelativePath.create(Environment.getStringValue("TESTFX_BUILDXL_PROJECT"))
        ],
        msBuildRuntime: "DotNetCore",
        msBuildSearchLocations: [Environment.getDirectoryValue("TESTFX_BUILDXL_MSBUILD")],
        dotNetSearchLocations: [Environment.getDirectoryValue("DOTNET_ROOT")],
        initialTargets: ["Build"],
        environment: Map.empty<string, string>().addRange(
            ["PATH", Environment.getStringValue("PATH")],
            ["SystemRoot", Environment.getStringValue("SystemRoot")],
            ["SystemDrive", Environment.getStringValue("SystemDrive")],
            ["ComSpec", Environment.getStringValue("ComSpec")],
            ["OS", "Windows_NT"],
            ["TEMP", Environment.getStringValue("TEMP")],
            ["TMP", Environment.getStringValue("TMP")],
            ["USERPROFILE", Environment.getStringValue("USERPROFILE")],
            ["APPDATA", Environment.getStringValue("APPDATA")],
            ["LOCALAPPDATA", Environment.getStringValue("LOCALAPPDATA")],
            ["ProgramFiles", Environment.getStringValue("ProgramFiles")],
            ["ProgramFiles(x86)", Environment.getStringValue("ProgramFiles(x86)")],
            ["DOTNET_ROOT", Environment.getStringValue("DOTNET_ROOT")],
            ["DOTNET_HOST_PATH", Environment.getStringValue("DOTNET_ROOT") + "/dotnet.exe"],
            ["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", Environment.getStringValue("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR")],
            ["DOTNET_MULTILEVEL_LOOKUP", "0"],
            ["DOTNET_NOLOGO", "1"],
            ["DOTNET_CLI_TELEMETRY_OPTOUT", "1"],
            ["NUGET_PACKAGES", Environment.getStringValue("NUGET_PACKAGES")]
        ),
        globalProperties: Map.empty<string, string>().addRange(
            ["Configuration", Environment.getStringValue("TESTFX_BUILDXL_CONFIGURATION")],
            ["ArtifactsBinDir", Environment.getStringValue("TESTFX_BUILDXL_BIN")],
            ["ArtifactsObjDir", Environment.getStringValue("TESTFX_BUILDXL_OBJ")],
            ["CustomBeforeDirectoryBuildTargets", Environment.getStringValue("TESTFX_BUILDXL_ROOT") + "/eng/buildxl/msbuild.targets"],
            ["SkipUapTargetFramework", "true"],
            // BuildXL omits CurrentSolutionConfigurationContents from project command lines.
            ["ShouldUnsetParentConfigurationAndPlatform", "false"],
            ["EnableProjectDataOnBuild", "false"],
            ["UseSharedCompilation", "true"],
            ...addIf<[string, string]>(Environment.getStringValue("TESTFX_BUILDXL_ADDITIONAL_TARGETS") !== "*",
                ["CustomAfterMicrosoftCommonTargets", Environment.getStringValue("TESTFX_BUILDXL_ADDITIONAL_TARGETS")]),
            ...addIf<[string, string]>(Environment.getStringValue("TESTFX_BUILDXL_TFM") !== "*",
                ["TargetFrameworks", Environment.getStringValue("TESTFX_BUILDXL_TFM")])
        ),
        logVerbosity: "minimal",
        enableBinLogTracing: true,
        keepProjectGraphFile: true,
        useManagedSharedCompilation: true
    }]
});
