// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
//
// ICustomAttributeProvider is an interface that is implemented by reflection
// 
// <OWNER>WESU</OWNER>
//    objects which support custom attributes.
//
// <EMAIL>Author: darylo & Rajesh Chandrashekaran (rajeshc)</EMAIL>
// Date: July 99
//
namespace System.Reflection {
    
    using System;

    /// <summary>
    /// Ϊ֧���Զ������Եķ�ӳ�����ṩ�Զ������ԡ�
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface ICustomAttributeProvider
    {

        /// <summary>
        /// ���ش˳�Ա�϶�����Զ������Ե����飨�����ͱ�ʶ�������������û���Զ������ԣ��򷵻ؿ����顣
        /// </summary>
        /// <param name="attributeType">�Զ������Ե����͡�</param>
        /// <param name="inherit">��Ϊ true ʱ�����Ҽ̳е��Զ������ԵĲ�νṹ����</param>
        /// <returns>��ʾ�Զ������ԵĶ�������������顣</returns>
        /// <exception cref="System.TypeLoadException">�޷������Զ����������͡�</exception>
        /// <exception cref="System.ArgumentNullException">attributeType Ϊ null</exception>
        Object[] GetCustomAttributes(Type attributeType, bool inherit);


        /// <summary>
        /// �����ڴ˳�Ա�϶���������Զ������ԣ��������Գ��⣩�����飬�����û���Զ������ԣ�����һ�������顣
        /// </summary>
        /// <param name="inherit">��Ϊ true ʱ�����Ҽ̳е��Զ������ԵĲ�νṹ����</param>
        /// <returns>��ʾ�Զ������ԵĶ�������������顣</returns>
        /// <exception cref="System.TypeLoadException">�޷������Զ����������͡�</exception>
        /// <exception cref="System.ArgumentNullException">attributeType Ϊ null</exception>
        Object[] GetCustomAttributes(bool inherit);

    
        /// <summary>
        /// ָʾ�Ƿ��ڴ˳�Ա�϶���һ������ attributeType ��ʵ����
        /// </summary>
        /// <param name="attributeType">�Զ������Ե����͡�</param>
        /// <param name="inherit">��Ϊ true ʱ�����Ҽ̳е��Զ������ԵĲ�νṹ����</param>
        /// <returns>����ڴ˳�Ա�϶��� attributeType����Ϊ true������Ϊ false��</returns>
        bool IsDefined (Type attributeType, bool inherit);
    
    }
}
