// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
//
// <OWNER>[....]</OWNER>
namespace System.Threading
{
    using System;
    using System.Security.Permissions;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.Versioning;
    using System.Runtime;

    // After much discussion, we decided the Interlocked class doesn't need 
    // any HPA's for synchronization or external threading.  They hurt C#'s 
    // codegen for the yield keyword, and arguably they didn't protect much.  
    // Instead, they penalized people (and compilers) for writing threadsafe 
    // code.
    // �ھ����ܶ�����֮������ȷ��Interlocked�಻��Ҫ�κ�HPAΪ��ͬ�����߶�����̡߳�
    // ���ǻ�����c#����ĵ���keyword���������ǲ�Ӧ�ñ���˱���������������Ӧ�ô�����Щ
    // д�̰߳�ȫ�Ĵ�����ˡ�
    /// <summary>
    /// Ϊ����̹߳���ı����ṩԭ�Ӳ�����
    /// </summary>
    public static class Interlocked
    {
        #region Increment ��ԭ�Ӳ�������ʽ����ָ��������ֵ���洢�������
        /******************************
         * Increment ����
         *   Implemented��ִ�У�: int
         *                        long
         *****************************/
        /// <summary>
        /// ��ԭ�Ӳ�������ʽ����ָ��������ֵ���洢�����
        /// </summary>
        /// <param name="location">��ֵҪ�����ı�����</param>
        /// <returns>������ֵ��</returns>
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static int Increment(ref int location)
        {
            return Add(ref location, 1);
        }

        /// <summary>
        /// ��ԭ�Ӳ�������ʽ����ָ��������ֵ���洢�����
        /// </summary>
        /// <param name="location">��ֵҪ�����ı�����</param>
        /// <returns>������ֵ��</returns>
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static long Increment(ref long location)
        {
            return Add(ref location, 1);
        } 
        #endregion

        #region Decrement ����ԭ�Ӳ�������ʽ�ݼ�ָ��������ֵ���洢�������
        /******************************
         * Decrement ����
         *   Implemented��ʵ�֣�: int
         *                        long
         *****************************/
        /// <summary>
        /// ��ԭ�Ӳ�������ʽ�ݼ�ָ��������ֵ���洢�����
        /// </summary>
        /// <param name="location">��ֵҪ�ݼ��ı�����</param>
        /// <returns>�ݼ���ֵ��</returns>
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static int Decrement(ref int location)
        {
            return Add(ref location, -1);
        }

        /// <summary>
        /// ��ԭ�Ӳ�������ʽ�ݼ�ָ��������ֵ���洢�����
        /// </summary>
        /// <param name="location">��ֵҪ�ݼ��ı�����</param>
        /// <returns>�ݼ���ֵ��</returns>
        [ResourceExposure(ResourceScope.None)]
        public static long Decrement(ref long location)
        {
            return Add(ref location, -1);
        } 
        #endregion

        #region Exchange ����ԭ�Ӳ�������ʽ����ֵ����Ϊָ����ֵ������ԭʼֵ����
        /******************************
         * Exchange(����)
         *   Implemented��ʵ�֣�: int
         *                        long
         *                        float
         *                        double
         *                        Object
         *                        IntPtr
         *****************************/

        /// <summary>
        /// ��ԭ�Ӳ�������ʽ���� 32 λ�з�����������Ϊָ����ֵ������ԭʼֵ��
        /// </summary>
        /// <param name="location1">Ҫ����Ϊָ��ֵ�ı�����</param>
        /// <param name="value">location1 ����������Ϊ��ֵ��</param>
        /// <returns>location1 ��ԭʼֵ��</returns>
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern int Exchange(ref int location1, int value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern long Exchange(ref long location1, long value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern float Exchange(ref float location1, float value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern double Exchange(ref double location1, double value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern Object Exchange(ref Object location1, Object value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern IntPtr Exchange(ref IntPtr location1, IntPtr value);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Runtime.InteropServices.ComVisible(false)]
        [System.Security.SecuritySafeCritical]
        public static T Exchange<T>(ref T location1, T value) where T : class
        {
            _Exchange(__makeref(location1), __makeref(value));//__makeref���ԴӶ�����������ȡ��TypedReference����
            //Since value is a local we use trash its data on return
            //  The Exchange replaces the data with new data
            // Exchange �����������������
            //  so after the return "value" contains the original location1
            // ����֮�󷵻صġ�value��������ԭʼlocation1
            //See ExchangeGeneric for more details   
            // ExchangeGeneric�����ܶ�ϸ��
            return value;
        }

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        private static extern void _Exchange(TypedReference location1, TypedReference value); 
        #endregion

        #region CompareExchange���Ƚ�����ֵ�Ƿ���ȣ������ȣ����滻����һ��ֵ����
        /******************************
         * CompareExchange
         *    Implemented: int
         *                         long
         *                         float
         *                         double
         *                         Object
         *                         IntPtr
         *****************************/

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern int CompareExchange(ref int location1, int value, int comparand);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern long CompareExchange(ref long location1, long value, long comparand);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern float CompareExchange(ref float location1, float value, float comparand);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [System.Security.SecuritySafeCritical]
        public static extern double CompareExchange(ref double location1, double value, double comparand);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern Object CompareExchange(ref Object location1, Object value, Object comparand);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        public static extern IntPtr CompareExchange(ref IntPtr location1, IntPtr value, IntPtr comparand);

        /*****************************************************************
         * CompareExchange<T>
         * 
         * Notice how CompareExchange<T>() uses the __makeref keyword
         * to create two TypedReferences before calling _CompareExchange().
         * This is horribly slow. Ideally we would like CompareExchange<T>()
         * to simply call CompareExchange(ref Object, Object, Object); 
         * however, this would require casting a "ref T" into a "ref Object", 
         * which is not legal in C#.
         * 
         * Thus we opted to cheat, and hacked to JIT so that when it reads
         * the method body for CompareExchange<T>() it gets back the
         * following IL:
         *
         *     ldarg.0 
         *     ldarg.1
         *     ldarg.2
         *     call System.Threading.Interlocked::CompareExchange(ref Object, Object, Object)
         *     ret
         *
         * See getILIntrinsicImplementationForInterlocked() in VM\JitInterface.cpp
         * for details.
         *****************************************************************/

        /// <summary>
        /// �Ƚ�ָ������������ T ������ʵ���Ƿ���ȣ������ȣ����滻����һ����
        /// </summary>
        /// <typeparam name="T">���� location1��value �� comparand �����͡� �����ͱ������������͡�</typeparam>
        /// <param name="location1">��ֵ���� comparand ���бȽϲ��ҿ��ܱ��滻��Ŀ�ꡣ ����һ�����ò������� C# ���� ref���� Visual Basic ���� ByRef����</param>
        /// <param name="value">�ȽϽ�����ʱ�滻Ŀ��ֵ��ֵ��</param>
        /// <param name="comparand">��λ�� location1 ����ֵ���бȽϵ�ֵ��</param>
        /// <returns>location1 �е�ԭʼֵ��</returns>
        /// <exception cref="System.NullReferenceException:">location1 �ĵ�ַΪ��ָ�롣</exception>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Runtime.InteropServices.ComVisible(false)]
        [System.Security.SecuritySafeCritical]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class
        {
            // _CompareExchange() passes back the value read from location1 via local named 'value'
            _CompareExchange(__makeref(location1), __makeref(value), comparand);
            return value;
        }

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        private static extern void _CompareExchange(TypedReference location1, TypedReference value, Object comparand);

        // BCL-internal overload that returns success via a ref bool param, useful for reliable spin locks.
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [System.Security.SecuritySafeCritical]
        internal static extern int CompareExchange(ref int location1, int value, int comparand, ref bool succeeded); 
        #endregion

        #region Add��������ֵ������Ͳ��ú��滻��һ������������������Ϊһ��ԭ�Ӳ�����ɡ���
        /******************************
         * Add
         *    Implemented: int
         *                         long
         *****************************/

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern int ExchangeAdd(ref int location1, int value);

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern long ExchangeAdd(ref long location1, long value);

        /// <summary>
        /// ������ 32 λ����������Ͳ��ú��滻��һ������������������Ϊһ��ԭ�Ӳ�����ɡ�
        /// </summary>
        /// <param name="location1">һ������������Ҫ��ӵĵ�һ��ֵ�� ����ֵ�ĺʹ洢�� location1 �С�</param>
        /// <param name="value">Ҫ��ӵ������е� location1 λ�õ�ֵ��</param>
        /// <returns>�洢�� location1 ������ֵ��</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static int Add(ref int location1, int value)
        {
            return ExchangeAdd(ref location1, value) + value;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static long Add(ref long location1, long value)
        {
            return ExchangeAdd(ref location1, value) + value;
        } 
        #endregion
     
        /******************************
         * Read
         *****************************/
        /// <summary>
        /// ����һ����ԭ�Ӳ�����ʽ���ص� 64 λֵ��
        /// </summary>
        /// <param name="location">Ҫ���ص� 64 λֵ��</param>
        /// <returns>���ص�ֵ��</returns>
        public static long Read(ref long location)
        {
            return Interlocked.CompareExchange(ref location,0,0);
        }

        /// <summary>
        /// �����·�ʽͬ���ڴ��ȡ��ִ�е�ǰ�̵߳Ĵ������ڶ�ָ����������ʱ�����ܲ�����ִ�� System.Threading.Interlocked.MemoryBarrier()
        /// ����֮����ڴ��ȡ����ִ�� System.Threading.Interlocked.MemoryBarrier() ����֮ǰ���ڴ��ȡ�ķ�ʽ��
        /// </summary>
        public static void MemoryBarrier()
        {
            Thread.MemoryBarrier();
        }
    }
}
