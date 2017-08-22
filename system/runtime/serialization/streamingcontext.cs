// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** ValueType: StreamingContext
**
**
** Purpose: A value type indicating the source or destination of our streaming.
**
**
===========================================================*/
namespace System.Runtime.Serialization {

    using System.Runtime.Remoting;
    using System;
    /// <summary>
    /// �������������л�����Դ��Ŀ�꣬���ṩһ���ɵ��÷�����ĸ��������ġ�
    /// </summary>
    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
    public struct StreamingContext {
        /// <summary>
        /// ������Ϣ
        /// </summary>
        internal Object m_additionalContext;
        /// <summary>
        /// ������״̬��ʼ��
        /// </summary>
        internal StreamingContextStates m_state;
        /// <summary>
        /// ʹ�ø�����������״̬��ʼ�� StreamingContext �����ʵ����
        /// </summary>
        /// <param name="state">������״̬��ʼ��</param>
        public StreamingContext(StreamingContextStates state) 
            : this (state, null) {
        }
        /// <summary>
        /// ʹ�ø�����������״̬�Լ�һЩ������Ϣ����ʼ�� StreamingContext �����ʵ����
        /// </summary>
        /// <param name="state">������״̬��ʼ��</param>
        /// <param name="additional">������Ϣ</param>
        public StreamingContext(StreamingContextStates state, Object additional) {
            m_state = state;
            m_additionalContext = additional;
        }
        /// <summary>
        /// ��ȡָ��Ϊ����������һ���ֵ������ġ�
        /// </summary>
        public Object Context {
            get { return m_additionalContext; }
        }
        /// <summary>
        /// ȷ������ StreamingContext ʵ���Ƿ������ͬ��ֵ�� ����д ValueType.Equals(Object)����
        /// </summary>
        /// <param name="obj">StreamingContext ʵ��</param>
        /// <returns>�Ƿ���ͬ</returns>
        public override bool Equals(Object obj) {
            if (!(obj is StreamingContext)) {//�������StreamiingContext,�򷵻�False
                return false;
            }
            if (((StreamingContext)obj).m_additionalContext == m_additionalContext &&//��������ĺͶ�����Ϣ��ͬ���򷵻�True
                ((StreamingContext)obj).m_state == m_state) {
                return true;
            } 
            return false;
        }
        /// <summary>
        /// ���ظö���Ĺ�ϣ���롣 ����д ValueType.GetHashCode()����
        /// </summary>
        /// <returns>��ϣ��</returns>
        public override int GetHashCode() {
            return (int)m_state;
        }
        /// <summary>
        /// ��ȡ�������ݵ�Դ��Ŀ�ꡣ
        /// </summary>
        public StreamingContextStates State {
            get { return m_state; } 
        }
    }
    
    // **********************************************************
    // Keep these in [....] with the version in vm\runtimehandles.h
    // **********************************************************
    /// <summary>
    /// ����һ����Ǽ������������л�������ָ������Դ��Ŀ�������ġ�
    /// </summary>
[Serializable]
[Flags]
[System.Runtime.InteropServices.ComVisible(true)]
    public enum StreamingContextStates {
        /// <summary>
        /// ָ��Դ��Ŀ����������ͬһ������ϵ�����һ�����̡�
        /// </summary>
        CrossProcess=0x01,
        /// <summary>
        /// ָ��Դ��Ŀ��������������һ̨�������
        /// </summary>
        CrossMachine=0x02,
        /// <summary>
        /// ָ��Դ��Ŀ�����������ļ���
        /// �û����Լٶ��ļ��ĳ���ʱ�䳤�ڴ������ǵĽ��̣������ļ����ض���ʽ���������л����˷�ʽ����ʹ�����л�����Ҫ����ʵ�ǰ�����е��κ����ݡ�
        /// </summary>
        File        =0x04,
        /// <summary>
        /// ָ��Դ��Ŀ���������ǳ����Ĵ洢���������԰������ݿ⡢�ļ��������󱸴洢����
        /// �û����Լٶ��������ݵĳ���ʱ�䳤�ڴ������ݵĽ��̣����ҳ����������ض���ʽ���������л����˷�ʽ����ʹ�����л�����Ҫ����ʵ�ǰ�����е��κ����ݡ�
        /// </summary>
        Persistence =0x08,
        /// <summary>
        /// ָ��������δ֪λ�õ��������н���Զ�̴��� �û��޷��ٶ����Ƿ���ͬһ̨������ϡ�
        /// </summary>
        Remoting    =0x10,
        /// <summary>
        /// ָ�����л�������δ֪��
        /// </summary>
        Other       =0x20,
        /// <summary>
        /// ָ������ͼ�����ڽ��п�¡��
        /// �û����Լٶ���¡ͼ�ν�������ͬһ�����д��ڣ����԰�ȫ�ط��ʾ���������Է��й���Դ�����á�
        /// </summary>
        Clone       =0x40,
        /// <summary>
        /// ָ��Դ��Ŀ��������������һ�� AppDomain��
        /// </summary>
        CrossAppDomain =0x80,
        /// <summary>
        /// ָ�������������κ������Ĵ��䣨��������κ������Ľ��գ����л����ݡ�
        /// </summary>
        All         =0xFF,
    }
}
