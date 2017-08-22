// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Interface:  ICollection
** 
** <OWNER>kimhamil</OWNER>
**
**
** Purpose: Base interface for all generic collections.
**
** 
===========================================================*/
namespace System.Collections.Generic {
    using System;
    using System.Runtime.CompilerServices;
    using System.Diagnostics.Contracts;

    // ���ڽӿڼ���,����ö����,��С,��ͬ��������

    // Note that T[] : IList<T>, and we want to ensure that if you use
    // IList<YourValueType>, we ensure a YourValueType[] can be used 
    // without jitting.  Hence the TypeDependencyAttribute on SZArrayHelper.
    // This is a special hack internally though - see VM\compile.cpp.
    // The same attribute is on IEnumerable<T> and ICollection<T>.
    // ע��,T[]:IList < T >,������ȷ�������ʹ��IList < YourValueType >,����ȷ��YourValueTypeû��jit����ʹ��[]��
    // ���,TypeDependencyAttribute SZArrayHelper��
    //����һ��������ڲ�������Ȼ��������VM \ compile.cpp����ͬ��������IEnumerable < T >��ICollection < T >��
#if CONTRACTS_FULL
    [ContractClass(typeof(ICollectionContract<>))]
#endif
    [TypeDependencyAttribute("System.SZArrayHelper")]
    public interface ICollection<T> : IEnumerable<T>
    {
        /// <summary>
        ///  ��ȡ�ڼ����е���Ŀ����  
        /// </summary>
        int Count { get; }
        /// <summary>
        /// ��ȡ�Ƿ���ֻ��
        /// </summary>
        bool IsReadOnly { get; }
        /// <summary>
        /// ��
        /// </summary>
        /// <param name="item"></param>
        void Add(T item);

        void Clear();

        bool Contains(T item); 
                
        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        // 
        void CopyTo(T[] array, int arrayIndex);
                
        //void CopyTo(int sourceIndex, T[] destinationArray, int destinationIndex, int count);

        bool Remove(T item);
    }

#if CONTRACTS_FULL
    [ContractClassFor(typeof(ICollection<>))]
    internal abstract class ICollectionContract<T> : ICollection<T>
    {
        int ICollection<T>.Count {
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return default(int);
            }
        }

        bool ICollection<T>.IsReadOnly {
            get { return default(bool); }
        }

        void ICollection<T>.Add(T item)
        {
            //Contract.Ensures(((ICollection<T>)this).Count == Contract.OldValue(((ICollection<T>)this).Count) + 1);  // not threadsafe
        }

        void ICollection<T>.Clear()
        {
        }

        bool ICollection<T>.Contains(T item)
        {
            return default(bool);
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
        }

        bool ICollection<T>.Remove(T item)
        {
            return default(bool);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return default(IEnumerator<T>);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return default(IEnumerator);
        }
    }
#endif // CONTRACTS_FULL
}
