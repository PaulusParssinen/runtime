// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a type in the
    /// DependencyAnalysis infrastructure during compilation.
    /// </summary>
    public sealed class ExternEETypeSymbolNode : ExternSymbolNode, IEETypeNode
    {
        private TypeDesc _type;

        public ExternEETypeSymbolNode(NodeFactory factory, TypeDesc type)
            : base(GetMangledMethodTableName(factory, type))
        {
            _type = type;

            factory.TypeSystemContext.EnsureLoadableType(type);
        }

        public TypeDesc Type => _type;

        // TODO: ..
        public static Utf8String GetMangledMethodTableName(NodeFactory factory, TypeDesc type)
        {
            var sb = new Utf8StringBuilder(stackalloc byte[256]);
            factory.NameMangler.NodeMangler.AppendMethodTable(type, ref sb);
            return sb.ToUtf8StringAndDispose();
        }
    }
}
