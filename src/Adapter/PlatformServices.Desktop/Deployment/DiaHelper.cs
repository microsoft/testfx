// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment
{
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment.Dia2Lib;
    
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// This class implements various utility functions for interaction with debug
    /// information.
    /// </summary>
    internal static class DiaHelper
    {
        /// <summary>
        /// Opens the image and reads information about the symbols file, associated
        /// to this image.
        /// </summary>
        /// <param name="imagePath">The full path to executable or DLL.</param>
        /// <returns>Full path to corresponding symbols file or null if the image does
        /// not contain symbols file information.</returns>
        public static string GetSymbolsFileName(string imagePath)
        {
            Debug.Assert(!string.IsNullOrEmpty(imagePath), "imagePath");

            IDiaDataSource source;

            try
            {
                source = (IDiaDataSource)new DiaSourceClass();
            }
            catch (COMException ex)
            {
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, "DIA initialization threw:\n" + ex.ToString());

                // Let things go fine if we can find a local file, return
                // null if we can't.  This is needed to support xcopy deployment.
                string pdbFile = Path.ChangeExtension(imagePath, ".pdb");
                if (File.Exists(pdbFile))
                {
                    return pdbFile;
                }

                return null;
            }

            IDiaSession session = null;
            IDiaSymbol global = null;
            try
            {
                // Load the image:
                source.loadDataForExe(imagePath, null, null);

                // Extract the main symbol via session:
                source.openSession(out session);
                global = session.globalScope;

                Debug.Assert(global != null, "globalScope Symbol");
                return global.symbolsFileName;
            }
            catch (COMException ex)
            {
                // If exception is thrown the image does not contain symbols or the symbols found
                // are not correct.
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, "DIA thew in retrieving symbols: " + ex.ToString());
                return null;
            }
            finally
            {
                // Ensure that the resources allocated by DIA get cleaned up.
                // In particular, after loadDataForExe, the PDB will be locked
                // until CDiaDataSource::~CDiaDataSource is called.
                ReleaseComObject(global);
                ReleaseComObject(session);
                ReleaseComObject(source);
            }
        }

        /// <summary>
        /// Release all references held by the runtime callable wrapper (RCW).
        /// </summary>
        /// <param name="o"> The object. </param>
        private static void ReleaseComObject(object o)
        {
            if (o != null)
            {
                int refCount;
                do
                {
                    refCount = Marshal.ReleaseComObject(o);
                }
                while (refCount != 0);
            }
        }
    }
}

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment.Dia2Lib
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    [ComImport, CoClass(typeof(DiaSourceClass)), Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D")]
    internal interface DiaSource : IDiaDataSource
    {
    }

    [ComImport, ClassInterface((short)0), Guid("3bfcea48-620f-4b6b-81f7-b9af75454c7d"), TypeLibType((short)2)]
    internal class DiaSourceClass
    {
    }

    [ComImport, Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D"), InterfaceType((short)1)]
    internal interface IDiaDataSource
    {
        [DispId(1)]
        string lastError { [return: MarshalAs(UnmanagedType.BStr)] get; }
        void loadDataFromPdb([In, MarshalAs(UnmanagedType.LPWStr)] string pdbPath);
        void loadAndValidateDataFromPdb([In, MarshalAs(UnmanagedType.LPWStr)] string pdbPath, [In] ref Guid pcsig70, [In] uint sig, [In] uint age);
        void loadDataForExe([In, MarshalAs(UnmanagedType.LPWStr)] string executable, [In, MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In, MarshalAs(UnmanagedType.IUnknown)] object pCallback);
        void loadDataFromIStream([In, MarshalAs(UnmanagedType.Interface)] IStream pIStream);
        void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);
    }


    [ComImport, DefaultMember("symIndexId"), Guid("CB787B2F-BD6C-4635-BA52-933126BD2DCD"), InterfaceType((short)1)]
    internal interface IDiaSymbol
    {
        [System.Runtime.InteropServices.DispIdAttribute(0)]
        uint symIndexId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(1)]
        uint symTag { get; }
        [System.Runtime.InteropServices.DispIdAttribute(2)]
        string name { get; }
        [System.Runtime.InteropServices.DispIdAttribute(3)]
        Dia2Lib.IDiaSymbol lexicalParent { get; }
        [System.Runtime.InteropServices.DispIdAttribute(4)]
        Dia2Lib.IDiaSymbol classParent { get; }
        [System.Runtime.InteropServices.DispIdAttribute(5)]
        Dia2Lib.IDiaSymbol type { get; }
        [System.Runtime.InteropServices.DispIdAttribute(6)]
        uint dataKind { get; }
        [System.Runtime.InteropServices.DispIdAttribute(7)]
        uint locationType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(8)]
        uint addressSection { get; }
        [System.Runtime.InteropServices.DispIdAttribute(9)]
        uint addressOffset { get; }
        [System.Runtime.InteropServices.DispIdAttribute(10)]
        uint relativeVirtualAddress { get; }
        [System.Runtime.InteropServices.DispIdAttribute(11)]
        ulong virtualAddress { get; }
        [System.Runtime.InteropServices.DispIdAttribute(12)]
        uint registerId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(13)]
        int offset { get; }
        [System.Runtime.InteropServices.DispIdAttribute(14)]
        ulong length { get; }
        [System.Runtime.InteropServices.DispIdAttribute(15)]
        uint slot { get; }
        [System.Runtime.InteropServices.DispIdAttribute(16)]
        int volatileType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(17)]
        int constType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(18)]
        int unalignedType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(19)]
        uint access { get; }
        [System.Runtime.InteropServices.DispIdAttribute(20)]
        string libraryName { get; }
        [System.Runtime.InteropServices.DispIdAttribute(21)]
        uint platform { get; }
        [System.Runtime.InteropServices.DispIdAttribute(22)]
        uint language { get; }
        [System.Runtime.InteropServices.DispIdAttribute(23)]
        int editAndContinueEnabled { get; }
        [System.Runtime.InteropServices.DispIdAttribute(24)]
        uint frontEndMajor { get; }
        [System.Runtime.InteropServices.DispIdAttribute(25)]
        uint frontEndMinor { get; }
        [System.Runtime.InteropServices.DispIdAttribute(26)]
        uint frontEndBuild { get; }
        [System.Runtime.InteropServices.DispIdAttribute(27)]
        uint backEndMajor { get; }
        [System.Runtime.InteropServices.DispIdAttribute(28)]
        uint backEndMinor { get; }
        [System.Runtime.InteropServices.DispIdAttribute(29)]
        uint backEndBuild { get; }
        [System.Runtime.InteropServices.DispIdAttribute(30)]
        string sourceFileName { get; }
        [System.Runtime.InteropServices.DispIdAttribute(31)]
        string unused { get; }
        [System.Runtime.InteropServices.DispIdAttribute(32)]
        uint thunkOrdinal { get; }
        [System.Runtime.InteropServices.DispIdAttribute(33)]
        int thisAdjust { get; }
        [System.Runtime.InteropServices.DispIdAttribute(34)]
        uint virtualBaseOffset { get; }
        [System.Runtime.InteropServices.DispIdAttribute(35)]
        int @virtual { get; }
        [System.Runtime.InteropServices.DispIdAttribute(36)]
        int intro { get; }
        [System.Runtime.InteropServices.DispIdAttribute(37)]
        int pure { get; }
        [System.Runtime.InteropServices.DispIdAttribute(38)]
        uint callingConvention { get; }
        [System.Runtime.InteropServices.DispIdAttribute(39)]
        object value { get; }
        [System.Runtime.InteropServices.DispIdAttribute(40)]
        uint baseType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(41)]
        uint token { get; }
        [System.Runtime.InteropServices.DispIdAttribute(42)]
        uint timeStamp { get; }
        [System.Runtime.InteropServices.DispIdAttribute(43)]
        System.Guid guid { get; }
        [System.Runtime.InteropServices.DispIdAttribute(44)]
        string symbolsFileName { [return: MarshalAs(UnmanagedType.BStr)] get; }
        [System.Runtime.InteropServices.DispIdAttribute(46)]
        int reference { get; }
        [System.Runtime.InteropServices.DispIdAttribute(47)]
        uint count { get; }
        [System.Runtime.InteropServices.DispIdAttribute(49)]
        uint bitPosition { get; }
        [System.Runtime.InteropServices.DispIdAttribute(50)]
        Dia2Lib.IDiaSymbol arrayIndexType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(51)]
        int packed { get; }
        [System.Runtime.InteropServices.DispIdAttribute(52)]
        int constructor { get; }
        [System.Runtime.InteropServices.DispIdAttribute(53)]
        int overloadedOperator { get; }
        [System.Runtime.InteropServices.DispIdAttribute(54)]
        int nested { get; }
        [System.Runtime.InteropServices.DispIdAttribute(55)]
        int hasNestedTypes { get; }
        [System.Runtime.InteropServices.DispIdAttribute(56)]
        int hasAssignmentOperator { get; }
        [System.Runtime.InteropServices.DispIdAttribute(57)]
        int hasCastOperator { get; }
        [System.Runtime.InteropServices.DispIdAttribute(58)]
        int scoped { get; }
        [System.Runtime.InteropServices.DispIdAttribute(59)]
        int virtualBaseClass { get; }
        [System.Runtime.InteropServices.DispIdAttribute(60)]
        int indirectVirtualBaseClass { get; }
        [System.Runtime.InteropServices.DispIdAttribute(61)]
        int virtualBasePointerOffset { get; }
        [System.Runtime.InteropServices.DispIdAttribute(62)]
        Dia2Lib.IDiaSymbol virtualTableShape { get; }
        [System.Runtime.InteropServices.DispIdAttribute(64)]
        uint lexicalParentId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(65)]
        uint classParentId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(66)]
        uint typeId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(67)]
        uint arrayIndexTypeId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(68)]
        uint virtualTableShapeId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(69)]
        int code { get; }
        [System.Runtime.InteropServices.DispIdAttribute(70)]
        int function { get; }
        [System.Runtime.InteropServices.DispIdAttribute(71)]
        int managed { get; }
        [System.Runtime.InteropServices.DispIdAttribute(72)]
        int msil { get; }
        [System.Runtime.InteropServices.DispIdAttribute(73)]
        uint virtualBaseDispIndex { get; }
        [System.Runtime.InteropServices.DispIdAttribute(74)]
        string undecoratedName { get; }
        [System.Runtime.InteropServices.DispIdAttribute(75)]
        uint age { get; }
        [System.Runtime.InteropServices.DispIdAttribute(76)]
        uint signature { get; }
        [System.Runtime.InteropServices.DispIdAttribute(77)]
        int compilerGenerated { get; }
        [System.Runtime.InteropServices.DispIdAttribute(78)]
        int addressTaken { get; }
        [System.Runtime.InteropServices.DispIdAttribute(79)]
        uint rank { get; }
        [System.Runtime.InteropServices.DispIdAttribute(80)]
        Dia2Lib.IDiaSymbol lowerBound { get; }
        [System.Runtime.InteropServices.DispIdAttribute(81)]
        Dia2Lib.IDiaSymbol upperBound { get; }
        [System.Runtime.InteropServices.DispIdAttribute(82)]
        uint lowerBoundId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(83)]
        uint upperBoundId { get; }
        void get_dataBytes(uint cbData, out uint pcbData, out byte pbData);
        void findChildren(/*Dia2Lib.SymTagEnum symTag, string name, uint compareFlags, out Dia2Lib.IDiaEnumSymbols ppResult*/);
        [System.Runtime.InteropServices.DispIdAttribute(84)]
        uint targetSection { get; }
        [System.Runtime.InteropServices.DispIdAttribute(85)]
        uint targetOffset { get; }
        [System.Runtime.InteropServices.DispIdAttribute(86)]
        uint targetRelativeVirtualAddress { get; }
        [System.Runtime.InteropServices.DispIdAttribute(87)]
        ulong targetVirtualAddress { get; }
        [System.Runtime.InteropServices.DispIdAttribute(88)]
        uint machineType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(89)]
        uint oemId { get; }
        [System.Runtime.InteropServices.DispIdAttribute(90)]
        uint oemSymbolId { get; }
        void get_types(uint cTypes, out uint pcTypes, out Dia2Lib.IDiaSymbol pTypes);
        void get_typeIds(uint cTypeIds, out uint pcTypeIds, out uint pdwTypeIds);
        [System.Runtime.InteropServices.DispIdAttribute(91)]
        Dia2Lib.IDiaSymbol objectPointerType { get; }
        [System.Runtime.InteropServices.DispIdAttribute(92)]
        uint udtKind { get; }
        void get_undecoratedNameEx(uint undecorateOptions, out string name);
        void get_liveLVarInstances(/*ulong va, uint cInstances, out uint pcInstances, out Dia2Lib.IDiaLVarInstance instances*/);
        [System.Runtime.InteropServices.DispIdAttribute(93)]
        int noReturn { get; }
        [System.Runtime.InteropServices.DispIdAttribute(94)]
        int customCallingConvention { get; }
        [System.Runtime.InteropServices.DispIdAttribute(95)]
        int noInline { get; }
        [System.Runtime.InteropServices.DispIdAttribute(96)]
        int optmizedCodeDebugInfo { get; }
        [System.Runtime.InteropServices.DispIdAttribute(97)]
        int notReached { get; }
        [System.Runtime.InteropServices.DispIdAttribute(98)]
        int interruptReturn { get; }
        [System.Runtime.InteropServices.DispIdAttribute(99)]
        int farReturn { get; }
        [System.Runtime.InteropServices.DispIdAttribute(100)]
        int isStatic { get; }
        [System.Runtime.InteropServices.DispIdAttribute(101)]
        int hasDebugInfo { get; }
        [System.Runtime.InteropServices.DispIdAttribute(102)]
        int isLTCG { get; }
        [System.Runtime.InteropServices.DispIdAttribute(103)]
        int isDataAligned { get; }
        [System.Runtime.InteropServices.DispIdAttribute(104)]
        int hasSecurityChecks { get; }
        [System.Runtime.InteropServices.DispIdAttribute(105)]
        string compilerName { get; }
        [System.Runtime.InteropServices.DispIdAttribute(106)]
        int hasAlloca { get; }
        [System.Runtime.InteropServices.DispIdAttribute(107)]
        int hasSetJump { get; }
        [System.Runtime.InteropServices.DispIdAttribute(108)]
        int hasLongJump { get; }
        [System.Runtime.InteropServices.DispIdAttribute(109)]
        int hasInlAsm { get; }
        [System.Runtime.InteropServices.DispIdAttribute(110)]
        int hasEH { get; }
        [System.Runtime.InteropServices.DispIdAttribute(111)]
        int hasSEH { get; }
        [System.Runtime.InteropServices.DispIdAttribute(112)]
        int hasEHa { get; }
        [System.Runtime.InteropServices.DispIdAttribute(113)]
        int isNaked { get; }
        [System.Runtime.InteropServices.DispIdAttribute(114)]
        int isAggregated { get; }
        [System.Runtime.InteropServices.DispIdAttribute(115)]
        int isSplitted { get; }
        [System.Runtime.InteropServices.DispIdAttribute(116)]
        Dia2Lib.IDiaSymbol container { get; }
        [System.Runtime.InteropServices.DispIdAttribute(117)]
        int inlSpec { get; }
        [System.Runtime.InteropServices.DispIdAttribute(118)]
        int noStackOrdering { get; }
        [System.Runtime.InteropServices.DispIdAttribute(119)]
        Dia2Lib.IDiaSymbol virtualBaseTableType { get; }
    }

    [ComImport, Guid("6FC5D63F-011E-40C2-8DD2-E6486E9D6B68"), InterfaceType((short)1)]
    internal interface IDiaSession
    {
        [System.Runtime.InteropServices.DispIdAttribute(1)]
        ulong loadAddress { get; set; }
        IDiaSymbol globalScope { [return: MarshalAs(UnmanagedType.Interface)] get; }
        void getEnumTables(/*out Dia2Lib.IDiaEnumTables ppEnumTables*/);
        void getSymbolsByAddr(/*out Dia2Lib.IDiaEnumSymbolsByAddr ppEnumbyAddr*/);
        void findChildren(/*Dia2Lib.IDiaSymbol parent, Dia2Lib.SymTagEnum symTag, string name, uint compareFlags, out Dia2Lib.IDiaEnumSymbols ppResult*/);
        void findSymbolByAddr(/*uint isect, uint offset, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol*/);
        void findSymbolByRVA(/*uint rva, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol*/);
        void findSymbolByVA(/*ulong va, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol*/);
        void findSymbolByToken(/*uint token, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol*/);
        void symsAreEquiv(/*Dia2Lib.IDiaSymbol symbolA, Dia2Lib.IDiaSymbol symbolB*/);
        void symbolById(/*uint id, out Dia2Lib.IDiaSymbol ppSymbol*/);
        void findSymbolByRVAEx(/*uint rva, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol, out int displacement*/);
        void findSymbolByVAEx(/*ulong va, Dia2Lib.SymTagEnum symTag, out Dia2Lib.IDiaSymbol ppSymbol, out int displacement*/);
        void findFile(/*Dia2Lib.IDiaSymbol pCompiland, string name, uint compareFlags, out Dia2Lib.IDiaEnumSourceFiles ppResult*/);
        void findFileById(/*uint uniqueId, out Dia2Lib.IDiaSourceFile ppResult*/);
        void findLines(/*Dia2Lib.IDiaSymbol compiland, Dia2Lib.IDiaSourceFile file, out Dia2Lib.IDiaEnumLineNumbers ppResult*/);
        void findLinesByAddr(/*uint seg, uint offset, uint length, out Dia2Lib.IDiaEnumLineNumbers ppResult*/);
        void findLinesByRVA(/*uint rva, uint length, out Dia2Lib.IDiaEnumLineNumbers ppResult*/);
        void findLinesByVA(/*ulong va, uint length, out Dia2Lib.IDiaEnumLineNumbers ppResult*/);
        void findLinesByLinenum(/*Dia2Lib.IDiaSymbol compiland, Dia2Lib.IDiaSourceFile file, uint linenum, uint column, out Dia2Lib.IDiaEnumLineNumbers ppResult*/);
        void findInjectedSource(/*string srcFile, out Dia2Lib.IDiaEnumInjectedSources ppResult*/);
        void getEnumDebugStreams(out Dia2Lib.IDiaEnumDebugStreams ppEnumDebugStreams);
    }

    [System.Reflection.DefaultMemberAttribute("Item")]
    [System.Runtime.InteropServices.GuidAttribute("08CBB41E-47A6-4F87-92F1-1C9C87CED044")]
    [System.Runtime.InteropServices.InterfaceTypeAttribute(1)]
    internal interface IDiaEnumDebugStreams
    {
        System.Collections.IEnumerator GetEnumerator();
        [System.Runtime.InteropServices.DispIdAttribute(1)]
        int count { get; }
        Dia2Lib.IDiaEnumDebugStreamData Item(object index);
        void Next(uint celt, out Dia2Lib.IDiaEnumDebugStreamData rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out Dia2Lib.IDiaEnumDebugStreams ppenum);
    }

    [System.Runtime.InteropServices.InterfaceTypeAttribute(1)]
    [System.Runtime.InteropServices.GuidAttribute("486943E8-D187-4A6B-A3C4-291259FFF60D")]
    [System.Reflection.DefaultMemberAttribute("Item")]
    internal interface IDiaEnumDebugStreamData
    {
        System.Collections.IEnumerator GetEnumerator();
        [System.Runtime.InteropServices.DispIdAttribute(1)]
        int count { get; }
        [System.Runtime.InteropServices.DispIdAttribute(2)]
        string name { get; }
        void Item(uint index, uint cbData, out uint pcbData, out byte pbData);
        void Next(uint celt, uint cbData, out uint pcbData, out byte pbData, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out Dia2Lib.IDiaEnumDebugStreamData ppenum);
    }
}