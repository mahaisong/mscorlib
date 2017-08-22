// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  DictionaryEntry
** 
** <OWNER>[....]</OWNER>
**
**
** Purpose: Return Value for IDictionaryEnumerator::GetEntry
**
** 
===========================================================*/
namespace System.Collections {
    
    using System;
    // A DictionaryEntry holds a key and a value from a dictionary.
    // It is returned by IDictionaryEnumerator::GetEntry().
    // DictionaryEntry����һ������һ��ֵ��һ���ֵ䡣���Ƿ���IDictionaryEnumerator:GetEntry()��
    [System.Runtime.InteropServices.ComVisible(true)]
    [Serializable]
    public struct DictionaryEntry
    {
        private Object _key;
        private Object _value;
    
        /// <summary>
        /// ����һ���µ�DictionaryEnumeratorͨ�������ʵ��ļ���ֵ���ֶΡ�
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public DictionaryEntry(Object key, Object value) {
            _key = key;
            _value = value;
        }

        public Object Key {
            get {
                return _key;
            }
            
            set {
                _key = value;
            }
        }

        public Object Value {
            get {
                return _value;
            }

            set {
                _value = value;
            }
        }
    }
}
