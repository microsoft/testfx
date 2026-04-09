// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This file provides polyfills for types that are not available on older target frameworks (netstandard2.0, .NET Framework).
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1502 // Element should not be on a single line
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1405 // Debug.Assert should provide message text
#pragma warning disable SA1407 // Arithmetic expressions should declare precedence
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1516 // Elements should be separated by blank line
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
#pragma warning disable IDE0046 // If statement can be simplified
#pragma warning disable IDE0065 // Misplaced using directive
#pragma warning disable IDE0280 // Use 'nameof'
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line

// On TFMs where IsExternalInit is provided by the BCL, add a TypeForwardedTo so that
// assemblies compiled against the netstandard2.0 version (which had the polyfill type)
// can resolve the type at runtime on net5+.
// See https://github.com/SimonCropp/Polyfill/issues/290
#if NET5_0_OR_GREATER
#pragma warning disable RS0016 // Symbol is not part of the declared public API - compiler support type forwarding
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Runtime.CompilerServices.IsExternalInit))]
#pragma warning restore RS0016
#else

namespace System.Runtime.CompilerServices
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static class IsExternalInit;

    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct | global::System.AttributeTargets.Field | global::System.AttributeTargets.Property, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        public string FeatureName { get; }

        public bool IsOptional { get; init; }

        public const string RefStructs = nameof(RefStructs);

        public const string RequiredMembers = nameof(RequiredMembers);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class DoesNotReturnAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class DoesNotReturnIfAttribute : Attribute
    {
        public DoesNotReturnIfAttribute(bool parameterValue) => ParameterValue = parameterValue;

        public bool ParameterValue { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(string member) => Members = [member];

        public MemberNotNullAttribute(params string[] members) => Members = members;

        public string[] Members { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = [member];
        }

        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }

        public bool ReturnValue { get; }

        public string[] Members { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class NotNullIfNotNullAttribute : Attribute
    {
        public NotNullIfNotNullAttribute(string parameterName) => ParameterName = parameterName;

        public string ParameterName { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        public bool ReturnValue { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.ReturnValue, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class NotNullAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class AllowNullAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class DisallowNullAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.ReturnValue, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class MaybeNullAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Field | global::System.AttributeTargets.Property, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class StringSyntaxAttribute : Attribute
    {
        public const string CompositeFormat = nameof(CompositeFormat);
        public const string CSharp = "C#";
        public const string DateOnlyFormat = nameof(DateOnlyFormat);
        public const string DateTimeFormat = nameof(DateTimeFormat);
        public const string EnumFormat = nameof(EnumFormat);
        public const string FSharp = "F#";
        public const string GuidFormat = nameof(GuidFormat);
        public const string Json = nameof(Json);
        public const string NumericFormat = nameof(NumericFormat);
        public const string Regex = nameof(Regex);
        public const string TimeOnlyFormat = nameof(TimeOnlyFormat);
        public const string TimeSpanFormat = nameof(TimeSpanFormat);
        public const string Uri = nameof(Uri);
        public const string VisualBasic = "Visual Basic";
        public const string Xml = nameof(Xml);

        public StringSyntaxAttribute(string syntax)
        {
            Syntax = syntax;
            Arguments = [];
        }

        public StringSyntaxAttribute(string syntax, params object?[] arguments)
        {
            Syntax = syntax;
            Arguments = arguments;
        }

        public string Syntax { get; }

        public object?[] Arguments { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.ReturnValue | global::System.AttributeTargets.GenericParameter | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property | global::System.AttributeTargets.Method | global::System.AttributeTargets.Class | global::System.AttributeTargets.Interface | global::System.AttributeTargets.Struct, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    [global::System.Flags]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,
        NonPublicConstructors = 0x0004,
        PublicMethods = 0x0008,
        NonPublicMethods = 0x0010,
        PublicFields = 0x0020,
        NonPublicFields = 0x0040,
        PublicNestedTypes = 0x0080,
        NonPublicNestedTypes = 0x0100,
        PublicProperties = 0x0200,
        NonPublicProperties = 0x0400,
        PublicEvents = 0x0800,
        NonPublicEvents = 0x1000,
        Interfaces = 0x2000,
        All = ~None,
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }

        public string CheckId { get; }

        public string? Scope { get; set; }

        public string? Target { get; set; }

        public string? MessageId { get; set; }

        public string? Justification { get; set; }
    }
}

namespace System.Runtime.Versioning
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly | global::System.AttributeTargets.Module | global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct | global::System.AttributeTargets.Enum | global::System.AttributeTargets.Constructor | global::System.AttributeTargets.Method | global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Event | global::System.AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class SupportedOSPlatformAttribute : Attribute
    {
        public SupportedOSPlatformAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class SupportedOSPlatformGuardAttribute : Attribute
    {
        public SupportedOSPlatformGuardAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly | global::System.AttributeTargets.Module | global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct | global::System.AttributeTargets.Enum | global::System.AttributeTargets.Constructor | global::System.AttributeTargets.Method | global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Event | global::System.AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class UnsupportedOSPlatformAttribute : Attribute
    {
        public UnsupportedOSPlatformAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class UnsupportedOSPlatformGuardAttribute : Attribute
    {
        public UnsupportedOSPlatformGuardAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }
}

#endif

#if !NET6_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;

        public string ParameterName { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute { }

    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public InterpolatedStringHandlerArgumentAttribute(string argument) => Arguments = [argument];

        public InterpolatedStringHandlerArgumentAttribute(params string[] arguments) => Arguments = arguments;

        public string[] Arguments { get; }
    }
}

namespace System.Diagnostics
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Method | global::System.AttributeTargets.Constructor | global::System.AttributeTargets.Struct, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class StackTraceHiddenAttribute : Attribute { }
}
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

#if !NET8_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly | global::System.AttributeTargets.Module | global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct | global::System.AttributeTargets.Enum | global::System.AttributeTargets.Constructor | global::System.AttributeTargets.Method | global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Event | global::System.AttributeTargets.Interface | global::System.AttributeTargets.Delegate, Inherited = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class ExperimentalAttribute : Attribute
    {
        public ExperimentalAttribute(string diagnosticId) => DiagnosticId = diagnosticId;

        public string DiagnosticId { get; }

        public string? UrlFormat { get; set; }
    }
}

#endif

#if !NET9_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class ParamCollectionAttribute : Attribute { }
}

#endif

#if !NET9_0_OR_GREATER

namespace System.Threading
{
#pragma warning disable CS9216 // A value of type 'Lock' converted to a different type will use likely unintended monitor-based locking
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class Lock
    {
        public bool IsHeldByCurrentThread => Monitor.IsEntered(this);

        public void Enter() => Monitor.Enter(this);

        public bool TryEnter() => Monitor.TryEnter(this);

        public bool TryEnter(TimeSpan timeout) => Monitor.TryEnter(this, timeout);

        public bool TryEnter(int millisecondsTimeout) => TryEnter(TimeSpan.FromMilliseconds(millisecondsTimeout));

        public void Exit() => Monitor.Exit(this);

        public Scope EnterScope()
        {
            Enter();
            return new Scope(this);
        }

        public readonly ref struct Scope(Lock owner)
        {
            public void Dispose() => owner.Exit();
        }
    }
}

#endif

#if !NET5_0_OR_GREATER

namespace System
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = fromEnd ? ~value : value;
        }

        private Index(int value) => _value = value;

        public static Index Start => new(0);

        public static Index End => new(~0);

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public static Index FromStart(int value) => value >= 0 ? new Index(value) : throw new IndexOutOfRangeException(nameof(value));

        public static Index FromEnd(int value) => value >= 0 ? new Index(~value) : throw new IndexOutOfRangeException(nameof(value));

        public static implicit operator Index(int value) => FromStart(value);

        public int GetOffset(int length)
        {
            int offset = _value;
            if (IsFromEnd)
            {
                offset += length + 1;
            }

            return offset;
        }

        public override bool Equals(object? value) => value is Index index && _value == index._value;

        public bool Equals(Index other) => _value == other._value;

        public override int GetHashCode() => _value;

        public override string ToString() => IsFromEnd ? $"^{(uint)Value}" : ((uint)Value).ToString();
    }

    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal readonly struct Range : IEquatable<Range>
    {
        public Index Start { get; }

        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range StartAt(Index start) => new(start, Index.End);

        public static Range EndAt(Index end) => new(Index.Start, end);

        public static Range All => new(Index.Start, Index.End);

        public override bool Equals(object? value) => value is Range r && r.Start.Equals(Start) && r.End.Equals(End);

        public bool Equals(Range other) => other.Start.Equals(Start) && other.End.Equals(End);

        public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();

        public override string ToString() => $"{Start}..{End}";

        public void GetOffsetAndLength(int length, out int offset, out int len)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            if ((uint)end > (uint)length || (uint)start > (uint)end)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            offset = start;
            len = end - start;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static class RuntimeHelpers
    {
        /// <summary>Slices the specified array using the specified range.</summary>
        /// <typeparam name="T">The type of the array elements.</typeparam>
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            range.GetOffsetAndLength(array.Length, out int offset, out int length);
            T[] dest = new T[length];
            Array.Copy(array, offset, dest, 0, length);
            return dest;
        }
    }
}

#endif

namespace Microsoft.CodeAnalysis
{
    [global::System.AttributeUsage(global::System.AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed partial class EmbeddedAttribute : global::System.Attribute { }
}

#pragma warning disable CS8603 // Possible null reference return
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting
#pragma warning disable RS0030 // Banned API - Ensure is the guard implementation itself
[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class Ensure
{
    [return: global::System.Diagnostics.CodeAnalysis.NotNull]
    public static T NotNull<T>([global::System.Diagnostics.CodeAnalysis.NotNull] T? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
        {
            throw new global::System.ArgumentNullException(paramName);
        }

        return argument;
    }

    public static string NotNullOrEmpty([global::System.Diagnostics.CodeAnalysis.NotNull] string? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
        {
            throw new global::System.ArgumentNullException(paramName);
        }

        if (argument.Length == 0)
        {
            throw new global::System.ArgumentException("Value cannot be null or empty.", paramName);
        }

        return argument;
    }

    public static void NotNullOrEmpty<T>([global::System.Diagnostics.CodeAnalysis.NotNull] global::System.Collections.Generic.IEnumerable<T>? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
        {
            throw new global::System.ArgumentNullException(paramName);
        }

        if (argument is global::System.Collections.Generic.ICollection<T> collection)
        {
            if (collection.Count == 0)
            {
                throw new global::System.ArgumentException("Value cannot be empty.", paramName);
            }

            return;
        }

        using global::System.Collections.Generic.IEnumerator<T> enumerator = argument.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new global::System.ArgumentException("Value cannot be empty.", paramName);
        }
    }

    public static string NotNullOrWhiteSpace([global::System.Diagnostics.CodeAnalysis.NotNull] string? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
        {
            throw new global::System.ArgumentNullException(paramName);
        }

        if (argument.Trim().Length == 0)
        {
            throw new global::System.ArgumentException("Value cannot be null or whitespace.", paramName);
        }

        return argument;
    }
}

// Backward-compatible stubs for Polyfills.Guard and Polyfills.Ensure types.
// The old Polyfill NuGet package generated these types into the Microsoft.Testing.Platform assembly;
// old extension packages (compiled against the previous platform version with InternalsVisibleTo)
// may reference these types at runtime. Providing them here prevents TypeLoadExceptions.
#if IS_CORE_MTP
namespace Polyfills
{
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [global::System.Diagnostics.DebuggerNonUserCode]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static class Guard
    {
        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        public static T NotNull<T>([global::System.Diagnostics.CodeAnalysis.NotNull] T? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? name = null)
            where T : class
        {
            if (argument is null)
            {
                throw new global::System.ArgumentNullException(name);
            }

            return argument;
        }

        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        public static string NotNull([global::System.Diagnostics.CodeAnalysis.NotNull] string? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? name = null)
        {
            if (argument is null)
            {
                throw new global::System.ArgumentNullException(name);
            }

            return argument;
        }

        public static string NotNullOrEmpty([global::System.Diagnostics.CodeAnalysis.NotNull] string? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
            => Ensure.NotNullOrEmpty(value, name);

        public static T NotNullOrEmpty<T>([global::System.Diagnostics.CodeAnalysis.NotNull] T? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
            where T : global::System.Collections.IEnumerable
            => Ensure.NotNullOrEmpty(value, name);

        public static string NotNullOrWhiteSpace([global::System.Diagnostics.CodeAnalysis.NotNull] string? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
            => Ensure.NotNullOrWhiteSpace(value, name);

        public static void FileExists(string path, [global::System.Runtime.CompilerServices.CallerArgumentExpression("path")] string? name = null)
            => Ensure.FileExists(path, name);

        public static void DirectoryExists(string path, [global::System.Runtime.CompilerServices.CallerArgumentExpression("path")] string? name = null)
            => Ensure.DirectoryExists(path, name);
    }

    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [global::System.Diagnostics.DebuggerNonUserCode]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static class Ensure
    {
        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        public static T NotNull<T>([global::System.Diagnostics.CodeAnalysis.NotNull] T? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? name = null)
            where T : class
        {
            if (argument is null)
            {
                throw new global::System.ArgumentNullException(name);
            }

            return argument;
        }

        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        public static string NotNull([global::System.Diagnostics.CodeAnalysis.NotNull] string? argument, [global::System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? name = null)
        {
            if (argument is null)
            {
                throw new global::System.ArgumentNullException(name);
            }

            return argument;
        }

        public static string NotNullOrEmpty([global::System.Diagnostics.CodeAnalysis.NotNull] string? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new global::System.ArgumentException("Must not be null or empty.", name);
            }

            return value;
        }

        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        public static T NotNullOrEmpty<T>([global::System.Diagnostics.CodeAnalysis.NotNull] T? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
            where T : global::System.Collections.IEnumerable
        {
            if (value is null)
            {
                throw new global::System.ArgumentNullException(name);
            }

            global::System.Collections.IEnumerator enumerator = value.GetEnumerator();
            try
            {
                if (!enumerator.MoveNext())
                {
                    throw new global::System.ArgumentException("Must not be empty.", name);
                }
            }
            finally
            {
                (enumerator as global::System.IDisposable)?.Dispose();
            }

            return value;
        }

        public static string NotNullOrWhiteSpace([global::System.Diagnostics.CodeAnalysis.NotNull] string? value, [global::System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? name = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new global::System.ArgumentException("Must not be null or whitespace.", name);
            }

            return value;
        }

        public static void FileExists(string path, [global::System.Runtime.CompilerServices.CallerArgumentExpression("path")] string? name = null)
        {
            if (!global::System.IO.File.Exists(path))
            {
                throw new global::System.ArgumentException($"File not found. Path: {path}", name);
            }
        }

        public static void DirectoryExists(string path, [global::System.Runtime.CompilerServices.CallerArgumentExpression("path")] string? name = null)
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::System.ArgumentException($"Directory not found. Path: {path}", name);
            }
        }
    }

    // Polyfills.Polyfill stub: The old Polyfill NuGet package generated this type into every assembly.
    // Old extensions (compiled with InternalsVisibleTo to the platform) may reference methods on
    // this type from the platform assembly. We need to provide it for backward compatibility.
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [global::System.Diagnostics.DebuggerNonUserCode]
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static class Polyfill
    {
#if NETCOREAPP
        public static bool Contains<T>(global::System.ReadOnlySpan<T> span, T value, global::System.Collections.Generic.IEqualityComparer<T>? comparer = null)
        {
            comparer ??= global::System.Collections.Generic.EqualityComparer<T>.Default;
            foreach (T item in span)
            {
                if (comparer.Equals(item, value))
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
#endif

// All extension method polyfills are guarded with IS_CORE_MTP to avoid ambiguity when
// projects reference Microsoft.Testing.Platform with InternalsVisibleTo. Projects that
// do not reference Platform and need these extensions should provide their own copies.
#if IS_CORE_MTP

#if !NET8_0_OR_GREATER

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class CancellationTokenSourceExtensions
{
    public static global::System.Threading.Tasks.Task CancelAsync(this global::System.Threading.CancellationTokenSource cancellationTokenSource)
    {
        if (cancellationTokenSource.IsCancellationRequested)
        {
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        global::System.Threading.Tasks.Task task = global::System.Threading.Tasks.Task.Run(cancellationTokenSource.Cancel);

        while (!cancellationTokenSource.IsCancellationRequested)
        {
        }

        return task;
    }
}

#endif // !NET8_0_OR_GREATER

#if !NET5_0_OR_GREATER

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillStringExtensions
{
    public static bool Contains(this string s, char c) => s.IndexOf(c) >= 0;

    public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;

    public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;

    public static string[] Split(this string s, char separator, global::System.StringSplitOptions options = global::System.StringSplitOptions.None) =>
        s.Split([separator], options);

    public static string[] Split(this string s, char separator, int count, global::System.StringSplitOptions options = global::System.StringSplitOptions.None) =>
        s.Split([separator], count, options);

    public static string Replace(this string s, string oldValue, string? newValue, global::System.StringComparison comparisonType)
    {
        if (comparisonType == global::System.StringComparison.Ordinal)
        {
            return s.Replace(oldValue, newValue);
        }

        var sb = new global::System.Text.StringBuilder();
        int previousIndex = 0;
        int index = s.IndexOf(oldValue, comparisonType);
        while (index != -1)
        {
            sb.Append(s, previousIndex, index - previousIndex);
            sb.Append(newValue);
            index += oldValue.Length;
            previousIndex = index;
            index = s.IndexOf(oldValue, index, comparisonType);
        }

        sb.Append(s, previousIndex, s.Length - previousIndex);
        return sb.ToString();
    }

    public static bool Contains(this string s, string value, global::System.StringComparison comparisonType) =>
        s.IndexOf(value, comparisonType) >= 0;

    public static int GetHashCode(this string s, global::System.StringComparison comparisonType) =>
        comparisonType switch
        {
            global::System.StringComparison.CurrentCulture => global::System.StringComparer.CurrentCulture.GetHashCode(s),
            global::System.StringComparison.CurrentCultureIgnoreCase => global::System.StringComparer.CurrentCultureIgnoreCase.GetHashCode(s),
            global::System.StringComparison.InvariantCulture => global::System.StringComparer.InvariantCulture.GetHashCode(s),
            global::System.StringComparison.InvariantCultureIgnoreCase => global::System.StringComparer.InvariantCultureIgnoreCase.GetHashCode(s),
            global::System.StringComparison.Ordinal => global::System.StringComparer.Ordinal.GetHashCode(s),
            global::System.StringComparison.OrdinalIgnoreCase => global::System.StringComparer.OrdinalIgnoreCase.GetHashCode(s),
            _ => throw new global::System.ArgumentOutOfRangeException(nameof(comparisonType), comparisonType, null),
        };
}

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillEnumExtensions
{
    public static TEnum Parse<TEnum>(string value)
        where TEnum : struct, global::System.Enum =>
        (TEnum)global::System.Enum.Parse(typeof(TEnum), value);

    public static TEnum Parse<TEnum>(string value, bool ignoreCase)
        where TEnum : struct, global::System.Enum =>
        (TEnum)global::System.Enum.Parse(typeof(TEnum), value, ignoreCase);
}

// Note: Deconstruct for KeyValuePair is intentionally NOT provided here because it causes
// ambiguity errors when projects reference each other with InternalsVisibleTo and both
// compile this file. It is provided in Microsoft.Testing.Platform project's own source.

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillDictionaryExtensions
{
    public static bool TryAdd<TKey, TValue>(this global::System.Collections.Generic.Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
            return true;
        }

        return false;
    }
}

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillStringBuilderExtensions
{
    public static global::System.Text.StringBuilder AppendJoin(this global::System.Text.StringBuilder sb, string separator, global::System.Collections.Generic.IEnumerable<string> values)
    {
        bool first = true;
        foreach (string value in values)
        {
            if (!first)
            {
                sb.Append(separator);
            }

            sb.Append(value);
            first = false;
        }

        return sb;
    }

    public static global::System.Text.StringBuilder AppendJoin<T>(this global::System.Text.StringBuilder sb, string separator, global::System.Collections.Generic.IEnumerable<T> values)
    {
        bool first = true;
        foreach (T value in values)
        {
            if (!first)
            {
                sb.Append(separator);
            }

            sb.Append(value);
            first = false;
        }

        return sb;
    }

    public static global::System.Text.StringBuilder AppendJoin(this global::System.Text.StringBuilder sb, char separator, global::System.Collections.Generic.IEnumerable<string> values) =>
        sb.AppendJoin(separator.ToString(), values);
}

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillTaskExtensions
{
    public static async global::System.Threading.Tasks.Task WaitAsync(this global::System.Threading.Tasks.Task task, global::System.Threading.CancellationToken cancellationToken)
    {
        var tcs = new global::System.Threading.Tasks.TaskCompletionSource<bool>(global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(s => ((global::System.Threading.Tasks.TaskCompletionSource<bool>)s!).TrySetCanceled(cancellationToken), tcs))
        {
            if (task != await global::System.Threading.Tasks.Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
            {
                throw new global::System.OperationCanceledException(cancellationToken);
            }
        }

        await task.ConfigureAwait(false);
    }

    public static async global::System.Threading.Tasks.Task WaitAsync(this global::System.Threading.Tasks.Task task, global::System.TimeSpan timeout, global::System.Threading.CancellationToken cancellationToken = default)
    {
        using var cts = global::System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (global::System.OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new global::System.TimeoutException();
        }
    }

    public static async global::System.Threading.Tasks.Task<T> WaitAsync<T>(this global::System.Threading.Tasks.Task<T> task, global::System.Threading.CancellationToken cancellationToken)
    {
        var tcs = new global::System.Threading.Tasks.TaskCompletionSource<bool>(global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(s => ((global::System.Threading.Tasks.TaskCompletionSource<bool>)s!).TrySetCanceled(cancellationToken), tcs))
        {
            if (task != await global::System.Threading.Tasks.Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
            {
                throw new global::System.OperationCanceledException(cancellationToken);
            }
        }

        return await task.ConfigureAwait(false);
    }
}

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class PolyfillProcessExtensions
{
    public static async global::System.Threading.Tasks.Task WaitForExitAsync(this global::System.Diagnostics.Process process, global::System.Threading.CancellationToken cancellationToken = default)
    {
        if (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                process.EnableRaisingEvents = true;
            }
            catch (global::System.InvalidOperationException) when (process.HasExited)
            {
                return;
            }

            var tcs = new global::System.Threading.Tasks.TaskCompletionSource<object?>(global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

            global::System.EventHandler handler = (_, _) => tcs.TrySetResult(null);
            process.Exited += handler;

            try
            {
                if (!process.HasExited)
                {
                    using (cancellationToken.Register(state => ((global::System.Threading.Tasks.TaskCompletionSource<object?>)state!).TrySetCanceled(cancellationToken), tcs))
                    {
                        await tcs.Task.ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                process.Exited -= handler;
            }
        }
    }

    public static void Kill(this global::System.Diagnostics.Process process, bool entireProcessTree) =>
        // entireProcessTree not supported on netstandard2.0 - just kill the process
        process.Kill();
}

#endif // !NET5_0_OR_GREATER

#endif // IS_CORE_MTP

#if !NET5_0_OR_GREATER

[global::Microsoft.CodeAnalysis.EmbeddedAttribute]
internal static class OperatingSystem
{
    public static bool IsBrowser() => false;

    public static bool IsWasi() => false;

    public static bool IsAndroid() => false;

    public static bool IsIOS() => false;

    public static bool IsTvOS() => false;

#if NETFRAMEWORK
    public static bool IsWindows() => global::System.Environment.OSVersion.Platform == global::System.PlatformID.Win32NT;

    public static bool IsLinux() => false;

    public static bool IsMacOS() => false;
#else
    public static bool IsWindows() => global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows);

    public static bool IsLinux() => global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Linux);

    public static bool IsMacOS() => global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.OSX);
#endif
}

#endif

#if !NET7_0_OR_GREATER

namespace System.Diagnostics
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal sealed class UnreachableException : Exception
    {
        public UnreachableException()
            : base("The program executed an instruction that was thought to be unreachable.")
        {
        }

        public UnreachableException(string? message)
            : base(message)
        {
        }

        public UnreachableException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}

#endif
