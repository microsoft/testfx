// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class represents the live NON public INTERNAL object in the system.
/// </summary>
public partial class PrivateObject
{
    #region Data

    // bind everything
    private const BindingFlags BindToEveryThing = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    private static readonly BindingFlags ConstructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.NonPublic;

    private object _target;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that contains
    /// the already existing object of the private class.
    /// </summary>
    /// <param name="obj"> object that serves as starting point to reach the private members.</param>
    /// <param name="memberToAccess">the de-referencing string using . that points to the object to be retrieved as in m_X.m_Y.m_Z.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj, string memberToAccess)
    {
        _ = obj ?? throw new ArgumentNullException(nameof(obj));
        ValidateAccessString(memberToAccess);

        var temp = obj as PrivateObject;
        temp ??= new PrivateObject(obj);

        // Split The access string
        string[] arr = memberToAccess.Split(['.']);

        for (int i = 0; i < arr.Length; i++)
        {
            object? next = temp.InvokeHelper(arr[i], BindToEveryThing | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty, null, CultureInfo.InvariantCulture);
            DebugEx.Assert(next is not null, "next should not be null");
            temp = new PrivateObject(next);
        }

        _target = temp._target;
        RealType = temp.RealType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="typeName">fully qualified name.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(string assemblyName, string typeName, params object?[]? args)
        : this(assemblyName, typeName, null, args)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="typeName">fully qualified name.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the constructor to get.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(string assemblyName, string typeName, Type[]? parameterTypes, object?[]? args)
        : this(Type.GetType(string.Format(CultureInfo.InvariantCulture, "{0}, {1}", typeName, assemblyName), false), parameterTypes, args)
    {
        _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        _ = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="type">type of the object to create.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(Type type, params object?[]? args)
        : this(type, null, args) =>
        _ = type ?? throw new ArgumentNullException(nameof(type));

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="type">type of the object to create.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the constructor to get.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(Type type, Type[]? parameterTypes, object?[]? args)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));
        object? o;
        if (parameterTypes != null)
        {
            ConstructorInfo ci = type.GetConstructor(BindToEveryThing, null, parameterTypes, null)
                                 ?? throw new ArgumentException(FrameworkMessages.PrivateAccessorConstructorNotFound);
            try
            {
                o = ci.Invoke(args);
            }
            catch (TargetInvocationException e)
            {
                DebugEx.Assert(e.InnerException != null, "Inner exception should not be null.");
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                throw;
            }
        }
        else
        {
            o = Activator.CreateInstance(type, ConstructorFlags, null, args, null);
        }

        _target = o;
        RealType = o.GetType();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
    /// the given object.
    /// </summary>
    /// <param name="obj">object to wrap.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj)
    {
        _target = obj ?? throw new ArgumentNullException(nameof(obj));
        RealType = obj.GetType();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
    /// the given object.
    /// </summary>
    /// <param name="obj">object to wrap.</param>
    /// <param name="type">PrivateType object.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj, PrivateType type)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));
        _target = obj;
        RealType = type.ReferencedType;
    }

    #endregion

    /// <summary>
    /// Gets or sets the target.
    /// </summary>
    public object Target
    {
        get => _target;
        set
        {
            _target = value ?? throw new ArgumentNullException(nameof(value));
            RealType = value.GetType();
        }
    }

    /// <summary>
    /// Gets the type of underlying object.
    /// </summary>
    public Type RealType { get; private set; }

    [field: AllowNull]
    [field: MaybeNull]
    private Dictionary<string, LinkedList<MethodInfo>> GenericMethodCache
    {
        get
        {
            if (field == null)
            {
                BuildGenericMethodCacheForType(RealType);
            }

            DebugEx.Assert(field != null, "Invalid method cache for type.");

            return field;
        }
        set;
    }

    /// <summary>
    /// returns the hash code of the target object.
    /// </summary>
    /// <returns>int representing hashcode of the target object.</returns>
    public override int GetHashCode()
    {
        DebugEx.Assert(_target != null, "target should not be null.");
        return _target.GetHashCode();
    }

    /// <summary>
    /// Equals.
    /// </summary>
    /// <param name="obj">Object with whom to compare.</param>
    /// <returns>returns true if the objects are equal.</returns>
    public override bool Equals(object? obj)
    {
        if (this != obj)
        {
            DebugEx.Assert(_target != null, "target should not be null.");
            return typeof(PrivateObject) == obj?.GetType()
                && _target.Equals(((PrivateObject)obj)._target);
        }

        return true;
    }
}
#endif
