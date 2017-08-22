// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
namespace System {
 
    using System;
    using System.Runtime.Serialization;
    /// <summary>
    /// ����ϵͳ�쳣�����ռ�Ļ��ࡣ
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class SystemException : Exception
    {
        /// <summary>
        /// ��ʼ�� SystemException �����ʵ����
        /// </summary>
        public SystemException() 
            : base(Environment.GetResourceString("Arg_SystemException")) {
            SetErrorCode(__HResults.COR_E_SYSTEM);
        }

        /// <summary>
        /// ʹ��ָ���Ĵ�����Ϣ��ʼ�� SystemException �����ʵ����
        /// </summary>
        /// <param name="message">�����������Ϣ��</param>
        public SystemException(String message) 
            : base(message) {
            SetErrorCode(__HResults.COR_E_SYSTEM);
        }

        /// <summary>
        /// ʹ��ָ��������Ϣ�Ͷ���Ϊ���쳣ԭ����ڲ��쳣����������ʼ�� SystemException �����ʵ����
        /// </summary>
        /// <param name="message">�����쳣ԭ��Ĵ�����Ϣ��</param>
        /// <param name="innerException">���µ�ǰ�쳣���쳣����� innerException �������ǿ����ã��� Visual Basic ��Ϊ Nothing�������ڴ����ڲ��쳣�� catch ����������ǰ�쳣��</param>
        public SystemException(String message, Exception innerException) 
            : base(message, innerException) {
            SetErrorCode(__HResults.COR_E_SYSTEM);
        }

        protected SystemException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}
