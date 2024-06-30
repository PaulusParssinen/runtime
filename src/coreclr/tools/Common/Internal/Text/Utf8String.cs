// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Internal.Text
{
    public readonly struct Utf8String : IEquatable<Utf8String>, IComparable<Utf8String>
    {
        private readonly byte[] _value;

        public Utf8String(byte[] underlyingArray)
        {
            _value = underlyingArray;
        }
        public Utf8String(ReadOnlySpan<byte> span)
        {
            _value = span.ToArray();
        }
        public Utf8String(string s)
        {
            _value = Encoding.UTF8.GetBytes(s);
        }

        public int Length => _value.Length;

        public byte this[int index] => _value[index];

        public ReadOnlySpan<byte> AsSpan() => _value;

        public bool StartsWith(char value) => _value.Length > 0 && _value[0] == (byte)value;

        public override string ToString()
        {
            return Encoding.UTF8.GetString(_value);
        }

        public override bool Equals(object obj)
            => obj is Utf8String utf8String && Equals(utf8String);

        public override unsafe int GetHashCode() => GetHashCode(_value);

        public bool Equals(Utf8String other) => Equals(other.AsSpan());
        public bool Equals(ReadOnlySpan<byte> other) => AsSpan().SequenceEqual(other);

        public int CompareTo(Utf8String other) => AsSpan().SequenceCompareTo(other.AsSpan());

        public static bool operator ==(Utf8String left, Utf8String right) => left.Equals(right);
        public static bool operator !=(Utf8String left, Utf8String right) => !(left == right);

        public static unsafe int GetHashCode(ReadOnlySpan<byte> utf8StringSpan)
        {
            int length = utf8StringSpan.Length;
            uint hash = (uint)length;
            fixed (byte* ap = utf8StringSpan)
            {
                byte* a = ap;

                while (length >= 4)
                {
                    hash = (hash + BitOperations.RotateLeft(hash, 5)) ^ *(uint*)a;
                    a += 4; length -= 4;
                }
                if (length >= 2)
                {
                    hash = (hash + BitOperations.RotateLeft(hash, 5)) ^ *(ushort*)a;
                    a += 2; length -= 2;
                }
                if (length > 0)
                {
                    hash = (hash + BitOperations.RotateLeft(hash, 5)) ^ *a;
                }
                hash += BitOperations.RotateLeft(hash, 7);
                hash += BitOperations.RotateLeft(hash, 15);
                return (int)hash;
            }
        }

        public static Utf8String Create(scoped Span<byte> initialBuffer,
            [InterpolatedStringHandlerArgument(nameof(initialBuffer))] scoped ref InterpolatedStringHandler handler)
        {
            return handler.ToUtf8StringAndDispose();
        }

        [InterpolatedStringHandler]
        public ref struct InterpolatedStringHandler
        {
            internal Utf8StringBuilder _builder;

            public InterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                scoped ref Utf8StringBuilder builder)
            {
                _builder = builder;
            }
            public InterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                Span<byte> initialBuffer)
            {
                _builder = new Utf8StringBuilder(initialBuffer);
            }

            public Utf8String ToUtf8StringAndDispose() => _builder.ToUtf8StringAndDispose();

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // we want 'value' exposed to the JIT as a constant
            public void AppendLiteral([ConstantExpected] string value) => _builder.AppendLiteral(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // streamline the call to builder
            public void AppendFormatted<T>(T value)
            {
                if (typeof(T) == typeof(char))
                {
                    _builder.Append((char)(object)value);
                }
                else if (typeof(T) == typeof(Utf8String))
                {
                    _builder.Append((Utf8String)(object)value);
                }
                else if (value is IUtf8SpanFormattable)
                {
                    _builder.AppendInvariant((IUtf8SpanFormattable)(object)value, default);
                }
                else
                {
                    // Fall back to object?.ToString
                    _builder.Append(value?.ToString());
                }
            }

            public void AppendFormatted<T>(T value, string format) where T : IUtf8SpanFormattable => _builder.AppendInvariant(value, format);

            public void AppendFormatted(scoped ReadOnlySpan<char> value) => _builder.Append(value);
            public void AppendFormatted(scoped ReadOnlySpan<byte> value) => _builder.Append(value);

            public void AppendFormatted(string value) => AppendFormatted(value.AsSpan());
        }
    }
}
