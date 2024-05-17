// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Globalization;

namespace Internal.Text
{
    // TODO: Mark this IDisposable when we get ref struct interfaces
    public ref struct Utf8StringBuilder
    {
        private byte[] _arrayToReturnToPool;
        private Span<byte> _bytes;
        private int _pos;

        public Utf8StringBuilder(Span<byte> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _bytes = initialBuffer;
            _pos = 0;
        }

        public Utf8StringBuilder(int initialCapacity)
        {
            _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _bytes = _arrayToReturnToPool;
            _pos = 0;
        }

        public readonly int Length => _pos;

        public readonly ReadOnlySpan<byte> AsSpan() => _bytes.Slice(0, _pos);
        public readonly ReadOnlySpan<byte> AsSpan(int start) => _bytes.Slice(start, _pos - start);
        public readonly ReadOnlySpan<byte> AsSpan(int start, int length) => _bytes.Slice(start, length);

        public void Clear()
        {
            _pos = 0;
        }

        public void Truncate(int newLength)
        {
            Debug.Assert(newLength <= _pos);
            _pos = newLength;
        }

        public void Append(Utf8String value)
        {
            Append(value.AsSpan());
        }

        public void Append(scoped ReadOnlySpan<byte> value)
        {
            EnsureCapacity(value.Length);

            value.CopyTo(_bytes.Slice(_pos));
            _pos += value.Length;
        }

        public void Append(char value)
        {
            Debug.Assert(Ascii.IsValid(value));

            EnsureCapacity(1);
            _bytes[_pos++] = (byte)value;
        }

        public void Append(scoped ReadOnlySpan<char> value)
        {
            int length = Encoding.UTF8.GetByteCount(value);
            EnsureCapacity(length);

            Encoding.UTF8.GetBytes(value, _bytes.Slice(_pos));
            _pos += length;
        }

        public void AppendInvariant<T>(T value, string format = null)
            where T : IUtf8SpanFormattable, IFormattable
        {
            var invariantCulture = CultureInfo.InvariantCulture;
            if (value.TryFormat(_bytes.Slice(_pos), out int bytesWritten, format, invariantCulture))
            {
                _pos += bytesWritten;
            }
            else
            {
                Append(value.ToString(format, invariantCulture));
            }
        }

        public override readonly string ToString()
        {
            return Encoding.UTF8.GetString(AsSpan());
        }
        public string ToStringAndDispose()
        {
            string value = ToString();
            Dispose();

            return value;
        }

        public readonly Utf8String ToUtf8String()
        {
            return new Utf8String(AsSpan().ToArray());
        }
        public Utf8String ToUtf8StringAndDispose()
        {
            Utf8String value = ToUtf8String();
            Dispose();

            return value;
        }

        private void EnsureCapacity(int extraSpace)
        {
            if ((uint)(_pos + extraSpace) > (uint)_bytes.Length)
                Grow(extraSpace);
        }

        /// <summary>
        /// Resize the internal buffer either by doubling current buffer size or
        /// by adding <paramref name="additionalCapacityBeyondPos"/> to
        /// <see cref="_pos"/> whichever is greater.
        /// </summary>
        /// <param name="additionalCapacityBeyondPos">
        /// Number of bytes requested beyond current position.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            Debug.Assert(additionalCapacityBeyondPos > 0);
            Debug.Assert(_pos > _bytes.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

            const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

            // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
            // to double the size if possible, bounding the doubling to not go beyond the max array length.
            int newCapacity = (int)Math.Max(
                (uint)(_pos + additionalCapacityBeyondPos),
                Math.Min((uint)_bytes.Length * 2, ArrayMaxLength));

            // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
            // This could also go negative if the actual required length wraps around.
            byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCapacity);

            _bytes.Slice(0, _pos).CopyTo(poolArray);

            byte[] toReturn = _arrayToReturnToPool;
            _bytes = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            byte[] toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }
    }
}
