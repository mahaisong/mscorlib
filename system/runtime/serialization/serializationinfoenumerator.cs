// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class: SerializationInfoEnumerator
**
**
** �ṩһ�ֶԸ�ʽ�������ѺõĻ��ƣ����ڷ��� SerializationInfo �е����ݡ����಻�ܱ��̳С�
**
============================================================*/
namespace System.Runtime.Serialization {
    using System;
    using System.Collections;
    using System.Diagnostics.Contracts;

    //
    // The tuple returned by SerializationInfoEnumerator.Current.
    //
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct SerializationEntry {
        private Type   m_type;
        private Object m_value;
        private String m_name;

        public Object Value {
            get {
                return m_value;
            }
        }

        public String Name {
            get {
                return m_name;
            }
        }

        public Type ObjectType {
            get {
                return m_type;
            }
        }

        internal SerializationEntry(String entryName, Object entryValue, Type entryType) {
            m_value = entryValue;
            m_name = entryName;
            m_type = entryType;
        }
    }

    //
    // һ���򵥵�ֵ�洢��SerializationInfoö������
    // �Ⲣ������ֵ,��ֻ��ʹָ���Ա������SerializationInfo��������
    //
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class SerializationInfoEnumerator : IEnumerator {
        String[] m_members;
        Object[] m_data;
        Type[]   m_types;
        int      m_numItems;
        int      m_currItem;
        bool     m_current;

        /// <summary>
        /// ���л���Ϣö��
        /// </summary>
        /// <param name="members">��Ա</param>
        /// <param name="info">��Ϣ</param>
        /// <param name="types">��ʽ</param>
        /// <param name="numItems">��Ŀ��</param>
        internal SerializationInfoEnumerator(String[] members, Object[] info, Type[] types, int numItems) {
            Contract.Assert(members!=null, "[SerializationInfoEnumerator.ctor]members!=null");
            Contract.Assert(info!=null, "[SerializationInfoEnumerator.ctor]info!=null");
            Contract.Assert(types!=null, "[SerializationInfoEnumerator.ctor]types!=null");
            Contract.Assert(numItems>=0, "[SerializationInfoEnumerator.ctor]numItems>=0");
            Contract.Assert(members.Length>=numItems, "[SerializationInfoEnumerator.ctor]members.Length>=numItems");
            Contract.Assert(info.Length>=numItems, "[SerializationInfoEnumerator.ctor]info.Length>=numItems");
            Contract.Assert(types.Length>=numItems, "[SerializationInfoEnumerator.ctor]types.Length>=numItems");

            m_members = members;
            m_data = info;
            m_types = types;
            //MoveNext����������������ִ��[0 . .m_numItems)����Ч����Ŀ����ö����,������Ǽ�ȥ1��
            m_numItems = numItems-1;
            m_currItem = -1;
            m_current = false;
        }

        /// <summary>
        /// ��ö�ٸ��µ���һ��
        /// ʵ�֣�IEnumerator.MoveNext()
        /// </summary>
        /// <returns>����ҵ��µ�Ԫ�أ���Ϊ true������Ϊ false��</returns>
        public bool MoveNext() {
            if (m_currItem<m_numItems) {
                m_currItem++;
                m_current = true;
            } else {
                m_current = false;
            }
            return m_current;
        }

        /// <internalonly/>
        Object IEnumerator.Current { //Actually returns a SerializationEntry
            get {
                if (m_current==false) {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                }
                return (Object)(new SerializationEntry(m_members[m_currItem], m_data[m_currItem], m_types[m_currItem]));
            }
        }


        public SerializationEntry Current { //Actually returns a SerializationEntry
            get {
                if (m_current==false) {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                }
                return (new SerializationEntry(m_members[m_currItem], m_data[m_currItem], m_types[m_currItem]));
            }
        }

        /// <summary>
        /// ��ö��������Ϊ��һ�
        /// </summary>
        public void Reset() {
            m_currItem = -1;
            m_current = false;
        }

        /// <summary>
        /// ��ȡ��ǰ������������ơ�
        /// </summary>
        public String Name {
            get {
                if (m_current==false) {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                }
                return m_members[m_currItem];
            }
        }

        /// <summary>
        /// ��ȡ��ǰ���������ֵ��
        /// </summary>
        public Object Value {
            get {
                if (m_current==false) {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                }
                return m_data[m_currItem];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public Type ObjectType {
            get {
                if (m_current==false) {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                }
                return m_types[m_currItem];
            }
        }
    }
}
