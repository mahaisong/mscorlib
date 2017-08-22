// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** File:    AssemblyVersionCompatibility
**
** <EMAIL>Author:  Suzanne Cook</EMAIL>
**
** Purpose: defining the different flavor's assembly version compatibility
**
** Date:    June 4, 1999
**
===========================================================*/
namespace System.Configuration.Assemblies {
    
    using System;
    /// <summary>
    /// ���岻ͬ���ͳ��򼯰汾�ļ����ԡ�.NET Framework 1.0 ����û���ṩ����ܡ�
    /// </summary>
     [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum AssemblyVersionCompatibility
    {
         /// <summary>
        /// �ó����޷��������汾��ͬһ̨�������һ��ִ�С�
         /// </summary>
        SameMachine         = 1,
         /// <summary>
        /// �����޷��������汾��ͬһ������һ��ִ�С�
         /// </summary>
        SameProcess         = 2,
         /// <summary>
        /// �����޷��������汾��ͬһӦ�ó�������һ��ִ�С�
         /// </summary>
        SameDomain          = 3,
    }
}
