// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

public class GetByteCountUtf8
{
    private const string Cjk4 = "鉄鉄鉄鉄";
    private const string Cjk16 = Cjk4 + Cjk4 + Cjk4 + Cjk4;
    private const string Cjk64 = Cjk16 + Cjk16 + Cjk16 + Cjk16;
    private const string Cjk256 = Cjk64 + Cjk64 + Cjk64 + Cjk64;

    private static readonly string s_text = "A\u9244\uD83D\uDE00";
    private static readonly char[] s_chars = ['A', '\uD800'];
    private static int s_sideEffects;

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(4, s_text.Length);
        Assert.Equal(0, GetByteCountEmpty());
        Assert.Equal(5, GetByteCountAscii());
        Assert.Equal(11, GetByteCountSpan());
        Assert.Equal(13, GetByteCountSurrogates());
        Assert.Equal(7, GetByteCountSlice());
        Assert.Equal(3, GetByteCountSplitSurrogate());
        Assert.Equal(8, GetByteCountReadonlyString());
        Assert.Equal(768, GetByteCountLimit());
        Assert.Equal(771, GetByteCountAboveLimit());
        Assert.Equal(0, Encoding.UTF8.GetByteCount(ReadOnlySpan<char>.Empty));
        Assert.Equal(11, GetByteCountVariable("A\u00E9\u9244\uD83D\uDE00\0"));

        s_chars[1] = '\uD800';
        Assert.Equal(4, GetByteCountMutableArray());
        s_chars[1] = 'B';
        Assert.Equal(2, GetByteCountMutableArray());

        s_sideEffects = 0;
        Assert.Equal(7, GetByteCountWithSideEffects());
        Assert.Equal(1, s_sideEffects);

        Assert.Throws<ArgumentNullException>("chars", () => Encoding.UTF8.GetByteCount((string)null));
        Assert.Throws<ArgumentOutOfRangeException>(() => Encoding.UTF8.GetByteCount("hello".AsSpan(1, 5)));

        Encoding cloned = (Encoding)Encoding.UTF8.Clone();
        cloned.EncoderFallback = new EncoderReplacementFallback("!");
        Assert.Equal(1, cloned.GetByteCount("\uD800"));
        Assert.Equal(1, cloned.GetByteCount("\uD800".AsSpan()));

        Encoding strict = new UTF8Encoding(false, true);
        Assert.Throws<EncoderFallbackException>(() => strict.GetByteCount("\uD800"));
        Assert.Throws<EncoderFallbackException>(() => strict.GetByteCount("\uD800".AsSpan()));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountEmpty() => Encoding.UTF8.GetByteCount("");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountAscii() => Encoding.UTF8.GetByteCount("hello");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountSpan() => Encoding.UTF8.GetByteCount("A\u00E9\u9244\uD83D\uDE00\0".AsSpan());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountSurrogates() => Encoding.UTF8.GetByteCount("\uD800a\uDC00\uD800\uD800");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountSlice() => Encoding.UTF8.GetByteCount("x\u9244\uD83D\uDE00y".AsSpan(1, 3));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountSplitSurrogate() => Encoding.UTF8.GetByteCount("x\u9244\uD83D\uDE00y".AsSpan(2, 1));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountReadonlyString() => Encoding.UTF8.GetByteCount(s_text);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountLimit() => Encoding.UTF8.GetByteCount(Cjk256);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountAboveLimit() => Encoding.UTF8.GetByteCount(Cjk256 + "鉄");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountVariable(string text) => Encoding.UTF8.GetByteCount(text);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountMutableArray() => Encoding.UTF8.GetByteCount(s_chars.AsSpan());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetByteCountWithSideEffects() => Encoding.UTF8.GetByteCount("x\u9244\uD83D\uDE00y".AsSpan(1, GetLength()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLength()
    {
        s_sideEffects++;
        return 3;
    }
}
