// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
namespace System.Runtime.InteropServices {
    using System;
    /// <summary>
    /// ���Ƶ����������йܴ���ʱ����Ĳ��֡�
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    [Serializable]
    public enum LayoutKind
    {
        /// <summary>
        /// ����ĳ�Ա�������Ǳ����������й��ڴ�ʱ���ֵ�˳�����β��֡���Щ��Ա����System.Runtime.InteropServices.StructLayoutAttribute.Pack
        /// ��ָ���ķ�װ���в��֣����ҿ��Բ������ġ�
        /// </summary>
        Sequential      = 0, // 0x00000008,
        /// <summary>
        /// ����ĸ�����Ա�ڷ��й��ڴ��еľ�ȷλ�ñ���ʽ���ơ�ÿ����Ա����ʹ��System.Runtime.InteropServices.FieldOffsetAttribute
        /// ָʾ���ֶ��������е�λ��
        /// </summary>
        Explicit        = 2, // 0x00000010,
        /// <summary>
        /// ����ʱ�Զ�Ϊ���й��ڴ��еĶ���ĳ�Աѡ���ʵ��Ĳ��֡�ʹ�ô�ö�ٳ�Ա����Ķ��������йܴ�����ⲿ���������������������쳣    
        /// </summary>
        Auto            = 3, // 0x00000000,
    }
}
