// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETSTANDARD || NETCOREAPP || NETFRAMEWORK || WINDOWS_UWP
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.IO;
using System.Reflection;

#if WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

/// <summary>
/// The file operations.
/// </summary>
public class FileOperations : IFileOperations
{
#if WIN_UI
    private readonly bool _isPackaged;

    public FileOperations()
    {
        _isPackaged = AppModel.IsPackagedProcess();
    }
#endif

    /// <summary>
    /// Loads an assembly.
    /// </summary>
    /// <param name="assemblyName"> The assembly name. </param>
    /// <param name="isReflectionOnly">Indicates whether this should be a reflection only load.</param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
    {
#if WIN_UI
        if (!_isPackaged && Path.IsPathRooted(assemblyName))
        {
            return Assembly.LoadFrom(assemblyName);
        }
#endif
#if NETSTANDARD || NETCOREAPP || WINDOWS_UWP
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
        return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
#elif NETFRAMEWORK

        if (isReflectionOnly)
        {
            return Assembly.ReflectionOnlyLoadFrom(assemblyName);
        }
        else
        {
            return Assembly.LoadFrom(assemblyName);
        }
#endif
    }

    /// <summary>
    /// Gets the path to the .DLL of the assembly.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Path to the .DLL of the assembly.</returns>
    public string GetAssemblyPath(Assembly assembly)
    {
#if NETSTANDARD || NETCOREAPP || NETFRAMEWORK
        return assembly.Location;
#elif WINDOWS_UWP
        return null; // TODO: what are the options here?
#endif
    }

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
        return (SafeInvoke<bool>(() => File.Exists(assemblyFileName)) as bool?) ?? false;
#elif WINDOWS_UWP
        var fileExists = false;

        try
        {
            var fileNameWithoutPath = Path.GetFileName(assemblyFileName);
            var searchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileNameWithoutPath).AsTask();
            searchTask.Wait();
            fileExists = searchTask.Result != null;
        }
        catch (Exception)
        {
            // ignore
        }

        return fileExists;
#elif WIN_UI
        var path = GetFullFilePath(assemblyFileName);
        return File.Exists(path);
#endif
    }

    /// <summary>
    /// Creates a Navigation session for the source file.
    /// This is used to get file path and line number information for its components.
    /// </summary>
    /// <param name="source"> The source file. </param>
    /// <returns> A Navigation session instance for the current platform. </returns>
    public object CreateNavigationSession(string source)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        return DiaSessionOperations.CreateNavigationSession(source);
#elif NETFRAMEWORK
        var messageFormatOnException =
            string.Join("MSTestDiscoverer:DiaSession: Could not create diaSession for source:", source, ". Reason:{0}");
        return SafeInvoke<DiaSession>(() => new DiaSession(source), messageFormatOnException) as DiaSession;
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
    public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string fileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        DiaSessionOperations.GetNavigationData(navigationSession, className, methodName, out minLineNumber, out fileName);
#elif NETFRAMEWORK
        fileName = null;
        minLineNumber = -1;

        var diasession = navigationSession as DiaSession;
        var navigationData = diasession?.GetNavigationData(className, methodName);

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
    public void DisposeNavigationSession(object navigationSession)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP || WIN_UI
        DiaSessionOperations.DisposeNavigationSession(navigationSession);
#elif NETFRAMEWORK
        var diasession = navigationSession as DiaSession;
        diasession?.Dispose();
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
        var packagePath = AppContainer.AppModel.GetCurrentPackagePath();
        if (packagePath == null)
        {
            return assemblyFileName;
        }

        return Path.Combine(packagePath, assemblyFileName);
#elif NETFRAMEWORK

        return (SafeInvoke<string>(() => Path.GetFullPath(assemblyFileName)) as string) ?? assemblyFileName;
#endif
    }

#if NETFRAMEWORK
    private static object SafeInvoke<T>(Func<T> action, string messageFormatOnException = null)
    {
        try
        {
            return action.Invoke();
        }
        catch (Exception exception)
        {
            if (string.IsNullOrEmpty(messageFormatOnException))
            {
                messageFormatOnException = "{0}";
            }

            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, messageFormatOnException, exception.Message);
        }

        return null;
    }
#endif

}
#endif
