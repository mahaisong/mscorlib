// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// SecurityPermission.cs
// 
// <OWNER>[....]</OWNER>
//

namespace System.Security.Permissions
{
    using System;
    using System.IO;
    using System.Security.Util;
    using System.Text;
    using System.Threading;
    using System.Runtime.Remoting;
    using System.Security;
    using System.Runtime.Serialization;
    using System.Reflection;
    using System.Globalization;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Ϊ��ȫȨ�޶���ָ�����ʱ�־��
    /// </summary>
    [Serializable]
    [Flags]
[System.Runtime.InteropServices.ComVisible(true)]
#if !FEATURE_CAS_POLICY
    // The csharp compiler requires these types to be public, but they are not used elsewhere.
    [Obsolete("SecurityPermissionFlag is no longer accessible to application code.")]
#endif
    public enum SecurityPermissionFlag
    {
        /// <summary>
        /// �ް�ȫ�Է��ʡ�
        /// </summary>
        NoFlags = 0x00,
        /* The following enum value is used in the EE (ASSERT_PERMISSION in security.cpp)
         * Should this value change, make corresponding changes there
         */ 
        /// <summary>
        /// ���Դ˴�������е��÷����иò��������Ȩ�޵�������
        /// </summary>
        Assertion = 0x01,
        /// <summary>
        /// �ܹ����÷��йܴ��롣
        /// </summary>
        UnmanagedCode = 0x02,       // Update vm\Security.h if you change this !
        /// <summary>
        /// �ܹ������˳����ڶԴ������֤
        /// </summary>
        SkipVerification = 0x04,    // Update vm\Security.h if you change this !
        /// <summary>
        /// ʹ�������е�Ȩ�ޡ�
        /// </summary>
        Execution = 0x08,
        /// <summary>
        /// �ܹ����߳���ʹ��ĳЩ�߼�������
        /// </summary>
        ControlThread = 0x10,
        /// <summary>
        /// �ܹ��ṩ֤�ݣ������ܹ����Ĺ�����������ʱ���ṩ��֤�ݡ�
        /// </summary>
        ControlEvidence = 0x20,
        /// <summary>
        /// �ܹ��鿴���޸Ĳ��ԡ�
        /// </summary>
        ControlPolicy = 0x40,
        /// <summary>
        /// �ܹ��ṩ���л����� �����л���ʽ������ʹ�á�
        /// </summary>
        SerializationFormatter = 0x80,
        /// <summary>
        /// �ܹ�ָ������ԡ�
        /// </summary>
        ControlDomainPolicy = 0x100,
        /// <summary>
        /// �ܹ��ٿ��û�����
        /// </summary>
        ControlPrincipal = 0x200,
        /// <summary>
        /// �ܹ������Ͳٿ� AppDomain��
        /// </summary>
        ControlAppDomain = 0x400,
        /// <summary>
        /// ��������Զ�����ͺ��ŵ���Ȩ�ޡ�
        /// </summary>
        RemotingConfiguration = 0x800,
        /// <summary>
        /// ���ڽ�������빫����������ʱ�ṹ��Ȩ�ޣ������ Remoting Context Sink��Envoy Sink �� Dynamic Sink��
        /// </summary>
        Infrastructure = 0x1000,
        /// <summary>
        /// ��Ӧ�ó��������ļ���ִ����ʽ���ض��������Ȩ�ޡ�
        /// </summary>
        BindingRedirects = 0x2000,
        /// <summary>
        /// Ȩ�޵�������״̬��
        /// </summary>
        AllFlags = 0x3fff,
    }

    /// <summary>
    /// ����Ӧ���ڴ���İ�ȫȨ�޼��� ���಻�ܱ��̳С�
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    [Serializable]
    sealed public class SecurityPermission 
           : CodeAccessPermission, IUnrestrictedPermission, IBuiltInPermission
    {
#pragma warning disable 618
        private SecurityPermissionFlag m_flags;//��ȫȨ�޶���ָ�����ʱ�־
#pragma warning restore 618
        
        //
        // �������캯��
        //
        
        /// <summary>
        /// ��ָ���������ƻ������Ƶ�Ȩ�޳�ʼ�� SecurityPermission �����ʵ����
        /// </summary>
        /// <param name="state"></param>
        public SecurityPermission(PermissionState state)
        {
            if (state == PermissionState.Unrestricted)
            {
                SetUnrestricted( true );
            }
            else if (state == PermissionState.None)
            {
                SetUnrestricted( false );
                Reset();
            }
            else
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidPermissionState"));//������Ч���״̬
            }
        }
        
        
        // SecurityPermission
        //
#pragma warning disable 618
        /// <summary>
        /// ʹ��ָ���ı�־��ʼ����״̬��ʼ�� SecurityPermission �����ʵ����
        /// </summary>
        /// <param name="flag"></param>
        public SecurityPermission(SecurityPermissionFlag flag)
#pragma warning restore 618
        {
            VerifyAccess(flag);
            
            SetUnrestricted(false);
            m_flags = flag;
        }
    
    
        //------------------------------------------------------
        //
        // PRIVATE AND PROTECTED MODIFIERS 
        //
        //------------------------------------------------------
        
        /// <summary>
        /// �������ƣ�True:������״̬��False:��������
        /// </summary>
        /// <param name="unrestricted"></param>
        private void SetUnrestricted(bool unrestricted)
        {
            if (unrestricted)
            {
#pragma warning disable 618
                m_flags = SecurityPermissionFlag.AllFlags;//��״̬����ΪȨ��������״̬
#pragma warning restore 618
            }
        }
        
        /// <summary>
        /// ���Ȩ��
        /// </summary>
        private void Reset()
        {
#pragma warning disable 618
            m_flags = SecurityPermissionFlag.NoFlags;//�ް�ȫ�Է�������
#pragma warning restore 618
        }
        
        
        /// <summary>
        /// ��ȡ��������� SecurityPermission Ȩ�޵�����Ȩ�ޱ�־��
        /// </summary>
#pragma warning disable 618
        public SecurityPermissionFlag Flags
#pragma warning restore 618
        {
            set
            {
                VerifyAccess(value);
            
                m_flags = value;
            }
            
            get
            {
                return m_flags;
            }
        }
        
        //
        // CodeAccessPermission methods
        // 
        
       /*
         * IPermission interface implementation
         */
        /// <summary>
        /// ȷ����ǰȨ���Ƿ�Ϊָ��Ȩ�޵��Ӽ��� ����д CodeAccessPermission.IsSubsetOf(IPermission)����
        /// </summary>
        /// <param name="target">Ŀ�����</param>
        /// <returns></returns>
        public override bool IsSubsetOf(IPermission target)
        {
            if (target == null)//��ֵΪ��ʱ�������ް�ȫ�Է��ʡ�
            {
                return m_flags == 0;
            }

            SecurityPermission operand = target as SecurityPermission;//��Ŀ��ֵת��ΪSecurityPermission
            if (operand != null)//�����Ϊ�գ�������жϡ������׳� Argument_WrongType �쳣
            {
                return (((int)this.m_flags) & ~((int)operand.m_flags)) == 0;//��λ���㣬���Ŀ��Ȩ���Ƿ��������������򷵻�True,���򷵻�Flase;
            }
            else
            {
                throw new 
                    ArgumentException(
                                    Environment.GetResourceString("Argument_WrongType", this.GetType().FullName)
                                     );
            }

        }
        
        /// <summary>
        /// ����һ��Ȩ�ޣ���Ȩ���ǵ�ǰȨ����ָ��Ȩ�޵Ĳ����� ����д CodeAccessPermission.Union(IPermission)����
        /// </summary>
        /// <param name="target">Ŀ�����</param>
        /// <returns></returns>
        public override IPermission Union(IPermission target) {
            if (target == null) return(this.Copy());
            if (!VerifyType(target)) {
                throw new 
                    ArgumentException(
                                    Environment.GetResourceString("Argument_WrongType", this.GetType().FullName)
                                     );
            }
            SecurityPermission sp_target = (SecurityPermission)target;//��Ŀ��ת��ΪSecurityPermission��ʽ
            if (sp_target.IsUnrestricted() || IsUnrestricted()) {//������������Ŀ�������һ��Ϊ��Ȩ�����ƣ��򷵻�Ϊ������Ȩ�޵�SecurityPermission����
                return(new SecurityPermission(PermissionState.Unrestricted));
            }
#pragma warning disable 618
            SecurityPermissionFlag flag_union = (SecurityPermissionFlag)(m_flags | sp_target.m_flags);//��λ���㣬�����������Ŀ�����Ľ���
#pragma warning restore 618
            return(new SecurityPermission(flag_union));
        }
    
        /// <summary>
        /// ����������һ��Ȩ�ޣ���Ȩ���ǵ�ǰȨ�޺�ָ��Ȩ�޵Ľ�����(��� CodeAccessPermission.Intersect(IPermission)��)
        /// </summary>
        /// <param name="target">Ŀ�����</param>
        /// <returns></returns>
        public override IPermission Intersect(IPermission target)
        {
            if (target == null)//���Ŀ�����Ϊ�գ��򷵻�Ϊ��
                return null;
            else if (!VerifyType(target))//�����ʽ����ȷ�����׳�Argument_WrongType�Ĳ����쳣
            {
                throw new 
                    ArgumentException(
                                    Environment.GetResourceString("Argument_WrongType", this.GetType().FullName)
                                     );
            }

            SecurityPermission operand = (SecurityPermission)target;//��Ŀ��ӿ�ת��ΪSecurityPermission��������
#pragma warning disable 618
            SecurityPermissionFlag isectFlags = SecurityPermissionFlag.NoFlags;//����һ��Ƕ���ް�ȫ�Է��ʱ�־
#pragma warning restore 618
           
            if (operand.IsUnrestricted())//�����������Ȩ������Ȩ�����Ƶȼ�
            {
                if (this.IsUnrestricted())//���������ҲΪ��Ȩ�����Ƶȼ����򷵻�һ����Ȩ������SecurityPermission����
                    return new SecurityPermission(PermissionState.Unrestricted);
                else
#pragma warning disable 618
                    isectFlags = (SecurityPermissionFlag)this.m_flags;//���������־��ֵΪǶ���־
#pragma warning restore 618
            }
            else if (this.IsUnrestricted())//�������������Ȩ�����Ƶȼ����򽫲��������־��ֵ��Ƕ���־
            {
#pragma warning disable 618
                isectFlags = (SecurityPermissionFlag)operand.m_flags;
#pragma warning restore 618
            }
            else
            {
#pragma warning disable 618
                isectFlags = (SecurityPermissionFlag)m_flags & (SecurityPermissionFlag)operand.m_flags;//���ر�������Ŀ�����Ľ���
#pragma warning restore 618
            }
            
            if (isectFlags == 0)
                return null;
            else
                return new SecurityPermission(isectFlags);
        }
    
        /// <summary>
        /// ���������ص�ǰȨ�޵���ͬ������ ����д CodeAccessPermission.Copy()����
        /// </summary>
        /// <returns></returns>
        public override IPermission Copy()
        {
            if (IsUnrestricted())
                return new SecurityPermission(PermissionState.Unrestricted);
            else
#pragma warning disable 618
                return new SecurityPermission((SecurityPermissionFlag)m_flags);
#pragma warning restore 618
        }
        
        /// <summary>
        /// �Ƿ������ɵģ�����Ȩ������
        /// </summary>
        /// <returns></returns>
        public bool IsUnrestricted()
        {
#pragma warning disable 618
            return m_flags == SecurityPermissionFlag.AllFlags;//������Ȩ��Ϊ����
#pragma warning restore 618
        }
        
        /// <summary>
        /// ��֤��־
        /// </summary>
        /// <param name="type">��ȫ���ʱ�־</param>
        private
#pragma warning disable 618
        void VerifyAccess(SecurityPermissionFlag type)
#pragma warning restore 618
        {
#pragma warning disable 618
            if ((type & ~SecurityPermissionFlag.AllFlags) != 0)//���Ȩ�޲������У��ⱨ������
#pragma warning restore 618
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)type));
            Contract.EndContractBlock();
        }

#if FEATURE_CAS_POLICY
        //------------------------------------------------------
        //
        // �������뷽��
        //
        //------------------------------------------------------
        
        private const String _strHeaderAssertion  = "Assertion";//����
        private const String _strHeaderUnmanagedCode = "UnmanagedCode";//Ϊ�йܴ���
        private const String _strHeaderExecution = "Execution";//ִ��
        private const String _strHeaderSkipVerification = "SkipVerification";//������֤
        private const String _strHeaderControlThread = "ControlThread";//�����߳�
        private const String _strHeaderControlEvidence = "ControlEvidence";//����֤��
        private const String _strHeaderControlPolicy = "ControlPolicy";//��������
        private const String _strHeaderSerializationFormatter = "SerializationFormatter";//���л���ʽ
        private const String _strHeaderControlDomainPolicy = "ControlDomainPolicy";//��������֤
        private const String _strHeaderControlPrincipal = "ControlPrincipal";//����ί��
        private const String _strHeaderControlAppDomain = "ControlAppDomain";//����Ӧ����
    
        /// <summary>
        /// ����Ȩ�޼��䵱ǰ״̬�� XML ���롣(��� CodeAccessPermission.ToXml()��)
        /// </summary>
        /// <returns></returns>
        public override SecurityElement ToXml()
        {
            SecurityElement esd = CodeAccessPermission.CreatePermissionElement( this, "System.Security.Permissions.SecurityPermission" );
            if (!IsUnrestricted())
            {
                esd.AddAttribute( "Flags", XMLUtil.BitFieldEnumToString( typeof( SecurityPermissionFlag ), m_flags ) );
            }
            else
            {
                esd.AddAttribute( "Unrestricted", "true" );
            }
            return esd;
        }
    
        public override void FromXml(SecurityElement esd)
        {
            CodeAccessPermission.ValidateElement( esd, this );
            if (XMLUtil.IsUnrestricted( esd ))
            {
                m_flags = SecurityPermissionFlag.AllFlags;
                return;
            }
           
            Reset () ;
            SetUnrestricted (false) ;
    
            String flags = esd.Attribute( "Flags" );
    
            if (flags != null)
                m_flags = (SecurityPermissionFlag)Enum.Parse( typeof( SecurityPermissionFlag ), flags );
        }
#endif // FEATURE_CAS_POLICY

        //
        // Object Overrides
        //
        
    #if ZERO   // Do not remove this code, usefull for debugging
        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("SecurityPermission(");
            if (IsUnrestricted())
            {
                sb.Append("Unrestricted");
            }
            else
            {
                if (GetFlag(SecurityPermissionFlag.Assertion))
                    sb.Append("Assertion; ");
                if (GetFlag(SecurityPermissionFlag.UnmanagedCode))
                    sb.Append("UnmangedCode; ");
                if (GetFlag(SecurityPermissionFlag.SkipVerification))
                    sb.Append("SkipVerification; ");
                if (GetFlag(SecurityPermissionFlag.Execution))
                    sb.Append("Execution; ");
                if (GetFlag(SecurityPermissionFlag.ControlThread))
                    sb.Append("ControlThread; ");
                if (GetFlag(SecurityPermissionFlag.ControlEvidence))
                    sb.Append("ControlEvidence; ");
                if (GetFlag(SecurityPermissionFlag.ControlPolicy))
                    sb.Append("ControlPolicy; ");
                if (GetFlag(SecurityPermissionFlag.SerializationFormatter))
                    sb.Append("SerializationFormatter; ");
                if (GetFlag(SecurityPermissionFlag.ControlDomainPolicy))
                    sb.Append("ControlDomainPolicy; ");
                if (GetFlag(SecurityPermissionFlag.ControlPrincipal))
                    sb.Append("ControlPrincipal; ");
            }
            
            sb.Append(")");
            return sb.ToString();
        }
    #endif

        /// <internalonly/>
        int IBuiltInPermission.GetTokenIndex()
        {
            return SecurityPermission.GetTokenIndex();
        }

        internal static int GetTokenIndex()
        {
            return BuiltInPermissionIndex.SecurityPermissionIndex;
        }
    }
}
