// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** File:    AssemblyHashAlgorithm
**
**
** Purpose: 
**
**
===========================================================*/
using System.Runtime.InteropServices;

namespace System.Configuration.Assemblies {
    
    using System;

    /// <summary>
    /// ָ�����ڹ�ϣ�ļ�����������ǿ���Ƶ����й�ϣ�㷨��
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum AssemblyHashAlgorithm
    {
        /// <summary>
        /// һ�����룬��ָʾ�޹�ϣ�㷨�����Ϊ��ģ�����ָ�� None���򹫹���������ʱĬ�ϲ��� SHA1 �㷨����Ϊ��ģ�������Ҫ���ɹ�ϣ��
        /// </summary>
        None = 0,
        /// <summary>
        /// ���� MD5 ��ϢժҪ�㷨��MD5 �� Rivest �� 1991 �꿪���ġ����� MD4 ������ͬ��ֻ�������˰�ȫ�ԡ�
        /// ����Ȼ�� MD4 ����һЩ��������ȫ�����㷨�����ĸ���ͬ�Ĳ��裬������� MD4 �����в�ͬ����ϢժҪ�Ĵ�С�Լ����Ҫ�󱣳ֲ��䡣
        /// </summary>
        MD5 = 0x8003,
        /// <summary>
        /// ���ڼ�������ȫ��ϣ�㷨���޶�������룬���޶�������� SHA �е�һ��δ�����Ĵ���
        /// </summary>
        SHA1 = 0x8004,
        /// <summary>
        /// ���ڼ�������ȫ��ϣ�㷨���İ汾�����룬���ϣֵ��СΪ 256 λ��
        /// </summary>
        [ComVisible(false)]
        SHA256 = 0x800c,
        /// <summary>
        /// ���ڼ�������ȫ��ϣ�㷨���İ汾�����룬���ϣֵ��СΪ 384 λ��
        /// </summary>
        [ComVisible(false)]
        SHA384 = 0x800d,
        /// <summary>
        /// ���ڼ�������ȫ��ϣ�㷨���İ汾�����룬���ϣֵ��СΪ 512 λ��
        /// </summary>
        [ComVisible(false)]
        SHA512 = 0x800e,
    }
}
