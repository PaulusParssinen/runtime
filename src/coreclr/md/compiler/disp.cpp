// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//*****************************************************************************
// Disp.cpp
//

//
// Implementation for the meta data dispenser code.
//
//*****************************************************************************
#include "stdafx.h"
#include "disp.h"
#include "regmeta.h"
#include "mdutil.h"
#include <corerror.h>
#include <mdlog.h>
#include <mdcommon.h>

//*****************************************************************************
// Ctor.
//*****************************************************************************
Disp::Disp() : m_cRef(0)
{
#if defined(LOGGING)
    // InitializeLogging() calls scattered around the code.
    // <TODO>@future: make this make some sense.</TODO>
    InitializeLogging();
#endif

    m_OptionValue.m_DupCheck = MDDupDefault;
    m_OptionValue.m_RefToDefCheck = MDRefToDefDefault;
    m_OptionValue.m_NotifyRemap = MDNotifyDefault;
    m_OptionValue.m_UpdateMode = MDUpdateFull;
    m_OptionValue.m_ErrorIfEmitOutOfOrder = MDErrorOutOfOrderDefault;
    m_OptionValue.m_ThreadSafetyOptions = MDThreadSafetyDefault;
    m_OptionValue.m_GenerateTCEAdapters = FALSE;
    m_OptionValue.m_ImportOption = MDImportOptionDefault;
    m_OptionValue.m_LinkerOption = MDAssembly;
    m_OptionValue.m_RuntimeVersion = NULL;
    m_OptionValue.m_MetadataVersion = MDDefaultVersion;
    m_OptionValue.m_MergeOptions = MergeFlagsNone;
    m_OptionValue.m_InitialSize = MDInitialSizeDefault;
    m_OptionValue.m_LocalRefPreservation = MDPreserveLocalRefsNone;
} // Disp::Disp

Disp::~Disp()
{
    if (m_OptionValue.m_RuntimeVersion != NULL)
        delete [] m_OptionValue.m_RuntimeVersion;
} // Disp::~Disp

//*****************************************************************************
// Create a brand new scope.  This is based on the CLSID that was used to get
// the dispenser.
//*****************************************************************************
__checkReturn
HRESULT
Disp::DefineScope(
    REFCLSID   rclsid,          // [in] What version to create.
    DWORD      dwCreateFlags,   // [in] Flags on the create.
    REFIID     riid,            // [in] The interface desired.
    IUnknown **ppIUnk)          // [out] Return interface on success.
{
#ifdef FEATURE_METADATA_EMIT
    HRESULT     hr = S_OK;
    PathString szFileName(PathString::Literal, W("file:"));
    PathString szFileNameSuffix;

    RegMeta     *pMeta = 0;
    OptionValue optionForNewScope = m_OptionValue;


    LOG((LF_METADATA, LL_INFO10, "Disp::DefineScope(0x%08x, 0x%08x, 0x%08x, 0x%08x)\n", rclsid, dwCreateFlags, riid, ppIUnk));

    if (dwCreateFlags)
        IfFailGo(E_INVALIDARG);

    // Figure out what version of the metadata to emit
    if (rclsid == CLSID_CLR_v1_MetaData)
    {
        optionForNewScope.m_MetadataVersion = MDVersion1;
    }
    else if (rclsid == CLSID_CLR_v2_MetaData)
    {
        optionForNewScope.m_MetadataVersion = MDVersion2;
    }
    else
    {
        // If it is a version we don't understand, then we cannot continue.
        IfFailGo(CLDB_E_FILE_OLDVER);
    }

    // Create a new coclass for this.
    pMeta = new (nothrow) RegMeta();
    IfNullGo(pMeta);

    IfFailGo(pMeta->SetOption(&optionForNewScope));

    // Create the MiniMd-style scope.
    IfFailGo(pMeta->CreateNewMD());

    // Get the requested interface.
    IfFailGo(pMeta->QueryInterface(riid, (void **)ppIUnk));

    // Add the new RegMeta to the cache.
    IfFailGo(pMeta->AddToCache());

    LOG((LOGMD, "{%08x} Created new emit scope\n", pMeta));

ErrExit:
    if (FAILED(hr))
    {
        if (pMeta != NULL)
            delete pMeta;
        *ppIUnk = NULL;
    }

    return hr;
#else //!FEATURE_METADATA_EMIT
    return E_NOTIMPL;
#endif //!FEATURE_METADATA_EMIT
} // Disp::DefineScope


//*****************************************************************************
// Deliver scope to caller of OpenScope or OpenScopeOnMemory
//*****************************************************************************
static HRESULT DeliverScope(IMDCommon *pMDCommon, REFIID riid, DWORD dwOpenFlags, IUnknown **ppIUnk)
{
    return pMDCommon->QueryInterface(riid, (void**)ppIUnk);
}

//*****************************************************************************
// Open an existing scope.
//*****************************************************************************
HRESULT Disp::OpenScope(                // Return code.
    LPCWSTR     szFileName,             // [in] The scope to open.
    DWORD       dwOpenFlags,            // [in] Open mode flags.
    REFIID      riid,                   // [in] The interface desired.
    IUnknown    **ppIUnk)               // [out] Return interface on success.
{
    HRESULT     hr;

    IMDCommon *pMDCommon = NULL;

    // Validate that there is some sort of file name.
    if ((szFileName == NULL) || (szFileName[0] == 0) || (ppIUnk == NULL))
        IfFailGo(E_INVALIDARG);

    *ppIUnk = NULL;
    IfFailGo(OpenRawScope(szFileName, dwOpenFlags, IID_IMDCommon, (IUnknown**)&pMDCommon));
    IfFailGo(DeliverScope(pMDCommon, riid, dwOpenFlags, ppIUnk));
 ErrExit:
    if (pMDCommon)
        pMDCommon->Release();

    return hr;
}


//*****************************************************************************
// Open a raw view of existing scope.
//*****************************************************************************
__checkReturn
HRESULT
Disp::OpenRawScope(
    LPCWSTR    szFileName,      // [in] The scope to open.
    DWORD      dwOpenFlags,     // [in] Open mode flags.
    REFIID     riid,            // [in] The interface desired.
    IUnknown **ppIUnk)          // [out] Return interface on success.
{
    HRESULT hr;

    _ASSERTE(szFileName != NULL);
    _ASSERTE(ppIUnk != NULL);
    RegMeta *pMeta = NULL;

#ifdef FEATURE_METADATA_LOAD_TRUSTED_IMAGES
    // Don't assert for code:ofTrustedImage (reserved) flag if the feature is supported
    _ASSERTE(!IsOfReserved(dwOpenFlags & ~ofTrustedImage));
#else
    _ASSERTE(!IsOfReserved(dwOpenFlags));
#endif //!FEATURE_METADATA_LOAD_TRUSTED_IMAGES

    if (IsOfReadOnly(dwOpenFlags) && IsOfReadWrite(dwOpenFlags))
    {   // Invalid combination of flags - ofReadOnly & ofWrite
        IfFailGo(E_INVALIDARG);
    }
    // Create a new coclass for this guy.
    pMeta = new (nothrow) RegMeta();
    IfNullGo(pMeta);

    IfFailGo(pMeta->SetOption(&m_OptionValue));

    // Always initialize the RegMeta's stgdb.
    // <TODO>@FUTURE: there are some cleanup for the open code!!</TODO>
    if (memcmp(szFileName, W("file:"), 10) == 0)
    {
        szFileName = &szFileName[5];
    }

    // Try to open the MiniMd-style scope.
    IfFailGo(pMeta->OpenExistingMD(szFileName, 0 /* pbData */,0 /* cbData */, dwOpenFlags));

    // Obtain the requested interface.
    IfFailGo(pMeta->QueryInterface(riid, (void **)ppIUnk) );

    // Add the new RegMeta to the cache.
    IfFailGo(pMeta->AddToCache());

#if defined(_DEBUG)
    if (CLRConfig::GetConfigValue(CLRConfig::INTERNAL_MD_RegMetaDump))
    {
        int DumpMD_impl(RegMeta *pMD);
        DumpMD_impl(pMeta);
    }
#endif // _DEBUG


ErrExit:
    if (FAILED(hr))
    {
        if (pMeta != NULL)
            delete pMeta;
        *ppIUnk = NULL;
    }

    return hr;
} // Disp::OpenScope


//*****************************************************************************
// Open an existing scope.
//*****************************************************************************
HRESULT Disp::OpenScopeOnMemory(        // Return code.
    LPCVOID     pData,                  // [in] Location of scope data.
    ULONG       cbData,                 // [in] Size of the data pointed to by pData.
    DWORD       dwOpenFlags,            // [in] Open mode flags.
    REFIID      riid,                   // [in] The interface desired.
    IUnknown    **ppIUnk)               // [out] Return interface on success.
{
    HRESULT     hr;
    LOG((LF_METADATA, LL_INFO10, "Disp::OpenScopeOnMemory(0x%08x, 0x%08x, 0x%08x, 0x%08x, 0x%08x)\n", pData, cbData, dwOpenFlags, riid, ppIUnk));

    IMDCommon *pMDCommon = NULL;

    _ASSERTE(!IsOfReserved(dwOpenFlags));
    if (ppIUnk == NULL)
        IfFailGo(E_INVALIDARG);
    *ppIUnk = NULL;
    IfFailGo(OpenRawScopeOnMemory(pData, cbData, dwOpenFlags, IID_IMDCommon, (IUnknown**)&pMDCommon));
    IfFailGo(DeliverScope(pMDCommon, riid, dwOpenFlags, ppIUnk));
 ErrExit:
    if (pMDCommon)
        pMDCommon->Release();

    return hr;
}

//*****************************************************************************
// Open a raw voew of existing scope.
//*****************************************************************************
HRESULT Disp::OpenRawScopeOnMemory(        // Return code.
    LPCVOID     pData,                  // [in] Location of scope data.
    ULONG       cbData,                 // [in] Size of the data pointed to by pData.
    DWORD       dwOpenFlags,            // [in] Open mode flags.
    REFIID      riid,                   // [in] The interface desired.
    IUnknown    **ppIUnk)               // [out] Return interface on success.
{
    HRESULT     hr;

    RegMeta     *pMeta = 0;

    _ASSERTE(!IsOfReserved(dwOpenFlags));

    // Create a new coclass for this guy.
    pMeta = new (nothrow) RegMeta();
    IfNullGo(pMeta);
    IfFailGo(pMeta->SetOption(&m_OptionValue));


    _ASSERTE(pMeta != NULL);
    // Always initialize the RegMeta's stgdb.
    IfFailGo(pMeta->OpenExistingMD(0 /* szFileName */, const_cast<void*>(pData), cbData, dwOpenFlags));

    LOG((LOGMD, "{%08x} Opened new scope on memory, pData: %08x    cbData: %08x\n", pMeta, pData, cbData));

    // Return the requested interface.
    IfFailGo( pMeta->QueryInterface(riid, (void **) ppIUnk) );

    // Add the new RegMeta to the cache.
    IfFailGo(pMeta->AddToCache());

#if defined(_DEBUG)
    if (CLRConfig::GetConfigValue(CLRConfig::INTERNAL_MD_RegMetaDump))
    {
        int DumpMD_impl(RegMeta *pMD);
        DumpMD_impl(pMeta);
    }
#endif // _DEBUG

ErrExit:
    if (FAILED(hr))
    {
        if (pMeta) delete pMeta;
        *ppIUnk = 0;
    }

    return hr;
} // Disp::OpenScopeOnMemory


//*****************************************************************************
// Get the directory where the CLR system resides.
//
// Implements public API code:IMetaDataDispenserEx::GetCORSystemDirectory.
//*****************************************************************************
HRESULT
Disp::GetCORSystemDirectory(
    _Out_writes_ (cchBuffer) LPWSTR szBuffer,      // [out] Buffer for the directory name
    DWORD                           cchBuffer,     // [in] Size of the buffer
    DWORD                          *pcchBuffer)    // [out] Number of characters returned
{

    UNREACHABLE_MSG("Calling IMetaDataDispenser::GetCORSystemDirectory!  This code should not be "
                "reachable or needs to be reimplemented for CoreCLR!");

    return E_NOTIMPL;
} // Disp::GetCORSystemDirectory

HRESULT Disp::FindAssembly(             // S_OK or error
    LPCWSTR     szAppBase,              // [IN] optional - can be NULL
    LPCWSTR     szPrivateBin,           // [IN] optional - can be NULL
    LPCWSTR     szGlobalBin,            // [IN] optional - can be NULL
    LPCWSTR     szAssemblyName,         // [IN] required - this is the assembly you are requesting
    LPCWSTR     szName,                 // [OUT] buffer - to hold name
    ULONG       cchName,                // [IN] the name buffer's size
    ULONG       *pcName)                // [OUT] the number of characters returned in the buffer
{
    return E_NOTIMPL;
} // Disp::FindAssembly

HRESULT Disp::FindAssemblyModule(           // S_OK or error
    LPCWSTR     szAppBase,                  // [IN] optional - can be NULL
    LPCWSTR     szPrivateBin,               // [IN] optional - can be NULL
    LPCWSTR     szGlobalBin,                // [IN] optional - can be NULL
    LPCWSTR     szAssemblyName,             // [IN] The assembly name or code base of the assembly
    LPCWSTR     szModuleName,               // [IN] required - the name of the module
    _Out_writes_ (cchName) LPWSTR  szName,  // [OUT] buffer - to hold name
    ULONG       cchName,                    // [IN]  the name buffer's size
    ULONG       *pcName)                    // [OUT] the number of characters returned in the buffer
{
    return E_NOTIMPL;
} // Disp::FindAssemblyModule

//*****************************************************************************
// Open a scope on an ITypeInfo
//*****************************************************************************
HRESULT Disp::OpenScopeOnITypeInfo(     // Return code.
    ITypeInfo   *pITI,                  // [in] ITypeInfo to open.
    DWORD       dwOpenFlags,            // [in] Open mode flags.
    REFIID      riid,                   // [in] The interface desired.
    IUnknown    **ppIUnk)               // [out] Return interface on success.
{
    return E_NOTIMPL;
} // Disp::OpenScopeOnITypeInfo

#ifdef FEATURE_METADATA_EMIT_PORTABLE_PDB
//*****************************************************************************
// Create a brand new scope which will be used for portable PDB metadata.
// This is based on the CLSID that was used to get the dispenser.
//
// The existing DefineScope method cannot be used for the purpose of PDB
// metadata generation, since it internally creates module and type def table
// entries.
//*****************************************************************************
__checkReturn
HRESULT
Disp::DefinePortablePdbScope(
    REFCLSID   rclsid,          // [in] What version to create.
    DWORD      dwCreateFlags,   // [in] Flags on the create.
    REFIID     riid,            // [in] The interface desired.
    IUnknown** ppIUnk)          // [out] Return interface on success.
{
#ifdef FEATURE_METADATA_EMIT
    HRESULT     hr = S_OK;

    RegMeta* pMeta = 0;
    OptionValue optionForNewScope = m_OptionValue;

    LOG((LF_METADATA, LL_INFO10, "Disp::DefinePortablePdbScope(0x%08x, 0x%08x, 0x%08x, 0x%08x)\n", rclsid, dwCreateFlags, riid, ppIUnk));

    if (dwCreateFlags)
        IfFailGo(E_INVALIDARG);

    // Currently the portable PDB tables are treated as an extension to the MDVersion2
    // TODO: this extension might deserve its own version number e.g. 'MDVersion3'
    if (rclsid == CLSID_CLR_v2_MetaData)
    {
        optionForNewScope.m_MetadataVersion = MDVersion2;
    }
    else
    {
        // If it is a version we don't understand, then we cannot continue.
        IfFailGo(CLDB_E_FILE_OLDVER);
    }

    // Create a new coclass for this.
    pMeta = new (nothrow) RegMeta();
    IfNullGo(pMeta);

    IfFailGo(pMeta->SetOption(&optionForNewScope));

    // Create the MiniMd-style scope for portable pdb
    IfFailGo(pMeta->CreateNewPortablePdbMD());

    // Get the requested interface.
    IfFailGo(pMeta->QueryInterface(riid, (void**)ppIUnk));

    // Add the new RegMeta to the cache.
    IfFailGo(pMeta->AddToCache());

    LOG((LOGMD, "{%08x} Created new emit scope\n", pMeta));

ErrExit:
    if (FAILED(hr))
    {
        if (pMeta != NULL)
            delete pMeta;
        *ppIUnk = NULL;
    }

    return hr;
#else //!FEATURE_METADATA_EMIT
    return E_NOTIMPL;
#endif //!FEATURE_METADATA_EMIT
} // Disp::DefineScope
#endif // FEATURE_METADATA_EMIT_PORTABLE_PDB

#ifdef FEATURE_METADATA_CUSTOM_DATA_SOURCE

//*****************************************************************************
// IMetaDataDispenserCustom
//*****************************************************************************

HRESULT Disp::OpenScopeOnCustomDataSource(  // S_OK or error
    IMDCustomDataSource  *pCustomSource, // [in] The scope to open.
    DWORD                dwOpenFlags,    // [in] Open mode flags.
    REFIID               riid,           // [in] The interface desired.
    IUnknown             **ppIUnk)       // [out] Return interface on success.
{
    HRESULT     hr;
    LOG((LF_METADATA, LL_INFO10, "Disp::OpenScopeOnCustomDataSource(0x%08x, 0x%08x, 0x%08x, 0x%08x)\n", pCustomSource, dwOpenFlags, riid, ppIUnk));

    IMDCommon *pMDCommon = NULL;

    _ASSERTE(!IsOfReserved(dwOpenFlags));
    if (ppIUnk == NULL)
        IfFailGo(E_INVALIDARG);
    *ppIUnk = NULL;
    IfFailGo(OpenRawScopeOnCustomDataSource(pCustomSource, dwOpenFlags, IID_IMDCommon, (IUnknown**)&pMDCommon));
    IfFailGo(DeliverScope(pMDCommon, riid, dwOpenFlags, ppIUnk));
ErrExit:
    if (pMDCommon)
        pMDCommon->Release();

    return hr;
}


//*****************************************************************************
// Open a raw view of existing scope.
//*****************************************************************************
HRESULT Disp::OpenRawScopeOnCustomDataSource(        // Return code.
    IMDCustomDataSource*  pDataSource,  // [in] scope data.
    DWORD       dwOpenFlags,            // [in] Open mode flags.
    REFIID      riid,                   // [in] The interface desired.
    IUnknown    **ppIUnk)               // [out] Return interface on success.
{
    HRESULT     hr;

    RegMeta     *pMeta = 0;

    _ASSERTE(!IsOfReserved(dwOpenFlags));

    // Create a new coclass for this guy.
    pMeta = new (nothrow)RegMeta();
    IfNullGo(pMeta);
    IfFailGo(pMeta->SetOption(&m_OptionValue));


    _ASSERTE(pMeta != NULL);
    // Always initialize the RegMeta's stgdb.
    // TODO
    IfFailGo(pMeta->OpenExistingMD(pDataSource, dwOpenFlags));

    LOG((LOGMD, "{%08x} Opened new scope on custom data source, pDataSource: %08x\n", pMeta, pDataSource));

    // Return the requested interface.
    IfFailGo(pMeta->QueryInterface(riid, (void **)ppIUnk));

    // Add the new RegMeta to the cache.
    IfFailGo(pMeta->AddToCache());

#if defined(_DEBUG)
    if (CLRConfig::GetConfigValue(CLRConfig::INTERNAL_MD_RegMetaDump))
    {
        int DumpMD_impl(RegMeta *pMD);
        DumpMD_impl(pMeta);
    }
#endif // _DEBUG

ErrExit:
    if (FAILED(hr))
    {
        if (pMeta) delete pMeta;
        *ppIUnk = 0;
    }

    return hr;
} // Disp::OpenRawScopeOnCustomDataSource

#endif

//*****************************************************************************
// IUnknown
//*****************************************************************************

ULONG Disp::AddRef()
{
    return InterlockedIncrement(&m_cRef);
} // Disp::AddRef

ULONG Disp::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (cRef == 0)
        delete this;
    return cRef;
} // Disp::Release

HRESULT Disp::QueryInterface(REFIID riid, void **ppUnk)
{
    *ppUnk = 0;

    if (riid == IID_IUnknown)
        *ppUnk = (IUnknown *) (IMetaDataDispenser *) this;
    else if (riid == IID_IMetaDataDispenser)
        *ppUnk = (IMetaDataDispenser *) this;
    else if (riid == IID_IMetaDataDispenserEx)
        *ppUnk = (IMetaDataDispenserEx *) this;
#ifdef FEATURE_METADATA_EMIT_PORTABLE_PDB
    else if (riid == IID_IMetaDataDispenserEx2)
        *ppUnk = (IMetaDataDispenserEx2 *) this;
    else if (riid == IID_IILAsmPortablePdbWriter)
        *ppUnk = (IILAsmPortablePdbWriter *) this;
#endif
#ifdef FEATURE_METADATA_CUSTOM_DATA_SOURCE
    else if (riid == IID_IMetaDataDispenserCustom)
        *ppUnk = static_cast<IMetaDataDispenserCustom*>(this);
#endif
    else
        return E_NOINTERFACE;
    AddRef();
    return S_OK;
} // Disp::QueryInterface


//*****************************************************************************
// Called by the class factory template to create a new instance of this object.
//*****************************************************************************
HRESULT Disp::CreateObject(REFIID riid, void **ppUnk)
{
    HRESULT     hr;
    Disp *pDisp = new (nothrow) Disp();

    if (pDisp == 0)
        return (E_OUTOFMEMORY);

    hr = pDisp->QueryInterface(riid, ppUnk);
    if (FAILED(hr))
        delete pDisp;
    return hr;
} // Disp::CreateObject

//*****************************************************************************
// This routine provides the user a way to set certain properties on the
// Dispenser.
//
// Implements public API code:IMetaDataDispenserEx::SetOption.
//*****************************************************************************
__checkReturn
HRESULT
Disp::SetOption(
    REFGUID        optionid,    // [in] GUID for the option to be set.
    const VARIANT *pvalue)      // [in] Value to which the option is to be set.
{
    HRESULT hr = S_OK;

    LOG((LF_METADATA, LL_INFO10, "Disp::SetOption(0x%08x, 0x%08x)\n", optionid, pvalue));

    if (optionid == MetaDataCheckDuplicatesFor)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_DupCheck = (CorCheckDuplicatesFor) V_UI4(pvalue);
    }
    else if (optionid == MetaDataRefToDefCheck)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_RefToDefCheck = (CorRefToDefCheck) V_UI4(pvalue);
    }
    else if (optionid == MetaDataErrorIfEmitOutOfOrder)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_ErrorIfEmitOutOfOrder = (CorErrorIfEmitOutOfOrder) V_UI4(pvalue);
    }
    else if (optionid == MetaDataThreadSafetyOptions)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_ThreadSafetyOptions = (CorThreadSafetyOptions) V_UI4(pvalue);
    }
// Note: mscordbi had all these options accessible in 3.5/4.0 RTM, let's keep it this way for AppCompat.
#if defined(FEATURE_METADATA_EMIT_ALL) || defined(FEATURE_METADATA_EMIT_IN_DEBUGGER)
    else if (optionid == MetaDataNotificationForTokenMovement)
    {   // Note: This is not used in CLR sources anymore, but we store the value and return it back in
        // IMetaDataDispenserEx::GetOption (code:RegMeta::GetOption), so we keep it here for backward-compat.
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_NotifyRemap = (CorNotificationForTokenMovement)V_UI4(pvalue);
    }
    else if (optionid == MetaDataSetENC)
    {   // EnC update mode (also aliased as code:MetaDataSetUpdate)
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_UpdateMode = V_UI4(pvalue);
    }
    else if (optionid == MetaDataImportOption)
    {   // Allows enumeration of EnC deleted items by Import API
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_ImportOption = (CorImportOptions) V_UI4(pvalue);
    }
    else if (optionid == MetaDataLinkerOptions)
    {   // Used only by code:RegMeta::UnmarkAll (code:IMetaDataFilter::UnmarkAll)
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_LinkerOption = (CorLinkerOptions) V_UI4(pvalue);
    }
    else if (optionid == MetaDataMergerOptions)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_MergeOptions = (MergeFlags) V_UI4(pvalue);
    }
    else if (optionid == MetaDataGenerateTCEAdapters)
    {   // Note: This is not used in CLR sources anymore, but we store the value and return it back in
        // IMetaDataDispenserEx::GetOption (code:RegMeta::GetOption), so we keep it for backward-compat.
        if (V_VT(pvalue) != VT_BOOL)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_GenerateTCEAdapters = V_BOOL(pvalue);
    }
    else if (optionid == MetaDataTypeLibImportNamespace)
    {   // Note: This is not used in CLR sources anymore, keep it here for backward-compat
        if (V_VT(pvalue) != VT_BSTR && V_VT(pvalue) != VT_EMPTY && V_VT(pvalue) != VT_NULL)
        {
            _ASSERTE(!"Invalid Variant Type value for namespace.");
            IfFailGo(E_INVALIDARG);
        }
    }
#endif //FEATURE_METADATA_EMIT_ALL || FEATURE_METADATA_EMIT_IN_DEBUGGER
    else if (optionid == MetaDataRuntimeVersion)
    {
        if (V_VT(pvalue) != VT_BSTR && V_VT(pvalue) != VT_EMPTY && V_VT(pvalue) != VT_NULL)
        {
            _ASSERTE(!"Invalid Variant Type value for version.");
            IfFailGo(E_INVALIDARG);
        }
        if (m_OptionValue.m_RuntimeVersion)
            delete [] m_OptionValue.m_RuntimeVersion;

        if ((V_VT(pvalue) == VT_EMPTY) || (V_VT(pvalue) == VT_NULL) || (*V_BSTR(pvalue) == 0))
        {
            m_OptionValue.m_RuntimeVersion = NULL;
        }
        else
        {
            INT32 len = WideCharToMultiByte(CP_UTF8, 0, V_BSTR(pvalue), -1, NULL, 0, NULL, NULL);
            m_OptionValue.m_RuntimeVersion = new (nothrow) char[len];
            if (m_OptionValue.m_RuntimeVersion == NULL)
            IfFailGo(E_INVALIDARG);
            WideCharToMultiByte(CP_UTF8, 0, V_BSTR(pvalue), -1, m_OptionValue.m_RuntimeVersion, len, NULL, NULL);
        }
    }
    else if (optionid == MetaDataInitialSize)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }
        m_OptionValue.m_InitialSize = V_UI4(pvalue);
    }
    else if (optionid == MetaDataPreserveLocalRefs)
    {
        if (V_VT(pvalue) != VT_UI4)
        {
            _ASSERTE(!"Invalid Variant Type value!");
            IfFailGo(E_INVALIDARG);
        }

        m_OptionValue.m_LocalRefPreservation = (CorLocalRefPreservation) V_UI4(pvalue);
    }
    else
    {
        _ASSERTE(!"Invalid GUID");
        IfFailGo(E_INVALIDARG);
    }

ErrExit:
    return hr;
} // Disp::SetOption

//*****************************************************************************
// This routine provides the user a way to set certain properties on the
// Dispenser.
//
// Implements public API code:IMetaDataDispenserEx::GetOption.
//*****************************************************************************
HRESULT Disp::GetOption(                // Return code.
    REFGUID     optionid,               // [in] GUID for the option to be set.
    VARIANT *pvalue)                    // [out] Value to which the option is currently set.
{
    HRESULT hr = S_OK;

    LOG((LF_METADATA, LL_INFO10, "Disp::GetOption(0x%08x, 0x%08x)\n", optionid, pvalue));

    _ASSERTE(pvalue);
    if (optionid == MetaDataCheckDuplicatesFor)
    {
        V_VT(pvalue) = VT_UI4;
        V_UI4(pvalue) = m_OptionValue.m_DupCheck;
    }
    else if (optionid == MetaDataRefToDefCheck)
    {
        V_VT(pvalue) = VT_UI4;
        V_UI4(pvalue) = m_OptionValue.m_RefToDefCheck;
    }
    else if (optionid == MetaDataErrorIfEmitOutOfOrder)
    {
        V_VT(pvalue) = VT_UI4;
        V_UI4(pvalue) = m_OptionValue.m_ErrorIfEmitOutOfOrder;
    }
// Note: mscordbi had all these options accessible in 3.5/4.0 RTM, let's keep it this way for AppCompat.
#if defined(FEATURE_METADATA_EMIT_ALL) || defined(FEATURE_METADATA_EMIT_IN_DEBUGGER)
    else if (optionid == MetaDataNotificationForTokenMovement)
    {   // Note: This is not used in CLR sources anymore, but we store the value and return it here,
        // so we keep it for backward-compat.
        V_VT(pvalue) = VT_UI4;
        V_UI4(pvalue) = m_OptionValue.m_NotifyRemap;
    }
    else if (optionid == MetaDataSetENC)
    {   // EnC update mode (also aliased as code:MetaDataSetUpdate)
        V_VT(pvalue) = VT_UI4;
        V_UI4(pvalue) = m_OptionValue.m_UpdateMode;
    }
    else if (optionid == MetaDataLinkerOptions)
    {   // Allows enumeration of EnC deleted items by Import API
        V_VT(pvalue) = VT_BOOL;
        V_UI4(pvalue) = m_OptionValue.m_LinkerOption;
    }
    else if (optionid == MetaDataGenerateTCEAdapters)
    {   // Note: This is not used in CLR sources anymore, but we store the value and return it here,
        // so we keep it for backward-compat.
        V_VT(pvalue) = VT_BOOL;
        V_BOOL(pvalue) = !!m_OptionValue.m_GenerateTCEAdapters ? VARIANT_TRUE : VARIANT_FALSE;
    }
#endif //FEATURE_METADATA_EMIT_ALL || FEATURE_METADATA_EMIT_IN_DEBUGGER
    else
    {
        _ASSERTE(!"Invalid GUID");
        IfFailGo(E_INVALIDARG);
    }
ErrExit:
    return hr;
} // Disp::GetOption

//
// This is the entrypoint for usages of MetaData that need to start with the dispenser (e.g.
// mscordbi.dll and profiling API).
//
// Notes:
//    This could be merged with the class factory support.
HRESULT CreateMetaDataDispenser(REFIID riid, void ** pMetaDataDispenserOut)
{
    _ASSERTE(pMetaDataDispenserOut != NULL);
    return Disp::CreateObject(riid, pMetaDataDispenserOut);
}
