// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  KeyValuePair
** 
** <OWNER>[....]</OWNER>
**
**
** Purpose: Generic key-value pair for dictionary enumerators.
**
** 
===========================================================*/
namespace System.Collections.Generic {
    
    using System;
    using System.Text;

    // A KeyValuePair holds a key and a value from a dictionary.
    // It is used by the IEnumerable<T> implementation for both IDictionary<TKey, TValue>
    // and IReadOnlyDictionary<TKey, TValue>.
    /// <summary>
    /// ��������û�����ļ�/ֵ�ԡ�
    /// </summary>
    /// <typeparam name="TKey">�������͡�</typeparam>
    /// <typeparam name="TValue">ֵ�����͡�</typeparam>
    [Serializable]
    public struct KeyValuePair<TKey, TValue> {
        private TKey key;
        private TValue value;

        public KeyValuePair(TKey key, TValue value) {
            this.key = key;
            this.value = value;
        }

        public TKey Key {
            get { return key; }
        }

        public TValue Value {
            get { return value; }
        }

        /// <summary>
        /// ʹ�ü���ֵ���ַ�����ʾ��ʽ���� System.Collections.Generic.KeyValuePair<TKey,TValue> ���ַ�����ʾ��ʽ��
        /// </summary>
        /// <returns>System.Collections.Generic.KeyValuePair<TKey,TValue> ���ַ�����ʾ��ʽ������������ֵ���ַ�����ʾ��ʽ��</returns>
        public override string ToString() {
            StringBuilder s = StringBuilderCache.Acquire();
            s.Append('[');
            if( Key != null) {
                s.Append(Key.ToString());
            }
            s.Append(", ");
            if( Value != null) {
               s.Append(Value.ToString());
            }
            s.Append(']');
            return StringBuilderCache.GetStringAndRelease(s);
        }
    }
}
