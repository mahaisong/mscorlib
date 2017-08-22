// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Security.Policy;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// ����йܴ��빫�� System.Activator �ࡣ
    /// </summary>
    [GuidAttribute("03973551-57A1-3900-A2B5-9083E3FF2943")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliant(false)]
    [TypeLibImportClassAttribute(typeof(System.Activator))]
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface _Activator
    {
#if !FEATURE_CORECLR
        /// <summary>
        /// ���������ṩ��������Ϣ�ӿڵ�������0 �� 1����
        /// </summary>
        /// <param name="pcTInfo">�˷�������ʱ����һ�����ڽ��ն����ṩ��������Ϣ�ӿ�������λ��ָ�롣 �ò���δ����ʼ���������ݡ�</param>
        void GetTypeInfoCount(out uint pcTInfo);

        /// <summary>
        /// ���������������Ϣ��Ȼ�����ʹ�ø���Ϣ��ȡ�ӿڵ�������Ϣ��
        /// </summary>
        /// <param name="iTInfo">Ҫ���ص�������Ϣ��</param>
        /// <param name="lcid"></param>
        /// <param name="ppTInfo"></param>
        void GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo);

        /// <summary>
        /// ��һ������ӳ��Ϊ��Ӧ��һ����ȱ�ʶ����
        /// </summary>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="rgszNames"> Ҫӳ������Ƶ����顣</param>
        /// <param name="cNames">Ҫӳ������Ƶļ�����</param>
        /// <param name="lcid">Ҫ�����н������Ƶ��������������ġ�</param>
        /// <param name="rgDispId">���÷���������飬���ն�Ӧ����Щ���Ƶı�ʶ����</param>
        void GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId);

        /// <summary>
        /// �ṩ��ĳһ���󹫿������Ժͷ����ķ��ʡ�
        /// </summary>
        /// <param name="dispIdMember">��Ա�ı�ʶ����</param>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="lcid">Ҫ�����н��Ͳ������������������ġ�</param>
        /// <param name="wFlags">�������õ������ĵı�־��</param>
        /// <param name="pDispParams">ָ��һ���ṹ��ָ�룬�ýṹ����һ���������顢һ������������ DISPID �������������Ԫ�صļ�����</param>
        /// <param name="pVarResult">ָ��һ�����洢�����λ�õ�ָ�롣</param>
        /// <param name="pExcepInfo">ָ��һ�������쳣��Ϣ�Ľṹ��ָ�롣</param>
        /// <param name="puArgErr">��һ�����������������</param>
        void Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr);
#endif
    }

    /// <summary>
    /// ����йܴ��빫�� System.Attribute �ࡣ
    /// </summary>
    [GuidAttribute("917B14D0-2D9E-38B8-92A9-381ACF52F7C0")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliant(false)]
    [TypeLibImportClassAttribute(typeof(System.Attribute))]
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface _Attribute
    {
#if !FEATURE_CORECLR
        /// <summary>
        /// ���������ṩ��������Ϣ�ӿڵ�������0 �� 1����
        /// </summary>
        /// <param name="pcTInfo"></param>
        void GetTypeInfoCount(out uint pcTInfo);

        /// <summary>
        /// ���������������Ϣ��Ȼ�����ʹ�ø���Ϣ��ȡ�ӿڵ�������Ϣ��
        /// </summary>
        /// <param name="iTInfo">Ҫ���ص�������Ϣ��</param>
        /// <param name="lcid">������Ϣ���������ñ�ʶ����</param>
        /// <param name="ppTInfo">ָ�������������Ϣ�����ָ�롣</param>
        void GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo);

        /// <summary>
        /// ��һ������ӳ��Ϊ��Ӧ��һ����ȱ�ʶ����
        /// </summary>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="rgszNames">Ҫӳ������Ƶ����顣</param>
        /// <param name="cNames">Ҫӳ������Ƶļ�����</param>
        /// <param name="lcid">Ҫ�����н������Ƶ��������������ġ�</param>
        /// <param name="rgDispId">���÷���������飬���ն�Ӧ����Щ���Ƶı�ʶ����</param>
        void GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId);

        /// <summary>
        /// �ṩ��ĳһ���󹫿������Ժͷ����ķ��ʡ�
        /// </summary>
        /// <param name="dispIdMember">��Ա�ı�ʶ����</param>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="lcid">Ҫ�����н��Ͳ������������������ġ�</param>
        /// <param name="wFlags">�������õ������ĵı�־��</param>
        /// <param name="pDispParams">ָ��һ���ṹ��ָ�룬�ýṹ����һ���������顢һ������������ DISPID �������������Ԫ�صļ�����</param>
        /// <param name="pVarResult">ָ��һ�����洢�����λ�õ�ָ�롣</param>
        /// <param name="pExcepInfo">ָ��һ�������쳣��Ϣ�Ľṹ��ָ�롣</param>
        /// <param name="puArgErr">��һ�����������������</param>
        void Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr);
#endif
    }

    /// <summary>
    /// ����йܴ��빫�� System.Threading.Thread �ࡣ
    /// </summary>
    [GuidAttribute("C281C7F1-4AA9-3517-961A-463CFED57E75")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliant(false)]
    [TypeLibImportClassAttribute(typeof(System.Threading.Thread))]
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface _Thread
    {
#if !FEATURE_CORECLR
        /// <summary>
        /// ���������ṩ��������Ϣ�ӿڵ�������0 �� 1����
        /// </summary>
        /// <param name="pcTInfo">�˷�������ʱ����һ�����ڽ��ն����ṩ��������Ϣ�ӿ�������λ��ָ�롣 �ò���δ����ʼ���������ݡ�</param>
        void GetTypeInfoCount(out uint pcTInfo);

        /// <summary>
        /// ���������������Ϣ��Ȼ�����ʹ�ø���Ϣ��ȡ�ӿڵ�������Ϣ��
        /// </summary>
        /// <param name="iTInfo">Ҫ���ص�������Ϣ��</param>
        /// <param name="lcid">������Ϣ���������ñ�ʶ����</param>
        /// <param name="ppTInfo">ָ�������������Ϣ�����ָ�롣</param>
        void GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo);

        /// <summary>
        /// ��һ������ӳ��Ϊ��Ӧ��һ����ȱ�ʶ����
        /// </summary>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="rgszNames">Ҫӳ������Ƶ����顣</param>
        /// <param name="cNames">Ҫӳ������Ƶļ�����</param>
        /// <param name="lcid">Ҫ�����н������Ƶ��������������ġ�</param>
        /// <param name="rgDispId">���÷���������飬���ն�Ӧ����Щ���Ƶı�ʶ����</param>
        void GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId);

        /// <summary>
        /// �ṩ��ĳһ���󹫿������Ժͷ����ķ��ʡ�
        /// </summary>
        /// <param name="dispIdMember">��Ա�ı�ʶ����</param>
        /// <param name="riid">����������ʹ�á� ����Ϊ IID_NULL��</param>
        /// <param name="lcid">Ҫ�����н��Ͳ������������������ġ�</param>
        /// <param name="wFlags">�������õ������ĵı�־��</param>
        /// <param name="pDispParams">ָ��һ���ṹ��ָ�룬�ýṹ����һ���������顢һ������������ DISPID �������������Ԫ�صļ�����</param>
        /// <param name="pVarResult">ָ��һ�����洢�����λ�õ�ָ�롣</param>
        /// <param name="pExcepInfo">ָ��һ�������쳣��Ϣ�Ľṹ��ָ�롣</param>
        /// <param name="puArgErr">��һ�����������������</param>
        void Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr);
#endif
    }
}
