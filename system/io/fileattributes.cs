// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  FileAttributes
** 
** Purpose: File attribute flags corresponding to NT's flags.
**
** 
===========================================================*/
using System;

namespace System.IO {
    // File attributes for use with the FileEnumerator class.
    // These constants correspond to the constants in WinNT.h.
    // 
    /// <summary>
    /// �ṩ�ļ���Ŀ¼�����ԡ�
    /// </summary>
    [Serializable]
    [Flags]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum FileAttributes
    {
        // From WinNT.h (FILE_ATTRIBUTE_XXX)
        /// <summary>
        /// ���ļ���ֻ���ġ�
        /// </summary>
        ReadOnly = 0x1,
        /// <summary>
        /// �ļ������صģ����û�а�������ͨ��Ŀ¼�б��С�
        /// </summary>
        Hidden = 0x2,
        /// <summary>
        /// ���ļ���ϵͳ�ļ����������ļ��ǲ���ϵͳ��һ���ֻ����ɲ���ϵͳ�Զ�ռ��ʽʹ�á�
        /// </summary>
        System = 0x4,
        /// <summary>
        /// ���ļ���һ��Ŀ¼��
        /// </summary>
        Directory = 0x10,
        /// <summary>
        /// ���ļ��Ǳ��ݻ��Ƴ��ĺ�ѡ�ļ���
        /// </summary>
        Archive = 0x20,
        /// <summary>
        /// ����������ʹ�á�
        /// </summary>
        Device = 0x40,
        /// <summary>
        /// ���ļ���û���������Եı�׼�ļ��������䵥��ʹ��ʱ�������Բ���Ч��
        /// </summary>
        Normal = 0x80,
        /// <summary>
        /// �ļ�����ʱ�ļ�����ʱ�ļ�������ִ��Ӧ�ó���ʱ��Ҫ�ģ�����Ӧ�ó�����ɺ���Ҫ�����ݡ�
        /// �ļ�ϵͳ���Խ��������ݱ������ڴ��У������ǽ�����ˢ�»ش������洢���Ա���Կ��ٷ��ʡ�
        /// ����ʱ�ļ�������Ҫʱ��Ӧ�ó���Ӧ����ɾ������
        /// </summary>
        Temporary = 0x100,
        /// <summary>
        /// ���ļ���ϡ���ļ���ϡ���ļ�һ��������ͨ��Ϊ��Ĵ��ļ���
        /// </summary>
        SparseFile = 0x200,
        /// <summary>
        /// �ļ�����һ�����·����㣬����һ�����ļ���Ŀ¼�������û���������ݿ顣
        /// </summary>
        ReparsePoint = 0x400,
        /// <summary>
        /// ���ļ���ѹ���ļ���
        /// </summary>
        Compressed = 0x800,
        /// <summary>
        /// ���ļ������ѻ�״̬���ļ����ݲ���������ʹ�á�
        /// </summary>
        Offline = 0x1000,
        /// <summary>
        /// ������ͨ������ϵͳ�����������������������ļ���
        /// </summary>
        NotContentIndexed = 0x2000,
        /// <summary>
        /// ���ļ���Ŀ¼�Ѽ��ܡ������ļ���˵����ʾ�ļ��е��������ݶ��Ǽ��ܵġ�
        /// ����Ŀ¼��˵����ʾ�´������ļ���Ŀ¼��Ĭ��������Ǽ��ܵġ�
        /// </summary>
        Encrypted = 0x4000,

#if !FEATURE_CORECLR
#if FEATURE_COMINTEROP
        /// <summary>
        /// �ļ���Ŀ¼����������֧�����ݡ��ڴ�ֵ�������ļ�ʱ���ļ��е���������������������֧�֡�
        /// ��ֵ��Ӧ����һ��Ŀ¼ʱ���������ļ�����Ŀ¼�ڸ�Ŀ¼�к�Ĭ�������Ӧ����������֧�֡�
        /// </summary>
        [System.Runtime.InteropServices.ComVisible(false)]        
#endif // FEATURE_COMINTEROP
        IntegrityStream = 0x8000,

#if FEATURE_COMINTEROP
        /// <summary>
        /// �ļ���Ŀ¼��������ɨ���������ų���
        /// ��ֵ��Ӧ����һ��Ŀ¼ʱ���������ļ�����Ŀ¼�ڸ�Ŀ¼�к�Ĭ�������Ӧ���������������ԡ�
        /// </summary>
        [System.Runtime.InteropServices.ComVisible(false)]        
#endif // FEATURE_COMINTEROP
        NoScrubData = 0x20000,
#endif
    }
}
