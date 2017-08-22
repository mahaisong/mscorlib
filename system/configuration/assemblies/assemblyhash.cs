// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** File:    AssemblyHash
**
**
** Purpose: 
**
**
===========================================================*/
namespace System.Configuration.Assemblies {
    using System;

    /// <summary>
    /// ��������嵥���ݵĹ�ϣ��
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
    public struct AssemblyHash : ICloneable
    {
        /// <summary>
        /// ���򼯼����㷨
        /// </summary>
        private AssemblyHashAlgorithm _Algorithm;
        /// <summary>
        /// 
        /// </summary>
        private byte[] _Value;
        
        /// <summary>
        /// �ѹ�ʱ��һ���� AssemblyHash ����
        /// </summary>
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public static readonly AssemblyHash Empty = new AssemblyHash(AssemblyHashAlgorithm.None, null);
    
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public AssemblyHash(byte[] value) {
            _Algorithm = AssemblyHashAlgorithm.SHA1;
            _Value = null;
    
            if (value != null) {
                int length = value.Length;
                _Value = new byte[length];
                Array.Copy(value, _Value, length);
            }
        }
    
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public AssemblyHash(AssemblyHashAlgorithm algorithm, byte[] value) {
            _Algorithm = algorithm;
            _Value = null;
    
            if (value != null) {
                int length = value.Length;
                _Value = new byte[length];
                Array.Copy(value, _Value, length);
            }
        }
    
        // Hash is made up of a byte array and a value from a class of supported 
        // algorithm types.
        /// <summary>
        /// �ѹ�ʱ����ȡ�����ù�ϣ�㷨��
        /// </summary>
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public AssemblyHashAlgorithm Algorithm {
            get { return _Algorithm; }
            set { _Algorithm = value; }
        }

        /// <summary>
        /// �ѹ�ʱ����ȡ��ϣֵ��
        /// </summary>
        /// <returns></returns>
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public byte[] GetValue() {
            return _Value;
        }

        /// <summary>
        /// �ѹ�ʱ�����ù�ϣֵ��
        /// </summary>
        /// <param name="value"></param>
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public void SetValue(byte[] value) {
            _Value = value;
        }
    
        /// <summary>
        /// �ѹ�ʱ����¡�ö���
        /// </summary>
        /// <returns></returns>
        [Obsolete("The AssemblyHash class has been deprecated. http://go.microsoft.com/fwlink/?linkid=14202")]
        public Object Clone() {
            return new AssemblyHash(_Algorithm, _Value);
        }
    }

}
