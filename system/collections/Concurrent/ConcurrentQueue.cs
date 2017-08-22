#pragma warning disable 0420

// ==++==
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// ConcurrentQueue.cs
//
// <OWNER>[....]</OWNER>
//
// A lock-free, concurrent queue primitive, and its associated debugger view type.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace System.Collections.Concurrent
{

    /// <summary>
    /// ��ʾ�̰߳�ȫ���Ƚ��ȳ� (FIFO) ���ϡ�
    /// </summary>
    /// <typeparam name="T"> �����а�����Ԫ�ص����͡�</typeparam>
    /// <remarks>
    /// All public  and protected members of <see cref="ConcurrentQueue{T}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
    [ComVisible(false)]
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(SystemCollectionsConcurrent_ProducerConsumerCollectionDebugView<>))]
    [HostProtection(Synchronization = true, ExternalThreading = true)]
    [Serializable]
    public class ConcurrentQueue<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        //fields of ConcurrentQueue
        [NonSerialized]
        private volatile Segment m_head;

        [NonSerialized]
        private volatile Segment m_tail;

        private T[] m_serializationArray; // Used for custom serialization.

        private const int SEGMENT_SIZE = 32;

        //number of snapshot takers, GetEnumerator(), ToList() and ToArray() operations take snapshot.
        [NonSerialized]
        internal volatile int m_numSnapshotTakers = 0;

        /// <summary>
        /// ��ʼ��һ���µ�ConcurrentQueue��
        /// </summary>
        public ConcurrentQueue()
        {
            m_head = m_tail = new Segment(0, this);
        }

        /// <summary>
        /// ��һ�����ڵļ����г�ʼ���������� �������������飩
        /// </summary>
        /// <param name="collection">���临��Ԫ�صļ���</param>
        private void InitializeFromCollection(IEnumerable<T> collection)
        {
            // ʹ�ñ��ر���ȥ�������Ķ���д�����ǰ�ȫ�ģ���Ϊ��ֻ������������
            Segment localTail = new Segment(0, this);
            m_head = localTail; 

            int index = 0;
            foreach (T element in collection)//������Ԫ�ر����������뵽segment��
            {
                Contract.Assert(index >= 0 && index < SEGMENT_SIZE);
                localTail.UnsafeAdd(element);//��Ԫ�ز��뵽localTail(segment)��
                index++;//��������һ

                if (index >= SEGMENT_SIZE)//�ٴ��ж�segment�Ƿ��Ѿ�����
                {
                    localTail = localTail.UnsafeGrow();//������ˣ�������localTail��������һ���µ�segment���ӵ���ǰ��segment��
                    index = 0;//��������
                }
            }

            m_tail = localTail;//���µ�ǰ���е�m_tail
        }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentQueue<T> �����ʵ�������������ָ�������и��Ƶ�Ԫ��
        /// </summary>
        /// <param name="collection">��Ԫ�ر����Ƶ��µ� System.Collections.Concurrent.ConcurrentQueue<T> �еļ��ϡ�</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="collection"/>collection ����Ϊ null��</exception>
        public ConcurrentQueue(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            InitializeFromCollection(collection);
        }

        /// <summary>
        /// ��ȡ�����������л�
        /// </summary>
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            // save the data into the serialization array to be saved
            m_serializationArray = ToArray();
        }

        /// <summary>
        /// ��һ�����л������й������
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Contract.Assert(m_serializationArray != null);
            InitializeFromCollection(m_serializationArray);
            m_serializationArray = null;
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see
        /// cref="T:System.Array"/>, starting at a particular
        /// <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array">Array</see> that is the
        /// destination of the elements copied from the
        /// <see cref="T:System.Collections.Concurrent.ConcurrentBag"/>. The <see
        /// cref="T:System.Array">Array</see> must have zero-based indexing.</param>
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

            // We must be careful not to corrupt the array, so we will first accumulate an
            // internal list of elements that we will then copy to the array. This requires
            // some extra allocation, but is necessary since we don't know up front whether
            // the array is sufficiently large to hold the stack's contents.
            ((ICollection)ToList()).CopyTo(array, index);
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is
        /// synchronized with the SyncRoot.
        /// </summary>
        /// <value>true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized
        /// with the SyncRoot; otherwise, false. For <see cref="ConcurrentQueue{T}"/>, this property always
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
        /// <exception cref="T:System.NotSupportedException">The SyncRoot property is not supported.</exception>
        object ICollection.SyncRoot
        {
            get
            {
                throw new NotSupportedException(Environment.GetResourceString("ConcurrentCollection_SyncRoot_NotSupported"));
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"/> that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        /// <summary>
        /// Attempts to add an object to the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>. The value can be a null
        /// reference (Nothing in Visual Basic) for reference types.
        /// </param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        /// <remarks>For <see cref="ConcurrentQueue{T}"/>, this operation will always add the object to the
        /// end of the <see cref="ConcurrentQueue{T}"/>
        /// and return true.</remarks>
        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            Enqueue(item);
            return true;
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
        /// <remarks>For <see cref="ConcurrentQueue{T}"/>, this operation will attempt to remove the object
        /// from the beginning of the <see cref="ConcurrentQueue{T}"/>.
        /// </remarks>
        bool IProducerConsumerCollection<T>.TryTake(out T item)
        {
            return TryDequeue(out item);
        }

        /// <summary>
        /// ��ȡһ��ָʾ System.Collections.Concurrent.ConcurrentQueue<T> �Ƿ�Ϊ�յ�ֵ��
        /// </summary>
        /// <value>��� System.Collections.Concurrent.ConcurrentQueue<T> Ϊ�գ���Ϊ true������Ϊ false��</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of this property is recommended
        /// rather than retrieving the number of items from the <see cref="Count"/> property and comparing it
        /// to 0.  However, as this collection is intended to be accessed concurrently, it may be the case
        /// that another thread will modify the collection after <see cref="IsEmpty"/> returns, thus invalidating
        /// the result.
        /// </remarks>
        public bool IsEmpty
        {
            get
            {
                Segment head = m_head;
                if (!head.IsEmpty)
                    //fast route 1:
                    //�����ǰhead��Ϊ�գ����ʾ���в�Ϊ��
                    return false;
                else if (head.Next == null)
                    //fast route 2:
                    //�����ǰhead�ǿղ������һ��segmentҲ�ǿգ����ʾ����Ϊ��
                    return true;
                else
                //slow route:
                //��ǰhead�ǿգ��������һ��segment��Ϊ�գ�����ζ�������߳����������µ�segment
                {
                    SpinWait spin = new SpinWait();
                    while (head.IsEmpty)//ѭ���ȴ��ж�
                    {
                        if (head.Next == null)
                            return true;

                        spin.SpinOnce();
                        head = m_head;
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// �� System.Collections.Concurrent.ConcurrentQueue<T> �д洢��Ԫ�ظ��Ƶ��������С�
        /// </summary>
        /// <returns>һ�������飬���а����� System.Collections.Concurrent.ConcurrentQueue<T> ���Ƶ�Ԫ�صĿ��ա�</returns>
        public T[] ToArray()
        {
            return ToList().ToArray();
        }

        /// <summary>
        /// ����ConcurrentQueue{T}Ԫ�ص�һ���µ�List{T}
        /// </summary>
        /// <returns>A new <see cref="T:System.Collections.Generic.List{T}"/> containing a snapshot of
        /// elements copied from the <see cref="ConcurrentQueue{T}"/>.</returns>
        private List<T> ToList()
        {
            // ������Ϊ���ղ������Ŀ���ڿ��շ���֮ǰ���ӱ��뷢�������ͬʱ����list������Ϻ��Լ����뷢����
            // ֻ��ͨ�����ַ�������segment.TryRemove()��ʱ�򣬼��m_numSnapshotTakers�Ƿ����0��������������������
            Interlocked.Increment(ref m_numSnapshotTakers);

            List<T> list = new List<T>();
            try
            {
                //�ڻ����д洢head �� tail λ��
                Segment head, tail;
                int headLow, tailHigh;
                GetHeadTailPositions(out head, out tail, out headLow, out tailHigh);

                if (head == tail)//��ʾhead �� tail segment����ͬ�ģ���ֻ��һ��segment
                {
                    head.AddToList(list, headLow, tailHigh);
                }
                else// �ж��segment
                {
                    head.AddToList(list, headLow, SEGMENT_SIZE - 1);// �Ƚ���һsegment����
                    Segment curr = head.Next;// ��ȡ��һ��segment
                    while (curr != tail)//����ѭ������list ֱ��β��segment
                    {
                        curr.AddToList(list, 0, SEGMENT_SIZE - 1);
                        curr = curr.Next;
                    }
                    //���βsegment
                    tail.AddToList(list, 0, tailHigh);
                }
            }
            finally
            {
                // �ڸ�����Ϻ��Լ����뷢��
                Interlocked.Decrement(ref m_numSnapshotTakers);
            }
            return list;
        }

        /// <summary>
        /// �洢��ǰhead��tailλ��
        /// </summary>
        /// <param name="head">return the head segment</param>
        /// <param name="tail">return the tail segment</param>
        /// <param name="headLow">return the head offset, value range [0, SEGMENT_SIZE]��head segment������</param>
        /// <param name="tailHigh">return the tail offset, value range [-1, SEGMENT_SIZE-1]��tail segment�е�����</param>
        private void GetHeadTailPositions(out Segment head, out Segment tail,
            out int headLow, out int tailHigh)
        {
            head = m_head;
            tail = m_tail;
            headLow = head.Low;
            tailHigh = tail.High;
            SpinWait spin = new SpinWait();

            //ֱ�����۲��ֵ���ȶ��ĺ����Եģ����ǽ�ѭ��
            //�Ᵽ֤ͨ�������������������Ĭ�ϵ�
            while (
                //if head and tail changed, retry
                head != m_head || tail != m_tail
                //if low and high pointers, retry
                || headLow != head.Low || tailHigh != tail.High
                //if head jumps ahead of tail because of concurrent grow and dequeue, retry
                || head.m_index > tail.m_index)
            {
                spin.SpinOnce();
                head = m_head;
                tail = m_tail;
                headLow = head.Low;
                tailHigh = tail.High;
            }
        }


        /// <summary>
        /// ��ȡ System.Collections.Concurrent.ConcurrentQueue<T> �а�����Ԫ������
        /// </summary>
        /// <value>System.Collections.Concurrent.ConcurrentQueue<T> �а�����Ԫ�ظ�����</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than retrieving the number of items from the <see cref="Count"/>
        /// property and comparing it to 0.
        /// </remarks>
        public int Count
        {
            get
            {
                //store head and tail positions in buffer, 
                Segment head, tail;
                int headLow, tailHigh;
                GetHeadTailPositions(out head, out tail, out headLow, out tailHigh);

                if (head == tail)//ֻ��һ��segment
                {
                    return tailHigh - headLow + 1;
                }

                //head segment ����
                int count = SEGMENT_SIZE - headLow;

                //�м�segment�����еĶ�������
                //We don't deal with overflow to be consistent with the behavior of generic types in CLR.
                count += SEGMENT_SIZE * ((int)(tail.m_index - head.m_index - 1));

                //tail segment ����
                count += tailHigh + 1;

                return count;
            }
        }


        /// <summary>
        /// Copies the <see cref="ConcurrentQueue{T}"/> elements to an existing one-dimensional <see
        /// cref="T:System.Array">Array</see>, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array">Array</see> that is the
        /// destination of the elements copied from the
        /// <see cref="ConcurrentQueue{T}"/>. The <see cref="T:System.Array">Array</see> must have zero-based
        /// indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in
        /// Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// zero.</exception>
        /// <exception cref="ArgumentException"><paramref name="index"/> is equal to or greater than the
        /// length of the <paramref name="array"/>
        /// -or- The number of elements in the source <see cref="ConcurrentQueue{T}"/> is greater than the
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
            ToList().CopyTo(array, index);
        }


        /// <summary>
        /// Returns an enumerator that iterates through the <see
        /// cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <returns>An enumerator for the contents of the <see
        /// cref="ConcurrentQueue{T}"/>.</returns>
        /// <remarks>
        /// The enumeration represents a moment-in-time snapshot of the contents
        /// of the queue.  It does not reflect any updates to the collection after 
        /// <see cref="GetEnumerator"/> was called.  The enumerator is safe to use
        /// concurrently with reads from and writes to the queue.
        /// </remarks>
        public IEnumerator<T> GetEnumerator()
        {
            // Increments the number of active snapshot takers. This increment must happen before the snapshot is 
            // taken. At the same time, Decrement must happen after the enumeration is over. Only in this way, can it
            // eliminate race condition when Segment.TryRemove() checks whether m_numSnapshotTakers == 0. 
            Interlocked.Increment(ref m_numSnapshotTakers);

            // Takes a snapshot of the queue. 
            // A design flaw here: if a Thread.Abort() happens, we cannot decrement m_numSnapshotTakers. But we cannot 
            // wrap the following with a try/finally block, otherwise the decrement will happen before the yield return 
            // statements in the GetEnumerator (head, tail, headLow, tailHigh) method.           
            Segment head, tail;
            int headLow, tailHigh;
            GetHeadTailPositions(out head, out tail, out headLow, out tailHigh);

            //If we put yield-return here, the iterator will be lazily evaluated. As a result a snapshot of
            // the queue is not taken when GetEnumerator is initialized but when MoveNext() is first called.
            // This is inconsistent with existing generic collections. In order to prevent it, we capture the 
            // value of m_head in a buffer and call out to a helper method.
            //The old way of doing this was to return the ToList().GetEnumerator(), but ToList() was an 
            // unnecessary perfomance hit.
            return GetEnumerator(head, tail, headLow, tailHigh);
        }

        /// <summary>
        /// Helper method of GetEnumerator to seperate out yield return statement, and prevent lazy evaluation. 
        /// </summary>
        private IEnumerator<T> GetEnumerator(Segment head, Segment tail, int headLow, int tailHigh)
        {
            try
            {
                SpinWait spin = new SpinWait();

                if (head == tail)
                {
                    for (int i = headLow; i <= tailHigh; i++)
                    {
                        // ���λ��ͨ����Ӳ���������������ֵȴδ��д�룬��ֵ��Ч֮ǰһֱ����
                        spin.Reset();
                        while (!head.m_state[i].m_value)
                        {
                            spin.SpinOnce();
                        }
                        yield return head.m_array[i];//����������������
                    }
                }
                else
                {
                    //��head segment �еĵ�����
                    for (int i = headLow; i < SEGMENT_SIZE; i++)
                    {
                        // If the position is reserved by an Enqueue operation, but the value is not written into,
                        // spin until the value is available.
                        spin.Reset();
                        while (!head.m_state[i].m_value)
                        {
                            spin.SpinOnce();
                        }
                        yield return head.m_array[i];
                    }
                    //���м�segment��
                    Segment curr = head.Next;
                    while (curr != tail)
                    {
                        for (int i = 0; i < SEGMENT_SIZE; i++)
                        {
                            // If the position is reserved by an Enqueue operation, but the value is not written into,
                            // spin until the value is available.
                            spin.Reset();
                            while (!curr.m_state[i].m_value)
                            {
                                spin.SpinOnce();
                            }
                            yield return curr.m_array[i];
                        }
                        curr = curr.Next;
                    }

                    //��tail segment�еĵ�����
                    for (int i = 0; i <= tailHigh; i++)
                    {
                        // If the position is reserved by an Enqueue operation, but the value is not written into,
                        // spin until the value is available.
                        spin.Reset();
                        while (!tail.m_state[i].m_value)
                        {
                            spin.SpinOnce();
                        }
                        yield return tail.m_array[i];
                    }
                }
            }
            finally
            {
                // ������һ�������ڵ�����Ϻ�
                Interlocked.Decrement(ref m_numSnapshotTakers);
            }
        }

        /// <summary>
        /// ��������ӵ� System.Collections.Concurrent.ConcurrentQueue<T> �Ľ�β����
        /// </summary>
        /// <param name="item">Ҫ��ӵ� System.Collections.Concurrent.ConcurrentQueue<T> �Ľ�β���Ķ���
        /// ��ֵ�����������Ϳ����ǿ�����
        /// </param>
        public void Enqueue(T item)
        {
            SpinWait spin = new SpinWait();
            while (true)//�ȴ������̴߳�������βsegment.TryAppend()�������
            {
                Segment tail = m_tail;
                if (tail.TryAppend(item))
                    return;
                spin.SpinOnce();
            }
        }


        /// <summary>
        /// �����Ƴ�������λ�ڲ������п�ͷ���Ķ���
        /// </summary>
        /// <param name="result">
        /// �˷�������ʱ����������ɹ����� result �������Ƴ��Ķ��� ���û�пɹ��Ƴ��Ķ�����ָ����ֵ��
        /// </param>
        /// <returns>����ɹ��� System.Collections.Concurrent.ConcurrentQueue<T> ��ͷ���Ƴ���������Ԫ�أ���Ϊ true������Ϊfalse.</returns>
        public bool TryDequeue(out T result)
        {
            while (!IsEmpty)//�ж϶����Ƿ�Ϊ��
            {
                Segment head = m_head;
                if (head.TryRemove(out result))//�Ƴ�Ԫ��
                    return true;
                //��IsEmpty��ʵ�����������������ǲ���Ҫ��whileѭ������
            }
            result = default(T);
            return false;
        }

        /// <summary>
        /// ���Է��� System.Collections.Concurrent.ConcurrentQueue<T> ��ͷ���Ķ��󵫲������Ƴ���.
        /// </summary>
        /// <param name="result">�˷�������ʱ��result ���� System.Collections.Concurrent.ConcurrentQueue<T> ��ʼ���Ķ���
        /// �������ʧ�ܣ������δָ����ֵ��</param>
        /// <returns>����ɹ������˶�����Ϊ true������Ϊ false��</returns>
        public bool TryPeek(out T result)
        {
            Interlocked.Increment(ref m_numSnapshotTakers);

            while (!IsEmpty)
            {
                Segment head = m_head;
                if (head.TryPeek(out result))
                {
                    Interlocked.Decrement(ref m_numSnapshotTakers);
                    return true;
                }
                //��IsEmpty��ʵ�����������������ǲ���Ҫ��whileѭ������
            }
            result = default(T);
            Interlocked.Decrement(ref m_numSnapshotTakers);
            return false;
        }


        /// <summary>
        /// ΪConcurrentQueue��˽����
        /// һ��������һ��С����������б�ÿ���ڵ㱻����һ��segment��
        /// һ��segment����һ�����飬һ��ָ����һ��segment����m_low��m_high Ŀ¼�ڵ�
        /// �������е�һ�������һ��Ԫ��
        /// </summary>
        private class Segment
        {
            //we define two volatile arrays: m_array and m_state. Note that the accesses to the array items 
            //do not get volatile treatment. But we don't need to worry about loading adjacent elements or 
            //store/load on adjacent elements would suffer reordering. 
            // - Two stores:  these are at risk, but CLRv2 memory model guarantees store-release hence we are safe.
            // - Two loads: because one item from two volatile arrays are accessed, the loads of the array references
            //          are sufficient to prevent reordering of the loads of the elements.
            internal volatile T[] m_array;// �ڲ�����

            // Ϊ��m_arrray�е�ÿһ����Ŀ����m_state����Ӧ����Ŀ�������λ���Ƿ����һ����Чֵ��m_state���������ʼ��Ϊfalse
            internal volatile VolatileBool[] m_state;

            //pointer to the next segment. null if the current segment is the last segment
            // ָ����һ��segment��ָ�룬�����ǰsegment�����һ��segment����ô�����ǿ�
            private volatile Segment m_next;

            //We use this zero based index to track how many segments have been created for the queue, and
            //to compute how many active segments are there currently. 
            // * The number of currently active segments is : m_tail.m_index - m_head.m_index + 1;
            // * m_index is incremented with every Segment.Grow operation. We use Int64 type, and we can safely 
            //   assume that it never overflows. To overflow, we need to do 2^63 increments, even at a rate of 4 
            //   billion (2^32) increments per second, it takes 2^31 seconds, which is about 64 years.
            internal readonly long m_index;

            //indices of where the first and last valid values
            // - m_low points to the position of the next element to pop from this segment, range [0, infinity)
            //      m_low >= SEGMENT_SIZE implies the segment is disposable
            // - m_high points to the position of the latest pushed element, range [-1, infinity)
            //      m_high == -1 implies the segment is new and empty
            //      m_high >= SEGMENT_SIZE-1 means this segment is ready to grow. 
            //        and the thread who sets m_high to SEGMENT_SIZE-1 is responsible to grow the segment
            // - Math.Min(m_low, SEGMENT_SIZE) > Math.Min(m_high, SEGMENT_SIZE-1) implies segment is empty
            // - initially m_low =0 and m_high=-1;
            private volatile int m_low;
            private volatile int m_high;

            private volatile ConcurrentQueue<T> m_source;

            /// <summary>
            /// ͨ���涨�������������ͳ�ʼ��һ��segment
            /// </summary>
            internal Segment(long index, ConcurrentQueue<T> source)
            {
                m_array = new T[SEGMENT_SIZE];
                m_state = new VolatileBool[SEGMENT_SIZE]; //ȫ����ʼ��Ϊfalse
                m_high = -1;
                Contract.Assert(index >= 0);
                m_index = index;
                m_source = source;
            }

            /// <summary>
            /// ������һ��segment
            /// </summary>
            internal Segment Next
            {
                get { return m_next; }
            }


            /// <summary>
            /// �����ǰsegment�ǿգ�û���κ���ЧԪ��ȥ��ӣ����򷵻�true
            /// ���򷵻�false
            /// </summary>
            internal bool IsEmpty
            {
                get { return (Low > High); }
            }

            /// <summary>
            /// ���һ��Ԫ�ص���ǰsegment��β����ר�ű�ConcurrentQueue.InitializedFromCollection����
            /// InitializedFromCollection �Ǳ�֤û������Խ�磬��û�����ۣ�����ȫ�أ�
            /// </summary>
            /// <param name="value"></param>
            internal void UnsafeAdd(T value)
            {
                Contract.Assert(m_high < SEGMENT_SIZE - 1);//�ж��Ƿ�Խ��
                m_high++;
                m_array[m_high] = value;
                m_state[m_high].m_value = true;
            }

            /// <summary>
            /// ����һ���µ�segment���Ҹ��ӵ���ǰsegment�ϣ����ǲ�����m_tail�ڵ�
            /// ר�ű�ConcurrentQueue.InitializedFromCollection����
            /// InitializedFromCollection �Ǳ�֤û������Խ�磬��û�����ۣ�����ȫ�أ�
            /// </summary>
            /// <returns>������segment������</returns>
            internal Segment UnsafeGrow()
            {
                Contract.Assert(m_high >= SEGMENT_SIZE - 1);
                Segment newSegment = new Segment(m_index + 1, m_source); //m_index��int64��long�����ǲ���Ҫ����Խ��
                m_next = newSegment;//����ǰsegment��next����Ϊ��ʼ����segment�����ǲ�����m_tail
                return newSegment;
            }

            /// <summary>
            /// ����һ���µ�segment���Ҹ��ӵ���ǰsegment�ϣ�������m_tailָ��
            /// ���޾���ʱ������������
            /// </summary>
            internal void Grow()
            {
                //no CAS is needed, since there is no contention (other threads are blocked, busy waiting)
                Segment newSegment = new Segment(m_index + 1, m_source);  //m_index is Int64, we don't need to worry about overflow
                m_next = newSegment;
                Contract.Assert(m_source.m_tail == this);//�жϵ�ǰsegment�Ƿ���β�ڵ�
                m_source.m_tail = m_next;
            }


            /// <summary>
            /// ���Խ�һ��Ԫ�ظ��ӵ����segment��β��
            /// </summary>
            /// <param name="value">���ӵ�Ԫ��</param>
            /// <param name="tail">The tail.</param>
            /// <returns>���Ԫ�ظ��ӳɹ����򷵻�true�������ǰsegment���ģ��򷵻�false</returns>
            /// <remarks>if appending the specified element succeeds, and after which the segment is full, 
            /// then grow the segment</remarks>
            internal bool TryAppend(T value)
            {
                //���ټ��m_high�Ƿ�Խ�磬���Խ�磬������
                if (m_high >= SEGMENT_SIZE - 1)
                {
                    return false;
                }

                //�������ǽ�ʹ��һ��CASȥ����m_high�����Ҵ洢�ṹ��newhigh�С�
                //�����������segment���ж��ٸ�����ڵ��Լ��ж����߳�ͬʱ�������룬���ص�"newhigh"����
                // 1) < SEGMENT_SIZE - 1 : ���������segment�в���һ���ڵ㣬���Ҳ������һ����
                // 2) == SEGMENT_SIZE - 1 : ���ǽ��������һ���ڵ㣬����ֵ��������segment
                // 3) > SEGMENT_SIZE - 1 : ����ʧ���������segment�д洢һ���ڵ㣬���ǽ���Queue.Enqueue����ʧ�ܣ�������������һ��segment�г���

                int newhigh = SEGMENT_SIZE; //��ʼ��ֵȥ��ֹԽ��

                //We need do Interlocked.Increment and value/state update in a finally block to ensure that they run
                //without interuption. This is to prevent anything from happening between them, and another dequeue
                //thread maybe spinning forever to wait for m_state[] to be true;
                try
                { }
                finally
                {
                    newhigh = Interlocked.Increment(ref m_high);
                    if (newhigh <= SEGMENT_SIZE - 1)
                    {
                        m_array[newhigh] = value;
                        m_state[newhigh].m_value = true;
                    }

                    //if this thread takes up the last slot in the segment, then this thread is responsible
                    //to grow a new segment. Calling Grow must be in the finally block too for reliability reason:
                    //if thread abort during Grow, other threads will be left busy spinning forever.
                    if (newhigh == SEGMENT_SIZE - 1)
                    {
                        Grow();
                    }
                }

                //��� newhigh <= SEGMENT_SIZE-1, ����ʾ��ǰ�̳߳ɹ�����һ��
                return newhigh <= SEGMENT_SIZE - 1;
            }


            /// <summary>
            /// ���Դӵ�ǰsegment���ײ��Ƴ�һ��Ԫ��
            /// </summary>
            /// <param name="result">�Ƴ����</param>
            /// <param name="head">The head.</param>
            /// <returns>�����ǰsegmentΪ�գ��򷵻�Ϊfalse</returns>
            internal bool TryRemove(out T result)
            {
                SpinWait spin = new SpinWait();
                int lowLocal = Low, highLocal = High;
                while (lowLocal <= highLocal)
                {
                    //���Ը���m_low����m_low����ƶ�һλ
                    if (Interlocked.CompareExchange(ref m_low, lowLocal + 1, lowLocal) == lowLocal)
                    {
                        //if the specified value is not available (this spot is taken by a push operation,
                        // but the value is not written into yet), then spin
                        SpinWait spinLocal = new SpinWait();
                        while (!m_state[lowLocal].m_value)// �����ǰλ�õ�m_stateΪfalse�����������һ�Σ�ֱ��Ϊtrue������һ�㲻��������ѭ��
                        {
                            spinLocal.SpinOnce();
                        }
                        result = m_array[lowLocal];//���н������

                        // If there is no other thread taking snapshot (GetEnumerator(), ToList(), etc), reset the deleted entry to null.
                        // It is ok if after this conditional check m_numSnapshotTakers becomes > 0, because new snapshots won't include 
                        // the deleted entry at m_array[lowLocal]. 
                        if (m_source.m_numSnapshotTakers <= 0)
                        {
                            m_array[lowLocal] = default(T); //release the reference to the object. 
                        }

                        //�����ǰ�߳�����m_low����SEGMENT_SIZE,�Ǳ�ʾ��ǰsegment�Ѿ����������ˣ�
                        //Ȼ������߳̽�������ȥע�����segment��Ȼ������m_head
                        if (lowLocal + 1 >= SEGMENT_SIZE)
                        {
                            //  Invariant: we only dispose the current m_head, not any other segment
                            //  In usual situation, disposing a segment is simply seting m_head to m_head.m_next
                            //  But there is one special case, where m_head and m_tail points to the same and ONLY
                            //segment of the queue: Another thread A is doing Enqueue and finds that it needs to grow,
                            //while the *current* thread is doing *this* Dequeue operation, and finds that it needs to 
                            //dispose the current (and ONLY) segment. Then we need to wait till thread A finishes its 
                            //Grow operation, this is the reason of having the following while loop
                            spinLocal = new SpinWait();//���̲߳����Ĵ���
                            while (m_next == null)
                            {
                                spinLocal.SpinOnce();
                            }
                            Contract.Assert(m_source.m_head == this);
                            m_source.m_head = m_next;
                        }
                        return true;
                    }
                    else
                    {
                        //CAS���ھ���ʧ�ܣ���������������
                        spin.SpinOnce();
                        lowLocal = Low; highLocal = High;
                    }
                }//end of while
                result = default(T);//segmentΪ�գ�����һ��Ĭ�϶����ֵ������false
                return false;
            }

            /// <summary>
            /// ����peek��ǰ��segment
            /// </summary>
            /// <param name="result">holds the return value of the element at the head position, 
            /// value set to default(T) if there is no such an element</param>
            /// <returns>true if there are elements in the current segment, false otherwise</returns>
            internal bool TryPeek(out T result)
            {
                result = default(T);
                int lowLocal = Low;
                if (lowLocal > High)
                    return false;
                SpinWait spin = new SpinWait();
                while (!m_state[lowLocal].m_value)
                {
                    spin.SpinOnce();
                }
                result = m_array[lowLocal];
                return true;
            }

            /// <summary>
            /// ����ǰsegment�Ĳ��ֻ���ȫ����ӵ�һ��List��
            /// </summary>
            /// <param name="list">the list to which to add</param>
            /// <param name="start">the start position</param>
            /// <param name="end">the end position</param>
            internal void AddToList(List<T> list, int start, int end)
            {
                for (int i = start; i <= end; i++)
                {
                    SpinWait spin = new SpinWait();
                    while (!m_state[i].m_value)
                    {
                        spin.SpinOnce();
                    }
                    list.Add(m_array[i]);
                }
            }

            /// <summary>
            /// ���ص�ǰsegment����λ�ã�ֵ�ķ�Χ��[0,SEGMENT_SIZE],
            /// �������SEGMENT_SIZE,����ζ�����segment�Ǻľ��ģ�Ҳ���ǿյ�
            /// </summary>
            internal int Low
            {
                get
                {
                    return Math.Min(m_low, SEGMENT_SIZE);
                }
            }

            /// <summary>
            /// ���ص�ǰsegmentβ���ĺ���λ�ã�ֵ�ķ�Χ��[-1,SEGMENT-1].
            /// ������-1ʱ������ζ������һ���µ�segment�����һ�û��Ԫ��
            /// </summary>
            internal int High
            {
                get
                {
                    //���m_high > SEGMENT_SIZE,����ʾ���Ѿ�Խ����Χ������Ӧ�÷���SEGMENT_SIZE-1��Ϊ�����λ��
                    return Math.Min(m_high, SEGMENT_SIZE - 1);
                }
            }

        }
    }//end of class Segment

    /// <summary>
    /// һ��Ϊ�˰�װvolatile(���ȶ���) bool�Ľṹ�壬��ע��ṹ�����Լ����ƽ�����volatile
    /// Ϊ��������ӽ����������ȶ��Ĳ���volatileBool1 = volatileBool2 jit�����ƽṹ�岢�ҽ�����volatile
    /// </summary>
    struct VolatileBool
    {
        public VolatileBool(bool value)
        {
            m_value = value;
        }
        public volatile bool m_value;
    }
}
