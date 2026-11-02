// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Portions of the code in this file were ported from the spectre.console by Patrik Svensson, Phil Scott, Nils Andresen
// https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Backends/Ansi/AnsiDetector.cs
// and from the supports-ansi project by Qingrong Ke
// https://github.com/keqingrong/supports-ansi/blob/master/index.js
using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Works together with the <see cref="NativeMethods"/> to figure out if the current console is capable of using ANSI output codes.
/// </summary>
[Embedded]
internal static class AnsiDetector
{
    public static bool IsAnsiSupported(string? termType)
    {
        if (RoslynString.IsNullOrEmpty(termType))
        {
            return false;
        }

        // Each condition mirrors the original regex pattern but uses plain string operations,
        // eliminating the 17 Regex allocations and their compilation cost.
        return termType.StartsWith("xterm", StringComparison.Ordinal) // ^xterm — xterm, PuTTY, Mintty
            || termType.StartsWith("rxvt", StringComparison.Ordinal) // ^rxvt — RXVT
            || (termType.IndexOf("eterm", StringComparison.Ordinal) >= 0 // ^(?!eterm-color).*eterm.* — eterm but not eterm-color (#9950)
                && !termType.StartsWith("eterm-color", StringComparison.Ordinal))
            || termType.StartsWith("screen", StringComparison.Ordinal) // ^screen — GNU screen, tmux
            || termType.IndexOf("tmux", StringComparison.Ordinal) >= 0 // tmux — tmux
            || termType.StartsWith("vt100", StringComparison.Ordinal) // ^vt100 — DEC VT series
            || termType.StartsWith("vt102", StringComparison.Ordinal) // ^vt102 — DEC VT series
            || termType.StartsWith("vt220", StringComparison.Ordinal) // ^vt220 — DEC VT series
            || termType.StartsWith("vt320", StringComparison.Ordinal) // ^vt320 — DEC VT series
            || termType.IndexOf("ansi", StringComparison.Ordinal) >= 0 // ansi — ANSI
            || termType.IndexOf("scoansi", StringComparison.Ordinal) >= 0 // scoansi — SCO ANSI
            || termType.IndexOf("cygwin", StringComparison.Ordinal) >= 0 // cygwin — Cygwin, MinGW
            || termType.IndexOf("linux", StringComparison.Ordinal) >= 0 // linux — Linux console
            || termType.IndexOf("konsole", StringComparison.Ordinal) >= 0 // konsole — Konsole
            || termType.IndexOf("bvterm", StringComparison.Ordinal) >= 0 // bvterm — Bitvise SSH Client
            || termType.StartsWith("st-256color", StringComparison.Ordinal) // ^st-256color — Suckless Simple Terminal, st
            || termType.IndexOf("alacritty", StringComparison.Ordinal) >= 0; // alacritty — Alacritty
    }
}
