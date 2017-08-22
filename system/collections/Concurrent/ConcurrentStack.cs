#pragma warning disable 0420

// ==++==
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// ConcurrentStack.cs
//
// <OWNER>[....]</OWNER>
//
// A lock-free, concurrent stack primitive, and its associated debugger view type.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace System.Collections.Concurrent
{
    // A stack that uses CAS operations internally to maintain thread-safety in a lock-free
    // manner. Attempting to push or pop concurrently from the stack will not trigger waiting,
    // although some optimistic concurrency and retry is used, possibly leading to lack of
    // fairness and/or livelock. The stack uses spinning and backoff to add some randomization,
    // in hopes of statistically decreasing the possibility of livelock.
    // 
    // Note that we currently allocate a new node on every push. This avoids having to worry
    // about potential ABA issues, since the CLR GC ensures that a memory address cannot be
    // reused before all references to it have died.

    /// <summary>
    /// ��ʾ�̰߳�ȫ�ĺ���ȳ� (LIFO) ���ϡ�
    /// </summary>
    /// <typeparam name="T">��ջ�а�����Ԫ�ص����͡�</typeparam>
    /// <remarks>
    /// <see cref="ConcurrentStack{T}"/> �еĹ����ͱ����ĳ�Ա�����̰߳�ȫ���ҿ��ܻ�ʹ���ڶ��̲߳�����
    /// </remarks>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(SystemCollectionsConcurrent_ProducerConsumerCollectionDebugView<>))]
    [HostProtection(Synchronization = true, ExternalThreading = true)]
#if !FEATURE_CORECLR
    [Serializable]
#endif //!FEATURE_CORECLR
    public class ConcurrentStack<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        /// <summary>
        /// һ���򵥵Ľڵ��ʽ�����ڴ洢����Ԫ�ص�ջ�Ͷ���
        /// </summary>
        private class Node
        {
            internal readonly T m_value; // Value of the node.
            internal Node m_next; // Next pointer.

            /// <summary>
            /// ͨ��ָ����ֵ������һ���ڵ㹹��һ���½ڵ�
            /// </summary>
            /// <param name="value">�ڵ�ֵ</param>
            internal Node(T value)
            {
                m_value = value;
                m_next = null;
            }
        }

#if !FEATURE_CORECLR
        [NonSerialized]
#endif //!FEATURE_CORECLR
        private volatile Node m_head; // The stack is a singly linked list, and only remembers the head.

#if !FEATURE_CORECLR
        private T[] m_serializationArray; // ���������������л�
#endif //!FEATURE_CORECLR

        private const int BACKOFF_MAX_YIELDS = 8; // Arbitrary number to cap backoff.

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentStack{T}"/>
        /// class.
        /// </summary>
        public ConcurrentStack()
        {
        }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentStack<T> �����ʵ�������������ָ�������и��Ƶ�Ԫ��
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new <see
        /// cref="ConcurrentStack{T}"/>.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/>����Ϊ��</exception>
        public ConcurrentStack(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }
            InitializeFromCollection(collection);
        }

        /// <summary>
        /// Initializes the contents of the stack from an existing collection.
        /// </summary>
        /// <param name="collection">A collection from which to copy elements.</param>
        private void InitializeFromCollection(IEnumerable<T> collection)
        {
            // ���ǽ��������ݸ��Ƶ�ջ��
            Node lastNode = null;
            foreach (T element in collection)
            {
                Node newNode = new Node(element);
                newNode.m_next = lastNode;
                lastNode = newNode;
            }

            m_head = lastNode;
        }

#if !FEATURE_CORECLR
        /// <summary>
        /// ��ȡ���������������л�
        /// </summary>
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            // save the data into the serialization array to be saved
            m_serializationArray = ToArray();
        }

        /// <summary>
        /// ��һ��Ԥ�ȵ����л�����һ��ջ
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Contract.Assert(m_serializationArray != null);
            Node prevNode = null;
            Node head = null;
            for (int i = 0; i < m_serializationArray.Length; i++)
            {
                Node currNode = new Node(m_serializationArray[i]);

                if (prevNode == null)
                {
                    head = currNode;
                }
                else
                {
                    prevNode.m_next = currNode;
                }

                prevNode = currNode;
            }

            m_head = head;
            m_serializationArray = null;//������л�����
        }
#endif //!FEATURE_CORECLR


        /// <summary>
        /// ��ȡһ��ֵָʾ<see cref="ConcurrentStack{T}"/>�Ƿ�Ϊ�յ�ֵ
        /// </summary>
        /// <value>true if the <see cref="ConcurrentStack{T}"/> is empty; otherwise, false.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of this property is recommended
        /// rather than retrieving the number of items from the <see cref="Count"/> property and comparing it
        /// to 0.  However, as this collection is intended to be accessed concurrently, it may be the case
        /// that another thread will modify the collection after <see cref="IsEmpty"/> returns, thus invalidating
        /// the result.
        /// </remarks>
        public bool IsEmpty
        {
            // Checks whether the stack is empty. Clearly the answer may be out of date even prior to
            // the function returning (i.e. if another thread concurrently adds to the stack). It does
            // guarantee, however, that, if another thread does not mutate the stack, a subsequent call
            // to TryPop will return true -- i.e. it will also read the stack as non-empty.
            get { return m_head == null; }
        }

        /// <summary>
        ///  ��ȡ System.Collections.Concurrent.ConcurrentStack<T> �а�����Ԫ������
        /// </summary>
        /// <value>The number of elements contained in the <see cref="ConcurrentStack{T}"/>.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than retrieving the number of items from the <see cref="Count"/>
        /// property and comparing it to 0.
        /// </remarks>
        public int Count
        {
            // Counts the number of entries in the stack. This is an O(n) operation. The answer may be out
            // of date before returning, but guarantees to return a count that was once valid. Conceptually,
            // the implementation snaps a copy of the list and then counts the entries, though physically
            // this is not what actually happens.
            get
            {
                int count = 0;

                // Just whip through the list and tally up the number of nodes. We rely on the fact that
                // node next pointers are immutable after being enqueued for the first time, even as
                // they are being dequeued. If we ever changed this (e.g. to pool nodes somehow),
                // we'd need to revisit this implementation.

                for (Node curr = m_head; curr != null; curr = curr.m_next)
                {
                    count++; //we don't handle overflow, to be consistent with existing generic collection types in CLR
                }

                return count;
            }
        }


        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is
        /// synchronized with the SyncRoot.
        /// </summary>
        /// <value>true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized
        /// with the SyncRoot; otherwise, false. For <see cref="ConcurrentStack{T}"/>, this property always
        /// returns false.</value>
        bool ICollection.IsSynchronized
        {
            // Gets a value indicating whether access to this collection is synchronized. Always returns
            // false. The reason is subtle. While access is in face thread safe, it's not the case that
            // locking on the SyncRoot would have prevented concurrent pushes and pops, as this property
            // would typically indicate; that's because we internally use CAS operations vs. true locks.
            get { return false; }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see
        /// cref="T:System.Collections.ICollection"/>. This property is not supported.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The SyncRoot property is not supported</exception>
        object ICollection.SyncRoot
        {
            get
            {
                throw new NotSupportedException(Environment.GetResourceString("ConcurrentCollection_SyncRoot_NotSupported"));
            }
        }

        /// <summary>
        /// �Ƴ�<see cref="ConcurrentStack{T}"/>�����ж���
        /// </summary>
        public void Clear()
        {
            // Clear the list by setting the head to null. We don't need to use an atomic
            // operation for this: anybody who is mutating the head by pushing or popping
            // will need to use an atomic operation to guarantee they serialize and don't
            // overwrite our setting of the head to null.
            // ͨ����head����Ϊnull���list�����ǲ���Ҫʹ��һ��ԭ�Ӳ�����
            // �κ�ͨ��push��pop�仯head���˽���Ҫʹ��һ��ԭ�Ӳ���ȥ��֤���л����Ҳ���Ҫ��д���ǽ�head����Ϊnull
            m_head = null;
        }

        /// <summary>
        /// ��<see cref="T:System.Collections.ICollection"/>����Ԫ�ظ��Ƹ�һ��<see
        /// cref="T:System.Array"/>,������Ŀ�ʼ������
        /// ����ʾ����ICollection.CopyTo��
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of
        /// the elements copied from the
        /// <see cref="ConcurrentStack{T}"/>. The <see cref="T:System.Array"/> must
        /// have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in
        /// Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// zero.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="array"/> is multidimensional. -or-
        /// <paramref name="array"/> does not have zero-based indexing. -or-
        /// <paramref name="index"/> is equal to or greater than the length of the <paramref name="array"/>
        /// -or- The number of elements in the source <see cref="T:System.Collections.ICollection"/> is
        /// greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>. -or- The type of the source <see
        /// cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the
        /// destination <paramref name="array"/>.
        /// </exception>
        void ICollection.CopyTo(Array array, int index)
        {
            // Validate arguments.
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            // ���Ǳ��������ֹ��array���������ǽ����Ȼ���һ���ڲ�Ԫ��list������������array����
            // ����Ҫ����ķ��䣬�����Ǳ���ĵ������ǲ�֪�������Ƿ����㹻��Ķ�������ջ������
            ((ICollection)ToList()).CopyTo(array, index);//����ICollection�е�CopyTo
        }

        /// <summary>
        /// Copies the <see cref="ConcurrentStack{T}"/> elements to an existing one-dimensional <see
        /// cref="T:System.Array"/>, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of
        /// the elements copied from the
        /// <see cref="ConcurrentStack{T}"/>. The <see cref="T:System.Array"/> must have zero-based
        /// indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in
        /// Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// zero.</exception>
        /// <exception cref="ArgumentException"><paramref name="index"/> is equal to or greater than the
        /// length of the <paramref name="array"/>
        /// -or- The number of elements in the source <see cref="ConcurrentStack{T}"/> is greater than the
        /// available space from <paramref name="index"/> to the end of the destination <paramref
        /// name="array"/>.
        /// </exception>
        public void CopyTo(T[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            // We must be careful not to corrupt the array, so we will first accumulate an
            // internal list of elements that we will then copy to the array. This requires
            // some extra allocation, but is necessary since we don't know up front whether
            // the array is sufficiently large to hold the stack's contents.
            ToList().CopyTo(array, index);//����List�е�CopyTo
        }


        /// <summary>
        /// ���������<see cref="ConcurrentStack{T}"/>����
        /// </summary>
        /// <param name="item">
        /// Ҫ���뵽 System.Collections.Concurrent.ConcurrentStack<T> �еĶ��� ��ֵ�����������Ϳ����ǿ�����
        /// </param>
        public void Push(T item)
        {
            // Pushes a node onto the front of the stack thread-safely. Internally���ڲ��أ�, this simply
            // swaps�������� the current head pointer using a (thread safe) CAS operation to accomplish����ɣ�
            // lock freedom. If the CAS fails, we add some back off to statistically��ͳ�ƣ� decrease�����٣�
            // contention�����ۣ� at the head, and then go back around and retry.

            Node newNode = new Node(item);
            newNode.m_next = m_head;
            if (Interlocked.CompareExchange(ref m_head, newNode, newNode.m_next) == newNode.m_next)
            {
                return;
            }

            // �������ʧ���ˣ�����·����ѭ�����룬ֱ�����ǳɹ���
            PushCore(newNode, newNode);
        }

        /// <summary>
        /// �Զ������������� System.Collections.Concurrent.ConcurrentStack<T> �Ķ�����
        /// </summary>
        /// <param name="items">Ҫ���뵽 System.Collections.Concurrent.ConcurrentStack<T> �еĶ���</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <remarks>
        /// When adding multiple items to the stack, using PushRange is a more efficient
        /// mechanism than using <see cref="Push"/> one item at a time.  Additionally, PushRange
        /// guarantees that all of the elements will be added atomically, meaning that no other threads will
        /// be able to inject elements between the elements being pushed.  Items at lower indices in
        /// the <paramref name="items"/> array will be pushed before items at higher indices.
        /// </remarks>
        public void PushRange(T[] items)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }
            PushRange(items, 0, items.Length);
        }

        /// <summary>
        /// �Զ������������� System.Collections.Concurrent.ConcurrentStack<T> �Ķ�����
        /// </summary>
        /// <param name="items">Ҫ���뵽 System.Collections.Concurrent.ConcurrentStack<T> �еĶ���</param>
        /// <param name="startIndex">items �д��㿪ʼ��ƫ�������Ӵ˴���ʼ��Ԫ�ز��뵽 
        /// System.Collections.Concurrent.ConcurrentStack<T>�Ķ�����</param>
        /// <param name="count">Ҫ���뵽 System.Collections.Concurrent.ConcurrentStack<T> �Ķ�����Ԫ������</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex �� count Ϊ���� �� startIndex ���ڻ���� items �ĳ��ȡ�</exception>
        /// <exception cref="ArgumentException"><paramref name="startIndex"/> + <paramref name="count"/> ����<paramref name="items"/>�ĳ���</exception>
        /// <remarks>
        /// When adding multiple items to the stack, using PushRange is a more efficient
        /// mechanism than using <see cref="Push"/> one item at a time. Additionally, PushRange
        /// guarantees that all of the elements will be added atomically, meaning that no other threads will
        /// be able to inject elements between the elements being pushed. Items at lower indices in the
        /// <paramref name="items"/> array will be pushed before items at higher indices.
        /// </remarks>
        public void PushRange(T[] items, int startIndex, int count)
        {
            ValidatePushPopRangeInput(items, startIndex, count);

            // No op if the count is zero
            if (count == 0)
                return;

            // ��items������з�װת��ΪNodes
            Node head, tail;
            head = tail = new Node(items[startIndex]);
            for (int i = startIndex + 1; i < startIndex + count; i++)
            {
                Node node = new Node(items[i]);
                node.m_next = head;
                head = node;
            }

            tail.m_next = m_head;
            if (Interlocked.CompareExchange(ref m_head, head, tail.m_next) == tail.m_next)
            {
                return;
            }

            // If we failed, go to the slow path and loop around until we succeed.
            PushCore(head, tail);

        }


        /// <summary>
        /// ��һ�������ڵ�ѹ��ջ�У����ͷ��β�ڵ�����ͬ����һ���ڵ�ѹ��ջ�У�����λ��ͷ��β�ڵ��е��б�ѹ��ջ��
        /// </summary>
        /// <param name="head">���б��ͷ�ڵ�</param>
        /// <param name="tail">���б��β�ڵ�</param>
        private void PushCore(Node head, Node tail)
        {
            SpinWait spin = new SpinWait();

            // Keep trying to CAS the exising head with the new node until we succeed.
            // ������ջ��head�ڵ㸳ֵ��Ҫ���ջ��β�ڵ��m_next���ٽ�head�ڵ㸳ֵ������ջ��head�ڵ㣬
            // ���ж�ԭm_head�е�ֵ�Ƿ���tail.m_next���
            do
            {
                spin.SpinOnce();
                // �ظ���ȡͷ�������µĽڵ�
                tail.m_next = m_head;
            }
            while (Interlocked.CompareExchange(
                ref m_head, head, tail.m_next) != tail.m_next);

#if !FEATURE_PAL && !FEATURE_CORECLR
            if (CDSCollectionETWBCLProvider.Log.IsEnabled())
            {
                CDSCollectionETWBCLProvider.Log.ConcurrentStack_FastPushFailed(spin.Count);
            }
#endif //!FEATURE_PAL && !FEATURE_CORECLR
        }

        /// <summary>
        /// ���ذ�������ȥ��֤pop push ��Χ�ķ�������У��
        /// </summary>
        private void ValidatePushPopRangeInput(T[] items, int startIndex, int count)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ConcurrentStack_PushPopRange_CountOutOfRange"));
            }
            int length = items.Length;
            if (startIndex >= length || startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ConcurrentStack_PushPopRange_StartOutOfRange"));
            }
            if (length - count < startIndex) //instead of (startIndex + count > items.Length) to prevent overflow
            {
                throw new ArgumentException(Environment.GetResourceString("ConcurrentStack_PushPopRange_InvalidCount"));
            }
        }

        /// <summary>
        /// ���Խ�һ��������ӵ� System.Collections.Concurrent.IProducerConsumerCollection<T> �С�
        /// </summary>
        /// <param name="item">The object to add to the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>. The value can be a null
        /// reference (Nothing in Visual Basic) for reference types.
        /// </param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        /// <remarks>For <see cref="ConcurrentStack{T}"/>, this operation
        /// will always insert the object onto the top of the <see cref="ConcurrentStack{T}"/>
        /// and return true.</remarks>
        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            Push(item);
            return true;
        }

        /// <summary>
        /// ���Է��� System.Collections.Concurrent.ConcurrentStack<T> �����Ķ��󵫲������Ƴ���
        /// </summary>
        /// <param name="result">�˷�������ʱ��result ���� System.Collections.Concurrent.ConcurrentStack<T> �����Ķ���
        /// �������ʧ�ܣ������δָ����ֵ��</param>
        /// <returns>����ɹ������˶�����Ϊ true������Ϊ false��</returns>
        public bool TryPeek(out T result)
        {
            Node head = m_head;

            // If the stack is empty, return false; else return the element and true.
            if (head == null)
            {
                result = default(T);
                return false;
            }
            else
            {
                result = head.m_value;
                return true;
            }
        }

        /// <summary>
        /// ���Ե��������� System.Collections.Concurrent.ConcurrentStack<T> �����Ķ���
        /// </summary>
        /// <param name="result">
        /// �˷�������ʱ����������ɹ����� result �������Ƴ��Ķ��� ���û�пɹ��Ƴ��Ķ�����ָ����ֵ��
        /// </param>
        /// <returns>����ɹ��Ƴ��������� System.Collections.Concurrent.ConcurrentStack<T> ������Ԫ�أ���Ϊ true��
        /// ����Ϊfalse��</returns>
        public bool TryPop(out T result)
        {
            Node head = m_head;
            //stack is empty
            if (head == null)
            {
                result = default(T);
                return false;
            }
            if (Interlocked.CompareExchange(ref m_head, head.m_next, head) == head)//��head.m_next�滻Ϊhead
            {
                result = head.m_value;
                return true;
            }

            // Fall through to the slow path.
            return TryPopCore(out result);
        }

        /// <summary>
        /// �����Զ����������� System.Collections.Concurrent.ConcurrentStack<T> �����Ķ������
        /// </summary>
        /// <param name="items">
        /// Ҫ���� System.Collections.Concurrent.ConcurrentStack<T> ���������Ķ�����ӵ��� System.Array��
        /// </param>
        /// <returns>�ѳɹ��� System.Collections.Concurrent.ConcurrentStack<T> 
        /// �������������뵽 items �еĶ�������</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is a null argument (Nothing
        /// in Visual Basic).</exception>
        /// <remarks>
        /// When popping multiple items, if there is little contention on the stack, using
        /// TryPopRange can be more efficient than using <see cref="TryPop"/>
        /// once per item to be removed.  Nodes fill the <paramref name="items"/>
        /// with the first node to be popped at the startIndex, the second node to be popped
        /// at startIndex + 1, and so on.
        /// </remarks>
        public int TryPopRange(T[] items)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }

            return TryPopRange(items, 0, items.Length);
        }

        /// <summary>
        /// �����Զ����������� System.Collections.Concurrent.ConcurrentStack<T> �����Ķ������
        /// </summary>
        /// <param name="items">
        /// Ҫ���� System.Collections.Concurrent.ConcurrentStack<T> ���������Ķ�����ӵ��� System.Array��
        /// </param>
        /// <param name="startIndex">items �д��㿪ʼ��ƫ������
        /// �Ӵ˴���ʼ���� System.Collections.Concurrent.ConcurrentStack<T>������Ԫ�ء�</param>
        /// <param name="count">T���� System.Collections.Concurrent.ConcurrentStack<T>
        /// �������������뵽 items �е�Ԫ������</param>
        /// <returns>�ѳɹ��Ӷ�ջ�������������뵽 items �еĶ�������</returns>        
        /// <exception cref="ArgumentNullException">items �� null ���ã��� Visual Basic ��Ϊ Nothing����</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex �� count Ϊ����
        /// �� startIndex ���ڻ���� items �ĳ��ȡ�</exception>
        /// <exception cref="ArgumentException">startIndex + count ���� items �ĳ��ȡ�</exception>
        /// <remarks>
        /// When popping multiple items, if there is little contention on the stack, using
        /// TryPopRange can be more efficient than using <see cref="TryPop"/>
        /// once per item to be removed.  Nodes fill the <paramref name="items"/>
        /// with the first node to be popped at the startIndex, the second node to be popped
        /// at startIndex + 1, and so on.
        /// </remarks>
        public int TryPopRange(T[] items, int startIndex, int count)
        {
            ValidatePushPopRangeInput(items, startIndex, count);

            // No op if the count is zero
            if (count == 0)
                return 0;

            Node poppedHead;
            int nodesCount = TryPopCore(count, out poppedHead);
            if (nodesCount > 0)
            {
                CopyRemovedItems(poppedHead, items, startIndex, nodesCount);

            }
            return nodesCount;

        }

        /// <summary>
        /// Local helper function to Pop an item from the stack, slow path
        /// </summary>
        /// <param name="result">The popped item</param>
        /// <returns>True if succeeded, false otherwise</returns>
        private bool TryPopCore(out T result)
        {
            Node poppedNode;

            if (TryPopCore(1, out poppedNode) == 1)
            {
                result = poppedNode.m_value;
                return true;
            }

            result = default(T);
            return false;

        }

        /// <summary>
        /// Slow path helper for TryPop. This method assumes an initial attempt to pop an element
        /// has already occurred and failed, so it begins spinning right away.
        /// </summary>
        /// <param name="count">The number of items to pop.</param>
        /// <param name="poppedHead">
        /// When this method returns, if the pop succeeded, contains the removed object. If no object was
        /// available to be removed, the value is unspecified. This parameter is passed uninitialized.
        /// </param>
        /// <returns>True if an element was removed and returned; otherwise, false.</returns>
        private int TryPopCore(int count, out Node poppedHead)
        {
            SpinWait spin = new SpinWait();

            // Try to CAS the head with its current next.  We stop when we succeed or
            // when we notice that the stack is empty, whichever comes first.
            Node head;
            Node next;
            int backoff = 1;
            Random r = new Random(Environment.TickCount & Int32.MaxValue); // avoid the case where TickCount could return Int32.MinValue
            while (true)
            {
                head = m_head;
                // ���Ϊ�գ��򷵻ؿ�ָ��
                if (head == null)
                {
#if !FEATURE_PAL && !FEATURE_CORECLR
                    if (count == 1 && CDSCollectionETWBCLProvider.Log.IsEnabled())
                    {
                        CDSCollectionETWBCLProvider.Log.ConcurrentStack_FastPopFailed(spin.Count);
                    }
#endif //!FEATURE_PAL && !FEATURE_CORECLR
                    poppedHead = null;
                    return 0;
                }

                // ���ڱ�����Ҫpop���ڵ㼯�ϵ���Ŀ��β��
                next = head;
                int nodesCount = 1;
                for (; nodesCount < count && next.m_next != null; nodesCount++)
                {
                    next = next.m_next;
                }

                // Try to swap the new head.  If we succeed, break out of the loop.
                // �����滻�µ�head��������ǳɹ�������ѭ��
                if (Interlocked.CompareExchange(ref m_head, next.m_next, head) == head)
                {
#if !FEATURE_PAL && !FEATURE_CORECLR
                    if (count == 1 && CDSCollectionETWBCLProvider.Log.IsEnabled())
                    {
                        CDSCollectionETWBCLProvider.Log.ConcurrentStack_FastPopFailed(spin.Count);
                    }
#endif //!FEATURE_PAL && !FEATURE_CORECLR
                    // Return the popped Node.
                    poppedHead = head;
                    return nodesCount;
                }

                // We failed to CAS the new head.  Spin briefly and retry.
                // ����Ĵ��벻֪��
                for (int i = 0; i < backoff; i++)
                {
                    spin.SpinOnce();
                }

                backoff = spin.NextSpinWillYield ? r.Next(1, BACKOFF_MAX_YIELDS) : backoff * 2;
            }
        }


        /// <summary>
        /// ���ذ�������ȥ����pop����Ԫ�ظ�һ������
        /// </summary>
        /// <param name="head">�����ڸ��Ƶ��б�ͷ</param>
        /// <param name="collection">���pop��Ԫ�صļ���</param>
        /// <param name="startIndex">���popԪ�صĿ�ʼ����</param>
        /// <param name="nodesCount">�ڵ���Ŀ</param>
        private void CopyRemovedItems(Node head, T[] collection, int startIndex, int nodesCount)
        {
            Node current = head;
            for (int i = startIndex; i < startIndex + nodesCount; i++)
            {
                collection[i] = current.m_value;
                current = current.m_next;
            }

        }

        /// <summary>
        /// Attempts to remove and return an object from the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>.
        /// </summary>
        /// <param name="item">
        /// When this method returns, if the operation was successful, <paramref name="item"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>true if an element was removed and returned succesfully; otherwise, false.</returns>
        /// <remarks>For <see cref="ConcurrentStack{T}"/>, this operation will attempt to pope the object at
        /// the top of the <see cref="ConcurrentStack{T}"/>.
        /// </remarks>
        bool IProducerConsumerCollection<T>.TryTake(out T item)
        {
            return TryPop(out item);
        }

        /// <summary>
        /// Copies the items stored in the <see cref="ConcurrentStack{T}"/> to a new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of elements copied from the <see
        /// cref="ConcurrentStack{T}"/>.</returns>
        public T[] ToArray()
        {
            return ToList().ToArray();
        }

        /// <summary>
        /// ����һ������list���ݿ��յ����飬ʹ��Ŀ���б�ڵ������б�Χ��ͷ
        /// </summary>
        /// <returns>һ��list���ݵ�����</returns>
        private List<T> ToList()
        {
            List<T> list = new List<T>();

            Node curr = m_head;
            while (curr != null)
            {
                list.Add(curr.m_value);
                curr = curr.m_next;
            }
            return list;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="ConcurrentStack{T}"/>.
        /// </summary>
        /// <returns>An enumerator for the <see cref="ConcurrentStack{T}"/>.</returns>
        /// <remarks>
        /// The enumeration represents a moment-in-time snapshot of the contents
        /// of the stack.  It does not reflect any updates to the collection after 
        /// <see cref="GetEnumerator"/> was called.  The enumerator is safe to use
        /// concurrently with reads from and writes to the stack.
        /// </remarks>
        public IEnumerator<T> GetEnumerator()
        {
            // Returns an enumerator for the stack. This effectively takes a snapshot
            // of the stack's contents at the time of the call, i.e. subsequent modifications
            // (pushes or pops) will not be reflected in the enumerator's contents.

            //If we put yield-return here, the iterator will be lazily evaluated. As a result a snapshot of
            //the stack is not taken when GetEnumerator is initialized but when MoveNext() is first called.
            //This is inconsistent with existing generic collections. In order to prevent it, we capture the 
            //value of m_head in a buffer and call out to a helper method
            return GetEnumerator(m_head);
        }

        private IEnumerator<T> GetEnumerator(Node head)
        {
            Node current = head;
            while (current != null)
            {
                yield return current.m_value;
                current = current.m_next;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"/> that can be used to iterate through
        /// the collection.</returns>
        /// <remarks>
        /// The enumeration represents a moment-in-time snapshot of the contents of the stack. It does not
        /// reflect any updates to the collection after
        /// <see cref="GetEnumerator"/> was called. The enumerator is safe to use concurrently with reads
        /// from and writes to the stack.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }
    }
}
