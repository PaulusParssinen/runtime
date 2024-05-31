// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.Unicode;
using System.Diagnostics.CodeAnalysis;

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

        public readonly int Length => _pos;

        public readonly ReadOnlySpan<byte> AsSpan() => _bytes.Slice(0, _pos);
        public readonly ReadOnlySpan<byte> AsSpan(int start) => _bytes.Slice(start, _pos - start);

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
            while (true)
            {
                if (value.TryCopyTo(_bytes.Slice(_pos)))
                {
                    _pos += value.Length;
                    return;
                }

                Grow(value.Length);
            }
        }

        public void Append(char value)
        {
            Debug.Assert(Ascii.IsValid(value));

            EnsureCapacity(1);
            _bytes[_pos++] = (byte)value;
        }

        public void Append(scoped ReadOnlySpan<char> value)
        {
            if (value.Length == 0) return;

            while (true)
            {
                if (Encoding.UTF8.TryGetBytes(value, _bytes.Slice(_pos), out int bytesWritten))
                {
                    _pos += bytesWritten;
                    return;
                }

                Grow(value.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // we want 'value' exposed to the JIT as a constant
        public void AppendLiteral([ConstantExpected] string value)
        {
            // We only allow ASCII because this method should only receive constant string literals as inputs.
            Debug.Assert(Ascii.IsValid(value));
            Debug.Assert(value is not null);

            while (true)
            {
                Span<byte> buffer = _bytes.Slice(_pos);

                // Use TryGetBytes once it gains UTF8EncodingSealed.ReadUtf8 call
                // which JIT/AOT can optimize away for string literals, eliding the transcoding.
                // Reference: https://github.com/dotnet/runtime/issues/93501
                var inner = new Utf8.TryWriteInterpolatedStringHandler(value.Length, 0, buffer, out _);
                if (inner.AppendLiteral(value))
                {
                    _pos += value.Length;
                    return;
                }

                Grow();
            }
        }

        public void AppendInvariant<T>(T value, ReadOnlySpan<char> format = default)
            where T : IUtf8SpanFormattable
        {
            while (true)
            {
                if (value.TryFormat(_bytes.Slice(_pos), out int bytesWritten, format, CultureInfo.InvariantCulture))
                {
                    _pos += bytesWritten;
                    return;
                }

                Grow();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int additionalBytes)
        {
            if ((uint)(_pos + additionalBytes) > (uint)_bytes.Length)
                Grow(additionalBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // keep consumers as streamlined as possible
        private void Grow(int additionalBytes)
        {
            // This method is called when the remaining space (_bytes.Length - _pos) is
            // insufficient to store a specific number of additional bytes.  Thus, we
            // need to grow to at least that new total. GrowCore will handle growing by more
            // than that if possible.
            Debug.Assert(additionalBytes > _bytes.Length - _pos);
            GrowCore((uint)_pos + (uint)additionalBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // keep consumers as streamlined as possible
        private void Grow()
        {
            // This method is called when the remaining space in _bytes isn't sufficient to continue
            // the operation.  Thus, we need at least one byte beyond _bytes.Length.  GrowCore
            // will handle growing by more than that if possible.
            GrowCore(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // but reuse this grow logic directly in both of the above grow routines
        private void GrowCore(uint requiredMinCapacity)
        {
            Debug.Assert(requiredMinCapacity > 0);

            const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

            // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
            // to double the size if possible, bounding the doubling to not go beyond the max array length.
            int newCapacity = (int)Math.Max(requiredMinCapacity, Math.Min((uint)_bytes.Length * 2, ArrayMaxLength));

            // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
            // This could also go negative if the actual required length wraps around.
            byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCapacity);
            _bytes.Slice(0, _pos).CopyTo(poolArray);

            byte[] toReturn = _arrayToReturnToPool;
            _bytes = _arrayToReturnToPool = poolArray;
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // used only on a few hot paths
        public void Dispose()
        {
            byte[] toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }
    }

    public static class Utf8Extensions
    {
        public static void AppendInterpolated(
            this ref Utf8StringBuilder builder,
            [InterpolatedStringHandlerArgument(nameof(builder))]
            ref Utf8String.InterpolatedStringHandler handler)
        {
            builder = handler._builder;
        }
    }
}
