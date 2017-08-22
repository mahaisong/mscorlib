// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:   FileOptions
** 
** <OWNER>gpaperin</OWNER>
**
**
** Purpose: Additional options to how to create a FileStream.
**    Exposes the more obscure CreateFile functionality.
**
**
===========================================================*/

using System;
using System.Runtime.InteropServices;

namespace System.IO {
    // Maps to FILE_FLAG_DELETE_ON_CLOSE and similar values from winbase.h.
    // We didn't expose(��¶) a number of these values because we didn't believe 
    // a number of them made sense(�������) in managed code, at least not yet.
    /// <summary>
    /// ��ʾ���ڴ��� FileStream ����ĸ߼�ѡ�
    /// </summary>
    [Serializable]
    [Flags]
    [ComVisible(true)]
    public enum FileOptions
    {
        // NOTE: any change to FileOptions enum needs to be 
        // matched in the FileStream ctor for error validation
        /// <summary>
        /// ָʾ������ FileStream ����ʱ����Ӧʹ������ѡ�
        /// </summary>
        None = 0,
        /// <summary>
        /// ָʾϵͳӦͨ���κ��м仺�桢ֱ��д����̡�
        /// </summary>
        WriteThrough = unchecked((int)0x80000000),
        /// <summary>
        /// ָʾ�ļ��������첽��ȡ��д�롣
        /// </summary>
        Asynchronous = unchecked((int)0x40000000), // FILE_FLAG_OVERLAPPED
        // NoBuffering = 0x20000000,
        /// <summary>
        /// ָʾ��������ļ���ϵͳ�ɽ���ѡ�������Ż��ļ��������ʾ��
        /// </summary>
        RandomAccess = 0x10000000,
        /// <summary>
        /// ָʾ������ʹ��ĳ���ļ�ʱ���Զ�ɾ�����ļ���
        /// </summary>
        DeleteOnClose = 0x04000000,
        /// <summary>
        /// ָʾ����ͷ��β��˳������ļ���ϵͳ�ɽ���ѡ�������Ż��ļ��������ʾ��
        /// ���Ӧ�ó����ƶ�����������ʵ��ļ�ָ�룬���ܲ������Ż����棬����Ȼ��֤��������ȷ�ԡ�
        /// </summary>
        SequentialScan = 0x08000000,
        // AllowPosix = 0x01000000,  // FILE_FLAG_POSIX_SEMANTICS
        // BackupOrRestore,
        // DisallowReparsePoint = 0x00200000, // FILE_FLAG_OPEN_REPARSE_POINT
        // NoRemoteRecall = 0x00100000, // FILE_FLAG_OPEN_NO_RECALL
        // FirstPipeInstance = 0x00080000, // FILE_FLAG_FIRST_PIPE_INSTANCE
        /// <summary>
        /// ָʾ�ļ��Ǽ��ܵģ�ֻ��ͨ�����ڼ��ܵ�ͬһ�û��ʻ������ܡ�
        /// </summary>
        Encrypted = 0x00004000, // FILE_ATTRIBUTE_ENCRYPTED
    }
}

