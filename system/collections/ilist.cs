// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  IList
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Base interface for all Lists.
**
** 
===========================================================*/
namespace System.Collections {
    
    using System;
    using System.Diagnostics.Contracts;

    // An IList is an ordered collection of objects.  The exact ordering
    // is up to the implementation of the list, ranging from a sorted
    // order to insertion order. 
    // ��ʾ�ɰ��������������ʵĶ���ķǷ��ͼ��ϡ�
#if CONTRACTS_FULL
    [ContractClass(typeof(IListContract))]
#endif // CONTRACTS_FULL
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface IList : ICollection
    {
        /// <summary>
        /// ��ȡ������λ��ָ����������Ԫ�ء�
        /// </summary>
        /// <param name="index">Ҫ��û����õ�Ԫ�ش��㿪ʼ��������</param>
        /// <returns>λ��ָ����������Ԫ�ء�</returns>
        Object this[int index] {
            get;
            set;
        }
    
        // Adds an item to the list.  The exact position in the list is 
        // implementation-dependent, so while ArrayList may always insert
        // in the last available location, a SortedList most likely would not.
        // The return value is the position the new element was inserted in.
        // ��һ����Ŀ��ӵ��б��С�
        // �б��е�ȷ��λ����������,������ȻArrayList���������һ�����õ�λ��,����һ��SortedList���п��ܲ��ᡣ
        // ����ֵ�ǲ�����Ԫ�ص�λ�á�
        /// <summary>
        /// ��ĳ����ӵ� IList �С�
        /// </summary>
        /// <param name="value">Ҫ��ӵ� IList �Ķ���</param>
        /// <returns>��Ԫ�������뵽��λ�ã���Ϊ -1 ��ָʾδ��������뵽�����С�</returns>
        int Add(Object value);
    
        /// <summary>
        /// ȷ�� IList �Ƿ�����ض�ֵ��
        /// </summary>
        /// <param name="value">Ҫ�� IList �ж�λ�Ķ���</param>
        /// <returns>����� IList ���ҵ� Object����Ϊ true������Ϊ false��</returns>
        bool Contains(Object value);
    
        /// <summary>
        /// �� IList ���Ƴ������
        /// </summary>
        void Clear();

        bool IsReadOnly 
        { get; }

    
        bool IsFixedSize
        {
            get;
        }

        /// <summary>
        /// ȷ�� IList ���ض����������
        /// </summary>
        /// <param name="value">Ҫ�� IList �ж�λ�Ķ���</param>
        /// <returns>������б����ҵ�����Ϊ value ������������Ϊ -1��</returns>
        int IndexOf(Object value);
    
        // Inserts value into the list at position index.
        // index must be non-negative and less than or equal to the 
        // number of elements in the list.  If index equals the number
        // of items in the list, then value is appended to the end.
        // ��ֵ���뵽�б������λ�á����������ǷǸ�����С�ڻ�������б���Ԫ�ص����������ָ�������б��е���Ŀ������,Ȼ��ֵ���ӵ�������
        /// <summary>
        /// ��һ�������ָ���������� IList��
        /// </summary>
        /// <param name="index">���㿪ʼ��������Ӧ�ڸ�λ�ò��� value��</param>
        /// <param name="value">Ҫ���� IList �Ķ���</param>
        void Insert(int index, Object value);
    
        /// <summary>
        /// �� IList ���Ƴ��ض�����ĵ�һ��ƥ���
        /// </summary>
        /// <param name="value"></param>
        void Remove(Object value);
    
        /// <summary>
        /// �Ƴ�ָ���������� IList �
        /// </summary>
        /// <param name="index"></param>
        void RemoveAt(int index);
    }

#if CONTRACTS_FULL
    [ContractClassFor(typeof(IList))]
    internal abstract class IListContract : IList
    {
        int IList.Add(Object value)
        {
            //Contract.Ensures(((IList)this).Count == Contract.OldValue(((IList)this).Count) + 1);  // Not threadsafe
            // This method should return the index in which an item was inserted, but we have
            // some internal collections that don't always insert items into the list, as well
            // as an MSDN sample code showing us returning -1.  Allow -1 to mean "did not insert".
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < ((IList)this).Count);
            return default(int);
        }

        Object IList.this[int index] {
            get {
                //Contract.Requires(index >= 0);
                //Contract.Requires(index < ((IList)this).Count);
                return default(int);
            }
            set {
                //Contract.Requires(index >= 0);
                //Contract.Requires(index < ((IList)this).Count);
            }
        }

        bool IList.IsFixedSize {
            get { return default(bool); }
        }

        bool IList.IsReadOnly {
            get { return default(bool); }
        }

        bool ICollection.IsSynchronized {
            get { return default(bool); }
        }

        void IList.Clear()
        {
            //Contract.Ensures(((IList)this).Count == 0  || ((IList)this).IsFixedSize);  // not threadsafe
        }

        bool IList.Contains(Object value)
        {
            return default(bool);
        }

        void ICollection.CopyTo(Array array, int startIndex)
        {
            //Contract.Requires(array != null);
            //Contract.Requires(startIndex >= 0);
            //Contract.Requires(startIndex + ((IList)this).Count <= array.Length);
        }

        int ICollection.Count {
            get {
                return default(int);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return default(IEnumerator);
        }

        [Pure]
        int IList.IndexOf(Object value)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < ((IList)this).Count);
            return default(int);
        }

        void IList.Insert(int index, Object value)
        {
            //Contract.Requires(index >= 0);
            //Contract.Requires(index <= ((IList)this).Count);  // For inserting immediately after the end.
            //Contract.Ensures(((IList)this).Count == Contract.OldValue(((IList)this).Count) + 1);  // Not threadsafe
        }

        void IList.Remove(Object value)
        {
            // No information if removal fails.
        }

        void IList.RemoveAt(int index)
        {
            //Contract.Requires(index >= 0);
            //Contract.Requires(index < ((IList)this).Count);
            //Contract.Ensures(((IList)this).Count == Contract.OldValue(((IList)this).Count) - 1);  // Not threadsafe
        }
        
        Object ICollection.SyncRoot {
            get {
                return default(Object);
            }
        }
    }
#endif // CONTRACTS_FULL
}
