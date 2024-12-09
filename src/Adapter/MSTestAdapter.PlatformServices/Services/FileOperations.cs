// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reflection;

#if WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// The file operations.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class FileOperations : IFileOperations
{
    private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new();

#if WIN_UI
    private readonly bool _isPackaged;
#endif

    internal FileOperations(bool skipSourceGeneratorCheck)
    {
        if (!skipSourceGeneratorCheck)
        {
            ApplicationStateGuard.Ensure(!SourceGeneratorToggle.UseSourceGenerator, $"{nameof(FileOperations)} should not be used when source generator mode is active, instead SourceGeneratedFileOperations should be used and delegate to here, with skipSourceGeneratorCheck = true, when needed.");
        }

#if WIN_UI
        _isPackaged = AppModel.IsPackagedProcess();
#endif
    }

    public FileOperations()
        : this(false)
    {
    }

    /// <summary>
    /// Loads an assembly.
    /// </summary>
    /// <param name="assemblyName"> The assembly name. </param>
    /// <param name="isReflectionOnly">Indicates whether this should be a reflection only load.</param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
    {
#if NETSTANDARD || NETCOREAPP || WINDOWS_UWP
#if WIN_UI
        if (!_isPackaged && Path.IsPathRooted(assemblyName))
        {
            return Assembly.LoadFrom(assemblyName);
        }
#endif
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
        Assembly assembly = _assemblyCache.GetOrAdd(fileNameWithoutExtension, fileNameWithoutExtension => Assembly.Load(new AssemblyName(fileNameWithoutExtension)));

        return assembly;
#elif NETFRAMEWORK
        if (isReflectionOnly)
        {
            return Assembly.ReflectionOnlyLoadFrom(assemblyName);
        }
        else
        {
            Assembly assembly = _assemblyCache.GetOrAdd(assemblyName, Assembly.LoadFrom);
            return assembly;
        }
#endif
    }

    /// <summary>
    /// Gets the path to the .DLL of the assembly.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Path to the .DLL of the assembly.</returns>
    public string? GetAssemblyPath(Assembly assembly)
#if NETSTANDARD || NETCOREAPP || NETFRAMEWORK
        // This method will never be called in source generator mode, we are providing a different provider for file operations.
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
        => assembly.Location;
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
#elif WINDOWS_UWP
        => null; // TODO: what are the options here?
#endif

    /// <summary>
    /// Verifies if file exists in context.
    /// </summary>
    /// <param name="assemblyFileName"> The assembly file name. </param>
    /// <returns> true if file exists. </returns>
    /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
    public bool DoesFileExist(string assemblyFileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI)
        // For projectK these assemblies can be created on the fly which means the file might not exist on disk.
        // Depend on Assembly Load failures instead of this validation.
        return true;
#elif NETFRAMEWORK
        return (SafeInvoke(() => File.Exists(assemblyFileName)) as bool?) ?? false;
#elif WINDOWS_UWP
        bool fileExists = false;

        try
        {
            string fileNameWithoutPath = Path.GetFileName(assemblyFileName);
            Task<Windows.Storage.StorageFile> searchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileNameWithoutPath).AsTask();
            searchTask.Wait();
            fileExists = searchTask.Result != null;
        }
        catch (Exception)
        {
            // ignore
        }

        return fileExists;
#elif WIN_UI
        string path = GetFullFilePath(assemblyFileName);
        return File.Exists(path);
#endif
    }

    /// <summary>
    /// Creates a Navigation session for the source file.
    /// This is used to get file path and line number information for its components.
    /// </summary>
    /// <param name="source"> The source file. </param>
    /// <returns> A Navigation session instance for the current platform. </returns>
    public object? CreateNavigationSession(string source)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        return DiaSessionOperations.CreateNavigationSession(source);
#elif NETFRAMEWORK
        string messageFormatOnException =
            string.Join("MSTestDiscoverer:DiaSession: Could not create diaSession for source:", source, ". Reason:{0}");
        return SafeInvoke(() => new DiaSession(source), messageFormatOnException) as DiaSession;
#endif
    }

    /// <summary>
    /// Gets the navigation data for a navigation session.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    /// <param name="className"> The class name. </param>
    /// <param name="methodName"> The method name. </param>
    /// <param name="minLineNumber"> The min line number. </param>
    /// <param name="fileName"> The file name. </param>
    public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string? fileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        DiaSessionOperations.GetNavigationData(navigationSession, className, methodName, out minLineNumber, out fileName);
#elif NETFRAMEWORK
        fileName = null;
        minLineNumber = -1;

        var diaSession = navigationSession as DiaSession;
        DiaNavigationData? navigationData = diaSession?.GetNavigationData(className, methodName);

        if (navigationData != null)
        {
            minLineNumber = navigationData.MinLineNumber;
            fileName = navigationData.FileName;
        }
#endif
    }

    /// <summary>
    /// Disposes the navigation session instance.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    public void DisposeNavigationSession(object? navigationSession)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        DiaSessionOperations.DisposeNavigationSession(navigationSession);
#elif NETFRAMEWORK
        var diaSession = navigationSession as DiaSession;
        diaSession?.Dispose();
#endif

    }

    /// <summary>
    /// Gets the full file path of an assembly file.
    /// </summary>
    /// <param name="assemblyFileName"> The file name. </param>
    /// <returns> The full file path. </returns>
    public string GetFullFilePath(string assemblyFileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP
        return assemblyFileName;
#elif WIN_UI
        string? packagePath = AppModel.GetCurrentPackagePath();
        return packagePath == null ? assemblyFileName : Path.Combine(packagePath, assemblyFileName);
#elif NETFRAMEWORK

        return (SafeInvoke(() => Path.GetFullPath(assemblyFileName)) as string) ?? assemblyFileName;
#endif
    }

#if NETFRAMEWORK
    private static object? SafeInvoke<T>(Func<T> action, string? messageFormatOnException = null)
    {
        try
        {
            return action.Invoke();
        }
        catch (Exception exception)
        {
            if (StringEx.IsNullOrEmpty(messageFormatOnException))
            {
                messageFormatOnException = "{0}";
            }

            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, messageFormatOnException, exception.Message);
        }

        return null;
    }
#endif

}
