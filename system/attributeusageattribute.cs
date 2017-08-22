// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  AttributeUsageAttribute
**
**
** Purpose: The class denotes how to specify the usage of an attribute
**          
**
===========================================================*/
namespace System {

    using System.Reflection;
    /// <summary>
    /// ָ����һ��������÷��� ���಻�ܱ��̳С�
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AttributeUsageAttribute : Attribute
    {
        /// <summary>
        /// Ĭ�����Ե�ʹ�÷�ΧΪALL
        /// </summary>
        internal AttributeTargets m_attributeTarget = AttributeTargets.All; // Defaults to all
        /// <summary>
        /// �ܷ�Ϊһ������Ԫ��ָ�����ָʾ����ʵ����Ĭ��ΪFalse
        /// </summary>
        internal bool m_allowMultiple = false; // Defaults to false
        /// <summary>
        /// ָʾ�������ܷ������������д��Ա�̳С�
        /// </summary>
        internal bool m_inherited = true; // Defaults to true
    
        internal static AttributeUsageAttribute Default = new AttributeUsageAttribute(AttributeTargets.All);

       //Constructors 
        public AttributeUsageAttribute(AttributeTargets validOn) {
            m_attributeTarget = validOn;
        }
       internal AttributeUsageAttribute(AttributeTargets validOn, bool allowMultiple, bool inherited) {
           m_attributeTarget = validOn;
           m_allowMultiple = allowMultiple;
           m_inherited = inherited;
       }
    
       
        /// <summary>
        /// ��ȡһ��ֵ������ֵ��ʶָʾ�����Կ�Ӧ�õ��ĳ���Ԫ�ء�
        /// </summary>
        public AttributeTargets ValidOn 
        {
           get{ return m_attributeTarget; }
        }
    
        /// <summary>
        /// ��ȡ������һ������ֵ����ֵָʾ�ܷ�Ϊһ������Ԫ��ָ�����ָʾ����ʵ����
        /// </summary>
        public bool AllowMultiple 
        {
            get { return m_allowMultiple; }
            set { m_allowMultiple = value; }
        }
    
        /// <summary>
        /// ��ȡ������һ������ֵ����ֵָʾָʾ�������ܷ������������д��Ա�̳С�
        /// </summary>
        public bool Inherited 
        {
            get { return m_inherited; }
            set { m_inherited = value; }
        }
    }
}
