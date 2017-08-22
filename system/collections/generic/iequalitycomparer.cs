// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// <OWNER>kimhamil</OWNER>
// 

namespace System.Collections.Generic {
    using System;

    // ͨ��IEqualityComparer�ӿ�ʵ�ַ���,
    // ������������������ȵ�,Ϊһ����������Hashcode��
    // �������ֵ��ࡣ
    /// <summary>
    /// ���巽����֧�ֶ������ȱȽϡ�
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEqualityComparer<in T>
    {
        bool Equals(T x, T y);
        int GetHashCode(T obj);                
    }
}

