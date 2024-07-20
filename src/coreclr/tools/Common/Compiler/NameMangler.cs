// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// NameMangler is responsible for giving extern C/C++ names to managed types, methods and fields.
    /// The key invariant is that the mangled names are independent on the compilation order.
    /// </summary>
    public abstract class NameMangler
    {
#if !READYTORUN
        public NameMangler(NodeMangler nodeMangler)
        {
            nodeMangler.NameMangler = this;
            NodeMangler = nodeMangler;
        }

        public NodeMangler NodeMangler { get; private set; }
#endif

        /// <summary>
        /// Used in multi-module builds to disambiguate symbols.
        /// </summary>
        public abstract string CompilationUnitPrefix { get; set; }

        public abstract void AppendSanitizedName(ReadOnlySpan<char> s, ref Utf8StringBuilder sb);

        public abstract Utf8String GetMangledTypeName(TypeDesc type);
        public abstract void AppendMangledTypeName(TypeDesc type, ref Utf8StringBuilder sb);

        public abstract Utf8String GetMangledMethodName(MethodDesc method);
        public abstract void AppendMangledMethodName(MethodDesc method, ref Utf8StringBuilder sb);

        public abstract Utf8String GetMangledFieldName(FieldDesc field);
        public abstract void AppendMangledFieldName(FieldDesc field, ref Utf8StringBuilder sb);

        public abstract void AppendMangledStringName(string literal, ref Utf8StringBuilder sb);
    }
}
