// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
//
// <OWNER>[....]</OWNER>
/*============================================================
**
** Class:   ConcurrentDictionary
**
**
** Purpose: A scalable dictionary for concurrent access
**
**
===========================================================*/

// If CDS_COMPILE_JUST_THIS symbol is defined, the ConcurrentDictionary.cs file compiles separately,
// with no dependencies other than .NET Framework 3.5.

//#define CDS_COMPILE_JUST_THIS

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Collections.ObjectModel;

#if !CDS_COMPILE_JUST_THIS
using System.Diagnostics.Contracts;
#endif

using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Concurrent
{

    /// <summary>
    /// ��ʾ���ɶ���߳�ͬʱ���ʵļ�/ֵ�Ե��̰߳�ȫ����
    /// </summary>
    /// <typeparam name="TKey">�ֵ��еļ�������</typeparam>
    /// <typeparam name="TValue">�ֵ��е�ֵ������</typeparam>
    /// <remarks>
    /// All public and protected members of <see cref="ConcurrentDictionary{TKey,TValue}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
#if !FEATURE_CORECLR
    [Serializable]
#endif
    [ComVisible(false)]
    [DebuggerTypeProxy(typeof(Mscorlib_DictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    [HostProtection(Synchronization = true, ExternalThreading = true)]
    public class ConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>
    {
        /// <summary>
        /// Tables that hold the internal state of the ConcurrentDictionary
        /// Wrapping the three tables in a single object allows us to atomically
        /// replace all tables at once.
        /// Tablesά��ConcurrentDictionary���ڲ�״̬
        /// ��һ����һ����������������ԭ�Ӳ����滻���е�tables   ��װ����tables
        /// </summary>
        private class Tables
        {
            internal readonly Node[] m_buckets; // һ����һ����listΪÿһ��bucket
            internal readonly object[] m_locks; // һϵ������ÿ��������table��һ��
            internal volatile int[] m_countPerLock; // The number of elements guarded by each lock.
            internal readonly IEqualityComparer<TKey> m_comparer; // ����ȱȽ���

            /// <summary>
            /// ���캯��
            /// </summary>
            /// <param name="buckets">�ڵ�����</param>
            /// <param name="locks">������</param>
            /// <param name="countPerLock">ÿ������Ŀ</param>
            /// <param name="comparer">�Ƚ���</param>
            internal Tables(Node[] buckets, object[] locks, int[] countPerLock, IEqualityComparer<TKey> comparer)
            {
                m_buckets = buckets;
                m_locks = locks;
                m_countPerLock = countPerLock;
                m_comparer = comparer;
            }
        }
#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private volatile Tables m_tables; // Internal tables of the dictionary       
        // NOTE: this is only used for compat reasons to serialize the comparer.
        // This should not be accessed from anywhere else outside of the serialization methods.
        internal IEqualityComparer<TKey> m_comparer; 
#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private readonly bool m_growLockArray; // Whether to dynamically increase the size of the striped lock

        // How many times we resized becaused of collisions. 
        // This is used to make sure we don't resize the dictionary because of multi-threaded Add() calls
        // that generate collisions. Whenever a GrowTable() should be the only place that changes this
#if !FEATURE_CORECLR
        // The field should be have been marked as NonSerialized but because we shipped it without that attribute in 4.5.1.
        // we can't add it back without breaking compat. To maximize compat we are going to keep the OptionalField attribute 
        // This will prevent��Ԥ���� cases������� where the field was not serialized.
        [OptionalField]
#endif
        private int m_keyRehashCount;//�����´�����Ŀ

#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private int m_budget; // The maximum number of elements per lock before a resize operation is triggered ��һ�����ô�С�Ĳ���ǰ��ÿ����Ԫ�������������������������

#if !FEATURE_CORECLR // These fields are not used in CoreCLR
        private KeyValuePair<TKey, TValue>[] m_serializationArray; // �����������л�����

        private int m_serializationConcurrencyLevel; // ���ڴ洢�����л������еĲ��еȼ�

        private int m_serializationCapacity; // ���ڴ洢���л�����
#endif
        // The default concurrency level is DEFAULT_CONCURRENCY_MULTIPLIER * #CPUs. The higher the
        // DEFAULT_CONCURRENCY_MULTIPLIER, the more concurrent writes can take place without interference
        // and blocking, but also the more expensive operations that require all locks become (e.g. table
        // resizing, ToArray, Count, etc). According to brief benchmarks that we ran, 4 seems like a good
        // compromise.
        private const int DEFAULT_CONCURRENCY_MULTIPLIER = 4;// Ĭ�ϲ�����

        // The default capacity, i.e. the initial # of buckets. When choosing this value, we are making
        // a trade-off between the size of a very small dictionary, and the number of resizes when
        // constructing a large dictionary. Also, the capacity should not be divisible by a small prime.
        private const int DEFAULT_CAPACITY = 31;//Ĭ������

        // The maximum size of the striped lock that will not be exceeded when locks are automatically
        // added as the dictionary grows. However, the user is allowed to exceed this limit by passing
        // a concurrency level larger than MAX_LOCK_NUMBER into the constructor.
        private const int MAX_LOCK_NUMBER = 1024;//�������Ŀ

        // Whether TValue is a type that can be written atomically (i.e., with no danger of torn reads)
        private static readonly bool s_isValueWriteAtomic = IsValueWriteAtomic();//ֵ�Ƿ���ԭ��д��


        /// <summary>
        /// ȷ����ʽTValue�Ƿ���ԭ��д��
        /// </summary>
        private static bool IsValueWriteAtomic()
        {
            Type valueType = typeof(TValue);//��ȡTValue�ĸ�ʽ

            //
            // Section 12.6.6 of ECMA CLI explains which types can be read and written atomically without
            // the risk of tearing.
            //
            // See http://www.ecma-international.org/publications/files/ECMA-ST/Ecma-335.pdf
            //
            bool isAtomic =  // �ж��Ƿ���ԭ�Ӹ�ʽ
                (valueType.IsClass)
                || valueType == typeof(Boolean)
                || valueType == typeof(Char)
                || valueType == typeof(Byte)
                || valueType == typeof(SByte)
                || valueType == typeof(Int16)
                || valueType == typeof(UInt16)
                || valueType == typeof(Int32)
                || valueType == typeof(UInt32)
                || valueType == typeof(Single);

            if (!isAtomic && IntPtr.Size == 8)//������
            {
                isAtomic |= valueType == typeof(Double) || valueType == typeof(Int64);
            }

            return isAtomic;
        }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��Ϊ�գ�
        /// ����Ĭ�ϵĲ��������Ĭ�ϵĳ�ʼ��������Ϊ������ʹ��Ĭ�ϱȽ�����
        /// </summary>
        public ConcurrentDictionary() : this(DefaultConcurrencyLevel, DEFAULT_CAPACITY, true, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��Ϊ�գ�����ָ���Ĳ����������������Ϊ������ʹ��Ĭ�ϱȽ�����
        /// </summary>
        /// <param name="concurrencyLevel">��ͬʱ���� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> ���̵߳Ĺ���������</param>
        /// <param name="capacity">System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �ɰ����ĳ�ʼԪ������</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/>С��1</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="capacity"/>С��0</exception>
        public ConcurrentDictionary(int concurrencyLevel, int capacity) : this(concurrencyLevel, capacity, false, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��������ָ����
        /// System.Collections.Generic.IEnumerable<T> �и��Ƶ�Ԫ�أ�����Ĭ�ϵĲ��������Ĭ�ϵĳ�ʼ��������Ϊ������ʹ��Ĭ�ϱȽ�����
        /// </summary>
        /// <param name="collection">System.Collections.Generic.IEnumerable<T>��
        /// ��Ԫ�ر����Ƶ��µ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>�С�</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="collection"/> contains one or more
        /// duplicate keys.</exception>
        public ConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection) : this(collection, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��Ϊ�գ�����Ĭ�ϵĲ����������������ʹ��ָ����
        ///     System.Collections.Generic.IEqualityComparer<T>��
        /// </summary>
        /// <param name="comparer">�ڱȽϼ�ʱҪʹ�õ���ȱȽ�ʵ�֡�</param>
        /// <exception cref="T:System.ArgumentNullException">comparer Ϊ null��</exception>
        public ConcurrentDictionary(IEqualityComparer<TKey> comparer) : this(DefaultConcurrencyLevel, DEFAULT_CAPACITY, true, comparer) { }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��������ָ����
        ///     System.Collections.IEnumerable �и��Ƶ�Ԫ�أ�����Ĭ�ϵĲ��������Ĭ�ϵĳ�ʼ��������ʹ��ָ���� System.Collections.Generic.IEqualityComparer<T>��
        /// </summary>
        /// <param name="collection">System.Collections.Generic.IEnumerable<T>��
        /// ��Ԫ�ر����Ƶ��µ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>��</param>
        /// <param name="comparer">�ڱȽϼ�ʱҪʹ�õ� System.Collections.Generic.IEqualityComparer<T> ʵ�֡�</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference
        /// (Nothing in Visual Basic). -or-
        /// <paramref name="comparer"/> is a null reference (Nothing in Visual Basic).
        /// </exception>
        public ConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            : this(comparer)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            InitializeFromCollection(collection);
        }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��������ָ����
        ///     System.Collections.IEnumerable �и��Ƶ�Ԫ�ز�ʹ��ָ���� System.Collections.Generic.IEqualityComparer<T>��
        /// </summary>
        /// <param name="concurrencyLevel">��ͬʱ���� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>
        /// ���̵߳Ĺ���������</param>
        /// <param name="collection">System.Collections.Generic.IEnumerable<T>��
        /// ��Ԫ�ر����Ƶ��µ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue></param>
        /// <param name="comparer">�ڱȽϼ�ʱҪʹ�õ� System.Collections.Generic.IEqualityComparer<T> ʵ�֡�</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="collection"/> is a null reference (Nothing in Visual Basic).
        /// -or-
        /// <paramref name="comparer"/> is a null reference (Nothing in Visual Basic).
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="concurrencyLevel"/> is less than 1.
        /// </exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
        public ConcurrentDictionary(
            int concurrencyLevel, IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, DEFAULT_CAPACITY, false, comparer)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            if (comparer == null) throw new ArgumentNullException("comparer");

            InitializeFromCollection(collection);
        }

        private void InitializeFromCollection(IEnumerable<KeyValuePair<TKey,TValue>> collection)
        {
            TValue dummy;
            foreach (KeyValuePair<TKey, TValue> pair in collection)
            {
                if (pair.Key == null) throw new ArgumentNullException("key");
                if (!TryAddInternal(pair.Key, pair.Value, false, false, out dummy))
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_SourceContainsDuplicateKeys"));
                }
            }

            if (m_budget == 0)
            {
                m_budget = m_tables.m_buckets.Length / m_tables.m_locks.Length;
            }
        }

        /// <summary>
        /// ��ʼ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �����ʵ������ʵ��Ϊ�գ�����ָ���Ĳ��������ָ���ĳ�ʼ��������ʹ��ָ����
        ///     System.Collections.Generic.IEqualityComparer<T>��
        /// </summary>
        /// <param name="concurrencyLevel">��ͬʱ���� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> ���̵߳Ĺ���������</param>
        /// <param name="capacity"> System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �ɰ����ĳ�ʼԪ������</param>
        /// <param name="comparer">�ڱȽϼ�ʱҪʹ�õ� System.Collections.Generic.IEqualityComparer<T> ʵ�֡�</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="concurrencyLevel"/> is less than 1. -or-
        /// <paramref name="capacity"/> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="comparer"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        public ConcurrentDictionary(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, capacity, false, comparer)
        {
        }

        internal ConcurrentDictionary(int concurrencyLevel, int capacity, bool growLockArray, IEqualityComparer<TKey> comparer)
        {
            if (concurrencyLevel < 1)
            {
                throw new ArgumentOutOfRangeException("concurrencyLevel", GetResource("ConcurrentDictionary_ConcurrencyLevelMustBePositive"));
            }
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException("capacity", GetResource("ConcurrentDictionary_CapacityMustNotBeNegative"));
            }
            if (comparer == null) throw new ArgumentNullException("comparer");

            // The capacity should be at least as large as the concurrency level. Otherwise, we would have locks that don't guard
            // any buckets.
            // ����Ӧ�����ٺ�concurrencyLevelһ�������������ǲ������κ�Ͱ
            if (capacity < concurrencyLevel)
            {
                capacity = concurrencyLevel;
            }

            object[] locks = new object[concurrencyLevel];//��ʼ��locks����
            for (int i = 0; i < locks.Length; i++)
            {
                locks[i] = new object();
            }

            int[] countPerLock = new int[locks.Length];//ÿ������Ŀ
            Node[] buckets = new Node[capacity];//�ڵ�����
            m_tables = new Tables(buckets, locks, countPerLock, comparer);//��ʼ��tables�ṹ

            m_growLockArray = growLockArray;
            m_budget = buckets.Length / locks.Length;
        }


        /// <summary>
        /// ���Խ�ָ���ļ���ֵ��ӵ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>�С�
        /// </summary>
        /// <param name="key">Ҫ��ӵ�Ԫ�صļ���</param>
        /// <param name="value">Ҫ��ӵ�Ԫ�ص�ֵ�� �����������ͣ���ֵ����Ϊ null��</param>
        /// <returns>����ü�/ֵ���ѳɹ���ӵ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>����Ϊ
        ///     true������ü��Ѵ��ڣ���Ϊ false��</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/>Ϊ null��</exception>
        /// <exception cref="T:System.OverflowException">�ֵ��Ѱ��������Ŀ��Ԫ�� (System.Int32.MaxValue)��</exception>
        public bool TryAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            TValue dummy;
            return TryAddInternal(key, value, false, true, out dummy);
        }

        /// <summary>
        /// ȷ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �Ƿ����ָ���ļ���
        /// </summary>
        /// <param name="key">Ҫ�� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �ж�λ�ļ���</param>
        /// <returns>��� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> ��������ָ������Ԫ�أ���Ϊ
        ///     true������Ϊ false��</returns>
        /// <exception cref="T:System.ArgumentNullException">key Ϊ null��</exception>
        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue throwAwayValue;
            return TryGetValue(key, out throwAwayValue);
        }

        /// <summary>
        /// ���Դ� System.Collections.Concurrent.ConcurrentDictionary���Ƴ������ؾ���ָ������ֵ��
        /// </summary>
        /// <param name="key">Ҫ�Ƴ������ص�Ԫ�صļ���</param>
        /// <param name="value">W���˷�������ʱ���������� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>
        ///     ���Ƴ��Ķ������ key �����ڣ������ TValue ���͡�</param>
        /// <returns>����ѳɹ��Ƴ�������Ϊ true������Ϊ false��</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/>Ϊ��</exception>
        public bool TryRemove(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            return TryRemoveInternal(key, out value, false, default(TValue));
        }

        /// <summary>
        /// Removes the specified key from the dictionary if it exists and returns its associated value.
        /// If matchValue flag is set, the key will be removed only if is associated with a particular
        /// value.
        /// ������ڣ����Ƴ�ָ���ļ����ֵ��в����������������ֵ
        /// ���matchValue��־�����õģ������һ�������ֵ������������ᱻ�Ƴ�
        /// </summary>
        /// <param name="key">The key to search for and remove if it exists.</param>
        /// <param name="value">The variable into which the removed value, if found, is stored.</param>
        /// <param name="matchValue">Whether removal of the key is conditional on its value.</param>
        /// <param name="oldValue">The conditional value to compare against if <paramref name="matchValue"/> is true</param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private bool TryRemoveInternal(TKey key, out TValue value, bool matchValue, TValue oldValue)
        {
            while (true)
            {
                Tables tables = m_tables;

                IEqualityComparer<TKey> comparer = tables.m_comparer;

                int bucketNo, lockNo;
                GetBucketAndLockNo(comparer.GetHashCode(key), out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                lock (tables.m_locks[lockNo])
                {
                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurence.
                    if (tables != m_tables)
                    {
                        continue;
                    }

                    Node prev = null;
                    for (Node curr = tables.m_buckets[bucketNo]; curr != null; curr = curr.m_next)
                    {
                        Assert((prev == null && curr == tables.m_buckets[bucketNo]) || prev.m_next == curr);

                        if (comparer.Equals(curr.m_key, key))
                        {
                            if (matchValue)
                            {
                                bool valuesMatch = EqualityComparer<TValue>.Default.Equals(oldValue, curr.m_value);//����ɾ����oldValue��ͬ��ֵ������ͬ�Ļ�����ɾ��ʧ�ܡ�
                                if (!valuesMatch)
                                {
                                    value = default(TValue);
                                    return false;
                                }
                            }

                            if (prev == null)
                            {
                                Volatile.Write<Node>(ref tables.m_buckets[bucketNo], curr.m_next);
                            }
                            else
                            {
                                prev.m_next = curr.m_next;
                            }

                            value = curr.m_value;
                            tables.m_countPerLock[lockNo]--;
                            return true;
                        }
                        prev = curr;
                    }
                }

                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// ���Դ�ConcurrentDictionary ��ȡ��ָ���ļ�������ֵ��
        /// </summary>
        /// <param name="key">Ҫ��ȡ��ֵ�ļ���</param>
        /// <param name="value">���˷�������ʱ�������� System.Collections.Concurrent.ConcurrentDictionary
        ///     �о���ָ�����Ķ����������ʧ�ܣ������Ĭ��ֵ��</param>
        /// <returns>����� System.Collections.Concurrent.ConcurrentDictionary���ҵ��ü�����Ϊ
        ///     true������Ϊ false��</returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            int bucketNo, lockNoUnused;

            // We must capture the m_buckets field in a local variable. It is set to a new table on each table resize.
            Tables tables = m_tables;
            IEqualityComparer<TKey> comparer = tables.m_comparer;
            GetBucketAndLockNo(comparer.GetHashCode(key), out bucketNo, out lockNoUnused, tables.m_buckets.Length, tables.m_locks.Length);//����hashcode������ڵ�λ��

            // We can get away w/out a lock here.
            // Volatile.Read��֤��buckets[i]�ڼ���֮ǰ���ᱻ���ƶ�
            Node n = Volatile.Read<Node>(ref tables.m_buckets[bucketNo]);

            while (n != null)
            {
                if (comparer.Equals(n.m_key, key))//�������ͬ���򷵻�ֵ�����򣬼���Ѱ��
                {
                    value = n.m_value;
                    return true;
                }
                n = n.m_next;
            }

            value = default(TValue);//����Ĭ��ֵ
            return false;
        }

        /// <summary>
        /// ��ָ����������ֵ��ָ��ֵ���бȽϣ������ȣ����õ�����ֵ���¸ü���
        /// </summary>
        /// <param name="key">��ֵ���� comparisonValue ���бȽϲ��ҿ��ܱ��滻�ļ���</param>
        /// <param name="newValue">һ��ֵ�����ȽϽ�����ʱ�����滻����ָ�� key ��Ԫ�ص�ֵ��</param>
        /// <param name="comparisonValue">�����ָ�� key ��Ԫ�ص�ֵ���бȽϵ�ֵ��</param>
        /// <returns>������� key ��ֵ�� comparisonValue ������滻Ϊ newValue����Ϊ true������Ϊ false��</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null
        /// reference.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        public bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
        {
            if (key == null) throw new ArgumentNullException("key");

            IEqualityComparer<TValue> valueComparer = EqualityComparer<TValue>.Default;

            while (true)
            {
                int bucketNo;
                int lockNo;
                int hashcode;

                Tables tables = m_tables;
                IEqualityComparer<TKey> comparer = tables.m_comparer;

                hashcode = comparer.GetHashCode(key);
                GetBucketAndLockNo(hashcode, out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                lock (tables.m_locks[lockNo])
                {
                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurence.
                    if (tables != m_tables)
                    {
                        continue;
                    }

                    // Try to find this key in the bucket
                    Node prev = null;
                    for (Node node = tables.m_buckets[bucketNo]; node != null; node = node.m_next)
                    {
                        Assert((prev == null && node == tables.m_buckets[bucketNo]) || prev.m_next == node);
                        if (comparer.Equals(node.m_key, key))
                        {
                            if (valueComparer.Equals(node.m_value, comparisonValue))
                            {
                                if (s_isValueWriteAtomic)
                                {
                                    node.m_value = newValue;
                                }
                                else
                                {
                                    Node newNode = new Node(node.m_key, newValue, hashcode, node.m_next);

                                    if (prev == null)
                                    {
                                        tables.m_buckets[bucketNo] = newNode;
                                    }
                                    else
                                    {
                                        prev.m_next = newNode;
                                    }
                                }

                                return true;
                            }

                            return false;
                        }

                        prev = node;
                    }

                    //didn't find the key
                    return false;
                }
            }
        }

        /// <summary>
        /// �� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> ���Ƴ����еļ���ֵ��
        /// </summary>
        public void Clear()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);//����������

                // ��ԭm_tables��locks��comparer���и�ֵ��������������¹���
                Tables newTables = new Tables(new Node[DEFAULT_CAPACITY], m_tables.m_locks, new int[m_tables.m_countPerLock.Length], m_tables.m_comparer);
                m_tables = newTables;
                m_budget = Math.Max(1, newTables.m_buckets.Length / newTables.m_locks.Length);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);//�ͷ�������
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection"/> to an array of
        /// type <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>, starting at the
        /// specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array of type <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// that is the destination of the <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/> elements copied from the <see
        /// cref="T:System.Collections.ICollection"/>. The array must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than
        /// the length of the <paramref name="array"/>. -or- The number of elements in the source <see
        /// cref="T:System.Collections.ICollection"/>
        /// is greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (index < 0) throw new ArgumentOutOfRangeException("index", GetResource("ConcurrentDictionary_IndexIsNegative"));

            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = 0;

                for (int i = 0; i < m_tables.m_locks.Length && count >= 0; i++)//��ȡ�ֵ��ڵ�����
                {
                    count += m_tables.m_countPerLock[i];
                }

                if (array.Length - count < index || count < 0) // "count" ���Լ� ��"count + index" �Ƿ�Խ��
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayNotLargeEnough"));
                }

                CopyToPairs(array, index);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copies the key and value pairs stored in the <see cref="ConcurrentDictionary{TKey,TValue}"/> to a
        /// new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of key and value pairs copied from the <see
        /// cref="ConcurrentDictionary{TKey,TValue}"/>.</returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                int count = 0;
                checked
                {
                    for (int i = 0; i < m_tables.m_locks.Length; i++)//����ֵ��ڵĸ���
                    {
                        count += m_tables.m_countPerLock[i];
                    }
                }

                KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[count];//���ݸ���������ֵ������

                CopyToPairs(array, 0);//�����ݸ��Ƶ���ֵ��������
                return array;
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// �����ֵ����ݵ�һ�����飬��ToArray��CopyTo�з���ʵ��
        /// ��Ҫ���ڵ���CopyToPair֮ǰ�������߱������ȫ����
        /// </summary>
        /// <param name="array">Ҫ���Ƶ���KeyValuePair<TKey, TValue>����</param>
        private void CopyToPairs(KeyValuePair<TKey, TValue>[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    index++; //this should never flow, CopyToPairs is only called when there's no overflow risk
                }
            }
        }

        /// <summary>
        /// �����ֵ����ݵ�һ�����飬��ToArray��CopyTo�з���ʵ��
        /// ��Ҫ���ڵ���CopyToPair֮ǰ�������߱������ȫ����
        /// </summary>
        /// <param name="array">Ҫ���Ƶ���DictionaryEntry����</param>
        private void CopyToEntries(DictionaryEntry[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new DictionaryEntry(current.m_key, current.m_value);
                    index++;  //this should never flow, CopyToEntries is only called when there's no overflow risk
                }
            }
        }

        /// <summary>
        /// �����ֵ����ݵ�һ�����飬��ToArray��CopyTo�з���ʵ��
        /// ��Ҫ���ڵ���CopyToPair֮ǰ�������߱������ȫ����
        /// </summary>
        /// <param name="array">Ҫ���Ƶ���object����</param>
        private void CopyToObjects(object[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    index++; //this should never flow, CopyToObjects is only called when there's no overflow risk
                }
            }
        }

        /// <summary>����һ��ö��������ConcurrentDictionary{TKey,TValue}</summary>
        /// <returns>An enumerator for the <see cref="ConcurrentDictionary{TKey,TValue}"/>.</returns>
        /// <remarks>
        /// The enumerator returned from the dictionary is safe to use concurrently with
        /// reads and writes to the dictionary, however it does not represent a moment-in-time snapshot
        /// of the dictionary.  The contents exposed through the enumerator may contain modifications
        /// made to the dictionary after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            Node[] buckets = m_tables.m_buckets;

            for (int i = 0; i < buckets.Length; i++)
            {
                // The Volatile.Read ensures that the load of the fields of 'current' doesn't move before the load from buckets[i].
                Node current = Volatile.Read<Node>(ref buckets[i]);

                while (current != null)
                {
                    yield return new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    current = current.m_next;
                }
            }
        }

        /// <summary>
        /// �����ڲ����ڲ���͸��µ�ʵ��
        /// ���key���ڣ��������Ƿ���false���Լ� ���updateIfExists == true ���ǽ��۽����޸�value
        /// ���key�����ڣ����ǽ����ֵ������true
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private bool TryAddInternal(TKey key, TValue value, bool updateIfExists, bool acquireLock, out TValue resultingValue)
        {
            while (true)
            {
                int bucketNo, lockNo;
                int hashcode;

                Tables tables = m_tables;//��ȡ��ǰtables
                IEqualityComparer<TKey> comparer = tables.m_comparer;//��ȡ���Ƚ���
                hashcode = comparer.GetHashCode(key);//��ȡҪҪ�������hashcode
                GetBucketAndLockNo(hashcode, out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);//��ȡbucketNo��lockNo

                bool resizeDesired = false;//�������ô�С
                bool lockTaken = false;
#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR                
                bool resizeDueToCollisions = false;
#endif // !FEATURE_CORECLR
#endif

                try
                {
                    if (acquireLock)//�����Ҫ��ȡ����
                        Monitor.Enter(tables.m_locks[lockNo], ref lockTaken);

                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurrence.
                    // ���tables���µ�����С�����ǿ���û�б�����ȷ��lock����������
                    // ��Ӧ����һ�ּ��亱�������
                    if (tables != m_tables)
                    {
                        continue;
                    }

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    int collisionCount = 0;
#endif // !FEATURE_CORECLR
#endif

                    // ������Ͱ�з��������
                    Node prev = null;
                    for (Node node = tables.m_buckets[bucketNo]; node != null; node = node.m_next)
                    {
                        Assert((prev == null && node == tables.m_buckets[bucketNo]) || prev.m_next == node);
                        if (comparer.Equals(node.m_key, key))//�ж�key�Ƿ����
                        {
                            // The key was found in the dictionary. If updates are allowed, update the value for that key.
                            // We need to create a new node for the update, in order to support TValue types that cannot
                            // be written atomically, since lock-free reads may be happening concurrently.
                            if (updateIfExists)//��������ж��Ƿ����
                            {
                                if (s_isValueWriteAtomic)//�ж��Ƿ���ԭ��д��
                                {
                                    node.m_value = value;
                                }
                                else
                                {
                                    Node newNode = new Node(node.m_key, value, hashcode, node.m_next);//�����µĽڵ㣬���ǽڵ�ļ�������ͬ�ģ����Ǹ�����ֵ
                                    if (prev == null)
                                    {
                                        tables.m_buckets[bucketNo] = newNode;
                                    }
                                    else
                                    {
                                        prev.m_next = newNode;
                                    }
                                }
                                resultingValue = value;//�����������򣬷��ظ��º��ֵ
                            }
                            else
                            {
                                resultingValue = node.m_value;//�����������£��򷵻ص�ǰ�ڵ��ֵ
                            }
                            return false;
                        }
                        prev = node;//����ǰ���ڵ�

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                        collisionCount++;
#endif // !FEATURE_CORECLR
#endif
                    }

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    if(collisionCount > HashHelpers.HashCollisionThreshold && HashHelpers.IsWellKnownEqualityComparer(comparer)) //ͨ����ͻ��Ŀ���ж��Ƿ������Ӧ����
                    {
                        resizeDesired = true;
                        resizeDueToCollisions = true;
                    }
#endif // !FEATURE_CORECLR
#endif

                    // The key was not found in the bucket. Insert the key-value pair.
                    // ��Ϊ��Ͱ�ڷ��֣������ֵ��
                    Volatile.Write<Node>(ref tables.m_buckets[bucketNo], new Node(key, value, hashcode, tables.m_buckets[bucketNo]));
                    checked //
                    {
                        tables.m_countPerLock[lockNo]++;
                    }

                    //
                    // If the number of elements guarded by this lock has exceeded the budget, resize the bucket table.
                    // It is also possible that GrowTable will increase the budget but won't resize the bucket table.
                    // That happens if the bucket table is found to be poorly utilized due to a bad hash function.
                    //
                    if (tables.m_countPerLock[lockNo] > m_budget)
                    {
                        resizeDesired = true;
                    }
                }
                finally
                {
                    if (lockTaken)//�����ȡ�����������ͷ�ָ�������ϵ�������
                        Monitor.Exit(tables.m_locks[lockNo]);
                }

                //
                // The fact that we got here means that we just performed an insertion. If necessary, we will grow the table.
                // ��ʵ�����ǵ�����ζ�����ǽ���ִ����һ�����룬����Ǳ�Ҫ�ģ����ǽ�������
                // Concurrency notes:
                // - Notice that we are not holding any locks at when calling GrowTable. This is necessary to prevent deadlocks.
                // - As a result, it is possible that GrowTable will be called unnecessarily. But, GrowTable will obtain lock 0
                //   and then verify that the table we passed to it as the argument is still the current table.
                // �����ڵ㣺
                // - ֪ͨ���ǽ�����GrowTable��ʱ�򣬽��������κ�����Ԥ���������Ǳ���ġ�
                // - ��Ϊ�����GrowTable��������ʱ���ܵġ����ǣ�GrowTable������
                if (resizeDesired)
                {
#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    if (resizeDueToCollisions)
                    {
                        GrowTable(tables, (IEqualityComparer<TKey>)HashHelpers.GetRandomizedEqualityComparer(comparer), true, m_keyRehashCount);
                    }
                    else
#endif // !FEATURE_CORECLR
                    {
                        GrowTable(tables, tables.m_comparer, false, m_keyRehashCount);
                    }
#else
                    GrowTable(tables, tables.m_comparer, false, m_keyRehashCount);
#endif
                }

                resultingValue = value;
                return true;
            }
        }

        /// <summary>
        /// ��ȡ��������ָ���ļ��������ֵ��
        /// </summary>
        /// <param name="key">Ҫ��ȡ�����õ�ֵ�ļ���</param>
        /// <value>The value associated with the specified key. If the specified key is not found, a get
        /// operation throws a
        /// <see cref="T:Sytem.Collections.Generic.KeyNotFoundException"/>, and a set operation creates a new
        /// element with the specified key.</value>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is retrieved and
        /// <paramref name="key"/>
        /// does not exist in the collection.</exception>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }
                return value;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");
                TValue dummy;
                TryAddInternal(key, value, true, true, out dummy);
            }
        }

        /// <summary>
        /// ��ȡ������ System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �еļ�/ֵ�Ե���Ŀ��
        /// </summary>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <value> ������ System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �еļ�/ֵ�Ե���Ŀ��</value>
        /// <remarks>Count has snapshot semantics and represents the number of items in the <see
        /// cref="ConcurrentDictionary{TKey,TValue}"/>
        /// at the moment when Count was accessed.</remarks>
        public int Count
        {
            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
            get
            {
                int count = 0;

                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    // Compute the count, we allow overflow
                    for (int i = 0; i < m_tables.m_countPerLock.Length; i++)
                    {
                        count += m_tables.m_countPerLock[i];//��ÿ�����ڵ���Ŀ���
                    }

                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }

                return count;
            }
        }

        /// <summary>
        /// ����ü��в����ڣ���ʹ��ָ����������/ֵ����ӵ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>��
        /// </summary>
        /// <param name="key">Ҫ��ӵ�Ԫ�صļ���</param>
        /// <param name="valueFactory">����Ϊ������ֵ�ĺ���</param>
        /// <exception cref="T:System.ArgumentNullException">key �� valueFactory Ϊ null��</exception>
        /// <exception cref="T:System.OverflowException">�ֵ��Ѱ��������Ŀ��Ԫ�� (System.Int32.MaxValue)��</exception>
        /// <returns>����ֵ�� ����ֵ����Ѵ���ָ���ļ�����Ϊ�ü�������ֵ��
        /// ����ֵ��в�����ָ���ļ�����Ϊ valueFactory ���صļ�����ֵ��</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (valueFactory == null) throw new ArgumentNullException("valueFactory");

            TValue resultingValue;
            if (TryGetValue(key, out resultingValue))
            {
                return resultingValue;
            }
            TryAddInternal(key, valueFactory(key), false, true, out resultingValue);
            return resultingValue;
        }

        /// <summary>
        /// ���ָ���ļ��в����ڣ��򽫼�/ֵ����ӵ� <see cref="ConcurrentDictionary{TKey,TValue}"/> �С�
        /// </summary>
        /// <param name="key">Ҫ��ӵ�Ԫ�صļ���</param>
        /// <param name="value">ָ���ļ�������ʱҪ��ӵ�ֵ</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The value for the key.  This will be either the existing value for the key if the 
        /// key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue resultingValue;
            TryAddInternal(key, value, false, true, out resultingValue);
            return resultingValue;
        }

        /// <summary>
        /// ����ü��в����ڣ���ʹ��ָ����������/ֵ����ӵ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>������ü��Ѵ��ڣ���ʹ�øú�������
        ///     System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �еļ�/ֵ�ԡ�
        /// </summary>
        /// <param name="key">Ҫ��ӵļ���Ӧ������ֵ�ļ�</param>
        /// <param name="addValueFactory">����Ϊ��ȱ������ֵ�ĺ���</param>
        /// <param name="updateValueFactory">���ڸ������м�������ֵΪ��������ֵ�ĺ���</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="addValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="updateValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>������ֵ�� �⽫�� addValueFactory �Ľ�������ȱ�ټ����� updateValueFactory �Ľ����������ڼ�����</returns>
        public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (addValueFactory == null) throw new ArgumentNullException("addValueFactory");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");

            TValue newValue, resultingValue;
            while (true)
            {
                TValue oldValue;
                if (TryGetValue(key, out oldValue))
                //�����ڣ�����ȥ����
                {
                    newValue = updateValueFactory(key, oldValue);
                    if (TryUpdate(key, newValue, oldValue))
                    {
                        return newValue;
                    }
                }
                else //�������
                {
                    newValue = addValueFactory(key);
                    if (TryAddInternal(key, newValue, false, true, out resultingValue))
                    {
                        return resultingValue;
                    }
                }
            }
        }

        /// <summary>
        /// ����ü��в����ڣ��򽫼�/ֵ����ӵ� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>������ü��Ѵ��ڣ���ʹ��ָ����������
        ///     System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> �еļ�/ֵ�ԡ�
        /// </summary>
        /// <param name="key">Ҫ��ӵļ���Ӧ������ֵ�ļ�</param>
        /// <param name="addValue">ҪΪ��ȱ����ӵ�ֵ</param>
        /// <param name="updateValueFactory">���ڸ������м�������ֵΪ��������ֵ�ĺ���</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="updateValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>������ֵ�� �⽫�� addValue �Ľ�������ȱ�ټ����� updateValueFactory �Ľ����������ڼ�����</returns>
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");
            TValue newValue, resultingValue;
            while (true)
            {
                TValue oldValue;
                if (TryGetValue(key, out oldValue))
                //key exists, try to update
                {
                    newValue = updateValueFactory(key, oldValue);
                    if (TryUpdate(key, newValue, oldValue))
                    {
                        return newValue;
                    }
                }
                else //try add
                {
                    if (TryAddInternal(key, addValue, false, true, out resultingValue))
                    {
                        return resultingValue;
                    }
                }
            }
        }



        /// <summary>
        /// ��ȡһ��ָʾ System.Collections.Concurrent.ConcurrentDictionary�Ƿ�Ϊ�յ�ֵ��
        /// </summary>
        /// <value>��� System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue> Ϊ�գ�
        /// ��Ϊtrue������Ϊ false��</value>
        public bool IsEmpty
        {
            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
            get
            {
                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    for (int i = 0; i < m_tables.m_countPerLock.Length; i++)
                    {
                        if (m_tables.m_countPerLock[i] != 0)
                        {
                            return false;
                        }
                    }
                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }

                return true;
            }
        }

        #region IDictionary<TKey,TValue> members

        /// <summary>
        /// Adds the specified key and value to the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <exception cref="T:System.ArgumentException">
        /// An element with the same key already exists in the <see
        /// cref="ConcurrentDictionary{TKey,TValue}"/>.</exception>
        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
            {
                throw new ArgumentException(GetResource("ConcurrentDictionary_KeyAlreadyExisted"));
            }
        }

        /// <summary>
        /// Removes the element with the specified key from the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the element is successfully remove; otherwise false. This method also returns
        /// false if
        /// <paramref name="key"/> was not found in the original <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            TValue throwAwayValue;
            return TryRemove(key, out throwAwayValue);
        }

        /// <summary>
        /// Gets a collection containing the keys in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.ICollection{TKey}"/> containing the keys in the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</value>
        public ICollection<TKey> Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.IEnumerable{TKey}"/> containing the keys of
        /// the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.IEnumerable{TKey}"/> containing the keys of
        /// the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.</value>
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Gets a collection containing the values in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.ICollection{TValue}"/> containing the values in
        /// the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</value>
        public ICollection<TValue> Values
        {
            get { return GetValues(); }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.IEnumerable{TValue}"/> containing the values
        /// in the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.IEnumerable{TValue}"/> containing the
        /// values in the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.</value>
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get { return GetValues(); }
        }
        #endregion

        #region ICollection<KeyValuePair<TKey,TValue>> Members

        /// <summary>
        /// Adds the specified value to the <see cref="T:System.Collections.Generic.ICollection{TValue}"/>
        /// with the specified key.
        /// </summary>
        /// <param name="keyValuePair">The <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure representing the key and value to add to the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="keyValuePair"/> of <paramref
        /// name="keyValuePair"/> is null.</exception>
        /// <exception cref="T:System.OverflowException">The <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>
        /// contains too many elements.</exception>
        /// <exception cref="T:System.ArgumentException">An element with the same key already exists in the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/></exception>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        {
            ((IDictionary<TKey, TValue>)this).Add(keyValuePair.Key, keyValuePair.Value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/>
        /// contains a specific key and value.
        /// </summary>
        /// <param name="keyValuePair">The <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure to locate in the <see
        /// cref="T:System.Collections.Generic.ICollection{TValue}"/>.</param>
        /// <returns>true if the <paramref name="keyValuePair"/> is found in the <see
        /// cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/>; otherwise, false.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
        {
            TValue value;
            if (!TryGetValue(keyValuePair.Key, out value))
            {
                return false;
            }
            return EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value);
        }

        /// <summary>
        /// Gets a value indicating whether the dictionary is read-only.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/> is
        /// read-only; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>, this property always returns
        /// false.</value>
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes a key and value from the dictionary.
        /// </summary>
        /// <param name="keyValuePair">The <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure representing the key and value to remove from the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</param>
        /// <returns>true if the key and value represented by <paramref name="keyValuePair"/> is successfully
        /// found and removed; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException">The Key property of <paramref
        /// name="keyValuePair"/> is a null reference (Nothing in Visual Basic).</exception>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
        {
            if (keyValuePair.Key == null) throw new ArgumentNullException(GetResource("ConcurrentDictionary_ItemKeyIsNull"));

            TValue throwAwayValue;
            return TryRemoveInternal(keyValuePair.Key, out throwAwayValue, true, keyValuePair.Value);
        }

        #endregion

        #region IEnumerable Members

        /// <summary>Returns an enumerator that iterates through the <see
        /// cref="ConcurrentDictionary{TKey,TValue}"/>.</summary>
        /// <returns>An enumerator for the <see cref="ConcurrentDictionary{TKey,TValue}"/>.</returns>
        /// <remarks>
        /// The enumerator returned from the dictionary is safe to use concurrently with
        /// reads and writes to the dictionary, however it does not represent a moment-in-time snapshot
        /// of the dictionary.  The contents exposed through the enumerator may contain modifications
        /// made to the dictionary after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ConcurrentDictionary<TKey, TValue>)this).GetEnumerator();
        }

        #endregion

        #region IDictionary Members

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key">The object to use as the key.</param>
        /// <param name="value">The object to use as the value.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="key"/> is of a type that is not assignable to the key type <typeparamref
        /// name="TKey"/> of the <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>. -or-
        /// <paramref name="value"/> is of a type that is not assignable to <typeparamref name="TValue"/>,
        /// the type of values in the <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// -or- A value with the same key already exists in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </exception>
        void IDictionary.Add(object key, object value)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (!(key is TKey)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfKeyIncorrect"));

            TValue typedValue;
            try
            {
                typedValue = (TValue)value;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfValueIncorrect"));
            }

            ((IDictionary<TKey, TValue>)this).Add((TKey)key, typedValue);
        }

        /// <summary>
        /// Gets whether the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> contains an
        /// element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</param>
        /// <returns>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> contains
        /// an element with the specified key; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"> <paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        bool IDictionary.Contains(object key)
        {
            if (key == null) throw new ArgumentNullException("key");

            return (key is TKey) && ((ConcurrentDictionary<TKey, TValue>)this).ContainsKey((TKey)key);
        }

        /// <summary>Provides an <see cref="T:System.Collections.Generics.IDictionaryEnumerator"/> for the
        /// <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</summary>
        /// <returns>An <see cref="T:System.Collections.Generics.IDictionaryEnumerator"/> for the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</returns>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new DictionaryEnumerator(this);
        }

        /// <summary>
        /// Gets a value indicating whether the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> has a fixed size.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> has a
        /// fixed size; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool IDictionary.IsFixedSize
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> is read-only.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> is
        /// read-only; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool IDictionary.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> containing the keys of the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.ICollection"/> containing the keys of the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</value>
        ICollection IDictionary.Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Removes the element with the specified key from the <see
        /// cref="T:System.Collections.IDictionary"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        void IDictionary.Remove(object key)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue throwAwayValue;
            if (key is TKey)
            {
                this.TryRemove((TKey)key, out throwAwayValue);
            }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> containing the values in the <see
        /// cref="T:System.Collections.IDictionary"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.ICollection"/> containing the values in the <see
        /// cref="T:System.Collections.IDictionary"/>.</value>
        ICollection IDictionary.Values
        {
            get { return GetValues(); }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <value>The value associated with the specified key, or a null reference (Nothing in Visual Basic)
        /// if <paramref name="key"/> is not in the dictionary or <paramref name="key"/> is of a type that is
        /// not assignable to the key type <typeparamref name="TKey"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>.</value>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentException">
        /// A value is being assigned, and <paramref name="key"/> is of a type that is not assignable to the
        /// key type <typeparamref name="TKey"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>. -or- A value is being
        /// assigned, and <paramref name="key"/> is of a type that is not assignable to the value type
        /// <typeparamref name="TValue"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>
        /// </exception>
        object IDictionary.this[object key]
        {
            get
            {
                if (key == null) throw new ArgumentNullException("key");

                TValue value;
                if (key is TKey && this.TryGetValue((TKey)key, out value))
                {
                    return value;
                }

                return null;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");

                if (!(key is TKey)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfKeyIncorrect"));
                if (!(value is TValue)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfValueIncorrect"));

                ((ConcurrentDictionary<TKey, TValue>)this)[(TKey)key] = (TValue)value;
            }
        }

        #endregion

        #region ICollection Members

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an array, starting
        /// at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from
        /// the <see cref="T:System.Collections.ICollection"/>. The array must have zero-based
        /// indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than
        /// the length of the <paramref name="array"/>. -or- The number of elements in the source <see
        /// cref="T:System.Collections.ICollection"/>
        /// is greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (index < 0) throw new ArgumentOutOfRangeException("index", GetResource("ConcurrentDictionary_IndexIsNegative"));

            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                Tables tables = m_tables;

                int count = 0;

                for (int i = 0; i < tables.m_locks.Length && count >= 0; i++)
                {
                    count += tables.m_countPerLock[i];
                }

                if (array.Length - count < index || count < 0) //"count" itself or "count + index" can overflow
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayNotLargeEnough"));
                }

                // To be consistent with the behavior of ICollection.CopyTo() in Dictionary<TKey,TValue>,
                // we recognize three types of target arrays:
                //    - an array of KeyValuePair<TKey, TValue> structs
                //    - an array of DictionaryEntry structs
                //    - an array of objects

                KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
                if (pairs != null)
                {
                    CopyToPairs(pairs, index);
                    return;
                }

                DictionaryEntry[] entries = array as DictionaryEntry[];
                if (entries != null)
                {
                    CopyToEntries(entries, index);
                    return;
                }

                object[] objects = array as object[];
                if (objects != null)
                {
                    CopyToObjects(objects, index);
                    return;
                }

                throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayIncorrectType"), "array");
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is
        /// synchronized with the SyncRoot.
        /// </summary>
        /// <value>true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized
        /// (thread safe); otherwise, false. For <see
        /// cref="T:System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool ICollection.IsSynchronized
        {
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

        #endregion

        /// <summary>
        /// Replaces the bucket table with a larger one. To prevent multiple threads from resizing the
        /// table as a result of ----s, the Tables instance that holds the table of buckets deemed too
        /// small is passed in as an argument to GrowTable(). GrowTable() obtains a lock, and then checks
        /// the Tables instance has been replaced in the meantime or not. 
        /// The <paramref name="rehashCount"/> will be used to ensure that we don't do two subsequent resizes
        /// because of a collision
        /// </summary>
        private void GrowTable(Tables tables, IEqualityComparer<TKey> newComparer, bool regenerateHashKeys, int rehashCount)
        {
            int locksAcquired = 0;
            try
            {
                // The thread that first obtains m_locks[0] will be the one doing the resize operation
                AcquireLocks(0, 1, ref locksAcquired);

                if (regenerateHashKeys && rehashCount == m_keyRehashCount)
                {
                    // This method is called with regenerateHashKeys==true when we detected 
                    // more than HashHelpers.HashCollisionThreshold collisions when adding a new element.
                    // In that case we are in the process of switching to another (randomized) comparer
                    // and we have to re-hash all the keys in the table.
                    // We are only going to do this if we did not just rehash the entire table while waiting for the lock
                    tables = m_tables;
                }
                else
                {
                    // If we don't require a regeneration of hash keys we want to make sure we don't do work when
                    // we don't have to
                    if (tables != m_tables)
                    {
                        // We assume that since the table reference is different, it was already resized (or the budget
                        // was adjusted). If we ever decide to do table shrinking, or replace the table for other reasons,
                        // we will have to revisit this logic.
                        return;
                    }

                    // Compute the (approx.) total size. Use an Int64 accumulation variable to avoid an overflow.
                    long approxCount = 0;
                    for (int i = 0; i < tables.m_countPerLock.Length; i++)
                    {
                        approxCount += tables.m_countPerLock[i];
                    }

                    //
                    // If the bucket array is too empty, double the budget instead of resizing the table
                    //
                    if (approxCount < tables.m_buckets.Length / 4)
                    {
                        m_budget = 2 * m_budget;
                        if (m_budget < 0)
                        {
                            m_budget = int.MaxValue;
                        }

                        return;
                    }
                }
                // Compute the new table size. We find the smallest integer larger than twice the previous table size, and not divisible by
                // 2,3,5 or 7. We can consider a different table-sizing policy in the future.
                int newLength = 0;
                bool maximizeTableSize = false;
                try
                {
                    checked
                    {
                        // Double the size of the buckets table and add one, so that we have an odd integer.
                        newLength = tables.m_buckets.Length * 2 + 1;

                        // Now, we only need to check odd integers, and find the first that is not divisible
                        // by 3, 5 or 7.
                        while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
                        {
                            newLength += 2;
                        }

                        Assert(newLength % 2 != 0);

                        if (newLength > Array.MaxArrayLength)
                        {
                            maximizeTableSize = true;
                        }
                    }
                }
                catch (OverflowException)
                {
                    maximizeTableSize = true;
                }

                if (maximizeTableSize)
                {
                    newLength = Array.MaxArrayLength;

                    // We want to make sure that GrowTable will not be called again, since table is at the maximum size.
                    // To achieve that, we set the budget to int.MaxValue.
                    //
                    // (There is one special case that would allow GrowTable() to be called in the future: 
                    // calling Clear() on the ConcurrentDictionary will shrink the table and lower the budget.)
                    m_budget = int.MaxValue;
                }

                // Now acquire all other locks for the table
                AcquireLocks(1, tables.m_locks.Length, ref locksAcquired);

                object[] newLocks = tables.m_locks;

                // Add more locks
                if (m_growLockArray && tables.m_locks.Length < MAX_LOCK_NUMBER)
                {
                    newLocks = new object[tables.m_locks.Length * 2];
                    Array.Copy(tables.m_locks, newLocks, tables.m_locks.Length);

                    for (int i = tables.m_locks.Length; i < newLocks.Length; i++)
                    {
                        newLocks[i] = new object();
                    }
                }

                Node[] newBuckets = new Node[newLength];
                int[] newCountPerLock = new int[newLocks.Length];

                // Copy all data into a new table, creating new nodes for all elements
                for (int i = 0; i < tables.m_buckets.Length; i++)
                {
                    Node current = tables.m_buckets[i];
                    while (current != null)
                    {
                        Node next = current.m_next;
                        int newBucketNo, newLockNo;
                        int nodeHashCode = current.m_hashcode;

                        if (regenerateHashKeys)
                        {
                            // Recompute the hash from the key
                            nodeHashCode = newComparer.GetHashCode(current.m_key);
                        }

                        GetBucketAndLockNo(nodeHashCode, out newBucketNo, out newLockNo, newBuckets.Length, newLocks.Length);

                        newBuckets[newBucketNo] = new Node(current.m_key, current.m_value, nodeHashCode, newBuckets[newBucketNo]);

                        checked
                        {
                            newCountPerLock[newLockNo]++;
                        }

                        current = next;
                    }
                }

                // If this resize regenerated the hashkeys, increment the count
                if (regenerateHashKeys)
                {
                    // We use unchecked here because we don't want to throw an exception if 
                    // an overflow happens
                    unchecked
                    {
                        m_keyRehashCount++;
                    }
                }

                // Adjust the budget
                m_budget = Math.Max(1, newBuckets.Length / newLocks.Length);

                // Replace tables with the new versions
                m_tables = new Tables(newBuckets, newLocks, newCountPerLock, newComparer);
            }
            finally
            {
                // Release all locks that we took earlier
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// ����Ͱ��һ�������������Ŀ
        /// </summary>
        private void GetBucketAndLockNo(
                int hashcode, out int bucketNo, out int lockNo, int bucketCount, int lockCount)
        {
            bucketNo = (hashcode & 0x7fffffff) % bucketCount;//����bucketNo
            lockNo = bucketNo % lockCount;//����lockNo

            Assert(bucketNo >= 0 && bucketNo < bucketCount);
            Assert(lockNo >= 0 && lockNo < lockCount);
        }

        /// <summary>
        /// The number of concurrent writes for which to optimize by default.
        /// </summary>
        private static int DefaultConcurrencyLevel
        {

            get { return DEFAULT_CONCURRENCY_MULTIPLIER * PlatformHelper.ProcessorCount; }
        }

        /// <summary>
        /// �����hashtable����������ͨ���ɹ��ػ�ȡ������Ŀ������lockAcquired��ͨ������order����ȡ��
        /// </summary>
        private void AcquireAllLocks(ref int locksAcquired)
        {
#if !FEATURE_PAL && !FEATURE_CORECLR    // PAL and CoreClr don't support  eventing
            if (CDSCollectionETWBCLProvider.Log.IsEnabled())
            {
                CDSCollectionETWBCLProvider.Log.ConcurrentDictionary_AcquiringAllLocks(m_tables.m_buckets.Length);
            }
#endif //!FEATURE_PAL && !FEATURE_CORECLR

            // First, acquire lock 0
            // ���Ȼ�ȡlock 0
            AcquireLocks(0, 1, ref locksAcquired);

            // ���������Ѿ�����lock 0�� m_locks���齫������иı�
            // ֮�����ǽ����԰�ȫ�Ķ�ȡ��locks.Length
            AcquireLocks(1, m_tables.m_locks.Length, ref locksAcquired);
            Assert(locksAcquired == m_tables.m_locks.Length);
        }

        /// <summary>
        /// ��hash table ��ȡһ��������Χ������ͨ���ɹ��ػ�ȡ������Ŀ������lockAcquired��ͨ������order����ȡ��
        /// </summary>
        private void AcquireLocks(int fromInclusive, int toExclusive, ref int locksAcquired)
        {
            Assert(fromInclusive <= toExclusive);
            object[] locks = m_tables.m_locks;//��ȡ����������

            for (int i = fromInclusive; i < toExclusive; i++)//��locks�����ϵĶ�����м�������
            {
                bool lockTaken = false;
                try
                {
#if CDS_COMPILE_JUST_THIS
                    Monitor.Enter(m_tables.m_locks[i]);
                    lockTaken = true;
#else
                    Monitor.Enter(locks[i], ref lockTaken);
#endif
                }
                finally
                {
                    if (lockTaken)//��������ɹ���������locksAcquired
                    {
                        locksAcquired++;
                    }
                }
            }
        }

        /// <summary>
        /// �ͷ�һ��������Χ�ڵ���
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private void ReleaseLocks(int fromInclusive, int toExclusive)
        {
            Assert(fromInclusive <= toExclusive);

            for (int i = fromInclusive; i < toExclusive; i++)//��m_locks�����ڵĶ����������ͷ�
            {
                Monitor.Exit(m_tables.m_locks[i]);
            }
        }

        /// <summary>
        /// Gets a collection containing the keys in the dictionary.
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        private ReadOnlyCollection<TKey> GetKeys()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                List<TKey> keys = new List<TKey>();

                for (int i = 0; i < m_tables.m_buckets.Length; i++)
                {
                    Node current = m_tables.m_buckets[i];
                    while (current != null)
                    {
                        keys.Add(current.m_key);
                        current = current.m_next;
                    }
                }

                return new ReadOnlyCollection<TKey>(keys);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        private ReadOnlyCollection<TValue> GetValues()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                List<TValue> values = new List<TValue>();

                for (int i = 0; i < m_tables.m_buckets.Length; i++)
                {
                    Node current = m_tables.m_buckets[i];
                    while (current != null)
                    {
                        values.Add(current.m_value);
                        current = current.m_next;
                    }
                }

                return new ReadOnlyCollection<TValue>(values);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// һ������asserts�İ�������
        /// </summary>
        [Conditional("DEBUG")]
        private void Assert(bool condition)
        {
#if CDS_COMPILE_JUST_THIS
            if (!condition)
            {
                throw new Exception("Assertion failed.");
            }
#else
            Contract.Assert(condition);
#endif
        }

        /// <summary>
        /// A helper function to obtain the string for a particular resource key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetResource(string key)
        {
            Assert(key != null);

#if CDS_COMPILE_JUST_THIS
            return key;
#else
            return Environment.GetResourceString(key);
#endif
        }

        /// <summary>
        /// һ�������б����һ������hashtableͰ�еĽڵ�
        /// </summary>
        private class Node
        {
            internal TKey m_key;//��
            internal TValue m_value;//ֵ
            internal volatile Node m_next;//��һ���ڵ�
            internal int m_hashcode;//�ڵ��hashcode

            /// <summary>
            /// �ڵ�Ĺ��캯��
            /// </summary>
            /// <param name="key"></param>
            /// <param name="value"></param>
            /// <param name="hashcode"></param>
            /// <param name="next"></param>
            internal Node(TKey key, TValue value, int hashcode, Node next)
            {
                m_key = key;
                m_value = value;
                m_next = next;
                m_hashcode = hashcode;
            }
        }

        /// <summary>
        /// A private class to represent enumeration over the dictionary that implements the 
        /// IDictionaryEnumerator interface.
        /// </summary>
        private class DictionaryEnumerator : IDictionaryEnumerator
        {
            IEnumerator<KeyValuePair<TKey, TValue>> m_enumerator; // Enumerator over the dictionary.

            internal DictionaryEnumerator(ConcurrentDictionary<TKey, TValue> dictionary)
            {
                m_enumerator = dictionary.GetEnumerator();
            }

            public DictionaryEntry Entry
            {
                get { return new DictionaryEntry(m_enumerator.Current.Key, m_enumerator.Current.Value); }
            }

            public object Key
            {
                get { return m_enumerator.Current.Key; }
            }

            public object Value
            {
                get { return m_enumerator.Current.Value; }
            }

            public object Current
            {
                get { return this.Entry; }
            }

            public bool MoveNext()
            {
                return m_enumerator.MoveNext();
            }

            public void Reset()
            {
                m_enumerator.Reset();
            }
        }

#if !FEATURE_CORECLR
        /// <summary>
        /// Get the data array to be serialized
        /// </summary>
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            Tables tables = m_tables;

            // save the data into the serialization array to be saved
            m_serializationArray = ToArray();
            m_serializationConcurrencyLevel = tables.m_locks.Length;
            m_serializationCapacity = tables.m_buckets.Length;
            m_comparer = (IEqualityComparer<TKey>)HashHelpers.GetEqualityComparerForSerialization(tables.m_comparer);
        }

        /// <summary>
        /// Construct the dictionary from a previously serialized one
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            KeyValuePair<TKey, TValue>[] array = m_serializationArray;

            var buckets = new Node[m_serializationCapacity];
            var countPerLock = new int[m_serializationConcurrencyLevel];

            var locks = new object[m_serializationConcurrencyLevel];
            for (int i = 0; i < locks.Length; i++)
            {
                locks[i] = new object();
            }

            m_tables = new Tables(buckets, locks, countPerLock, m_comparer);

            InitializeFromCollection(array);
            m_serializationArray = null;

        }
#endif
    }
}
