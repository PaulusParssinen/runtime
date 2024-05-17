// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ILCompiler.DependencyAnalysis;
using ILLink.Shared.TrimAnalysis;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class NativeAotNameMangler : NameMangler
    {
#if !READYTORUN
        public NativeAotNameMangler(NodeMangler nodeMangler) : base(nodeMangler)
        {
        }
#endif

        private string _compilationUnitPrefix;

        public override string CompilationUnitPrefix
        {
            get => _compilationUnitPrefix;
            set { _compilationUnitPrefix = GetSanitizedNameWithHash(value); }
        }

        /// <summary>
        /// Turns a name into a valid C/C++ identifier.
        /// </summary>
        public override void AppendSanitizedName(ReadOnlySpan<char> s, ref Utf8StringBuilder sb)
        {
            // TODO: SearchValue.Create("A-z0-9").IndexOfAnyExcept fast-path?

            int i = 0;
            if (s.Length > 0 && char.IsAsciiDigit(s[0]))
            {
                // C identifiers cannot start with a digit. Prepend underscore.
                sb.Append('_');

                sb.Append(s[0]);
                i++;
            }

            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsAsciiLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else
                {
                    // Everything else is replaced by underscore.
                    // TODO: We assume that there won't be collisions with our own or C++ built-in identifiers.
                    sb.Append('_');
                }
            }

            // The character sequences denoting generic instantiations, arrays, byrefs, or pointers must be
            // restricted to that use only. Replace them if they happened to be used in any identifiers in
            // the compilation input.
        }

        private string GetSanitizedNameWithHash(string literal)
        {
            var sb = new Utf8StringBuilder(stackalloc byte[64]);
            AppendSanitizedNameWithHash(literal, ref sb);
            return sb.ToStringAndDispose();
        }
        private void AppendSanitizedNameWithHash(string literal, ref Utf8StringBuilder sb)
        {
            // Track the sanitized name length so we can truncate it..
            int sanitizedNameStart = sb.Length;

            AppendSanitizedName(literal, ref sb);

            int sanitizedNameLength = sb.Length - sanitizedNameStart;
            if (sanitizedNameLength > 30)
                sb.Truncate(newLength: sanitizedNameStart + 30);

            // If the name was changed during sanitization, append SHA-256 hash of the original name to the sanitized name
            if (sb.AsSpan(sanitizedNameStart).SequenceEqual(Encoding.UTF8.GetBytes(literal)))
            {
                Span<byte> hashBuffer = stackalloc byte[SHA256.HashSizeInBytes];

                // Use SHA256 hash here to provide a high degree of uniqueness to symbol names without requiring them to be long
                // This hash function provides an exceedingly high likelihood that no two strings will be given equal symbol names
                // This is not considered used for security purpose; however collisions would be highly unfortunate as they will cause compilation
                // failure.
                bool success = SHA256.TryHashData(MemoryMarshal.AsBytes(literal.AsSpan()), hashBuffer, out _);
                Debug.Assert(success);

                sb.Append('_');
                sb.Append(Convert.ToHexString(hashBuffer));
            }
        }

        /// <summary>
        /// Dictionary given a mangled name for a given <see cref="TypeDesc"/>
        /// </summary>
        private readonly Dictionary<TypeDesc, Utf8String> _mangledTypeNames = new Dictionary<TypeDesc, Utf8String>();

        /// <summary>
        /// Given a set of <paramref name="nameCounts"/> check if hash of <paramref name="name"/>
        /// is unique, if not add a numbered suffix until it becomes unique and append the name to <paramref name="sb"/>.
        /// </summary>
        /// <remarks>
        /// The hashing is done with <see cref="Utf8String.GetHashCode(ReadOnlySpan{byte})"/>
        /// </remarks>
        /// <param name="name">Name as UTF-8 bytes to disambiguate.</param>
        /// <param name="nameCounts">The dictionary mapping a hash of name to number of times it has been encountered.</param>
        /// <param name="sb">The string builder to append the number suffix to.</param>
        private static void AppendDisambiguatingNameSuffix(ReadOnlySpan<byte> name, Dictionary<int, int> nameCounts, ref Utf8StringBuilder sb)
        {
            int nameHash = Utf8String.GetHashCode(name);

            // Try to insert the name into the deduplication dictionary with default value (0)
            ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(nameCounts, nameHash, out bool exists);

            // If the name was in the dictionary we append the zero-based number suffix and increment the count by reference.
            if (exists)
            {
                sb.Append('_');
                sb.AppendInvariant(count++);
            }
        }

        private const char EnterNameScopeSequence = '<';
        private const char ExitNameScopeSequence = '>';
        private const char DelimitNameScopeSequence = ',';

        protected void AppendNestedMangledName(string name, ref Utf8StringBuilder sb)
        {
            sb.Append(EnterNameScopeSequence);
            sb.Append(name);
            sb.Append(ExitNameScopeSequence);
        }

        /// <summary>
        /// Appends the namespace qualified sanitized name of a <see cref="DefType"/> to the builder.
        /// </summary>
        private void AppendSanitizedFullName(DefType metadataType, ref Utf8StringBuilder sb)
        {
            string ns = metadataType.Namespace;
            if (ns.Length > 0)
            {
                AppendSanitizedName(ns, ref sb);
                sb.Append('_');
            }
            AppendSanitizedName(metadataType.Name, ref sb);
        }

        private void AppendEncapsulatingTypeName(DefType type, ref Utf8StringBuilder sb)
        {
            DefType containingType = type.ContainingType;
            if (type.ContainingType is not null)
            {
                AppendEncapsulatingTypeName(containingType, ref sb);
                sb.Append('_');
            }
            AppendSanitizedFullName(type, ref sb);
        }

        public override Utf8String GetMangledTypeName(TypeDesc type)
        {
            var sb = new Utf8StringBuilder(stackalloc byte[256]);
            AppendMangledTypeName(type, ref sb);
            return sb.ToUtf8StringAndDispose();
        }

        /// <summary>
        /// If given <param name="type"/> is an <see cref="EcmaType"/> precompute its mangled type name
        /// along with all the other types from the same module as <paramref name="type"/>.
        /// <para/>
        /// Otherwise, it is a constructed type and to the <see cref="EcmaType"/>'s mangled name we add a suffix or prefix to
        /// show what kind of constructed type it is (e.g. prepending <c>__Array</c> for an array type).
        /// </summary>
        /// <param name="type">Type to mangle</param>
        /// <param name="sb">The string builder to append the mangled type name to.</param>
        public override void AppendMangledTypeName(TypeDesc type, ref Utf8StringBuilder sb)
        {
            lock (this)
            {
                if (_mangledTypeNames.TryGetValue(type, out Utf8String mangledName))
                {
                    sb.Append(mangledName);
                    return;
                }
            }

            if (type is EcmaType ecmaType)
            {
                // Create temporary builder for the module name mangling
                var moduleNameBuilder = new Utf8StringBuilder(stackalloc byte[256]);

                ReadOnlySpan<char> assemblyName = ((EcmaAssembly)ecmaType.EcmaModule).GetName().Name;
                bool isSystemPrivate = assemblyName.StartsWith("System.Private.", StringComparison.Ordinal);

                // Abbreviate System.Private to S.P. This might conflict with user defined assembly names,
                // but we already have a problem due to running SanitizeName without disambiguating the result
                // This problem needs a better fix.
                if (isSystemPrivate)
                {
                    moduleNameBuilder.Append("S_P_"u8);
                    assemblyName = assemblyName.Slice(15);
                }

                AppendSanitizedName(assemblyName, ref moduleNameBuilder);
                moduleNameBuilder.Append('_');

                int assemblyNameEnd = sb.Length;

                var nameCounts = new Dictionary<int, int>();

                // Add consistent names for all types in the module, independent on the order in which
                // they are compiled
                lock (this)
                {
                    bool isSystemModule = ecmaType.Module == ecmaType.Context.SystemModule;

                    foreach (MetadataType moduleType in ecmaType.EcmaModule.GetAllTypes())
                    {
                        // If this is one of the well known types, use a shorter name.
                        if (isSystemModule)
                        {
                            ReadOnlySpan<byte> wellKnownName = moduleType.Category switch
                            {
                                TypeFlags.Boolean => "Bool"u8,
                                TypeFlags.Byte => "UInt8"u8,
                                TypeFlags.SByte => "Int8"u8,
                                TypeFlags.UInt16 => "UInt16"u8,
                                TypeFlags.Int16 => "Int16"u8,
                                TypeFlags.UInt32 => "UInt32"u8,
                                TypeFlags.Int32 => "Int32"u8,
                                TypeFlags.UInt64 => "UInt64"u8,
                                TypeFlags.Int64 => "Int64"u8,
                                TypeFlags.Char => "Char"u8,
                                TypeFlags.Double => "Double"u8,
                                TypeFlags.Single => "Single"u8,
                                TypeFlags.IntPtr => "IntPtr"u8,
                                TypeFlags.UIntPtr => "UIntPtr"u8,
                                _ when !moduleType.IsObject => "String"u8,

                                _ => "Object"u8
                            };

                            // We know this won't conflict because all the other types are
                            // prefixed by the assembly name.
                            _mangledTypeNames.Add(moduleType, new Utf8String(wellKnownName.ToArray()));
                        }
                        else
                        {
                            // Include encapsulating type
                            AppendEncapsulatingTypeName(moduleType, ref moduleNameBuilder);

                            // Ensure that name is unique by prepending a number suffix if needed and update our tables accordingly.
                            AppendDisambiguatingNameSuffix(moduleNameBuilder.AsSpan(assemblyNameEnd), nameCounts, ref moduleNameBuilder);

                            _mangledTypeNames.Add(moduleType, moduleNameBuilder.ToUtf8String());

                            // Truncate to the assembly name
                            sb.Truncate(newLength: assemblyNameEnd);
                        }
                    }

                    moduleNameBuilder.Dispose();

                    // We're done precomputing module mangled names, append the wanted name.
                    if (_mangledTypeNames.TryGetValue(type, out Utf8String mangledName))
                    {
                        sb.Append(mangledName);
                        return;
                    };
                }
            }

            switch (type)
            {
                case ArrayType arrayType when type.Category is TypeFlags.Array:
                    sb.Append("__MDArray"u8);

                    sb.Append(EnterNameScopeSequence);
                    AppendMangledTypeName(arrayType.ElementType, ref sb);
                    sb.Append(DelimitNameScopeSequence);
                    sb.AppendInvariant(arrayType.Rank);
                    sb.Append(ExitNameScopeSequence);
                    break;
                case ArrayType arrayType when type.Category is TypeFlags.SzArray:
                    sb.Append("__Array"u8);
                    sb.Append(EnterNameScopeSequence);
                    AppendMangledTypeName(arrayType.ElementType, ref sb);
                    sb.Append(ExitNameScopeSequence);
                    break;
                case ByRefType byRefType:
                    AppendMangledTypeName(byRefType.ParameterType, ref sb);
                    sb.Append("<ByRef>"u8);
                    break;
                case PointerType pointerType:
                    AppendMangledTypeName(pointerType.ParameterType, ref sb);
                    sb.Append("<Pointer>"u8);
                    break;
                case FunctionPointerType fnPtrType:
                    sb.Append("__FnPtr_"u8);
                    sb.AppendInvariant((int)fnPtrType.Signature.Flags, format: "X2");
                    sb.Append(EnterNameScopeSequence);
                    AppendMangledTypeName(fnPtrType.Signature.ReturnType, ref sb);

                    sb.Append(EnterNameScopeSequence);
                    for (int i = 0; i < fnPtrType.Signature.Length; i++)
                    {
                        if (i != 0)
                            sb.Append(DelimitNameScopeSequence);
                        AppendMangledTypeName(fnPtrType.Signature[i], ref sb);
                    }
                    sb.Append(ExitNameScopeSequence);

                    sb.Append(ExitNameScopeSequence);
                    break;
                case IPrefixMangledMethod prefixMangledMethod:
                    AppendPrefixMangledMethodName(prefixMangledMethod, ref sb);
                    break;
                case IPrefixMangledType prefixMangledType:
                    AppendPrefixMangledTypeName(prefixMangledType, ref sb);
                    break;
                default:
                    // Case of a generic type. If `type' is a type definition we use the type name
                    // for mangling, otherwise we use the mangling of the type and its generic type
                    // parameters, e.g. A <B> becomes A_<___B_>_.
                    TypeDesc typeDefinition = type.GetTypeDefinition();
                    if (typeDefinition != type)
                    {
                        AppendMangledTypeName(typeDefinition, ref sb);

                        sb.Append(EnterNameScopeSequence);

                        Instantiation inst = type.Instantiation;
                        for (int i = 0; i < inst.Length; i++)
                        {
                            if (i > 0)
                                sb.Append("__"u8);

                            AppendMangledTypeName(inst[i], ref sb);
                        }

                        sb.Append(ExitNameScopeSequence);
                    }
                    else
                    {
                        // This is a type definition. Since we didn't fall in the `is EcmaType` case above,
                        // it's likely a compiler-generated type.
                        AppendSanitizedFullName((DefType)type, ref sb);
                    }
                    break;
            }

            lock (this)
            {
                // Ensure that name is unique and update our tables accordingly
                _mangledTypeNames.TryAdd(type, sb.ToUtf8String());
            }
        }

        private readonly Dictionary<MethodDesc, Utf8String> _mangledMethodNames = new Dictionary<MethodDesc, Utf8String>();
        private readonly Dictionary<MethodDesc, Utf8String> _unqualifiedMangledMethodNames = new Dictionary<MethodDesc, Utf8String>();

        public override Utf8String GetMangledMethodName(MethodDesc method)
        {
            var sb = new Utf8StringBuilder(stackalloc byte[256]);
            AppendMangledMethodName(method, ref sb);
            return sb.ToUtf8StringAndDispose();
        }

        public override void AppendMangledMethodName(MethodDesc method, ref Utf8StringBuilder sb)
        {
            lock (this)
            {
                if (_mangledMethodNames.TryGetValue(method, out Utf8String utf8MangledName))
                {
                    sb.Append(utf8MangledName);
                    return;
                }
            }

            AppendMangledTypeName(method.OwningType, ref sb);
            sb.Append("__"u8);
            AppendUnqualifiedMangledMethodName(method, ref sb);

            lock (this)
            {
                _mangledMethodNames.TryAdd(method, sb.ToUtf8String());
            }
        }

        private void AppendPrefixMangledTypeName(IPrefixMangledType prefixMangledType, ref Utf8StringBuilder sb)
        {
            AppendNestedMangledName(prefixMangledType.Prefix, ref sb);
            AppendMangledTypeName(prefixMangledType.BaseType, ref sb);
        }

        private void AppendPrefixMangledSignatureName(IPrefixMangledSignature prefixMangledSignature, ref Utf8StringBuilder sb)
        {
            AppendNestedMangledName(prefixMangledSignature.Prefix, ref sb);

            var signature = prefixMangledSignature.BaseSignature;
            sb.AppendInvariant((int)signature.Flags);

            sb.Append(EnterNameScopeSequence);

            AppendMangledTypeName(signature.ReturnType, ref sb);

            for (int i = 0; i < signature.Length; i++)
            {
                sb.Append("__"u8);
                AppendMangledTypeName(signature[i], ref sb); // sigArgName
            }

            sb.Append(ExitNameScopeSequence);
        }

        private void AppendPrefixMangledMethodName(IPrefixMangledMethod prefixMangledMethod, ref Utf8StringBuilder sb)
        {
            AppendNestedMangledName(prefixMangledMethod.Prefix, ref sb);
            AppendMangledMethodName(prefixMangledMethod.BaseMethod, ref sb);
        }

        private void AppendUnqualifiedMangledMethodName(MethodDesc method, ref Utf8StringBuilder sb)
        {
            lock (this)
            {
                if (_unqualifiedMangledMethodNames.TryGetValue(method, out Utf8String mangledName))
                {
                    sb.Append(mangledName);
                    return;
                }
            }

            if (method is EcmaMethod)
            {
                // Create temporary builder for the owning type method name mangling
                var methodNameBuilder = new Utf8StringBuilder(stackalloc byte[256]);

                var nameCounts = new Dictionary<int, int>();

                // Add consistent names for all methods of the type, independent on the order in which
                // they are compiled
                lock (this)
                {
                    foreach (MethodDesc m in method.OwningType.GetMethods())
                    {
                        AppendSanitizedName(m.Name, ref methodNameBuilder);
                        AppendDisambiguatingNameSuffix(methodNameBuilder.AsSpan(), nameCounts, ref methodNameBuilder);

                        _unqualifiedMangledMethodNames.Add(m, methodNameBuilder.ToUtf8String());

                        methodNameBuilder.Clear();
                    }

                    // We're done precomputing method names, append the wanted mangled name.
                    if (_unqualifiedMangledMethodNames.TryGetValue(method, out Utf8String mangledName))
                    {
                        sb.Append(mangledName);
                        return;
                    };
                }
            }

            var methodDefinition = method.GetMethodDefinition();
            if (methodDefinition != method)
            {
                // Instantiated generic method
                AppendUnqualifiedMangledMethodName(methodDefinition.GetTypicalMethodDefinition(), ref sb);

                sb.Append(EnterNameScopeSequence);

                Instantiation inst = method.Instantiation;
                for (int i = 0; i < inst.Length; i++)
                {
                    if (i > 0)
                        sb.Append("__"u8);
                    AppendMangledTypeName(inst[i], ref sb); // instArgName
                }

                sb.Append(ExitNameScopeSequence);;
            }
            else
            {
                var typicalMethodDefinition = method.GetTypicalMethodDefinition();
                if (typicalMethodDefinition != method)
                {
                    // Method on an instantiated type
                    AppendUnqualifiedMangledMethodName(typicalMethodDefinition, ref sb);
                }
                else if (method is IPrefixMangledMethod prefixMangledMethod)
                {
                    AppendPrefixMangledMethodName(prefixMangledMethod, ref sb);
                }
                else if (method is IPrefixMangledType prefixMangledType)
                {
                    AppendPrefixMangledTypeName(prefixMangledType, ref sb);
                }
                else if (method is IPrefixMangledSignature prefixMangledSig)
                {
                    AppendPrefixMangledSignatureName(prefixMangledSig, ref sb);
                }
                else
                {
                    // Assume that Name is unique for all other methods
                    AppendSanitizedName(method.Name, ref sb);
                }
            }
        }

        private readonly Dictionary<FieldDesc, Utf8String> _mangledFieldNames = new Dictionary<FieldDesc, Utf8String>();

        public override Utf8String GetMangledFieldName(FieldDesc field)
        {
            var sb = new Utf8StringBuilder(stackalloc byte[256]);
            AppendMangledFieldName(field, ref sb);
            return sb.ToUtf8StringAndDispose();
        }

        public override void AppendMangledFieldName(FieldDesc field, ref Utf8StringBuilder sb)
        {
            lock (this)
            {
                if (_mangledFieldNames.TryGetValue(field, out Utf8String mangledName))
                {
                    sb.Append(mangledName);
                    return;
                }
            }

            if (field is EcmaField)
            {
                // Create temporary builder for the owning type's field name mangling
                var fieldNameBuilder = new Utf8StringBuilder(stackalloc byte[256]);

                AppendMangledTypeName(field.OwningType, ref fieldNameBuilder); // TODO: Was prependTypeName. This could be null? I don't think so..
                sb.Append("__"u8);

                var nameCounts = new Dictionary<int, int>();

                int prependedTypeNameEnd = sb.Length;

                // Add consistent names for all fields of the type, independent on the order in which
                // they are compiled
                lock (this)
                {
                    foreach (FieldDesc f in field.OwningType.GetFields())
                    {
                        AppendSanitizedName(f.Name, ref fieldNameBuilder);
                        AppendDisambiguatingNameSuffix(fieldNameBuilder.AsSpan(prependedTypeNameEnd), nameCounts, ref fieldNameBuilder);

                        _mangledFieldNames.Add(f, fieldNameBuilder.ToUtf8String());

                        // Truncate back to the prepended type name prefix
                        fieldNameBuilder.Truncate(newLength: prependedTypeNameEnd);
                    }

                    // We're done precomputing owning type's mangled field names, append the wanted field name.
                    if (_mangledFieldNames.TryGetValue(field, out Utf8String mangledName))
                    {
                        sb.Append(mangledName);
                        return;
                    };
                }
            }

            AppendMangledTypeName(field.OwningType, ref sb); // prependTypeName TODO: This could previously be null?

            // TODO: if (prependTypeName != null)
            {
                sb.Append("__"u8);
                AppendSanitizedName(field.Name, ref sb);
            }

            lock (this)
            {
                _mangledFieldNames.TryAdd(field, sb.ToUtf8String());
            }
        }

        private readonly Dictionary<string, Utf8String> _mangledStringLiterals = new Dictionary<string, Utf8String>();

        public override void AppendMangledStringName(string literal, ref Utf8StringBuilder sb)
        {
            lock (this)
            {
                if (_mangledStringLiterals.TryGetValue(literal, out Utf8String mangledName))
                {
                    sb.Append(mangledName);
                    return;
                }
            }

            AppendSanitizedNameWithHash(literal, ref sb);

            lock (this)
            {
                _mangledStringLiterals.TryAdd(literal, sb.ToUtf8String());
            }
        }
    }
}
