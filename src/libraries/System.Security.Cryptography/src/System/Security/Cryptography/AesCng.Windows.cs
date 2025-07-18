// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// This file is one of a group of files (AesCng.cs, TripleDESCng.cs) that are almost identical except
// for the algorithm name. If you make a change to this file, there's a good chance you'll have to make
// the same change to the other files so please check. This is a pain but given that the contracts demand
// that each of these derive from a different class, it can't be helped.
//

using System.Runtime.Versioning;
using Internal.Cryptography;
using Internal.NativeCrypto;

namespace System.Security.Cryptography
{
    public sealed class AesCng : Aes, ICngSymmetricAlgorithm
    {
        private CngKey? _key;

        [SupportedOSPlatform("windows")]
        public AesCng()
        {
            _core = new CngSymmetricAlgorithmCore(this);
        }

        [SupportedOSPlatform("windows")]
        public AesCng(string keyName)
            : this(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider)
        {
        }

        [SupportedOSPlatform("windows")]
        public AesCng(string keyName, CngProvider provider)
            : this(keyName, provider, CngKeyOpenOptions.None)
        {
        }

        [SupportedOSPlatform("windows")]
        public AesCng(string keyName, CngProvider provider, CngKeyOpenOptions openOptions)
        {
            _core = new CngSymmetricAlgorithmCore(this, keyName, provider, openOptions);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="AesCng"/> class with the specified <see cref="CngKey"/>.
        /// </summary>
        /// <param name="key">
        ///   The key that will be used as input to the cryptographic operations performed by the current object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="CryptographicException">
        ///   <para>
        ///     <paramref name="key"/> does not represent an AES key.
        ///   </para>
        ///   <para> -or- </para>
        ///   <para>
        ///     An error occured while performing a cryptographic operation.
        ///   </para>
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        ///   Cryptography Next Generation (CNG) is not supported on this system.
        /// </exception>
        [SupportedOSPlatform("windows")]
        public AesCng(CngKey key)
        {
            ArgumentNullException.ThrowIfNull(key);

            CngKey duplicate = CngHelpers.Duplicate(key.HandleNoDuplicate, key.IsEphemeral);
            _core = new CngSymmetricAlgorithmCore(this, duplicate);
            _key = duplicate;
        }

        public override byte[] Key
        {
            get
            {
                return _core.GetKeyIfExportable();
            }
            set
            {
                _core.SetKey(value);
            }
        }

        public override int KeySize
        {
            get
            {
                return base.KeySize;
            }

            set
            {
                _core.SetKeySize(value, this);
            }
        }

        public override ICryptoTransform CreateDecryptor()
        {
            // Do not change to CreateDecryptor(this.Key, this.IV). this.Key throws if a non-exportable hardware key is being used.
            return _core.CreateDecryptor();
        }

        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIV)
        {
            return _core.CreateDecryptor(rgbKey, rgbIV);
        }

        public override ICryptoTransform CreateEncryptor()
        {
            // Do not change to CreateEncryptor(this.Key, this.IV). this.Key throws if a non-exportable hardware key is being used.
            return _core.CreateEncryptor();
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIV)
        {
            return _core.CreateEncryptor(rgbKey, rgbIV);
        }

        public override void GenerateKey()
        {
            _core.GenerateKey();
        }

        public override void GenerateIV()
        {
            _core.GenerateIV();
        }

        protected override bool TryDecryptEcbCore(
            ReadOnlySpan<byte> ciphertext,
            Span<byte> destination,
            PaddingMode paddingMode,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv: default,
                encrypting: false,
                CipherMode.ECB,
                feedbackSizeInBits: 0);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotDecrypt(cipher, paddingMode, ciphertext, destination, out bytesWritten);
            }
        }

        protected override bool TryEncryptEcbCore(
            ReadOnlySpan<byte> plaintext,
            Span<byte> destination,
            PaddingMode paddingMode,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv: default,
                encrypting: true,
                CipherMode.ECB,
                feedbackSizeInBits: 0);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotEncrypt(cipher, paddingMode, plaintext, destination, out bytesWritten);
            }
        }

        protected override bool TryEncryptCbcCore(
            ReadOnlySpan<byte> plaintext,
            ReadOnlySpan<byte> iv,
            Span<byte> destination,
            PaddingMode paddingMode,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv,
                encrypting: true,
                CipherMode.CBC,
                feedbackSizeInBits: 0);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotEncrypt(cipher, paddingMode, plaintext, destination, out bytesWritten);
            }
        }

        protected override bool TryDecryptCbcCore(
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> iv,
            Span<byte> destination,
            PaddingMode paddingMode,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv,
                encrypting: false,
                CipherMode.CBC,
                feedbackSizeInBits: 0);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotDecrypt(cipher, paddingMode, ciphertext, destination, out bytesWritten);
            }
        }

        protected override bool TryDecryptCfbCore(
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> iv,
            Span<byte> destination,
            PaddingMode paddingMode,
            int feedbackSizeInBits,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv,
                encrypting: false,
                CipherMode.CFB,
                feedbackSizeInBits);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotDecrypt(cipher, paddingMode, ciphertext, destination, out bytesWritten);
            }
        }

        protected override bool TryEncryptCfbCore(
            ReadOnlySpan<byte> plaintext,
            ReadOnlySpan<byte> iv,
            Span<byte> destination,
            PaddingMode paddingMode,
            int feedbackSizeInBits,
            out int bytesWritten)
        {
            ILiteSymmetricCipher cipher = _core.CreateLiteSymmetricCipher(
                iv,
                encrypting: true,
                CipherMode.CFB,
                feedbackSizeInBits);

            using (cipher)
            {
                return UniversalCryptoOneShot.OneShotEncrypt(cipher, paddingMode, plaintext, destination, out bytesWritten);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _key is not null)
            {
                _key.Dispose();
                _key = null;
            }

            base.Dispose(disposing);
        }

        byte[] ICngSymmetricAlgorithm.BaseKey { get { return base.Key; } set { base.Key = value; } }
        int ICngSymmetricAlgorithm.BaseKeySize { get { return base.KeySize; } set { base.KeySize = value; } }

        bool ICngSymmetricAlgorithm.IsWeakKey(byte[] key)
        {
            return false;
        }

        int ICngSymmetricAlgorithm.GetPaddingSize(CipherMode mode, int feedbackSizeBits)
        {
            return this.GetPaddingSize(mode, feedbackSizeBits);
        }

        SafeAlgorithmHandle ICngSymmetricAlgorithm.GetEphemeralModeHandle(CipherMode mode, int feedbackSizeInBits)
        {
            try
            {
                return AesBCryptModes.GetSharedHandle(mode, feedbackSizeInBits / 8);
            }
            catch (NotSupportedException)
            {
                throw new CryptographicException(SR.Cryptography_InvalidCipherMode);
            }
        }

        string ICngSymmetricAlgorithm.GetNCryptAlgorithmIdentifier()
        {
            return Cng.BCRYPT_AES_ALGORITHM;
        }

        byte[] ICngSymmetricAlgorithm.PreprocessKey(byte[] key)
        {
            return key;
        }

        bool ICngSymmetricAlgorithm.IsValidEphemeralFeedbackSize(int feedbackSizeInBits)
        {
            return feedbackSizeInBits == 8 || feedbackSizeInBits == 128;
        }

        private CngSymmetricAlgorithmCore _core;
    }
}
