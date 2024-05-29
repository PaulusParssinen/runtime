// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// <see cref="NodeMangler"/> is responsible for producing mangled names for specific nodes
    /// and for node-related purposes, where the name needs to be in a special format
    /// on some platform.
    /// </summary>
    public abstract class NodeMangler
    {
        public NameMangler NameMangler;

        protected const string GenericDictionaryNamePrefix = "__GenericDict_";

        // Mangled name of boxed version of a type
        public abstract void AppendMangledBoxedTypeName(TypeDesc type, ref Utf8StringBuilder sb);

        public abstract void AppendMethodTable(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendGCStatics(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendNonGCStatics(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendThreadStatics(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendThreadStaticsIndex(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendTypeGenericDictionary(TypeDesc type, ref Utf8StringBuilder sb);
        public abstract void AppendMethodGenericDictionary(MethodDesc method, ref Utf8StringBuilder sb);

        public abstract Utf8String ExternMethod(string unmangledName, MethodDesc method);
        public abstract Utf8String ExternVariable(Utf8String unmangledName);
    }
}
