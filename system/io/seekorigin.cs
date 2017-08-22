// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:   SeekOrigin
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Enum describing locations in a stream you could
** seek relative to.
**
**
===========================================================*/

using System;

namespace System.IO {
    // Provides seek reference points.  To seek to the end of a stream,
    // call stream.Seek(0, SeekOrigin.End).
    /// <summary>
    /// ָ��������λ���Բ���ʹ�á�
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum SeekOrigin
    {
        // These constants match Win32's FILE_BEGIN, FILE_CURRENT, and FILE_END
        /// <summary>
        /// ָ�����Ŀ�ͷ��
        /// </summary>
        Begin = 0,
        /// <summary>
        /// ָ�����ڵĵ�ǰλ�á�
        /// </summary>
        Current = 1,
        /// <summary>
        /// ָ�����Ľ�β��
        /// </summary>
        End = 2,
    }
}
