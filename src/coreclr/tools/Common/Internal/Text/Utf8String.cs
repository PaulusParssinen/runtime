// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Numerics;
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
        public Utf8String(string s)
        {
            _value = Encoding.UTF8.GetBytes(s);
        }

        public int Length => _value.Length;

        public byte this[int index] => _value[index];

        public ReadOnlySpan<byte> AsSpan() => _value;

        public override string ToString()
        {
            return Encoding.UTF8.GetString(_value);
        }

        public override bool Equals(object obj)
            => obj is Utf8String utf8String && Equals(utf8String);

        public override unsafe int GetHashCode() => GetHashCode(_value);

        public bool Equals(Utf8String other) => Equals(other.AsSpan());
        public bool Equals(ReadOnlySpan<byte> other) => AsSpan().SequenceEqual(other);

        public int CompareTo(Utf8String other) => Compare(this, other);

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

        private static int Compare(Utf8String strA, Utf8String strB)
        {
            return strA.AsSpan().SequenceCompareTo(strB.AsSpan());
        }

    }
}
