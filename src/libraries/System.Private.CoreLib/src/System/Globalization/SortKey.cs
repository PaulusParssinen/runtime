// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Globalization
{
    /// <summary>Represents the result of mapping a string to its sort key.</summary>
    public sealed partial class SortKey
    {
        private readonly CompareInfo _compareInfo;
        private readonly CompareOptions _options;
        private readonly string _string;
        private readonly byte[] _keyData;

        /// <summary>
        /// The following constructor is designed to be called from CompareInfo to get the
        /// the sort key of specific string for synthetic culture
        /// </summary>
        internal SortKey(CompareInfo compareInfo, string str, CompareOptions options, byte[] keyData)
        {
            _keyData = keyData;
            _compareInfo = compareInfo;
            _options = options;
            _string = str;
        }

        /// <summary>
        /// Returns the original string used to create the current instance
        /// of SortKey.
        /// </summary>
        public string OriginalString => _string;

        /// <summary>
        /// Returns a byte array representing the current instance of the
        /// sort key.
        /// </summary>
        public byte[] KeyData => (byte[])_keyData.Clone();

        /// <summary>
        /// Compares the two sort keys.  Returns 0 if the two sort keys are
        /// equal, a number less than 0 if sortkey1 is less than sortkey2,
        /// and a number greater than 0 if sortkey1 is greater than sortkey2.
        /// </summary>
        public static int Compare(SortKey sortkey1, SortKey sortkey2)
        {
            ArgumentNullException.ThrowIfNull(sortkey1);
            ArgumentNullException.ThrowIfNull(sortkey2);

            byte[] key1Data = sortkey1._keyData;
            byte[] key2Data = sortkey2._keyData;

            Debug.Assert(key1Data != null);
            Debug.Assert(key2Data != null);

            // SortKey comparisons are done as an ordinal comparison by the raw sort key bytes.

            return new ReadOnlySpan<byte>(key1Data).SequenceCompareTo(key2Data);
        }

        public override bool Equals([NotNullWhen(true)] object? value) =>
            value is SortKey other && new ReadOnlySpan<byte>(_keyData).SequenceEqual(other._keyData);

        public override int GetHashCode() => _compareInfo.GetHashCode(_string, _options);

        public override string ToString() => $"SortKey - {_compareInfo.Name}, {_options}, {_string}";
    }
}
