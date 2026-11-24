// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal writer that is used when writing ANSI is allowed. It is capable of batching as many updates as possible and writing them at the end,
/// because the terminal is responsible for rendering the colors and control codes.
/// </summary>
[Embedded]
internal sealed class AnsiTerminal : ITerminal
{
    /// <summary>
    /// The smallest window height that leaves room for both output and pinned progress rows.
    /// </summary>
    private const int MinimumScrollRegionHeight = 5;

    /// <summary>
    /// Lets output scroll the whole window again. Resetting the scroll region moves the cursor to the top left corner,
    /// so the cursor is saved and restored around it.
    /// </summary>
    private const string ResetScrollRegionKeepingCursor = AnsiCodes.SaveCursor + AnsiCodes.ResetScrollRegion + AnsiCodes.RestoreCursor;

    /// <summary>
    /// File extensions that we will link to directly, all other files
    /// are linked to their directory, to avoid opening dlls, or executables.
    /// </summary>
    private static readonly HashSet<string> KnownFileExtensions = new HashSet<string>(
        [".cs", ".vb", ".fs", ".log", ".txt", ".coverage", ".ctrf", ".html", ".junit", ".nunit", ".trx", ".xml", ".xunit"],
        StringComparer.Ordinal);

    private readonly IConsole _console;
    private readonly string? _baseDirectory;
    private readonly bool _useBusyIndicator;
    private readonly bool _useScrollRegion;
    private readonly StringBuilder _stringBuilder = new();
    private bool _isBatching;
    private AnsiTerminalTestProgressFrame _currentFrame = new(0, 0);
    private AnsiTerminalTestProgressFrame _spareFrame = new(0, 0);

    /// <summary>
    /// How long the process exit handler waits for a batch that another thread is writing. .NET Framework gives all
    /// process exit handlers about two seconds in total.
    /// </summary>
    private static readonly TimeSpan DefaultProcessExitBatchWaitTimeout = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan _processExitBatchWaitTimeout;

    // Rows reserved at the bottom of the window for progress, and the window size they were reserved for, as seen by
    // the batches built by the thread that owns the terminal. Only that thread reads and writes them.
    private int _pinnedRows;
    private int _pinnedWidth;
    private int _pinnedHeight;
    private bool _processExitHandlerSubscribed;

    // The pinned rows and window height that the terminal has after the last written batch, packed by PackPinnedState.
    // The process exit handler releases them, so a batch changes them only once it is written.
    private long _writtenPinnedState;
    private long _batchPinnedState;
    private bool _batchChangesPinnedState;

    // The managed thread id of the thread building a batch in scroll region mode, or 0, and whether the process is
    // exiting. Both are only accessed with interlocked operations, so that the exit handler either sees the batch, or
    // the batch sees that the process is exiting.
    private int _batchThreadId;
    private int _processExiting;

    public AnsiTerminal(IConsole console)
        : this(console, useScrollRegion: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnsiTerminal"/> class.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="useScrollRegion">
    /// Whether to pin progress to rows reserved at the bottom of the window by limiting scrolling to the rows above
    /// them. Output written by anyone, including the code under test, then scrolls above the progress instead of
    /// being overwritten when the progress is redrawn.
    /// </param>
    public AnsiTerminal(IConsole console, bool useScrollRegion)
        : this(console, useScrollRegion, DefaultProcessExitBatchWaitTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnsiTerminal"/> class.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="useScrollRegion">See <see cref="AnsiTerminal(IConsole, bool)"/>.</param>
    /// <param name="processExitBatchWaitTimeout">
    /// How long the process exit handler waits for a batch that another thread is writing, before releasing the
    /// rows pinned by the last written batch.
    /// </param>
    public AnsiTerminal(IConsole console, bool useScrollRegion, TimeSpan processExitBatchWaitTimeout)
    {
        _console = console;
        _useScrollRegion = useScrollRegion;
        _processExitBatchWaitTimeout = processExitBatchWaitTimeout;
        _baseDirectory = Directory.GetCurrentDirectory();

        // Output ansi code to get spinner on top of a terminal, to indicate in-progress task.
        // https://github.com/dotnet/msbuild/issues/8958: iTerm2 treats ;9 code to post a notification instead, so disable progress reporting on Mac.
        _useBusyIndicator = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    public int Width
        => _console.IsOutputRedirected || OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsWasi()
            ? int.MaxValue
            : _console.WindowWidth;

    public int Height
        => _console.IsOutputRedirected || OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsWasi()
            ? int.MaxValue
            : _console.WindowHeight;

    public void Append(char value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void Append(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void AppendLine()
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine();
        }
        else
        {
            _console.WriteLine();
        }
    }

    public void AppendLine(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine(value);
        }
        else
        {
            _console.WriteLine(value);
        }
    }

    public void SetColor(TerminalColor color)
    {
        string setColor = AnsiCodes.GetSetColorEscapeCode(color);
        if (_isBatching)
        {
            _stringBuilder.Append(setColor);
        }
        else
        {
            _console.Write(setColor);
        }
    }

    public void ResetColor()
    {
        string resetColor = AnsiCodes.SetDefaultColor;
        if (_isBatching)
        {
            _stringBuilder.Append(resetColor);
        }
        else
        {
            _console.Write(resetColor);
        }
    }

    public void ShowCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.ShowCursor);
        }
        else
        {
            _console.Write(AnsiCodes.ShowCursor);
        }
    }

    public void HideCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.HideCursor);
        }
        else
        {
            _console.Write(AnsiCodes.HideCursor);
        }
    }

    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException(TerminalResources.ConsoleIsAlreadyInBatchingMode);
        }

        _stringBuilder.Clear();
        _batchChangesPinnedState = false;
        if (_useScrollRegion)
        {
            Interlocked.Exchange(ref _batchThreadId, CurrentThreadId);
        }

        _isBatching = true;
    }

    public void StopUpdate()
    {
        if (!_useScrollRegion)
        {
            _console.Write(_stringBuilder);
            _isBatching = false;
            return;
        }

        try
        {
            // A frame saves the cursor, moves it to the pinned rows, and restores it. Output written by another
            // thread in the middle of the frame would land in the pinned rows, so the frame must be a single write.
            _console.WriteAtomically(_stringBuilder);
            if (_batchChangesPinnedState)
            {
                Interlocked.Exchange(ref _writtenPinnedState, _batchPinnedState);
            }
        }
        finally
        {
            _isBatching = false;
            Interlocked.Exchange(ref _batchThreadId, 0);
            if (Volatile.Read(ref _processExiting) != 0)
            {
                // The process started exiting while this batch was built, and the exit handler may have stopped
                // waiting for it. Release the rows that are pinned now, unless the exit handler already did.
                ReleaseWrittenPinnedRows();
            }
            else if (_processExitHandlerSubscribed && _pinnedRows == 0 && Interlocked.Read(ref _writtenPinnedState) == 0)
            {
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                _processExitHandlerSubscribed = false;
            }
        }
    }

    public void AppendLink(string? path, int? lineNumber)
    {
        if (RoslynString.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // For non code files, point to the directory, so we don't end up running the
        // exe by clicking at the link.
        string? extension = Path.GetExtension(path);
        bool linkToFile = !RoslynString.IsNullOrWhiteSpace(extension) && KnownFileExtensions.Contains(extension);

        bool knownNonExistingFile = path.StartsWith("/_/", ignoreCase: false, CultureInfo.CurrentCulture);

        string linkPath = path;
        if (!linkToFile)
        {
            try
            {
                linkPath = Path.GetDirectoryName(linkPath) ?? linkPath;
            }
            catch
            {
                // Ignore all GetDirectoryName errors.
            }
        }

        // If the output path is under the initial working directory, make the console output relative to that to save space.
        if (_baseDirectory is not null && path.StartsWith(_baseDirectory, FileUtilities.PathComparison))
        {
            if (path.Length > _baseDirectory.Length
                && (path[_baseDirectory.Length] == Path.DirectorySeparatorChar
                    || path[_baseDirectory.Length] == Path.AltDirectorySeparatorChar))
            {
                path = path[(_baseDirectory.Length + 1)..];
            }
        }

        if (lineNumber is not null)
        {
            path += $":{lineNumber}";
        }

        if (knownNonExistingFile)
        {
            Append(path);
            return;
        }

        // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
        if (Uri.TryCreate(linkPath, UriKind.Absolute, out Uri? uri))
        {
            // url.ToString() un-escapes the URL which is needed for our case file://
            linkPath = uri.ToString();
        }

        SetColor(TerminalColor.DarkGray);
        Append(AnsiCodes.LinkPrefix);
        Append(linkPath);
        Append(AnsiCodes.LinkInfix);
        Append(path);
        Append(AnsiCodes.LinkSuffix);
        ResetColor();
    }

    public void MoveCursorUp(int lineCount)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.CSI).Append(lineCount).Append(AnsiCodes.MoveUpToLineStart).AppendLine();
        }
        else
        {
            _console.WriteLine($"{AnsiCodes.CSI}{lineCount}{AnsiCodes.MoveUpToLineStart}");
        }
    }

    public void SetCursorHorizontal(int position)
    {
        string setCursor = AnsiCodes.SetCursorHorizontal(position);
        if (_isBatching)
        {
            _stringBuilder.Append(setCursor);
        }
        else
        {
            _console.Write(setCursor);
        }
    }

    /// <summary>
    /// Erases the previously printed live node output.
    /// </summary>
    public void EraseProgress()
    {
        if (_useScrollRegion)
        {
            ReleasePinnedRows(TryGetHeight());
            return;
        }

        if (_currentFrame.RenderedLinesCount == 0)
        {
            return;
        }

        AppendLine($"{AnsiCodes.CSI}{_currentFrame.RenderedLinesCount + 2}{AnsiCodes.MoveUpToLineStart}");
        Append(AnsiCodes.CsiEraseInDisplay);
        _currentFrame.Clear();
    }

    public void RenderProgress(TestProgressState?[] progress, TerminalProgressMessageState[] messages)
    {
        if (_useScrollRegion)
        {
            RenderPinnedProgress(progress, messages);
            return;
        }

        // Reuse the spare frame instead of allocating a new one every tick.
        AnsiTerminalTestProgressFrame nextFrame = _spareFrame;
        nextFrame.Reset(Width, Height);
        nextFrame.Render(_currentFrame, progress, messages, terminal: this);
        _spareFrame = _currentFrame;
        _currentFrame = nextFrame;
    }

    private void RenderPinnedProgress(TestProgressState?[] progress, TerminalProgressMessageState[] messages)
    {
        if (!_isBatching)
        {
            // The pinned rows change only when a batch is written, see StopUpdate.
            StartUpdate();
            try
            {
                RenderPinnedProgress(progress, messages);
            }
            finally
            {
                StopUpdate();
            }

            return;
        }

        // Once the process exit handler released the rows, reserving them again would leave the scroll region set.
        if (Volatile.Read(ref _processExiting) != 0)
        {
            return;
        }

        int width = Width;
        int height = Height;
        if (_pinnedRows > 0 && (width != _pinnedWidth || height != _pinnedHeight))
        {
            // The rows were reserved for another window size, release them and reserve them again below.
            ReleasePinnedRows(height);
        }

        // Without a known window height (redirected output) or with a tiny window, there is no room to pin
        // progress, and showing nothing is better than overwriting output.
        if (height is int.MaxValue or < MinimumScrollRegionHeight)
        {
            return;
        }

        AnsiTerminalTestProgressFrame frame = _currentFrame;
        frame.Reset(width, height);

        // Use at most a third of the window, one row of which separates the progress from the output above it.
        int maxRows = Math.Max(2, height / 3);
        int lineCount = frame.CollectPinnedLines(progress, messages, maxRows - 1);

        // Never shrink the reserved rows during the run, shrinking would leave a gap in the output.
        int rows = Math.Max(_pinnedRows, lineCount == 0 ? 0 : Math.Min(lineCount + 1, maxRows));
        if (rows == 0)
        {
            return;
        }

        if (rows > _pinnedRows)
        {
            ReservePinnedRows(rows, width, height);
        }

        frame.RenderPinnedRows(this, firstRow: height - rows + 1, rowCount: rows);
    }

    private void ReservePinnedRows(int rows, int width, int height)
    {
        if (!_processExitHandlerSubscribed)
        {
            // Make sure the shell is not left with a scroll region when the process exits without tearing down the
            // progress, for example on a second Ctrl+C or when the parent process exits. Subscribe before the rows
            // are reserved, so that the handler is called once they are.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitHandlerSubscribed = true;
        }

        // Remove any previous scroll region, so that the lines below can scroll the whole window. Setting the
        // region moves the cursor to the top left corner, so the cursor is saved and restored around it.
        Append(ResetScrollRegionKeepingCursor);

        // Move down as many lines as needed, scrolling the output above into the scrollback when the cursor
        // reaches the bottom of the window, then go back up. The cursor is now on the same output line and
        // column as before, with the requested number of rows below the output area. Rows that were pinned
        // before scroll by at most the number of added rows, so they stay within the new pinned rows, which
        // are all redrawn by the frame. Nothing is erased, so output below the cursor is kept.
        for (int i = 0; i < rows; i++)
        {
            Append(AnsiCodes.Index);
        }

        Append(AnsiCodes.MoveCursorUp(rows));

        // Limit scrolling to the output area above the pinned rows.
        Append(AnsiCodes.SaveCursor);
        Append(AnsiCodes.SetScrollRegion(1, height - rows));
        Append(AnsiCodes.RestoreCursor);

        _pinnedRows = rows;
        _pinnedWidth = width;
        _pinnedHeight = height;
        _batchPinnedState = PackPinnedState(rows, height);
        _batchChangesPinnedState = true;
    }

    private void ReleasePinnedRows(int currentHeight)
    {
        if (_pinnedRows == 0)
        {
            return;
        }

        if (!_isBatching)
        {
            // The pinned rows change only when a batch is written, see StopUpdate.
            StartUpdate();
            try
            {
                ReleasePinnedRows(currentHeight);
            }
            finally
            {
                StopUpdate();
            }

            return;
        }

        // Once the process is exiting, the rows are released by the exit handler, or by StopUpdate when the exit
        // handler stopped waiting for this batch, so nothing is written after them.
        if (Volatile.Read(ref _processExiting) == 0)
        {
            AppendReleaseSequence(_stringBuilder, _pinnedRows, _pinnedHeight, currentHeight);
            _batchPinnedState = 0;
            _batchChangesPinnedState = true;
        }

        _pinnedRows = 0;
        _currentFrame.Clear();
    }

    /// <summary>
    /// Lets output scroll the whole window again when the process exits while rows are pinned. Takes no lock, because
    /// the thread that owns the terminal can hold it while the process exits, and never throws. When another thread is
    /// writing a batch, waits a short time for it, so that the rows it pins or releases are known.
    /// </summary>
    public void ReleasePinnedRowsOnProcessExit()
    {
        try
        {
            Interlocked.Exchange(ref _processExiting, 1);
            WaitForBatchOfAnotherThread();
            ReleaseWrittenPinnedRows();
        }
        catch
        {
            // The process is exiting, so nothing can be done when the console cannot be written to.
        }
    }

    private void WaitForBatchOfAnotherThread()
    {
        int batchThreadId = Volatile.Read(ref _batchThreadId);
        if (batchThreadId == 0 || batchThreadId == CurrentThreadId)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        SpinWait spinWait = default;
        while (Volatile.Read(ref _batchThreadId) == batchThreadId && stopwatch.Elapsed < _processExitBatchWaitTimeout)
        {
            spinWait.SpinOnce();
        }
    }

    private void ReleaseWrittenPinnedRows()
    {
        try
        {
            // Claim the rows, so that they are released only once when the exit handler and StopUpdate both release them.
            long state = Interlocked.Exchange(ref _writtenPinnedState, 0);
            int rows = (int)(state & uint.MaxValue);
            if (rows == 0)
            {
                return;
            }

            StringBuilder release = new();
            AppendReleaseSequence(release, rows, pinnedHeight: (int)(state >> 32), TryGetHeight());
            _console.WriteAtomically(release);
        }
        catch
        {
            // The process is exiting, so nothing can be done when the console cannot be written to.
        }
    }

    // The thread id only identifies the thread that writes a batch, it is not a property of the environment to fake.
#pragma warning disable RS0030 // Do not use banned APIs
    private static int CurrentThreadId => Environment.CurrentManagedThreadId;
#pragma warning restore RS0030 // Do not use banned APIs

    private static long PackPinnedState(int rows, int height)
        => ((long)height << 32) | (uint)rows;

    private void OnProcessExit(object? sender, EventArgs e)
        => ReleasePinnedRowsOnProcessExit();

    private static void AppendReleaseSequence(StringBuilder builder, int rows, int pinnedHeight, int currentHeight)
    {
        if (currentHeight == pinnedHeight)
        {
            // Let output scroll the whole window again, and clear only the pinned rows, so that output on the
            // screen is kept wherever the cursor is. Origin mode is disabled so that the rows are addressed
            // from the top of the screen, and restoring the cursor restores the mode.
            builder.Append(AnsiCodes.SaveCursor)
                .Append(AnsiCodes.DisableOriginMode)
                .Append(AnsiCodes.ResetScrollRegion)
                .Append(AnsiCodes.MoveCursorTo(currentHeight - rows + 1, 1))
                .Append(AnsiCodes.CsiEraseInDisplay)
                .Append(AnsiCodes.RestoreCursor);
        }
        else
        {
            // The window was resized or its size is unknown, so the position of the pinned rows is unknown too.
            // Let output scroll the whole window again, and clear below the cursor, where the rows were.
            builder.Append(ResetScrollRegionKeepingCursor).Append(AnsiCodes.CsiEraseInDisplay);
        }
    }

    private int TryGetHeight()
    {
        try
        {
            return Height;
        }
        catch
        {
            // Treat a window whose size cannot be read as a window of unknown size.
            return int.MaxValue;
        }
    }

    public void StartBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.SetBusySpinner);
        }

        HideCursor();
    }

    public void StopBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.RemoveBusySpinner);
        }

        ShowCursor();
    }
}
