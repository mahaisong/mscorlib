// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:   FileAccess
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Enum describing whether you want read and/or write
** permission to a file.
**
**
===========================================================*/

using System;

namespace System.IO {
    // Contains constants for specifying the access you want for a file.
    // You can have Read, Write or ReadWrite access. 

    /// <summary>
    /// ���������ļ���ȡ��д����ȡ/д�����Ȩ�޵ĳ�����
    /// </summary>
    [Serializable]
    [Flags]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum FileAccess
    {
        // Specifies read access to the file. Data can be read from the file and
        // the file pointer can be moved. Combine with WRITE for read-write access.
        /// <summary>
        /// ���ļ��Ķ����ʡ��ɴ��ļ��ж�ȡ���ݡ��� Write ����Խ��ж�д���ʡ�
        /// </summary>
        Read = 1,

        // Specifies write access to the file. Data can be written to the file and
        // the file pointer can be moved. Combine with READ for read-write access.
        /// <summary>
        /// �ļ���д���ʡ��ɽ�����д���ļ���ͬ Read ��ϼ����ɶ�/д����Ȩ��
        /// </summary>
        Write = 2,

        // Specifies read and write access to the file. Data can be written to the
        // file and the file pointer can be moved. Data can also be read from the 
        // file.
        /// <summary>
        /// ���ļ��Ķ����ʺ�д���ʡ��ɴ��ļ���ȡ���ݺͽ�����д���ļ���
        /// </summary>
        ReadWrite = 3,
    }
}
