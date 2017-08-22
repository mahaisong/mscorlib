// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  ICloneable
**
** This interface is implemented by classes that support cloning.
**
===========================================================*/
namespace System {
    
    using System;
    // Defines an interface indicating that an object may be cloned.  Only objects 
    // that implement ICloneable may be cloned. The interface defines a single 
    // method which is called to create a clone of the object.   Object defines a method
    // MemberwiseClone to support default clone operations.
    // ����һ���ӿ�˵��һ����������ǿ�¡��ֻ��ʵ��ICloneable���ܿ�¡�Ķ���
    // �ӿڶ�����һ������,���Ǵ���һ����¡�Ķ��󡣶�������һ������MemberwiseClone֧��ȱʡ��¡������
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface ICloneable
    {
        // Interface does not need to be marked with the serializable attribute
        // Make a new object which is a copy of the object instanced.  This object may be either
        // deep copy or a shallow copy depending on the implementation of clone.  The default
        // Object support for clone does a shallow copy.
        // �ӿڲ���Ҫ���������л�������
        // �½�һ������ʵ�������󸱱�����������������ƻ�ǳ�������ݿ�¡��ʵ�֡�Ĭ�϶���֧�ֿ�¡һ��ǳ������
        Object Clone();
    }
}
