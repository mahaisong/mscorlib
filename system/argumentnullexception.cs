// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: ArgumentNullException
**
**
** Purpose: Exception class for null arguments to a method.
**
**
=============================================================================*/

namespace System {
    
    using System;
    using System.Runtime.Serialization;
    using System.Runtime.Remoting;
    using System.Security.Permissions;
    
    // ��һ��������Ӧ��Ϊ��ʱ���׳�һ�������쳣
    [System.Runtime.InteropServices.ComVisible(true)]
    [Serializable] 
    public class ArgumentNullException : ArgumentException
    {
        // ����һ���µ�ArgumentNullException����Ϣ�ַ�������ΪĬ����Ϣ����һ��������null��
       public ArgumentNullException() 
            : base(Environment.GetResourceString("ArgumentNull_Generic")) {
                SetErrorCode(__HResults.E_POINTER);//ʹ��E_POINTER - COMʹ�ÿ�ָ�롣�����ǡ���Ч��ָ�롱
        }

        public ArgumentNullException(String paramName) 
            : base(Environment.GetResourceString("ArgumentNull_Generic"), paramName) {
            SetErrorCode(__HResults.E_POINTER);
        }

        public ArgumentNullException(String message, Exception innerException) 
            : base(message, innerException) {
            SetErrorCode(__HResults.E_POINTER);
        }
            
        public ArgumentNullException(String paramName, String message) 
            : base(message, paramName) {
            SetErrorCode(__HResults.E_POINTER);   
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        protected ArgumentNullException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}
