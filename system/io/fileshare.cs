// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:   FileShare
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Enum describing how to share files with other 
** processes - ie, whether two processes can simultaneously
** read from the same file.
**
**
===========================================================*/

using System;

namespace System.IO {
    // Contains constants for controlling file sharing options while
    // opening files.  You can specify what access other processes trying
    // to open the same file concurrently can have.
    //
    // Note these values currently match the values for FILE_SHARE_READ,
    // FILE_SHARE_WRITE, and FILE_SHARE_DELETE in winnt.h
    // 
    /// <summary>
    /// �������ڿ������� FileStream �����ͬһ�ļ����Ծ��еķ������͵ĳ�����
    /// </summary>
    [Serializable]
    [Flags]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum FileShare
    {
        // No sharing. Any request to open the file (by this process or another
        // process) will fail until the file is closed.
        /// <summary>
        /// л������ǰ�ļ���
        /// �ļ��ر�ǰ���򿪸��ļ����κ������ɴ˽��̻���һ���̷��������󣩶���ʧ�ܡ�
        /// </summary>
        None = 0,

        // Allows subsequent opening of the file for reading. If this flag is not
        // specified, any request to open the file for reading (by this process or
        // another process) will fail until the file is closed.
        /// <summary>
        /// ���������ļ���ȡ��
        /// ���δָ���˱�־�����ļ��ر�ǰ���κδ򿪸��ļ��Խ��ж�ȡ�������ɴ˽��̻���һ���̷��������󣩶���ʧ�ܡ�
        /// ���ǣ���ʹָ���˴˱�־���Կ�����Ҫ����Ȩ�޲��ܹ����ʸ��ļ���
        /// </summary>
        Read = 1,

        // Allows subsequent opening of the file for writing. If this flag is not
        // specified, any request to open the file for writing (by this process or
        // another process) will fail until the file is closed.
        /// <summary>
        /// ���������ļ�д�롣
        /// ���δָ���˱�־�����ļ��ر�ǰ���κδ򿪸��ļ��Խ���д��������ɴ˽��̻���һ�����̷��������󣩶���ʧ�ܡ�
        /// ���ǣ���ʹָ���˴˱�־���Կ�����Ҫ����Ȩ�޲��ܹ����ʸ��ļ���
        /// </summary>
        Write = 2,

        // Allows subsequent opening of the file for writing or reading. If this flag
        // is not specified, any request to open the file for writing or reading (by
        // this process or another process) will fail until the file is closed.
        /// <summary>
        /// ���������ļ���ȡ��д�롣
        /// ���δָ���˱�־�����ļ��ر�ǰ���κδ򿪸��ļ��Խ��ж�ȡ��д��������ɴ˽��̻���һ���̷���������ʧ�ܡ�
        /// ���ǣ���ʹָ���˴˱�־���Կ�����Ҫ����Ȩ�޲��ܹ����ʸ��ļ���
        /// </summary>
        ReadWrite = 3,

        // Open the file, but allow someone else to delete the file.
        /// <summary>
        /// �������ɾ���ļ���
        /// </summary>
        Delete = 4,

        // Whether the file handle should be inheritable by child processes.
        // Note this is not directly supported like this by Win32.
        /// <summary>
        /// ʹ�ļ���������ӽ��̼̳С�Win32 ��ֱ��֧�ִ˹��ܡ�
        /// </summary>
        Inheritable = 0x10,
    }
}
