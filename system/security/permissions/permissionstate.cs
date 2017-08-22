// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
//  PermissionState.cs
// 
// <OWNER>ShawnFa</OWNER>
//
//   
//      ��������ȫ���޻���ȫ������״̬�´���Ȩ�ޡ� ��ȫ����״̬���������Դ�����κη��ʣ���ȫ������״̬������ض���Դ�������з��ʡ� ���磬�ļ�Ȩ�޹��캯�����Դ���һ�����󣬸ö����ʾ���ܶ��κ��ļ������κη��ʻ�ɶ�ȫ���ļ��������з��ʡ�
//      ÿ��Ȩ�����;���ȷ�����˼��˵�״̬����ʾ�������пɱ��ֵ�����Ȩ�޻�û���κ�Ȩ�ޡ� ��ˣ���������ȫ���޻���ȫ������״̬�´����������ض�Ȩ����Ϣ��һ��Ȩ�ޣ����ǣ��м�״ֻ̬�ܸ����ض���Ȩ������������á�
//      �� .NET Framework ��ʵ�ֵ����д������Ȩ�޿ɽ� PermissionState ֵ��Ϊ�乹�캯���Ĳ�����
//

namespace System.Security.Permissions {
    
    using System;
    /// <summary>
    /// ָ��Ȩ���ڴ���ʱ�Ƿ����Դ�����з���Ȩ�޻�û���κη���Ȩ�ޡ�
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum PermissionState
    {
        /// <summary>
        /// ���ԶԸ�Ȩ������������Դ������ȫ���ʡ�
        /// </summary>
        Unrestricted = 1,
        /// <summary>
        /// ���ܶԸ�Ȩ������������Դ���з��ʡ�
        /// </summary>
        None = 0,
    } 
    
}
