// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  IComparer
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Interface for comparing two Objects.
**
** 
===========================================================*/
namespace System.Collections {
    
    using System;
    // ����һ�ֱȽ���������ķ�����
    // �ӿڲ��ܱ����л�
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface IComparer {

        /// <summary>
        /// �Ƚ��������󲢷���һ��ֵ����ֵָʾһ������С�ڡ����ڻ��Ǵ�����һ������
        /// </summary>
        /// <param name="x">Ҫ�Ƚϵĵ�һ������</param>
        /// <param name="y">Ҫ�Ƚϵĵڶ�������</param>
        /// <returns>һ���з���������ָʾ x �� y �����ֵ�����±���ʾ��</returns>
        int Compare(Object x, Object y);
    }
}
