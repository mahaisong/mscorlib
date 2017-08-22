// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
namespace System {
    
    using System;
    
    // Enum used to indicate all the elements of the
    // VOS it is valid to attach this element to.
[Serializable]
    [Flags]
[System.Runtime.InteropServices.ComVisible(true)]
    public enum AttributeTargets
    {
        /// <summary>
        /// ����
        /// </summary>
        Assembly      = 0x0001,
        /// <summary>
        /// ģ��
        /// </summary>
        Module        = 0x0002,
        /// <summary>
        /// ��
        /// </summary>
        Class         = 0x0004,
        /// <summary>
        /// �ṹ
        /// </summary>
        Struct        = 0x0008,
        /// <summary>
        /// ö��
        /// </summary>
        Enum          = 0x0010,
        /// <summary>
        /// ������
        /// </summary>
        Constructor   = 0x0020,
        /// <summary>
        /// ����
        /// </summary>
        Method        = 0x0040,
        /// <summary>
        /// ����
        /// </summary>
        Property      = 0x0080,
        /// <summary>
        /// �ֶ�
        /// </summary>
        Field         = 0x0100,
        /// <summary>
        /// �¼�
        /// </summary>
        Event         = 0x0200,
        /// <summary>
        /// �ӿ�
        /// </summary>
        Interface     = 0x0400,
        /// <summary>
        /// ����
        /// </summary>
        Parameter     = 0x0800,
        /// <summary>
        /// ί��
        /// </summary>
        Delegate      = 0x1000,
        /// <summary>
        /// ����ֵ
        /// </summary>
        ReturnValue   = 0x2000,
        //@todo GENERICS: document GenericParameter
        /// <summary>
        /// ���Ͳ���
        /// </summary>
        GenericParameter = 0x4000,
        
        
        All           = Assembly | Module   | Class | Struct | Enum      | Constructor | 
                        Method   | Property | Field | Event  | Interface | Parameter   | 
                        Delegate | ReturnValue | GenericParameter,
    }
}
