// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  IEnumerator
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Base interface for all enumerators.
**
** 
===========================================================*/
namespace System.Collections {    
    using System;
    using System.Runtime.InteropServices;

    // Base interface for all enumerators, providing a simple approach
    // to iterating over a collection.
    [Guid("496B0ABF-CDEE-11d3-88E8-00902754C43A")]
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface IEnumerator
    {
        // �ӿڲ�ö���������л��Ľ���ö�ٵ���һ��Ԫ��,������һ������ֵ��ʾһ��Ԫ���Ƿ���á�
        // �ڴ���,һ��ö���������϶�λ��ö�ٵĵ�һ��Ԫ��֮ǰ,�͵�һ�ε���MoveNext������һ��Ԫ�ص�ö�١�
        /// <summary>
        /// ��ö�����ƽ������ϵ���һ��Ԫ�ء�
        /// </summary>
        /// <returns>���ö�����ѳɹ����ƽ�����һ��Ԫ�أ���Ϊ true�����ö�������ݵ����ϵ�ĩβ����Ϊ false��</returns>
        bool MoveNext();

        // ���ص�ǰԪ�ص�ö�١�
        // ���ص�ֵ��δ�����ڵ�һ�ε���MoveNext����MoveNext��,����false��
        // �������GetCurrentû�и�ԤMoveNext���ý�������ͬ�Ķ���
        /// <summary>
        /// ��ȡ������λ��ö������ǰλ�õ�Ԫ�ء�
        /// </summary>
        Object Current {
            get; 
        }

        // ���ü�����ö�ٵĿ�ʼ,���¿�ʼ��
        // ���õ���ѡ��Ϊ�Ƿ�����ͬ��ö�١�
        // ����ζ��������޸ĵײ㼯��Ȼ���������,IEnumerator������Ч��,��������������г�ΪMoveNext��Current��
        /// <summary>
        /// ��ö��������Ϊ���ʼλ�ã���λ��λ�ڼ����е�һ��Ԫ��֮ǰ��
        /// </summary>
        void Reset();
    }
}
