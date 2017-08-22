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
** Purpose: Base interface for all collections.
**
** 
===========================================================*/
namespace System.Collections {
    using System;
    using System.Diagnostics.Contracts;

    // Base interface for all collections, defining enumerators, size, and 
    // synchronization methods.
#if CONTRACTS_FULL
    [ContractClass(typeof(ICollectionContract))]
#endif // CONTRACTS_FULL
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface ICollection : IEnumerable
    {
        // Interfaces are not serialable
        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        // �ӿڲ�serialable CopyTo���ϸ��Ƶ�һ������,��һ���ض����������顣
        void CopyTo(Array array, int index);
        
        // ��ȡ�ڼ����е���Ŀ����
        int Count
        { get; }
        
        
        // SyncRoot will return an Object to use for synchronization 
        // (thread safety).  You can use this object in your code to take a
        // lock on the collection, even if this collection is a wrapper around
        // another collection.  The intent is to tunnel through to a real 
        // implementation of a collection, and use one of the internal objects
        // found in that code.
        // 
        // SyncRoot������һ����������ͬ��(�̰߳�ȫ)��
        // �������ڴ�����ʹ�����������������,��ʹ�����������һ�����ϰ�װ��
        // Ŀ������������ϵ�������ʵ��,��ʹ��һ���ڲ�������롣
        //
        // In the absense of a static Synchronized method on a collection, 
        // the expected usage for SyncRoot would look like this:
        // 
        // �ھ�̬ͬ������Ҳ��һ������,SyncRootԤ�ڵ�ʹ�������������:
        // 
        // ICollection col = ...
        // lock (col.SyncRoot) {
        //     // Some operation on the collection, which is now thread safe.
        //     // This may include multiple operations.
        // }
        // 
        // 
        // The system-provided collections have a static method called 
        // Synchronized which will create a thread-safe wrapper around the 
        // collection.  All access to the collection that you want to be 
        // thread-safe should go through that wrapper collection.  However, if
        // you need to do multiple calls on that collection (such as retrieving
        // two items, or checking the count then doing something), you should
        // NOT use our thread-safe wrapper since it only takes a lock for the
        // duration of a single method call.  Instead, use Monitor.Enter/Exit
        // or your language's equivalent to the C# lock keyword as mentioned 
        // above.
        // 
        // ϵͳ�ṩ׼������һ����̬������ͬ��������һ���̰߳�ȫ�ļ��ϰ�װ��
        //��������Ҫ���ʼ���ͨ����װ���ռ�Ӧ�����̰߳�ȫ�ġ�
        //Ȼ��,�������Ҫ�������,����(�����������Ŀ,�������Ȼ����),���ǲ�Ӧ��ʹ���̰߳�ȫ�İ�װ��,��Ϊ��ֻ��Ҫһ�����ĵ����������õĳ���ʱ�䡣
        //�෴,ʹ��Monitor.Enter /�˳�����������Եĵȼ���c#�����ؼ�������������

        // For collections with no publically available underlying store, the 
        // expected implementation is to simply return the this pointer.  Note 
        // that the this pointer may not be sufficient for collections that 
        // wrap other collections;  those should return the underlying 
        // collection's SyncRoot property.
        // Ϊ����û�й������õĵײ�洢,Ԥ�ڵ�ʵ���Ƿ���ָ�롣
        // ע��,���ָ�벢�����������װ�������ϵļ���;��ЩӦ�÷��صײ�SyncRoot���ز����ϡ�
        Object SyncRoot
        { get; }
            
        // Is this collection synchronized (i.e., thread-safe)?  If you want a 
        // thread-safe collection, you can use SyncRoot as an object to 
        // synchronize your collection with.  If you're using one of the 
        // collections in System.Collections, you could call the static 
        // Synchronized method to get a thread-safe wrapper around the 
        // underlying collection.
        // ���Ǽ���(��ͬ����,�̰߳�ȫ)?�������Ҫһ���̰߳�ȫ�ļ���,������ʹ��SyncRoot��Ϊ����ͬ������ղء�
        // �����ʹ��һ�����ϵ�ϵͳ������,����Ե��þ�̬ͬ�������õ�һ���̰߳�ȫ�İ�װ���ײ㼯�ϡ�
        bool IsSynchronized
        { get; }
    }

#if CONTRACTS_FULL
    [ContractClassFor(typeof(ICollection))]
    internal abstract class ICollectionContract : ICollection
    {
        void ICollection.CopyTo(Array array, int index)
        {
        }

        int ICollection.Count { 
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return default(int);
            }
        }

        Object ICollection.SyncRoot {
            get {
                Contract.Ensures(Contract.Result<Object>() != null);
                return default(Object);
            }
        }

        bool ICollection.IsSynchronized {
            get { return default(bool); }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return default(IEnumerator);
        }
    }
#endif // CONTRACTS_FULL
}
