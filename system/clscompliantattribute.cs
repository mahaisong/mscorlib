// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: CLSCompliantAttribute
**
**
** Purpose: Container for assemblies.
**
**
=============================================================================*/

namespace System {

    /// <summary>
    /// ָʾ����Ԫ���Ƿ���Ϲ������Թ淶 (CLS)�� ���಻�ܱ��̳С�
    /// </summary>
    [Serializable]
    [AttributeUsage (AttributeTargets.All, Inherited=true, AllowMultiple=false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class CLSCompliantAttribute : Attribute 
    {
        /// <summary>
        /// �Ƿ���Ϲ������Թ淶
        /// </summary>
        private bool m_compliant;

        /// <summary>
        /// �ò���ֵ��ʼ�� System.CLSCompliantAttribute ���ʵ������ֵָʾ��ָʾ�ĳ���Ԫ���Ƿ���� CLS��
        /// </summary>
        /// <param name="isCompliant">�������Ԫ�ط��� CLS����Ϊ true������Ϊ false��</param>
        public CLSCompliantAttribute (bool isCompliant)
        {
            m_compliant = isCompliant;
        }

        /// <summary>
        /// ��ȡָʾ��ָʾ�ĳ���Ԫ���Ƿ���� CLS �Ĳ���ֵ��
        /// </summary>
        public bool IsCompliant 
        {
            get 
            {
                return m_compliant;
            }
        }
    }
}
