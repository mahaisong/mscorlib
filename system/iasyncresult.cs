// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface: IAsyncResult
**
** Purpose: Interface to encapsulate the results of an async
**          operation
**
===========================================================*/
namespace System {
    
    using System;
    using System.Threading;
[System.Runtime.InteropServices.ComVisible(true)]
    public interface IAsyncResult
    {
        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ�첽�����Ƿ�����ɡ�
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// ��ȡ���ڵȴ��첽������ɵ� WaitHandle��
        /// </summary>
        WaitHandle AsyncWaitHandle { get; }

        /// <summary>
        /// ��ȡ�û�����Ķ������޶�����������첽��������Ϣ��
        /// </summary>
        Object AsyncState      { get; }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ�첽�����Ƿ�ͬ����ɡ�
        /// </summary>
        bool CompletedSynchronously { get; }
   
    
    }

}
