// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//
// <OWNER>[....]</OWNER>
//
// ==--==
/*=============================================================================
**
** Class: Exception
**
**
** Purpose: The base class for all exceptional conditions.
**
**
========================================================================== ===*/

namespace System {
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Runtime.Versioning;
    using System.Diagnostics;
    using System.Security.Permissions;
    using System.Security;
    using System.IO;
    using System.Text;
    using System.Reflection;
    using System.Collections;
    using System.Globalization;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// ��ʾ��Ӧ�ó���ִ�й����з����Ĵ���
    /// </summary>
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_Exception))]
    [Serializable]
    [ComVisible(true)]
    public class Exception : ISerializable, _Exception//���л��ӿڣ��쳣�ӿ�
    {
        /// <summary>
        /// ��ʼ������ֶ�
        /// </summary>
        private void Init()
        {
            _message = null;//��ȡ������ǰ�쳣����Ϣ��
            _stackTrace = null;//�õ����ַ�����ʾ��ֱ�ӵ��ö�ջ֡��
            _dynamicMethods = null;//��̬����
            HResult = __HResults.COR_E_EXCEPTION;//��ȡ������ HRESULT��һ��������ض��쳣�ı�������ֵ����
            _xcode = _COMPlusExceptionCode;
            _xptrs = (IntPtr) 0;

            // ��ʼ��WatsonBuckets Ϊ��
            _watsonBuckets = null;

            // ��ʼ����ɭͰװIP
            _ipForWatsonBuckets = UIntPtr.Zero;

#if FEATURE_SERIALIZATION
             _safeSerializationManager = new SafeSerializationManager();//��ȫ���л�����
#endif // FEATURE_SERIALIZATION
        }

        /// <summary>
        /// ����Exception
        /// </summary>
        public Exception() {
            Init();
        }
    
        /// <summary>
        ///����Exception
        /// </summary>
        /// <param name="message">��ǰ�쳣����Ϣ</param>
        public Exception(String message) {
            Init();
            _message = message;
        }
    
        /// <summary>
        /// ʹ��ָ���Ĵ�����Ϣ�Ͷ���Ϊ���쳣ԭ����ڲ��쳣����������ʼ�� Exception �����ʵ����
        /// </summary>
        /// <param name="message">�쳣��Ϣ</param>
        /// <param name="innerException">���µ�ǰ�쳣�� Exception ʵ����</param>
        public Exception (String message, Exception innerException) {
            Init();
            _message = message;
            _innerException = innerException;
        }

        /// <summary>
        /// �����л����ݳ�ʼ�� Exception �����ʵ����
        /// </summary>
        /// <param name="info">���л���Ϣ</param>
        /// <param name="context">�������������л�����Դ��Ŀ�꣬���ṩһ���ɵ��÷�����ĸ��������ġ�</param>
        /// <exception cref="ArgumentNullException">info Ϊ��</exception>
        /// <exception cref="SerializationException">���л���ϢΪ��</exception>
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected Exception(SerializationInfo info, StreamingContext context) 
        {
            if (info==null)
                throw new ArgumentNullException("info");
            Contract.EndContractBlock();

            #region �����л���Ϣ��ֵ
            _className = info.GetString("ClassName");
            _message = info.GetString("Message");
            _data = (IDictionary)(info.GetValueNoThrow("Data", typeof(IDictionary)));
            _innerException = (Exception)(info.GetValue("InnerException", typeof(Exception)));
            _helpURL = info.GetString("HelpURL");
            _stackTraceString = info.GetString("StackTraceString");
            _remoteStackTraceString = info.GetString("RemoteStackTraceString");
            _remoteStackIndex = info.GetInt32("RemoteStackIndex");

            _exceptionMethodString = (String)(info.GetValue("ExceptionMethod", typeof(String)));
            HResult = info.GetInt32("HResult");
            _source = info.GetString("Source");

            // ���л���WatsonBuckets����������֧�ִ���ADת���쳣��
            // ����ʹ��û���׳��汾��Ϊ���ǿ��Է����л�pre-V4�쳣����,����û�������Ŀ�������������,���ǿ��Եõ�null��
            _watsonBuckets = (Object)info.GetValueNoThrow("WatsonBuckets", typeof(byte[])); 
            

#if FEATURE_SERIALIZATION
            _safeSerializationManager = info.GetValueNoThrow("SafeSerializationManager", typeof(SafeSerializationManager)) as SafeSerializationManager;
#endif // FEATURE_SERIALIZATION
            #endregion

            if (_className == null || HResult==0)
                throw new SerializationException(Environment.GetResourceString("Serialization_InsufficientState"));
            
            // �ڵ���CrossAppDomain�����ڹ���һ���µ��쳣
            if (context.State == StreamingContextStates.CrossAppDomain)
            {
                // ...this new exception may get thrown.  It is logically a re-throw, but 
                //  physically a brand-new exception.  Since the stack trace is cleared 
                //  on a new exception, the "_remoteStackTraceString" is provided to 
                //  effectively import a stack trace from a "remote" exception.  So,
                //  move the _stackTraceString into the _remoteStackTraceString.  Note
                //  that if there is an existing _remoteStackTraceString, it will be 
                //  preserved at the head of the new string, so everything works as 
                //  expected.
                // Even if this exception is NOT thrown, things will still work as expected
                //  because the StackTrace property returns the concatenation of the
                //  _remoteStackTraceString and the _stackTraceString.
                // ������ܻ��׳��µ��쳣��
                // �����߼�����һ���׳��յ�,�������ϵ�һ��ȫ�µ����⡣
                // �������ջ�����µ��쳣,�ṩ��Ч�ص��롰_remoteStackTraceString���ӡ�Զ�̡��쳣��ջ���١�
                // ���,�ƶ�_stackTraceString _remoteStackTraceString��
                // ע��,���������_remoteStackTraceString,�������������������ַ���,����һ�а�Ԥ�ڹ���
                _remoteStackTraceString = _remoteStackTraceString + _stackTraceString;
                _stackTraceString = null;
            }
        }
        
        /// <summary>
        /// ��ȡ������ǰ�쳣����Ϣ
        /// </summary>
        public virtual String Message {
               get {  
                if (_message == null) {//����쳣��ϢΪΪ�գ��򷵻�className
                    if (_className==null) {
                        _className = GetClassName();
                    }
                    return Environment.GetResourceString("Exception_WasThrown", _className);

                } else {
                    return _message;
                }
            }
        }

        /// <summary>
        /// ��ȡ�ṩ�й��쳣�������û�������Ϣ�ļ�/ֵ�Լ��ϡ�
        /// </summary>
        public virtual IDictionary Data { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                if (_data == null)//������ݼ�ֵ��Ϊ�գ��ڸ���������ʼ�����ݼ�ֵ��
                    if (IsImmutableAgileException(this))
                        _data = new EmptyReadOnlyDictionaryInternal();
                    else
                        _data = new ListDictionaryInternal();
                
                return _data;
            }
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern bool IsImmutableAgileException(Exception e);

#if FEATURE_COMINTEROP
        // �쳣��Ҫ��ӵ��κ������ֵ��ǿ����л���
        // �����װ���ǿ����л���,��������һҪ��,�������л��������л��ڼ�,��ȫ������,��Ϊ����ֻ��ҪӦ�ó�����д�����쳣ʵ������
        // һ�����������л��ĵ�����,������ֻ��Ҫ�����ַ����Ĵ���
        /// <summary>
        /// ���޴������
        /// </summary>
        [Serializable]
        internal class __RestrictedErrorObject
        {
            //���д���Ķ���ʵ��,�������л�/�����л�
            [NonSerialized]
            private object _realErrorObject;

            internal __RestrictedErrorObject(object errorObject)
            {
                _realErrorObject = errorObject;    
            }

            public object RealErrorObject
            {
               get
               {
                   return _realErrorObject;
               }
            }
        }

        /// <summary>
        /// Ϊ���ƴ����������쳣
        /// </summary>
        /// <param name="restrictedError">���ƴ���</param>
        /// <param name="restrictedErrorReference">���ƴ�������</param>
        /// <param name="restrictedCapabilitySid"></param>
        /// <param name="restrictedErrorObject">���ƴ������</param>
        /// <param name="hasrestrictedLanguageErrorObject">�Ƿ����������Դ������</param>
        [FriendAccessAllowed]
        internal void AddExceptionDataForRestrictedErrorInfo(
            string restrictedError, 
            string restrictedErrorReference, 
            string restrictedCapabilitySid,
            object restrictedErrorObject,
            bool hasrestrictedLanguageErrorObject = false)
        {
            IDictionary dict = Data;
            if (dict != null)
            {
                dict.Add("RestrictedDescription", restrictedError);
                dict.Add("RestrictedErrorReference", restrictedErrorReference);
                dict.Add("RestrictedCapabilitySid", restrictedCapabilitySid);

                // Keep the error object alive so that user could retrieve error information
                // using Data["RestrictedErrorReference"]
                dict.Add("__RestrictedErrorObject", (restrictedErrorObject == null ? null : new __RestrictedErrorObject(restrictedErrorObject)));
                dict.Add("__HasRestrictedLanguageErrorObject", hasrestrictedLanguageErrorObject);
            }
        }

        /// <summary>
        /// ���Ի�ȡ�������Դ������
        /// </summary>
        /// <param name="restrictedErrorObject">�������Դ������</param>
        /// <returns></returns>
        internal bool TryGetRestrictedLanguageErrorObject(out object restrictedErrorObject)
        {
            restrictedErrorObject = null;
            if (Data != null && Data.Contains("__HasRestrictedLanguageErrorObject"))//�ж��쳣������Ϣ�Ƿ�Ϊ�գ������ж��Ƿ���__HasRestrictedLanguageErrorObject
            {
                if (Data.Contains("__RestrictedErrorObject"))//����쳣������Ϣ����__RestrictedErrorObject�����и�ֵ
                {
                    __RestrictedErrorObject restrictedObject = Data["__RestrictedErrorObject"] as __RestrictedErrorObject;
                    if (restrictedObject != null)
                        restrictedErrorObject = restrictedObject.RealErrorObject;
                }
                return (bool)Data["__HasRestrictedLanguageErrorObject"];
            }

            return false;
        }
#endif // FEATURE_COMINTEROP

        /// <summary>
        /// ����Class����
        /// </summary>
        /// <returns></returns>
        private string GetClassName()
        {
            // Will include namespace but not full instantiation and assembly name.
            if (_className == null)
                _className = GetType().ToString();

            return _className;
        }
    
        // Retrieves the lowest exception (inner most) for the given Exception.
        // This will traverse exceptions using the innerException property.
        // ��ȡ��͵��쳣���ڴ�������������쳣
        // �⽫����ʹ��innerException�����쳣
        /// <summary>
        /// ��������������дʱ������ Exception������һ�������������쳣�ĸ�Դ��
        /// </summary>
        /// <returns>�쳣���е�һ�����������쳣�� �����ǰ�쳣�� InnerException ������ null ���ã�Visual Basic ��Ϊ Nothing����������Է��ص�ǰ�쳣��</returns>
        public virtual Exception GetBaseException() 
        {
            Exception inner = InnerException;//�ڲ��쳣
            Exception back = this;
            
            while (inner != null) {
                back = inner;
                inner = inner.InnerException;
            }
            
            return back;
        }
        
        /// <summary>
        /// ����������쳣���ص��ڲ��쳣
        /// </summary>
        public Exception InnerException {
            get { return _innerException; }
        }


        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static extern private IRuntimeMethodInfo GetMethodFromStackTrace(Object stackTrace);

        /// <summary>
        /// �Ӷ�ջ�켣�л�ȡ�쳣����
        /// </summary>
        /// <returns>�������캯������Ϣ</returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private MethodBase GetExceptionMethodFromStackTrace()
        {
            IRuntimeMethodInfo method = GetMethodFromStackTrace(_stackTrace);

            // Under certain race conditions when exceptions are re-used, this can be null
            if (method == null)
                return null;

            return RuntimeType.GetMethodBase(method);
        }
        
        /// <summary>
        /// ��ȡ������ǰ�쳣�ķ�����
        /// </summary>
        public MethodBase TargetSite {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                return GetTargetSiteInternal();
            }
        }


        // �����������Ϊ˽������,�Ա��ⰲȫ����
        [System.Security.SecurityCritical]  // auto-generated
        private MethodBase GetTargetSiteInternal() {
            if (_exceptionMethod!=null) {
                return _exceptionMethod;
            }
            if (_stackTrace==null) {
                return null;
            }

            if (_exceptionMethodString!=null) {
                _exceptionMethod = GetExceptionMethodFromString();
            } else {
                _exceptionMethod = GetExceptionMethodFromStackTrace();
            }
            return _exceptionMethod;
        }
    
        /// <summary>
        /// ���ַ�����ʽ���ض�ջ���١����û�п��õĶ�ջ����,����null��
        /// </summary>
        public virtual String StackTrace
        {
#if FEATURE_CORECLR
            [System.Security.SecuritySafeCritical] 
#endif
            get 
            {
                return GetStackTrace(true); //Ĭ���������ͼ�����ļ����к���Ϣ
            }
        }

        // ���needFileInfo����ȷ�ģ����㲢���ض�ջ������Ϊ�ַ�����ͼ��ȡԴ�ļ����к���Ϣ��
        // ע��,����ҪFileIOPermission(PathDiscovery),������CoreCLRͨ����ʧ�ܡ�
        // ������������SecurityException���ǿ�����ȷ������ͼ��fileinfo��
        #if FEATURE_CORECLR
        [System.Security.SecurityCritical] // auto-generated
        #endif
        private string GetStackTrace(bool needFileInfo)
        {
            string stackTraceString = _stackTraceString;
            string remoteStackTraceString = _remoteStackTraceString;

#if !FEATURE_CORECLR
            if (!needFileInfo)
            {
                // �Ӷ�ջ�켣��Զ�̶�ջ�켣�У������ļ���/·�����к�
                // ����ֻ�е����ɶ�ջ���������ַ�������PII-free��ɭ��
                stackTraceString = StripFileInfo(stackTraceString, false);
                remoteStackTraceString = StripFileInfo(remoteStackTraceString, true);
            }
#endif // !FEATURE_CORECLR

            // if no stack trace, try to get one
            if (stackTraceString != null)
            {
                return remoteStackTraceString + stackTraceString;
            }
            if (_stackTrace == null)
            {
                return remoteStackTraceString;
            }

            // Obtain the stack trace string. Note that since Environment.GetStackTrace
            // will add the path to the source file if the PDB is present and a demand
            // for FileIOPermission(PathDiscovery) succeeds, we need to make sure we 
            // don't store the stack trace string in the _stac��kTraceString member variable.
            String tempStackTraceString = Environment.GetStackTrace(this, needFileInfo);
            return remoteStackTraceString + tempStackTraceString;
         }
    
        /// <summary>
        /// ���ô������
        /// </summary>
        /// <param name="hr"></param>
        [FriendAccessAllowed]
        internal void SetErrorCode(int hr)
        {
            HResult = hr;
        }
        
        // Sets the help link for this exception.
        // This should be in a URL/URN form, such as:
        // "file:///C:/Applications/Bazzal/help.html#ErrorNum42"
        // Changed to be a read-write String and not return an exception
        /// <summary>
        /// ��ȡ�������쳣��������
        /// </summary>
        public virtual String HelpLink
        {
            get
            {
                return _helpURL;
            }
            set
            {
                _helpURL = value;
            }
        }
        
        /// <summary>
        /// ��ȡ�����õ��´����Ӧ�ó�����������ơ�
        /// </summary>
        public virtual String Source {
            #if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
            #endif
            get { 
                if (_source == null)
                {
                    StackTrace st = new StackTrace(this, true);//��ȡ��ջ�켣
                    if (st.FrameCount > 0)//�����ջ�����е�֡��������
                    {
                        StackFrame sf = st.GetFrame(0);//��ȡָ���Ķ�ջ֡
                        MethodBase method = sf.GetMethod();//��ȡ������Ϣ

                        Module module = method.Module;//��ȡ�÷���ģ��

                        RuntimeModule rtModule = module as RuntimeModule;//ת��Ϊ����ʱģ��

                        if (rtModule == null)//���Ϊ�գ��򹹽�����ʱģ��
                        {
                            System.Reflection.Emit.ModuleBuilder moduleBuilder = module as System.Reflection.Emit.ModuleBuilder;//��ȡģ��Builder
                            if (moduleBuilder != null)
                                rtModule = moduleBuilder.InternalModule;
                            else
                                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeReflectionObject"));
                        }

                        _source = rtModule.GetRuntimeAssembly().GetSimpleName();//��ȡģ�����ƣ��������쳣Դ
                    }
                }

                return _source;
            }
            #if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
            #endif
            set { _source = value; }
        }

        /// <summary>
        /// �����ַ���ת��
        /// </summary>
        /// <returns></returns>
#if FEATURE_CORECLR
        [System.Security.SecuritySafeCritical] 
#endif
        public override String ToString()
        {
            return ToString(true, true);
        }

        /// <summary>
        /// �����ַ���ת��
        /// </summary>
        /// <param name="needFileLineInfo">�Ƿ��ļ�����Ϣ</param>
        /// <param name="needMessage">�Ƿ���Ҫ��Ϣ</param>
        /// <returns></returns>
        #if FEATURE_CORECLR
        [System.Security.SecurityCritical] // auto-generated
        #endif
        private String ToString(bool needFileLineInfo, bool needMessage) {
            String message = (needMessage ? Message : null);//������Ϣ
            String s;//�쳣�ַ���

            if (message == null || message.Length <= 0) {
                s = GetClassName();
            }
            else {
                s = GetClassName() + ": " + message;
            }

            if (_innerException!=null) {
                s = s + " ---> " + _innerException.ToString(needFileLineInfo, needMessage) + Environment.NewLine + 
                "   " + Environment.GetResourceString("Exception_EndOfInnerExceptionStack");

            }

            string stackTrace = GetStackTrace(needFileLineInfo);
            if (stackTrace != null)
            {
                s += Environment.NewLine + stackTrace;
            }

            return s;
        }
    
        /// <summary>
        /// ��ȡ�쳣�����ַ���
        /// </summary>
        /// <returns></returns>
        [System.Security.SecurityCritical]  // auto-generated
        private String GetExceptionMethodString() {
            MethodBase methBase = GetTargetSiteInternal();
            if (methBase==null) {
                return null;
            }
            if (methBase is System.Reflection.Emit.DynamicMethod.RTDynamicMethod)
            {
                // DynamicMethods���ܱ����л�
                return null;
            }

            // ע�⻻�з��ָ���ֻ��һ���ָ���,ѡ��,����������(ͨ��)�����ڷ������ơ�
            // ����ַ����Ľ��������л��쳣�ķ�����
            char separator = '\n';
            StringBuilder result = new StringBuilder();
            if (methBase is ConstructorInfo) {//��������ǹ��캯��
                RuntimeConstructorInfo rci = (RuntimeConstructorInfo)methBase;//��Ŀ�꺯��ת��Ϊ����ʱ���캯��
                Type t = rci.ReflectedType;//��ȡ���캯�������ø�ʽ
                result.Append((int)MemberTypes.Constructor);//��ӳ�Ա����Ϊ����
                result.Append(separator);//��ӻ��з�
                result.Append(rci.Name);//�������ʱ���캯������
                if (t!=null)
                {
                    result.Append(separator);//��ӻ��к�
                    result.Append(t.Assembly.FullName);//��ӳ���ȫ��
                    result.Append(separator);//��ӻ��з�
                    result.Append(t.FullName);//��Ӹ�ʽȫ��
                }
                result.Append(separator);//��ӻ��з�
                result.Append(rci.ToString());//�������ʱ��Ϣ
            } else {//������ǹ��캯��
                Contract.Assert(methBase is MethodInfo, "[Exception.GetExceptionMethodString]methBase is MethodInfo");
                RuntimeMethodInfo rmi = (RuntimeMethodInfo)methBase;//��Ŀ�꺯��ת��Ϊ����ʱ����
                Type t = rmi.DeclaringType;//��ȡ����ʱ��������
                result.Append((int)MemberTypes.Method);//��ӳ�Ա����Ϊ����
                result.Append(separator);//��ӻ��з�
                result.Append(rmi.Name);//�������ʱ��������
                result.Append(separator);//��ӻ��з�
                result.Append(rmi.Module.Assembly.FullName);//��ӳ�����Ϣ
                result.Append(separator);//��ӻ��з�
                if (t != null)
                {
                    result.Append(t.FullName);//��ӷ���ȫ��
                    result.Append(separator);//��ӻ��з�
                }
                result.Append(rmi.ToString());//�������ʱ������Ϣ
            }
            
            return result.ToString();
        }

        /// <summary>
        /// ���ַ����л�ȡ�����쳣�ķ���
        /// </summary>
        /// <returns>�йغ�����Ϣ</returns>
        [System.Security.SecurityCritical]  // auto-generated
        private MethodBase GetExceptionMethodFromString() {
            Contract.Assert(_exceptionMethodString != null, "Method string cannot be NULL!");
            String[] args = _exceptionMethodString.Split(new char[]{'\0', '\n'});
            if (args.Length!=5) {
                throw new SerializationException();
            }
            SerializationInfo si = new SerializationInfo(typeof(MemberInfoSerializationHolder), new FormatterConverter());//��ȡ���л���Ϣ
            si.AddValue("MemberType", (int)Int32.Parse(args[0], CultureInfo.InvariantCulture), typeof(Int32));//������л���Ϣ
            si.AddValue("Name", args[1], typeof(String));
            si.AddValue("AssemblyName", args[2], typeof(String));
            si.AddValue("ClassName", args[3]);
            si.AddValue("Signature", args[4]);
            MethodBase result;
            StreamingContext sc = new StreamingContext(StreamingContextStates.All);//���ÿ���������
            try {
                result = (MethodBase)new MemberInfoSerializationHolder(si, sc).GetRealObject(sc);//��ȡʵ�ʶ��󣬷�����Ϣ
            } catch (SerializationException) {
                result = null;
            }
            return result;
        }

#if FEATURE_SERIALIZATION
        /// <summary>
        /// ���л������¼�������
        /// </summary>
        protected event EventHandler<SafeSerializationEventArgs> SerializeObjectState
        {
            add { _safeSerializationManager.SerializeObjectState += value; }
            remove { _safeSerializationManager.SerializeObjectState -= value; }
        }
#endif // FEATURE_SERIALIZATION

        /// <summary>
        /// ��ȡ��������
        /// </summary>
        /// <param name="info">���л���Ϣ</param>
        /// <param name="context">���л�Դ��Ŀ��</param>
        /// <exception cref="ArgumentNullException">infoΪnull</exception>
        [System.Security.SecurityCritical]  // auto-generated_required
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) 
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            Contract.EndContractBlock();

            String tempStackTraceString = _stackTraceString; //��ȡ��ջ�켣�ַ���      

            if (_stackTrace != null) //�����ջ�켣Ϊ�գ�������tempStackTraceString��_exceptionMethod
            {
                if (tempStackTraceString==null) 
                {
                    tempStackTraceString = Environment.GetStackTrace(this, true);
                }
                if (_exceptionMethod==null) 
                {
                    _exceptionMethod = GetExceptionMethodFromStackTrace();
                }
            }

            if (_source == null) 
            {
                _source = Source; // ���л�֮ǰ��ȷ����Դ��Ϣ
            }

            #region �������л���Ϣ
            info.AddValue("ClassName", GetClassName(), typeof(String));
            info.AddValue("Message", _message, typeof(String));
            info.AddValue("Data", _data, typeof(IDictionary));
            info.AddValue("InnerException", _innerException, typeof(Exception));
            info.AddValue("HelpURL", _helpURL, typeof(String));
            info.AddValue("StackTraceString", tempStackTraceString, typeof(String));
            info.AddValue("RemoteStackTraceString", _remoteStackTraceString, typeof(String));
            info.AddValue("RemoteStackIndex", _remoteStackIndex, typeof(Int32));
            info.AddValue("ExceptionMethod", GetExceptionMethodString(), typeof(String));
            info.AddValue("HResult", HResult);
            info.AddValue("Source", _source, typeof(String));

            // Serialize the Watson bucket details as well
            info.AddValue("WatsonBuckets", _watsonBuckets, typeof(byte[])); 
            #endregion

#if FEATURE_SERIALIZATION
            if (_safeSerializationManager != null && _safeSerializationManager.IsActive)//�жϰ�ȫ���л����������״̬
            {
                info.AddValue("SafeSerializationManager", _safeSerializationManager, typeof(SafeSerializationManager));

                // User classes derived from Exception must have a valid _safeSerializationManager.
                // Exceptions defined in mscorlib don't use this field might not have it initalized (since they are 
                // often created in the VM with AllocateObject instead if the managed construtor)
                // If you are adding code to use a SafeSerializationManager from an mscorlib exception, update
                // this assert to ensure that it fails when that exception's _safeSerializationManager is NULL 
                Contract.Assert(((_safeSerializationManager != null) || (this.GetType().Assembly == typeof(object).Assembly)), 
                                "User defined exceptions must have a valid _safeSerializationManager");
            
                // Handle serializing any transparent or partial trust subclass data
                _safeSerializationManager.CompleteSerialization(this, info, context);
            }
#endif // FEATURE_SERIALIZATION
        }

        // ����Զ��ά������������ʹ�õĶ�ջ����ͨ�����ӵ���Ϣ��������rethrown֮ǰ�ڿͻ��˵���վ�㡣
        /// <summary>
        /// ΪԶ��׼�����쳣��Ϣ
        /// </summary>
        /// <returns>�쳣��Ϣ</returns>
        internal Exception PrepForRemoting()
        {
            String tmp = null;

            if (_remoteStackIndex == 0)//���Զ�̶�����Ϊ0����Ϊ����������Ϣ
            {
                tmp = Environment.NewLine+ "Server stack trace: " + Environment.NewLine
                    + StackTrace 
                    + Environment.NewLine + Environment.NewLine 
                    + "Exception rethrown at ["+_remoteStackIndex+"]: " + Environment.NewLine;
            }
            else
            {
                tmp = StackTrace 
                    + Environment.NewLine + Environment.NewLine 
                    + "Exception rethrown at ["+_remoteStackIndex+"]: " + Environment.NewLine;
            }

            _remoteStackTraceString = tmp;
            _remoteStackIndex++;

            return this;
        }

        // This method will clear the _stackTrace of the exception object upon deserialization
        // to ensure that references from another AD/Process dont get accidently used.
        // �����������ȷ���쳣�����ڷ����л���_stackTraceȷ������������һ��ת��/���̲��ò�С��ʹ�á�
        /// <summary>
        /// �����л�
        /// </summary>
        /// <param name="context">���л���</param>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            _stackTrace = null;

            // ���ǲ������л������л���IP��ɭ��Ͱװ,��Ϊ���ǲ�֪�������л�����ʹ�õĵط���
            // ʹ�ÿ���̻�һ��Ӧ�ó������п�����Ч,����AV������ʱ��
            // ���,���ǰ��������л�����ʱΪ�㡣
            _ipForWatsonBuckets = UIntPtr.Zero;

#if FEATURE_SERIALIZATION
            if (_safeSerializationManager == null)//
            {
                _safeSerializationManager = new SafeSerializationManager();
            }
            else
            {
                _safeSerializationManager.CompleteDeserialization(this);
            }
#endif // FEATURE_SERIALIZATION
        }

        // ����ʹ�õ�����ʱ�׳��յ��������⡣��������_remoteStackTraceString�Ķ�ջ���١�
        /// <summary>
        /// �ڲ�������ջ�켣
        /// </summary>
#if FEATURE_CORECLR
        [System.Security.SecuritySafeCritical] 
#endif
        internal void InternalPreserveStackTrace()
        {
            string tmpStackTraceString;

#if FEATURE_APPX
            if (AppDomain.IsAppXModel())
            {
                // Call our internal GetStackTrace in AppX so we can parse the result should
                // we need to strip file/line info from it to make it PII-free. Calling the
                // public and overridable StackTrace getter here was probably not intended.
                //�����ڲ�GetStackTrace������ǿ��Խ������Ӧ��������Ҫ���ļ�/����ϢPII-free���쳣��ջ���ù�������д��getter������������⡣
                tmpStackTraceString = GetStackTrace(true);

                // Make sure that the _source field is initialized if Source is not overriden.
                // We want it to contain the original faulting point.
                // �����Դ�������أ�ȷ��_source�ֶγ�ʼ��������ϣ��������ԭʼ�Ķ��ѵ㡣
                string source = Source;
            }
            else
#else // FEATURE_APPX
#if FEATURE_CORESYSTEM
            // Preinitialize _source on CoreSystem as well. The legacy behavior is not ideal and
            // we keep it for back compat but we can afford to make the change on the Phone.
            string source = Source;
#endif // FEATURE_CORESYSTEM
#endif // FEATURE_APPX
            {
                // Call the StackTrace getter in classic for compat.
                tmpStackTraceString = StackTrace;
            }

            if (tmpStackTraceString != null && tmpStackTraceString.Length > 0)
            {
                _remoteStackTraceString = tmpStackTraceString + Environment.NewLine;
            }
            
            _stackTrace = null;
            _stackTraceString = null;
        }
        
#if FEATURE_EXCEPTIONDISPATCHINFO

        // This is the object against which a lock will be taken
        // when attempt to restore the EDI. Since its static, its possible
        // that unrelated exception object restorations could get blocked
        // for a small duration but that sounds reasonable considering
        // such scenarios are going to be extremely rare, where timing
        // matches precisely.
        // ����һ�����Ķ�����ͼ�ָ�EDI��
        // ��̬����,�����ܲ���ص��쳣�����޸�����ֹСʱ�䵫�����������������ĳ����ἫΪ����,�ڼ�ʱ��ȷƥ�䡣
        [OptionalField]
        private static object s_EDILock = new object();

        internal UIntPtr IPForWatsonBuckets
        {
            get {
                return _ipForWatsonBuckets;
            }        
        }
    
        internal object WatsonBuckets
        {
            get 
            {
                return _watsonBuckets;
            }
        }

        internal string RemoteStackTrace
        {
            get
            {
                return _remoteStackTraceString;
            }
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void PrepareForForeignExceptionRaise();

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void GetStackTracesDeepCopy(Exception exception, out object currentStackTrace, out object dynamicMethodArray);

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void SaveStackTracesFromDeepCopy(Exception exception, object currentStackTrace, object dynamicMethodArray);

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern object CopyStackTrace(object currentStackTrace);

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern object CopyDynamicMethods(object currentDynamicMethods);

#if !FEATURE_CORECLR
        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern string StripFileInfo(string stackTrace, bool isRemoteStackTrace);
#endif // !FEATURE_CORECLR

        [SecuritySafeCritical]
        internal object DeepCopyStackTrace(object currentStackTrace)
        {
            if (currentStackTrace != null)
            {
                return CopyStackTrace(currentStackTrace);
            }
            else
            {
                return null;
            }
        }

        [SecuritySafeCritical]
        internal object DeepCopyDynamicMethods(object currentDynamicMethods)
        {
            if (currentDynamicMethods != null)
            {
                return CopyDynamicMethods(currentDynamicMethods);
            }
            else
            {
                return null;
            }
        }
        
        [SecuritySafeCritical]
        internal void GetStackTracesDeepCopy(out object currentStackTrace, out object dynamicMethodArray)
        {
            GetStackTracesDeepCopy(this, out currentStackTrace, out dynamicMethodArray);
        }

        // This is invoked by ExceptionDispatchInfo.Throw to restore the exception stack trace, corresponding to the original throw of the
        // exception, just before the exception is "rethrown".
        [SecuritySafeCritical]
        internal void RestoreExceptionDispatchInfo(System.Runtime.ExceptionServices.ExceptionDispatchInfo exceptionDispatchInfo)
        {
            bool fCanProcessException = !(IsImmutableAgileException(this));
            // Restore only for non-preallocated exceptions
            if (fCanProcessException)
            {
                // Take a lock to ensure only one thread can restore the details
                // at a time against this exception object that could have
                // multiple ExceptionDispatchInfo instances associated with it.
                //
                // We do this inside a finally clause to ensure ThreadAbort cannot
                // be injected while we have taken the lock. This is to prevent
                // unrelated exception restorations from getting blocked due to TAE.
                try{}
                finally
                {
                    // When restoring back the fields, we again create a copy and set reference to them
                    // in the exception object. This will ensure that when this exception is thrown and these
                    // fields are modified, then EDI's references remain intact.
                    //
                    // Since deep copying can throw on OOM, try to get the copies
                    // outside the lock.
                    object _stackTraceCopy = (exceptionDispatchInfo.BinaryStackTraceArray == null)?null:DeepCopyStackTrace(exceptionDispatchInfo.BinaryStackTraceArray);
                    object _dynamicMethodsCopy = (exceptionDispatchInfo.DynamicMethodArray == null)?null:DeepCopyDynamicMethods(exceptionDispatchInfo.DynamicMethodArray);
                    
                    // Finally, restore the information. 
                    //
                    // Since EDI can be created at various points during exception dispatch (e.g. at various frames on the stack) for the same exception instance,
                    // they can have different data to be restored. Thus, to ensure atomicity of restoration from each EDI, perform the restore under a lock.
                    lock(Exception.s_EDILock)
                    {
                        _watsonBuckets = exceptionDispatchInfo.WatsonBuckets;
                        _ipForWatsonBuckets = exceptionDispatchInfo.IPForWatsonBuckets;
                        _remoteStackTraceString = exceptionDispatchInfo.RemoteStackTrace;
                        SaveStackTracesFromDeepCopy(this, _stackTraceCopy, _dynamicMethodsCopy);
                    }
                    _stackTraceString = null;

                    // Marks the TES state to indicate we have restored foreign exception
                    // dispatch information.
                    Exception.PrepareForForeignExceptionRaise();
                }
            }
        }
#endif // FEATURE_EXCEPTIONDISPATCHINFO

        /// <summary>
        /// ���ö�������
        /// </summary>
        private String _className;  //Needed for serialization.  
        /// <summary>
        /// ���÷�������
        /// </summary>
        private MethodBase _exceptionMethod;  //Needed for serialization. 
        /// <summary>
        /// �쳣�����ַ���
        /// </summary>
        private String _exceptionMethodString; //Needed for serialization. 
        /// <summary>
        /// ��ȡ������ǰ�쳣����Ϣ��
        /// </summary>
        internal String _message;
        /// <summary>
        /// ��ȡ�ṩ�й��쳣�������û�������Ϣ�ļ�/ֵ�Լ��ϡ�
        /// </summary>
        private IDictionary _data;
        /// <summary>
        /// ��ȡ���µ�ǰ�쳣�� Exception ʵ����
        /// </summary>
        private Exception _innerException;
        /// <summary>
        /// ��ȡ������ָ������쳣�����İ����ļ����ӡ�
        /// </summary>
        private String _helpURL;
        /// <summary>
        /// ��ȡ���ö�ջ�ϵļ�ʱ����ַ�����ʾ��ʽ��
        /// </summary>
        private Object _stackTrace;
        [OptionalField] // This isnt present in pre-V4 exception objects that would be serialized.
        private Object _watsonBuckets;
        /// <summary>
        /// ��ȡ���ö�ջ�ϵļ�ʱ����ַ�����ʾ��ʽ��
        /// </summary>
        private String _stackTraceString; //Needed for serialization. 
        /// <summary>
        /// ��ȡ���ö�ջ�ϵ�Զ�̼�ʱ����ַ�����ʾ��ʽ��
        /// </summary>
        private String _remoteStackTraceString;
        /// <summary>
        /// Զ�̶�����
        /// </summary>
        private int _remoteStackIndex;
#pragma warning disable 414  // Field is not used from managed.        
        // _dynamicMethods is an array of System.Resolver objects, used to keep
        // DynamicMethodDescs alive for the lifetime of the exception. We do this because
        // the _stackTrace field holds MethodDescs, and a DynamicMethodDesc can be destroyed
        // unless a System.Resolver object roots it.
        private Object _dynamicMethods; 
#pragma warning restore 414

        // @MANAGED: HResult is used from within the EE!  Rename with care - check VM directory
        internal int _HResult;     // HResult

        /// <summary>
        /// ��ȡ������ HRESULT��һ��������ض��쳣�ı�������ֵ����
        /// </summary>
        public int HResult
        {
            get
            {
                return _HResult;
            }
            protected set
            {
                _HResult = value;
            }
        }
        
        private String _source;         // ��Ҫ������VB
        // WARNING: Don't delete/rename _xptrs and _xcode - used by functions
        // on Marshal class.  Native functions are in COMUtilNative.cpp & AppDomain
        // ����:��Ҫɾ����������_xptrs��_xcode����Ԫ˧��ʹ�õĹ��ܡ���������COMUtilNative��cpp & AppDomain
        private IntPtr _xptrs;             // �ڲ�EE����
#pragma warning disable 414  // �ֶβ����й���ʹ��
        private int _xcode;             // �ڲ�EE����
#pragma warning restore 414
        [OptionalField]
        private UIntPtr _ipForWatsonBuckets; // ����Ϊ��ɭͰ����IP

#if FEATURE_SERIALIZATION
        /// <summary>
        /// ��ȫ�̹߳���
        /// </summary>
        [OptionalField(VersionAdded = 4)]
        private SafeSerializationManager _safeSerializationManager;
#endif // FEATURE_SERIALIZATION

    // See clr\src\vm\excep.h's EXCEPTION_COMPLUS definition:
        /// <summary>
        /// Win32λCOM+�쳣
        /// </summary>
        private const int _COMPlusExceptionCode = unchecked((int)0xe0434352);   

        // InternalToString��Ϊ����ʱ���쳣���ı��ʹ���һ����Ӧ��CrossAppDomainMarshaledException(���� �����쳣).
        /// <summary>
        /// ����ʱ���쳣���ı�
        /// </summary>
        /// <returns></returns>
        [System.Security.SecurityCritical]  // auto-generated
        internal virtual String InternalToString()
        {
            try 
            {
#pragma warning disable 618
                SecurityPermission sp= new SecurityPermission(SecurityPermissionFlag.ControlEvidence | SecurityPermissionFlag.ControlPolicy);
#pragma warning restore 618
                sp.Assert();
            }
            catch  
            {
                //under normal conditions there should be no exceptions
                //however if something wrong happens we still can call the usual ToString
            }

            // Get the current stack trace string.  On CoreCLR we don't bother
            // to try and include file/line-number information because all AppDomains
            // are sandboxed, and so this won't succeed in most (or all) cases.  Therefore the
            // Demand and exception overhead is a waste.
            // We currently have some bugs in watson bucket generation where the SecurityException
            // here causes us to lose saved bucket parameters.  By not even doing the demand
            // we avoid those problems (although there are deep underlying problems that need to
            // be fixed there - relying on this to avoid problems is incomplete and brittle).
            bool fGetFileLineInfo = true;
#if FEATURE_CORECLR
            fGetFileLineInfo = false;
#endif
            return ToString(fGetFileLineInfo, true);
        }

#if !FEATURE_CORECLR
        // �÷�������Ķ��󡣷��������ɱ���������_Exception.GetType()
        public new Type GetType()
        {
            return base.GetType();
        }
#endif

        internal bool IsTransient
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                return nIsTransient(_HResult);
            }
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern static bool nIsTransient(int hr);


        // This piece of infrastructure exists to help avoid deadlocks 
        // between parts of mscorlib that might throw an exception while 
        // holding a lock that are also used by mscorlib's ResourceManager
        // instance.  As a special case of code that may throw while holding
        // a lock, we also need to fix our asynchronous exceptions to use
        // Win32 resources as well (assuming we ever call a managed 
        // constructor on instances of them).  We should grow this set of
        // exception messages as we discover problems, then move the resources
        // involved to native code.
        internal enum ExceptionMessageKind
        {
            ThreadAbort = 1,
            ThreadInterrupted = 2,
            OutOfMemory = 3
        }

        // See comment on ExceptionMessageKind
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal static String GetMessageFromNativeResources(ExceptionMessageKind kind)
        {
            string retMesg = null;
            GetMessageFromNativeResources(kind, JitHelpers.GetStringHandleOnStack(ref retMesg));
            return retMesg;
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity]
        private static extern void GetMessageFromNativeResources(ExceptionMessageKind kind, StringHandleOnStack retMesg);
    }



#if FEATURE_CORECLR

    //--------------------------------------------------------------------------
    // Telesto: Telesto doesn't support appdomain marshaling of objects so
    // managed exceptions that leak across appdomain boundaries are flatted to
    // its ToString() output and rethrown as an CrossAppDomainMarshaledException.
    // The Message field is set to the ToString() output of the original exception.
    //--------------------------------------------------------------------------

    [Serializable]
    internal sealed class CrossAppDomainMarshaledException : SystemException 
    {
        public CrossAppDomainMarshaledException(String message, int errorCode) 
            : base(message) 
        {
            SetErrorCode(errorCode);
        }

        // Normally, only Telesto's UEF will see these exceptions.
        // This override prints out the original Exception's ToString()
        // output and hides the fact that it is wrapped inside another excepton.
        #if FEATURE_CORECLR
        [System.Security.SecurityCritical] // auto-generated
        #endif
        internal override String InternalToString()
        {
            return Message;
        }
    
    }
#endif


}

