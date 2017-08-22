// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Interface: _Exception
**
**
** Purpose: COM backwards compatibility with v1 Exception
**        object layout.
**
**
=============================================================================*/

namespace System.Runtime.InteropServices {
    using System;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Security.Permissions;
    
    /// <summary>
    /// ����йܴ��빫�� System.Exception ��Ĺ�����Ա����API������CLS��
    /// </summary>
    [GuidAttribute("b36b5c63-42ef-38bc-a07e-0b34c98f164a")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsDual)]
    [CLSCompliant(false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface _Exception
    {
#if !FEATURE_CORECLR
        // This contains all of our V1 Exception class's members.

        // From Object
        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.ToString �����İ汾�޹صķ���
        /// </summary>
        /// <returns>�ַ���</returns>
        String ToString();
        /// <summary>
        /// Ϊ COM �����ṩ�� Object.Equals �����İ汾�޹صķ��ʡ�
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        bool Equals (Object obj);
        /// <summary>
        /// Ϊ COM �����ṩ�� Object.GetHashCode �����İ汾�޹صķ��ʡ�
        /// </summary>
        /// <returns></returns>
        int GetHashCode ();

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.GetType �����İ汾�޹صķ��ʡ�
        /// </summary>
        /// <returns></returns>
        Type GetType ();

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.Message ���Եİ汾�޹صķ��ʡ�
        /// </summary>
        String Message {
            get;
        }

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.GetBaseException �����İ汾�޹صķ��ʡ�
        /// </summary>
        /// <returns></returns>
        Exception GetBaseException();

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.StackTrace ���Եİ汾�޹صķ��ʡ�
        /// </summary>
        String StackTrace {
            get;
        }

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.HelpLink ���Եİ汾�޹صķ��ʡ�
        /// </summary>
        String HelpLink {
            get;
            set;
        }

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.Source ���Եİ汾�޹صķ��ʡ�
        /// </summary>
        String Source {
            #if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
            #endif
            get;
            #if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
            #endif
            set;
        }

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.GetObjectData �����İ汾�޹صķ���
        /// </summary>
        /// <param name="info">���л���Ϣ</param>
        /// <param name="context">����������</param>
        [System.Security.SecurityCritical]  // auto-generated_required
        void GetObjectData(SerializationInfo info, StreamingContext context);
#endif

        // ��������ǹ��������CoreCLR���⡣get_InnerException��newslot�����������MEF get_InnerException ComposablePartExceptionȡ������ʽ�ӿ�ʵ�ֵĻ������ṩ�ġ�
        // ֻ�����⡣get_InnerException������ġ�

        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.InnerException ���Եİ汾�޹صķ��ʡ�һЩ�ֻ�Ӧ�ó������MEF������Silverlight��
        /// </summary>
        Exception InnerException {
            get;
        }

#if !FEATURE_CORECLR     
        /// <summary>
        /// Ϊ COM �����ṩ�� Exception.TargetSite ���Եİ汾�޹صķ��ʡ�
        /// </summary>
        MethodBase TargetSite {
            get;
        }
#endif
   }

}
