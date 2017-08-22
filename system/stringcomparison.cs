// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:  StringComparison
**
**
** Purpose: A mechanism to expose a simplified infrastructure for 
**          Comparing strings. This enum lets you choose of the custom 
**          implementations provided by the runtime for the user.
**
** 
===========================================================*/
namespace System{
    
    /// <summary>
    /// ָ�� System.String.Compare(System.String,System.String) �� System.String.Equals(System.Object)
    /// ������ĳЩ����Ҫʹ�õ����򡢴�Сд���������
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum StringComparison {
        
        /// <summary>
        /// ʹ�����������������͵�ǰ����Ƚ��ַ���
        /// </summary>
        CurrentCulture = 0,
        /// <summary>
        /// ʹ����������������򡢵�ǰ�������Ƚ��ַ�����ͬʱ���Ա��Ƚ��ַ����Ĵ�Сд
        /// </summary>
        CurrentCultureIgnoreCase = 1,
        /// <summary>
        /// ʹ�����������������͹̶�����Ƚ��ַ�����
        /// </summary>
        InvariantCulture = 2,
        /// <summary>
        /// ʹ����������������򡢹̶��������Ƚ��ַ�����ͬʱ���Ա��Ƚ��ַ����Ĵ�Сд��
        /// </summary>
        InvariantCultureIgnoreCase = 3,
        /// <summary>
        /// ʹ������������Ƚ��ַ�����
        /// </summary>
        Ordinal = 4,
        /// <summary>
        /// ʹ�����������򲢺��Ա��Ƚ��ַ����Ĵ�Сд�����ַ������бȽϡ�
        /// </summary>
        OrdinalIgnoreCase = 5,
    }
}
