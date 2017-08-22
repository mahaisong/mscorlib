// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:   FileMode
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Enum describing whether to create a new file or 
** open an existing one.
**
**
===========================================================*/
    
using System;

namespace System.IO {
    // Contains constants for specifying how the OS should open a file.
    // These will control whether you overwrite a file, open an existing
    // file, or some combination thereof.
    // 
    // To append to a file, use Append (which maps to OpenOrCreate then we seek
    // to the end of the file).  To truncate a file or create it if it doesn't 
    // exist, use Create.
    /// <summary>
    /// ָ������ϵͳ���ļ��ķ�ʽ��
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum FileMode
    {
        // Creates a new file. An exception is raised if the file already exists.
        /// <summary>
        /// ָ������ϵͳӦ�������ļ���
        /// ����Ҫ FileIOPermissionAccess.Write Ȩ�ޡ�����ļ��Ѵ��ڣ������� IOException�쳣��
        /// </summary>
        CreateNew = 1,

        // Creates a new file. If the file already exists, it is overwritten.
        /// <summary>
        /// ָ������ϵͳӦ�������ļ�������ļ��Ѵ��ڣ����������ǡ�����Ҫ FileIOPermissionAccess.Write Ȩ�ޡ�
        /// FileMode.Create ��Ч����������������ļ������ڣ���ʹ�� CreateNew��
        /// ����ʹ�� Truncate��������ļ��Ѵ��ڵ�Ϊ�����ļ��������� UnauthorizedAccessException�쳣��
        /// </summary>
        Create = 2,

        // Opens an existing file. An exception is raised if the file does not exist.
        /// <summary>
        /// ָ������ϵͳӦ�������ļ������ļ�������ȡ���� FileAccess ö����ָ����ֵ��
        /// ����ļ������ڣ�����һ�� System.IO.FileNotFoundException �쳣��
        /// </summary>
        Open = 3,

        // Opens the file if it exists. Otherwise, creates a new file.
        /// <summary>
        /// ָ������ϵͳӦ���ļ�������ļ����ڣ�������Ӧ�������ļ���
        /// ����� FileAccess.Read ���ļ�������Ҫ FileIOPermissionAccess.ReadȨ�ޡ�
        /// ����ļ�����Ϊ FileAccess.Write������Ҫ FileIOPermissionAccess.WriteȨ�ޡ�
        /// ����� FileAccess.ReadWrite ���ļ�����ͬʱ��Ҫ FileIOPermissionAccess.Read �� FileIOPermissionAccess.WriteȨ�ޡ�
        /// </summary>
        OpenOrCreate = 4,

        // Opens an existing file. Once opened, the file is truncated so that its
        // size is zero bytes. The calling process must open the file with at least
        // WRITE access. An exception is raised if the file does not exist.
        /// <summary>
        /// ָ������ϵͳӦ�������ļ������ļ�����ʱ�������ض�Ϊ���ֽڴ�С��
        /// ����Ҫ FileIOPermissionAccess.Write Ȩ�ޡ�
        /// ���Դ�ʹ�� FileMode.Truncate �򿪵��ļ��н��ж�ȡ������ ArgumentException �쳣��
        /// </summary>
        Truncate = 5,

        // Opens the file if it exists and seeks to the end.  Otherwise, 
        // creates a new file.
        /// <summary>
        /// �������ļ�����򿪸��ļ������ҵ��ļ�β�����ߴ���һ�����ļ���
        /// ����Ҫ FileIOPermissionAccess.Append Ȩ�ޡ� FileMode.Append ֻ���� FileAccess.Write һ��ʹ�á�
        /// ��ͼ�����ļ�β֮ǰ��λ��ʱ������ IOException �쳣�������κ���ͼ��ȡ�Ĳ�������ʧ�ܲ����� NotSupportedException �쳣��
        /// </summary>
        Append = 6,
    }
}
