// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Security.Cryptography
{
    internal static class KeyPropertyName
    {
        internal const string Algorithm = "Algorithm Name";                 // NCRYPT_ALGORITHM_PROPERTY
        internal const string AlgorithmGroup = "Algorithm Group";           // NCRYPT_ALGORITHM_GROUP_PROPERTY
        internal const string ChainingMode = "Chaining Mode";               // NCRYPT_CHAINING_MODE_PROPERTY
        internal const string ECCCurveName = "ECCCurveName";                // NCRYPT_ECC_CURVE_NAME
        internal const string ECCParameters = "ECCParameters";              // BCRYPT_ECC_PARAMETERS
        internal const string ExportPolicy = "Export Policy";               // NCRYPT_EXPORT_POLICY_PROPERTY
        internal const string InitializationVector = "IV";                  // NCRYPT_INITIALIZATION_VECTOR
        internal const string KeyType = "Key Type";                         // NCRYPT_KEY_TYPE_PROPERTY
        internal const string KeyUsage = "Key Usage";                       // NCRYPT_KEY_USAGE_PROPERTY
        internal const string Length = "Length";                            // NCRYPT_LENGTH_PROPERTY
        internal const string Name = "Name";                                // NCRYPT_NAME_PROPERTY
        internal const string ParentWindowHandle = "HWND Handle";           // NCRYPT_WINDOW_HANDLE_PROPERTY
        internal const string ParameterSetName = "ParameterSetName";        // NCRYPT_PARAMETER_SET_NAME_PROPERTY
        internal const string PublicKeyLength = "PublicKeyLength";          // NCRYPT_PUBLIC_KEY_LENGTH (Win10+)
        internal const string ProviderHandle = "Provider Handle";           // NCRYPT_PROVIDER_HANDLE_PROPERTY
        internal const string UIPolicy = "UI Policy";                       // NCRYPT_UI_POLICY_PROPERTY
        internal const string UniqueName = "Unique Name";                   // NCRYPT_UNIQUE_NAME_PROPERTY
        internal const string UseContext = "Use Context";                   // NCRYPT_USE_CONTEXT_PROPERTY


        //
        // Properties defined by the CLR
        //

        /// <summary>
        ///     Is the key a CLR created ephemeral key, it will contain a single byte with value 1 if the
        ///     key was created by the CLR as an ephemeral key.
        /// </summary>
        internal const string ClrIsEphemeral = "CLR IsEphemeral";
    }
}
