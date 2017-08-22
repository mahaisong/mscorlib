// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
// <OWNER>[....]</OWNER>
// 

namespace System.Collections {
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// ��Ķ�/д�����е���Ŀ�Ӷ��������������õĻ���
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class CollectionBase : IList {
        ArrayList list;

        /// <summary>
        /// ʹ��Ĭ�ϳ�ʼ������ʼ�� CollectionBase �����ʵ����
        /// </summary>
        protected CollectionBase() {
            list = new ArrayList();
        }
        
        /// <summary>
        /// ʹ��ָ����������ʼ�� CollectionBase �����ʵ����
        /// </summary>
        /// <param name="capacity"></param>
        protected CollectionBase(int capacity) {
            list = new ArrayList(capacity);
        }

        /// <summary>
        /// ��ȡһ�� ArrayList�������� CollectionBase ʵ����Ԫ�ص��б�
        /// </summary>
        protected ArrayList InnerList { 
            get { 
                if (list == null)
                    list = new ArrayList();
                return list;
            }
        }

        /// <summary>
        /// ��ȡһ�� IList�������� CollectionBase ʵ����Ԫ�ص��б�
        /// </summary>
        protected IList List {
            get { return (IList)this; }
        }

        /// <summary>
        /// ��ȡ������ CollectionBase �ɰ�����Ԫ������
        /// </summary>
        [System.Runtime.InteropServices.ComVisible(false)]        
        public int Capacity {
            get {
                return InnerList.Capacity;
            }
            set {
                InnerList.Capacity = value;
            }
        }

        /// <summary>
        /// ��ȡ������ CollectionBase ʵ���е�Ԫ������������д�����ԡ�
        /// </summary>
        public int Count {
            get {
                return list == null ? 0 : list.Count;
            }
        }

        /// <summary>
        /// �� CollectionBase ʵ���Ƴ����ж��󡣲�����д�˷�����
        /// </summary>
        public void Clear() {
            OnClear();//�鷽����ʼ���
            InnerList.Clear();//ArrayList�������
            OnClearComplete();//�鷽��������
        }

        /// <summary>
        /// �Ƴ� CollectionBase ʵ����ָ����������Ԫ�ء��˷���������д��
        /// </summary>
        /// <param name="index">Ҫ�Ƴ���Ԫ�صĴ��㿪ʼ��������</param>
        /// <exception cref="ArgumentOutOfRangeException">index�����Ϸ�Χ</exception>
        public void RemoveAt(int index) {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            Contract.EndContractBlock();
            Object temp = InnerList[index];
            OnValidate(temp);//�鷽��
            OnRemove(index, temp);//�鷽��
            InnerList.RemoveAt(index);//ArrayList�����Ƴ�
            //����Ƴ�ʧ�ܣ������²���ֵ�����׳��쳣
            try {
                OnRemoveComplete(index, temp);
            }
            catch {
                InnerList.Insert(index, temp);
                throw;
            }

        }

        /// <summary>
        /// ��ȡ�Ƿ���ֻ����
        /// </summary>
        bool IList.IsReadOnly {
            get { return InnerList.IsReadOnly; }
        }

        /// <summary>
        /// ��ȡ�Ƿ��ǹ̶���С��
        /// </summary>
        bool IList.IsFixedSize {
            get { return InnerList.IsFixedSize; }
        }

        /// <summary>
        /// ��ȡ�Ƿ�֧���߳�ͬ������
        /// </summary>
        bool ICollection.IsSynchronized {
            get { return InnerList.IsSynchronized; }
        }

        /// <summary>
        /// ��ȡ������ͬ���� ArrayList �ķ��ʵĶ���
        /// </summary>
        Object ICollection.SyncRoot {
            get { return InnerList.SyncRoot; }
        }

        /// <summary>
        /// ���������б��Ƶ�һ�����ݵ�һά����,��Ŀ���ָ���������顣
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        void ICollection.CopyTo(Array array, int index) {
            InnerList.CopyTo(array, index);
        }

        /// <summary>
        /// ��ȡIList����������
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        Object IList.this[int index] {
            get { 
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();
                return InnerList[index]; 
            }
            set { 
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();
                OnValidate(value);
                Object temp = InnerList[index];
                OnSet(index, temp, value); 
                InnerList[index] = value; 
                try {
                    OnSetComplete(index, temp, value);
                }
                catch {
                    InnerList[index] = temp; 
                    throw;
                }
            }
        }

        #region ��ʾ�ӿ�ʵ��
        /// <summary>
        /// ȷ�� CollectionBase �Ƿ�����ض�Ԫ�ء�
        /// </summary>
        /// <param name="value">Ҫ�� CollectionBase �в��ҵ� Object��</param>
        /// <returns>Type: System.Boolean��� CollectionBase ����ָ���� value����Ϊ true������Ϊ false��</returns>
        bool IList.Contains(Object value)
        {
            return InnerList.Contains(value);
        }

        /// <summary>
        /// ��������ӵ� CollectionBase �Ľ�β����
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        int IList.Add(Object value)
        {
            OnValidate(value);
            OnInsert(InnerList.Count, value);
            int index = InnerList.Add(value);
            try
            {
                OnInsertComplete(index, value);
            }
            catch
            {
                InnerList.RemoveAt(index);
                throw;
            }
            return index;
        }


        /// <summary>
        /// �� CollectionBase ���Ƴ��ض�����ĵ�һ��ƥ���
        /// </summary>
        /// <param name="value"></param>
        void IList.Remove(Object value)
        {
            OnValidate(value);
            int index = InnerList.IndexOf(value);
            if (index < 0) throw new ArgumentException(Environment.GetResourceString("Arg_RemoveArgNotFound"));
            OnRemove(index, value);
            InnerList.RemoveAt(index);
            try
            {
                OnRemoveComplete(index, value);
            }
            catch
            {
                InnerList.Insert(index, value);
                throw;
            }
        }

        /// <summary>
        /// ����ָ���� Object������������ CollectionBase �е�һ��ƥ����Ĵ��㿪ʼ��������
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        int IList.IndexOf(Object value)
        {
            return InnerList.IndexOf(value);
        }

        /// <summary>
        /// ��Ԫ�ز��� CollectionBase ��ָ����������
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        void IList.Insert(int index, Object value)
        {
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            Contract.EndContractBlock();
            OnValidate(value);
            OnInsert(index, value);
            InnerList.Insert(index, value);
            try
            {
                OnInsertComplete(index, value);
            }
            catch
            {
                InnerList.RemoveAt(index);
                throw;
            }
        }
        #endregion

        /// <summary>
        /// ����ѭ������ CollectionBase ʵ����ö������
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return InnerList.GetEnumerator();
        } 


        /// <summary>
        /// ���� CollectionBase ʵ��������ֵ֮ǰִ�������Զ�����̡�
        /// </summary>
        /// <param name="index">���㿪ʼ�����������ڸ�λ���ҵ� oldValue��</param>
        /// <param name="oldValue">Ҫ�� newValue �滻��ֵ��</param>
        /// <param name="newValue">index ����Ԫ�ص���ֵ��</param>
        protected virtual void OnSet(int index, Object oldValue, Object newValue) { 
        }

        /// <summary>
        /// ���� CollectionBase ʵ���в�����Ԫ��֮ǰִ�������Զ�����̡�
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        protected virtual void OnInsert(int index, Object value) { 
        }

        /// <summary>
        /// ����� CollectionBase ʵ��������ʱִ�������Զ������
        /// </summary>
        protected virtual void OnClear() { 
        }

        /// <summary>
        /// ���� CollectionBase ʵ���Ƴ�Ԫ��ʱִ�������Զ�����̡�
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        protected virtual void OnRemove(int index, Object value) { 
        }

        /// <summary>
        /// ����ֵ֤ʱִ�������Զ�����̡�
        /// </summary>
        /// <param name="value"></param>
        protected virtual void OnValidate(Object value) { 
            if (value == null) throw new ArgumentNullException("value");
            Contract.EndContractBlock();
        }

        /// <summary>
        /// ���� CollectionBase ʵ��������ֵ��ִ�������Զ�����̡�
        /// </summary>
        /// <param name="index"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void OnSetComplete(int index, Object oldValue, Object newValue) { 
        }

        /// <summary>
        /// ���� CollectionBase ʵ���в�����Ԫ��֮��ִ�������Զ�����̡�
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        protected virtual void OnInsertComplete(int index, Object value) { 
        }

        /// <summary>
        /// ����� CollectionBase ʵ��������֮��ִ�������Զ�����̡�
        /// </summary>
        protected virtual void OnClearComplete() { 
        }

        /// <summary>
        /// �ڴ� CollectionBase ʵ�����Ƴ�Ԫ��֮��ִ�������Զ�����̡�
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        protected virtual void OnRemoveComplete(int index, Object value) { 
        }
    
    }

}
