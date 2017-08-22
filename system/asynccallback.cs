// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface: AsyncCallbackDelegate
**
** Purpose: Type of callback for async operations
**
===========================================================*/
namespace System {

    /// <summary>
    /// ��������Ӧ�첽�������ʱ���õķ���
    /// </summary>
    /// <param name="ar"></param>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public delegate void AsyncCallback(IAsyncResult ar);

}
