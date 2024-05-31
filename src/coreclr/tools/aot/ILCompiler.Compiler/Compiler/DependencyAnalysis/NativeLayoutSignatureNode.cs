// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a native layout signature. A signature is a pair where the first item is a pointer
    /// to the TypeManager that contains the native layout info blob of interest, and the second item
    /// is an offset into that native layout info blob
    /// </summary>
    public class NativeLayoutSignatureNode : DehydratableObjectNode, ISymbolDefinitionNode
    {
        private TypeSystemEntity _identity;
        private Utf8String _identityPrefix;
        private NativeLayoutSavedVertexNode _nativeSignature;

        public TypeSystemEntity Identity => _identity;

        public NativeLayoutSignatureNode(NativeLayoutSavedVertexNode nativeSignature, TypeSystemEntity identity, Utf8String identityPrefix)
        {
            _nativeSignature = nativeSignature;
            _identity = identity;
            _identityPrefix = identityPrefix;
        }

        public void AppendMangledName(NameMangler nameMangler, ref Utf8StringBuilder sb)
        {
            sb.AppendInterpolated($"{nameMangler.CompilationUnitPrefix}{_identityPrefix}");

            if (_identity is MethodDesc method)
            {
                nameMangler.AppendMangledMethodName(method, ref sb);
            }
            else if (_identity is TypeDesc type)
            {
                nameMangler.AppendMangledTypeName(type, ref sb);
            }
            else if (_identity is FieldDesc field)
            {
                nameMangler.AppendMangledFieldName(field, ref sb);
            }
            else
            {
                Debug.Assert(false);
                sb.AppendLiteral("unknown");
            }
        }

        public int Offset => 0;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        protected override ObjectNodeSection GetDehydratedSection(NodeFactory factory)
        {
            if (factory.Target.IsWindows)
                return ObjectNodeSection.ReadOnlyDataSection;
            else
                return ObjectNodeSection.DataSection;
        }
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(new DependencyListEntry(_nativeSignature, "NativeLayoutSignatureNode target vertex"));
            return dependencies;
        }

        protected override ObjectData GetDehydratableData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            // Ensure native layout is saved to get valid Vertex offsets
            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter(factory);

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);

            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            objData.EmitPointerReloc(factory.TypeManagerIndirection);
            objData.EmitNaturalInt(_nativeSignature.SavedVertex.VertexOffset);

            return objData.ToObjectData();
        }

        public override int ClassCode => 1887049331;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            NativeLayoutSignatureNode otherSignature = (NativeLayoutSignatureNode)other;
            if (_identity is MethodDesc)
            {
                if (otherSignature._identity is TypeDesc || otherSignature._identity is FieldDesc)
                    return -1;
                return comparer.Compare((MethodDesc)_identity, (MethodDesc)((NativeLayoutSignatureNode)other)._identity);
            }
            else if (_identity is TypeDesc)
            {
                if (otherSignature._identity is MethodDesc)
                    return 1;

                if (otherSignature._identity is FieldDesc)
                    return -1;

                return comparer.Compare((TypeDesc)_identity, (TypeDesc)((NativeLayoutSignatureNode)other)._identity);
            }
            else if (_identity is FieldDesc)
            {
                if (otherSignature._identity is MethodDesc || otherSignature._identity is TypeDesc)
                    return 1;
                return comparer.Compare((FieldDesc)_identity, (FieldDesc)((NativeLayoutSignatureNode)other)._identity);
            }
            else
            {
                throw new NotSupportedException("New type system entity needs a comparison");
            }
        }
    }
}
