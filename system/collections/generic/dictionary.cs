// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  Dictionary
** 
** <OWNER>[....]</OWNER>
**
** Purpose: Generic hash table implementation
**
** #DictionaryVersusHashtableThreadSafety
** Hashtable has multiple reader/single writer (MR/SW) thread safety built into 
** certain methods and properties, whereas Dictionary doesn't. If you're 
** converting framework code that formerly used Hashtable to Dictionary, it's
** important to consider whether callers may have taken a dependence on MR/SW
** thread safety. If a reader writer lock is available, then that may be used
** with a Dictionary to get the same thread safety guarantee. 
** 
** Reader writer locks don't exist in silverlight, so we do the following as a
** result of removing non-generic collections from silverlight: 
** 1. If the Hashtable was fully synchronized, then we replace it with a 
**    Dictionary with full locks around reads/writes (same thread safety
**    guarantee).
** 2. Otherwise, the Hashtable has the default MR/SW thread safety behavior, 
**    so we do one of the following on a case-by-case basis:
**    a. If the ---- can be addressed by rearranging the code and using a temp
**       variable (for example, it's only populated immediately after created)
**       then we address the ---- this way and use Dictionary.
**    b. If there's concern about degrading performance with the increased 
**       locking, we ifdef with FEATURE_NONGENERIC_COLLECTIONS so we can at 
**       least use Hashtable in the desktop build, but Dictionary with full 
**       locks in silverlight builds. Note that this is heavier locking than 
**       MR/SW, but this is the only option without rewriting (or adding back)
**       the reader writer lock. 
**    c. If there's no performance concern (e.g. debug-only code) we 
**       consistently replace Hashtable with Dictionary plus full locks to 
**       reduce complexity.
**    d. Most of serialization is dead code in silverlight. Instead of updating
**       those Hashtable occurences in serialization, we carved out references 
**       to serialization such that this code doesn't need to build in 
**       silverlight. 
===========================================================*/
namespace System.Collections.Generic {

    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.Serialization;
    using System.Security.Permissions;

    [DebuggerTypeProxy(typeof(Mscorlib_DictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(false)]
    public class Dictionary<TKey,TValue>: IDictionary<TKey,TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>, ISerializable, IDeserializationCallback  {
    
        /// <summary>
        /// ��Ŀ
        /// </summary>
        private struct Entry {
            public int hashCode;    // Lower 31 bits of hash code, -1 if unused
            public int next;        // Index of next entry, -1 if last
            public TKey key;           // Key of entry
            public TValue value;         // Value of entry
        }

        /// <summary>
        /// Ͱ
        /// </summary>
        private int[] buckets;
        /// <summary>
        /// ��Ŀ����
        /// </summary>
        private Entry[] entries;
        /// <summary>
        /// ����
        /// </summary>
        private int count;
        /// <summary>
        /// �汾
        /// </summary>
        private int version;
        /// <summary>
        /// �ͷŶ���
        /// </summary>
        private int freeList;
        /// <summary>
        /// �ͷ�����
        /// </summary>
        private int freeCount;
        /// <summary>
        /// ��ȡ����ȷ���ֵ��еļ��Ƿ���ȵ� IEqualityComparer<T>
        /// </summary>
        private IEqualityComparer<TKey> comparer;
        /// <summary>
        /// ���ļ���
        /// </summary>
        private KeyCollection keys;
        /// <summary>
        /// ֵ�ļ���
        /// </summary>
        private ValueCollection values;
        /// <summary>
        /// ͬ��������
        /// </summary>
        private Object _syncRoot;
        
        // Ϊ���л��ĳ���
        private const String VersionName = "Version";
        private const String HashSizeName = "HashSize";  //���뱣���Ͱ����
        private const String KeyValuePairsName = "KeyValuePairs";
        private const String ComparerName = "Comparer";

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��Ϊ�գ�����Ĭ�ϵĳ�ʼ������Ϊ������ʹ��Ĭ�ϵ���ȱȽ�����
        /// </summary>
        public Dictionary(): this(0, null) {}

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��Ϊ�գ�����ָ���ĳ�ʼ������Ϊ������ʹ��Ĭ�ϵ���ȱȽ���
        /// </summary>
        /// <param name="capacity"></param>
        public Dictionary(int capacity): this(capacity, null) {}

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��Ϊ�գ�����Ĭ�ϵĳ�ʼ������ʹ��ָ���� IEqualityComparer<T>��
        /// </summary>
        /// <param name="comparer"></param>
        public Dictionary(IEqualityComparer<TKey> comparer): this(0, comparer) {}

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��Ϊ�գ�����ָ���ĳ�ʼ������ʹ��ָ���� IEqualityComparer<T>��
        /// </summary>
        /// <param name="capacity">Dictionary<TKey, TValue> �ɰ����ĳ�ʼԪ������</param>
        /// <param name="comparer">�Ƚϼ�ʱҪʹ�õ� IEqualityComparer<T> ʵ�֣�����Ϊ null���Ա�Ϊ������ʹ��Ĭ�ϵ� EqualityComparer<T>��</param>
        public Dictionary(int capacity, IEqualityComparer<TKey> comparer) {
            if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);
            if (capacity > 0) Initialize(capacity);
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;//����Ĭ�ϱȽ���

#if FEATURE_CORECLR
            if (HashHelpers.s_UseRandomizedStringHashing && comparer == EqualityComparer<string>.Default)
            {
                this.comparer = (IEqualityComparer<TKey>) NonRandomizedStringEqualityComparer.Default;
            }
#endif // FEATURE_CORECLR
        }

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��������ָ���� IDictionary<TKey, TValue> ���Ƶ�Ԫ�ز�Ϊ������ʹ��Ĭ�ϵ���ȱȽ�����
        /// </summary>
        /// <param name="dictionary"></param>
        public Dictionary(IDictionary<TKey,TValue> dictionary): this(dictionary, null) {}

        /// <summary>
        /// ��ʼ�� Dictionary<TKey, TValue> �����ʵ������ʵ��������ָ���� IDictionary<TKey, TValue> �и��Ƶ�Ԫ�ز�ʹ��ָ���� IEqualityComparer<T>��
        /// </summary>
        /// <param name="dictionary">IDictionary<TKey, TValue>������Ԫ�ر����Ƶ��� Dictionary<TKey, TValue>��</param>
        /// <param name="comparer">�Ƚϼ�ʱҪʹ�õ� IEqualityComparer<T> ʵ�֣�����Ϊ null���Ա�Ϊ������ʹ��Ĭ�ϵ� EqualityComparer<T>��</param>
        public Dictionary(IDictionary<TKey,TValue> dictionary, IEqualityComparer<TKey> comparer):
            this(dictionary != null? dictionary.Count: 0, comparer) //����ֵ�Ϊ��������Ϊ0����������Ϊ�ֵ�����
        {

            if( dictionary == null) {//����ֵ�Ϊ�����׳��ֵ����Ϊ���쳣
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
            }

            foreach (KeyValuePair<TKey,TValue> pair in dictionary) {//��ԭ�ֵ��еļ�ֵ����ӵ��µ��ֵ���
                Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// �����л����ݳ�ʼ�� Dictionary<TKey, TValue> �����ʵ����
        /// </summary>
        /// <param name="info">һ�� System.Runtime.Serialization.SerializationInfo ����������л� Dictionary<TKey, TValue> �������Ϣ��</param>
        /// <param name="context">һ�� System.Runtime.Serialization.StreamingContext �ṹ������ Dictionary<TKey, TValue> ���������л�����Դ��Ŀ�ꡣ</param>
        protected Dictionary(SerializationInfo info, StreamingContext context) {
            //We can't do anything with the keys and values until the entire graph has been deserialized
            //and we have a resonable estimate that GetHashCode is not going to fail.  For the time being,
            //we'll just cache this.  The graph is not valid until OnDeserialization has been called.
            HashHelpers.SerializationInfoTable.Add(this, info);
        }
          
        /// <summary>
        /// ��ȡ����ȷ���ֵ��еļ��Ƿ���ȵ� IEqualityComparer<T>��
        /// </summary>
        public IEqualityComparer<TKey> Comparer {
            get {
                return comparer;                
            }               
        }
        
        /// <summary>
        /// ��ȡ������ Dictionary<TKey, TValue> �еļ�/ֵ�Ե���Ŀ��
        /// </summary>
        public int Count {
            get { return count - freeCount; }
        }

        /// <summary>
        /// ���һ������ Dictionary<TKey, TValue> �еļ��ļ��ϡ�
        /// </summary>
        public KeyCollection Keys {
            get {
                Contract.Ensures(Contract.Result<KeyCollection>() != null);
                if (keys == null) keys = new KeyCollection(this);//���Ϊ�գ�����ݼ�ֵ�Դ���������
                return keys;
            }
        }

        /// <summary>
        /// ��ȡһ��ICollection<TKey>���Ͻӿ�
        /// </summary>
        ICollection<TKey> IDictionary<TKey, TValue>.Keys {
            get {                
                if (keys == null) keys = new KeyCollection(this);                
                return keys;//�ӿ�ת��
            }
        }

        /// <summary>
        /// ��ȡһ��IEnumerable<TKey>�ӿ�
        /// </summary>
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys {
            get {                
                if (keys == null) keys = new KeyCollection(this);                
                return keys;
            }
        }

        /// <summary>
        /// ��ȡ��ֵ����ֵ����
        /// </summary>
        public ValueCollection Values {
            get {
                Contract.Ensures(Contract.Result<ValueCollection>() != null);
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        /// <summary>
        /// ��ȡ��ֵ����ICollection<TValue>�ӿ�
        /// </summary>
        ICollection<TValue> IDictionary<TKey, TValue>.Values {
            get {                
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values {
            get {                
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        /// <summary>
        /// ��ȡ��������ָ���ļ�������ֵ��
        /// </summary>
        /// <param name="key">ָ���ļ�</param>
        /// <returns>��ص�ֵ</returns>
        public TValue this[TKey key] {
            get {
                int i = FindEntry(key);
                if (i >= 0) return entries[i].value;//���i����0�����ȡ��Ŀ�е�ֵ
                ThrowHelper.ThrowKeyNotFoundException();//�׳���Ϊ������
                return default(TValue);//���������ʹ�� default �ؼ��֣��˹ؼ��ֶ����������ͻ᷵�ؿգ�������ֵ���ͻ᷵���㡣���ڽṹ���˹ؼ��ֽ����س�ʼ��Ϊ���յ�ÿ���ṹ��Ա������ȡ������Щ�ṹ��ֵ���ͻ����������͡�
            }
            set {
                Insert(key, value, false);
            }
        }

        /// <summary>
        /// ��ָ���ļ���ֵ��ӵ��ֵ��С�
        /// </summary>
        /// <param name="key">��</param>
        /// <param name="value">ֵ</param>
        public void Add(TKey key, TValue value) {
            Insert(key, value, true);
        }

        /// <summary>
        /// ��ʾ�ӿ�ʵ��add
        /// </summary>
        /// <param name="keyValuePair">��ֵ��</param>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair) {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        /// <summary>
        /// ��ʾ�ӿ�ʵ��contains
        /// </summary>
        /// <param name="keyValuePair">�鿴�Ƿ�����ļ�ֵ��</param>
        /// <returns>�����ͬ����true�������ͬ�򷵻�false</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair) {
            int i = FindEntry(keyValuePair.Key);//������
            if( i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value)) {//ֵ��ͬ
                return true;
            }
            return false;
        }

        /// <summary>
        /// �Ƴ����еļ�ֵ��
        /// </summary>
        /// <param name="keyValuePair">Ҫ�Ƴ��ļ�ֵ��</param>
        /// <returns>�Ƴ��ɹ��򷵻�Ϊtrue</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair) {
            int i = FindEntry(keyValuePair.Key);
            if( i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value)) {
                Remove(keyValuePair.Key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ���ֵ��е��������
        /// </summary>
        public void Clear() {
            if (count > 0) {
                for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
                Array.Clear(entries, 0, count);
                freeList = -1;
                count = 0;
                freeCount = 0;
                version++;
            }
        }

        /// <summary>
        /// ȷ���Ƿ� Dictionary<TKey, TValue> ����ָ������
        /// </summary>
        /// <param name="key">��</param>
        /// <returns></returns>
        public bool ContainsKey(TKey key) {
            return FindEntry(key) >= 0;
        }

        /// <summary>
        /// ȷ�� Dictionary<TKey, TValue> �Ƿ�����ض�ֵ��
        /// </summary>
        /// <param name="value">Ҫ�� Dictionary<TKey, TValue> �ж�λ��ֵ�������������ͣ���ֵ����Ϊ null</param>
        /// <returns></returns>
        public bool ContainsValue(TValue value) {
            if (value == null) {//���ֵΪ�գ�����д�����
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0 && entries[i].value == null) return true;
                }
            }
            else {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;//ʹ��һ��Ĭ�ϵıȽ���
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0 && c.Equals(entries[i].value, value)) return true;//�����ͬ�򷵻�Ϊtrue
                }
            }
            return false;
        }

        /// <summary>
        /// ���ֵ����������ʼ���Ƶ���ֵ��������
        /// </summary>
        /// <param name="array">��ֵ������</param>
        /// <param name="index">��ʼ����</param>
        private void CopyTo(KeyValuePair<TKey,TValue>[] array, int index) {
            if (array == null) {//�������Ϊ�գ����׳��쳣
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            }
            
            if (index < 0 || index > array.Length ) {//����Խ���׳��쳣
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count) {//�����СԽ��
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
            }

            int count = this.count;
            Entry[] entries = this.entries;//�����µ���Ŀ���ã��������鸴��
            for (int i = 0; i < count; i++) {
                if (entries[i].hashCode >= 0) {
                    array[index++] = new KeyValuePair<TKey,TValue>(entries[i].key, entries[i].value);
                }
            }
        }

        /// <summary>
        /// ����ѭ������ Dictionary<TKey, TValue> ��ö������
        /// </summary>
        /// <returns></returns>
        public Enumerator GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        /// <summary>
        /// ���� IDictionaryEnumerator �� IDictionary��
        /// </summary>
        /// <returns></returns>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }        


        /// <summary>
        /// ʵ�� System.Runtime.Serialization.ISerializable �ӿڣ����������л� Dictionary<TKey, TValue> ʵ����������ݡ�
        /// </summary>
        /// <param name="info">System.Runtime.Serialization.SerializationInfo ���󣬸ö���������л� Dictionary<TKey, TValue> ʵ���������Ϣ��</param>
        /// <param name="context">һ�� System.Runtime.Serialization.StreamingContext �ṹ���������� Dictionary<TKey, TValue> ʵ�����������л�����Դ��Ŀ�ꡣ</param>
        [System.Security.SecurityCritical]  // auto-generated_required
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            if (info==null) {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.info);
            }
            info.AddValue(VersionName, version);

#if FEATURE_RANDOMIZED_STRING_HASHING
            info.AddValue(ComparerName, HashHelpers.GetEqualityComparerForSerialization(comparer), typeof(IEqualityComparer<TKey>));
#else
            info.AddValue(ComparerName, comparer, typeof(IEqualityComparer<TKey>));
#endif

            info.AddValue(HashSizeName, buckets == null ? 0 : buckets.Length); //This is the length of the bucket array.
            if( buckets != null) {
                KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[Count];
                CopyTo(array, 0);
                info.AddValue(KeyValuePairsName, array, typeof(KeyValuePair<TKey, TValue>[]));
            }
        }

        /// <summary>
        /// ���ݼ����еļ���ȡ����Ŀ
        /// </summary>
        /// <param name="key">ָ���ļ�</param>
        /// <returns>������Ŀ</returns>
        private int FindEntry(TKey key) {
            if( key == null) {//�����Ϊ�գ����׳�Ϊ����Ϊ���쳣
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets != null) {//�����ɭͰ��Ϊ�գ����Ȼ�ȡ���ϣֵ��
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                for (int i = buckets[hashCode % buckets.Length]; i >= 0; i = entries[i].next) {//���ݹ�ϣ�ı���������������
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// ��ʼ����������
        /// </summary>
        /// <param name="capacity">������С</param>
        private void Initialize(int capacity) {
            int size = HashHelpers.GetPrime(capacity);//ͨ��hashֵ���ش�С
            buckets = new int[size];//��ʼ��Ͱ����
            for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;//��Ͱ����ȫ����ʼ��Ϊ-1
            entries = new Entry[size];//��ʼ����Ŀ����
            freeList = -1;
        }

        /// <summary>
        /// ��һ���ֵ�����ֵ����
        /// </summary>
        /// <param name="key">����ļ�</param>
        /// <param name="value">�����ֵ</param>
        /// <param name="add">�Ƿ������ٴ������ͬ�ļ�</param>
        private void Insert(TKey key, TValue value, bool add) {
        
            if( key == null ) {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets == null) Initialize(0);
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;//��ȡhashCode
            int targetBucket = hashCode % buckets.Length;//��ȡĿ�����ɭͰ

#if FEATURE_RANDOMIZED_STRING_HASHING
            int collisionCount = 0;//������ײ����
#endif

            for (int i = buckets[targetBucket]; i >= 0; i = entries[i].next) {//���й�ϣ����
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) {
                    if (add) { //���������ӣ����׳������쳣
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_AddingDuplicate);
                    }
                    entries[i].value = value;//����ֵ
                    version++;
                    return;//��������
                } 

#if FEATURE_RANDOMIZED_STRING_HASHING
                collisionCount++;//��ײ������1
#endif
            }
            int index;
            if (freeCount > 0) {//����ͷ���������0����
                index = freeList;//��������
                freeList = entries[index].next;//��λfreeList�ĵ�ǰλ��
                freeCount--;//��free������һ
            }
            else {//���С��0
                if (count == entries.Length)//������ϳ��ȵ�����������ʵ��size����
                {
                    Resize();
                    targetBucket = hashCode % buckets.Length;//��������Ͱ
                }
                index = count;//����������Ϊ�ֵ�����
                count++;
            }

            //������Ŀ����������Ϣ
            entries[index].hashCode = hashCode;
            entries[index].next = buckets[targetBucket];
            entries[index].key = key;
            entries[index].value = value;
            buckets[targetBucket] = index;
            version++;

#if FEATURE_RANDOMIZED_STRING_HASHING

#if FEATURE_CORECLR
            // In case we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
            // in this case will be EqualityComparer<string>.Default.
            // Note, randomized string hashing is turned on by default on coreclr so EqualityComparer<string>.Default will 
            // be using randomized string hashing

            if (collisionCount > HashHelpers.HashCollisionThreshold && comparer == NonRandomizedStringEqualityComparer.Default) 
            {
                comparer = (IEqualityComparer<TKey>) EqualityComparer<string>.Default;
                Resize(entries.Length, true);
            }
#else
            if(collisionCount > HashHelpers.HashCollisionThreshold && HashHelpers.IsWellKnownEqualityComparer(comparer)) 
            {
                comparer = (IEqualityComparer<TKey>) HashHelpers.GetRandomizedEqualityComparer(comparer);
                Resize(entries.Length, true);
            }
#endif // FEATURE_CORECLR

#endif

        }

        /// <summary>
        /// ʵ�� System.Runtime.Serialization.ISerializable �ӿڣ�������ɷ����л�֮�����������л��¼���
        /// </summary>
        /// <param name="sender"></param>
        public virtual void OnDeserialization(Object sender) {
            SerializationInfo siInfo;
            HashHelpers.SerializationInfoTable.TryGetValue(this, out siInfo);
            
            if (siInfo==null) {
                // It might be necessary to call OnDeserialization from a container if the container object also implements
                // OnDeserialization. However, remoting will call OnDeserialization again.
                // We can return immediately if this function is called twice. 
                // Note we set remove the serialization info from the table at the end of this method.
                return;
            }            
            
            int realVersion = siInfo.GetInt32(VersionName);
            int hashsize = siInfo.GetInt32(HashSizeName);
            comparer   = (IEqualityComparer<TKey>)siInfo.GetValue(ComparerName, typeof(IEqualityComparer<TKey>));
            
            if( hashsize != 0) {
                buckets = new int[hashsize];
                for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
                entries = new Entry[hashsize];
                freeList = -1;

                KeyValuePair<TKey, TValue>[] array = (KeyValuePair<TKey, TValue>[]) 
                    siInfo.GetValue(KeyValuePairsName, typeof(KeyValuePair<TKey, TValue>[]));

                if (array==null) {
                    ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_MissingKeys);
                }

                for (int i=0; i<array.Length; i++) {
                    if ( array[i].Key == null) {
                        ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_NullKey);
                    }
                    Insert(array[i].Key, array[i].Value, true);
                }
            }
            else {
                buckets = null;
            }

            version = realVersion;
            HashHelpers.SerializationInfoTable.Remove(this);
        }

        /// <summary>
        /// ��������size��С
        /// </summary>
        private void Resize() {
            Resize(HashHelpers.ExpandPrime(count), false);
        }

        /// <summary>
        /// ��������size
        /// </summary>
        /// <param name="newSize">�µĴ�С</param>
        /// <param name="forceNewHashCodes">�Ƿ���ʹ�����µ�HashCode</param>
        private void Resize(int newSize, bool forceNewHashCodes) {
            Contract.Assert(newSize >= entries.Length);
            int[] newBuckets = new int[newSize];//�����µ�Ͱ��
            for (int i = 0; i < newBuckets.Length; i++) newBuckets[i] = -1;//�����µ�Ͱ��
            Entry[] newEntries = new Entry[newSize];//��ʼ���µ���Ŀ����
            Array.Copy(entries, 0, newEntries, 0, count);//ʵ������ĸ���
            if(forceNewHashCodes) {//���Ҫ�����µ�hashcode
                for (int i = 0; i < count; i++) {//forѭ��
                    if(newEntries[i].hashCode != -1) {
                        newEntries[i].hashCode = (comparer.GetHashCode(newEntries[i].key) & 0x7FFFFFFF);//����ÿ����Ŀ�ļ���
                    }
                }
            }
            for (int i = 0; i < count; i++) {//����Ͱ������
                if (newEntries[i].hashCode >= 0) {
                    int bucket = newEntries[i].hashCode % newSize;
                    newEntries[i].next = newBuckets[bucket];
                    newBuckets[bucket] = i;
                }
            }
            buckets = newBuckets;
            entries = newEntries;
        }

        /// <summary>
        /// ������ָ������ֵ�� Dictionary<TKey, TValue> ���Ƴ���
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key) {
            if(key == null) {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets != null) {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;//��ȡhashcode
                int bucket = hashCode % buckets.Length;
                int last = -1;
                for (int i = buckets[bucket]; i >= 0; last = i, i = entries[i].next) {//����buckets
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) {//���hashcode��ͬ�����Ҽ���ͬ
                        if (last < 0) {
                            buckets[bucket] = entries[i].next;
                        }
                        else {
                            entries[last].next = entries[i].next;
                        }
                        entries[i].hashCode = -1;
                        entries[i].next = freeList;
                        entries[i].key = default(TKey);
                        entries[i].value = default(TValue);
                        freeList = i;
                        freeCount++;
                        version++;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// ��ȡ��ָ����������ֵ
        /// </summary>
        /// <param name="key">��</param>
        /// <param name="value">Ҫ���ص�ֵ</param>
        /// <returns>�Ƿ����</returns>
        public bool TryGetValue(TKey key, out TValue value) {
            int i = FindEntry(key);
            if (i >= 0) {
                value = entries[i].value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        // This is a convenience method for the internal callers that were converted from using Hashtable.
        // Many were combining key doesn't exist and key exists but null value (for non-value types) checks.
        // This allows them to continue getting that behavior with minimal code delta. This is basically
        // TryGetValue without the out param
        internal TValue GetValueOrDefault(TKey key) {
            int i = FindEntry(key);
            if (i >= 0) {
                return entries[i].value;//������Ŀ�е�ֵ
            }
            return default(TValue);//���ط����е�Ĭ�Ϲ���ֵ
        }

        /// <summary>
        /// ��ȡ�Ƿ���ֻ��
        /// </summary>
        bool ICollection<KeyValuePair<TKey,TValue>>.IsReadOnly {
            get { return false; }
        }

        /// <summary>
        /// ���ض���������������ʼ���� ICollection<T> ��Ԫ�ظ��Ƶ�һ����ֵ�������С�
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        void ICollection<KeyValuePair<TKey,TValue>>.CopyTo(KeyValuePair<TKey,TValue>[] array, int index) {
            CopyTo(array, index);
        }

        /// <summary>
        /// ���ض���������������ʼ���� ICollection<T> ��Ԫ�ظ��Ƶ�һ�������С�
        /// </summary>
        /// <param name="array">һά���飬������ ICollection<T> ���Ƶ�Ԫ�ص�Ŀ��λ�á������������������㿪ʼ��</param>
        /// <param name="index">array �д��㿪ʼ���������Ӵ˴���ʼ���ơ�</param>
        void ICollection.CopyTo(Array array, int index) {
            if (array == null) {//����Ϊ�գ��׳��������쳣
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            }
            
            if (array.Rank != 1) {//����ά�Ȳ�Ϊ1���׳���ά���鲻֧��
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
            }

            if( array.GetLowerBound(0) != 0 ) {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
            }
            
            if (index < 0 || index > array.Length) {//����Խ��
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count) {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
            }
            
            KeyValuePair<TKey,TValue>[] pairs = array as KeyValuePair<TKey,TValue>[];//������ת��Ϊ��ֵ��
            if (pairs != null) {
                CopyTo(pairs, index);//��ֵ�Ը���
            }
            else if( array is DictionaryEntry[]) {//���������DictionaryEntry����������鸴��
                DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
                Entry[] entries = this.entries;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) {
                        dictEntryArray[index++] = new DictionaryEntry(entries[i].key, entries[i].value);
                    }
                }                
            }
            else {//����ת��Ϊobject����
                object[] objects = array as object[];
                if (objects == null) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                }

                try {
                    int count = this.count;//���ֵ��е���Ŀת��ΪKeyValuePair���ͽ������鸴��
                    Entry[] entries = this.entries;
                    for (int i = 0; i < count; i++) {
                        if (entries[i].hashCode >= 0) {
                            objects[index++] = new KeyValuePair<TKey,TValue>(entries[i].key, entries[i].value);
                        }
                    }
                }
                catch(ArrayTypeMismatchException) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                }
            }
        }

        /// <summary>
        /// ��ȡö��Ԫ�صĵ�����
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }
    
        /// <summary>
        /// ��ȡ�Ƿ�֧��ͬ������֧�֣�
        /// </summary>
        bool ICollection.IsSynchronized {
            get { return false; }
        }

        /// <summary>
        /// ��ȡͬ������
        /// </summary>
        object ICollection.SyncRoot { 
            get { 
                if( _syncRoot == null) {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);    
                }
                return _syncRoot; 
            }
        }

        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// ��ȡ�Ƿ��ǹ̶���С��
        /// </summary>
        bool IDictionary.IsFixedSize {
            get { return false; }
        }

        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// ��ȡ�Ƿ���ֻ����
        /// </summary>
        bool IDictionary.IsReadOnly {
            get { return false; }
        }

        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// ��ȡIDictionary.Keys����
        /// </summary>
        ICollection IDictionary.Keys {
            get { return (ICollection)Keys; }
        }
        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// ��ȡIDictionary.Values����
        /// </summary>
        ICollection IDictionary.Values {
            get { return (ICollection)Values; }
        }
    
        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// ��ȡ�������ֵ��е�ֵ
        /// </summary>
        /// <param name="key">�ֵ��е�ֵ</param>
        /// <returns></returns>
        object IDictionary.this[object key] {
            get { 
                if( IsCompatibleKey(key)) { //���жϼ��Ƿ���ݣ����ؼ�������Ӧ����ֵ              
                    int i = FindEntry((TKey)key);
                    if (i >= 0) { 
                        return entries[i].value;                
                    }
                }
                return null;
            }
            set { //������Ӧ����ֵ                
                if (key == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
                }
                ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

                try {
                    TKey tempKey = (TKey)key;
                    try {
                        this[tempKey] = (TValue)value; 
                    }
                    catch (InvalidCastException) { 
                        ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));   
                    }
                }
                catch (InvalidCastException) { 
                    ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
                }
            }
        }

        /// <summary>
        /// �жϼ��Ƿ��뼯���еļ�����
        /// </summary>
        /// <param name="key">ƥ��ļ�</param>
        /// <returns></returns>
        private static bool IsCompatibleKey(object key) {
            if( key == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
                }
            return (key is TKey); 
        }
    
        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// IDictionary.Add
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void IDictionary.Add(object key, object value) {            
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);                          
            }
            ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

            try {
                TKey tempKey = (TKey)key;

                try {
                    Add(tempKey, (TValue)value);//������IDictionary�ӵ�ʵ�֣����Բ������ܲ�һ�����ϣ������Ҫ��������ת�������ת��ʧ�ܣ����׳��쳣
                }
                catch (InvalidCastException) { 
                    ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));   
                }
            }
            catch (InvalidCastException) { 
                ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
            }
        }
    
        /// <summary>
        /// ��ʾ�ӿ�ʵ��
        /// IDictionary.Contains�Ƿ��ֵ��а�����Ӧ��
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool IDictionary.Contains(object key) {    
            if(IsCompatibleKey(key)) {
                return ContainsKey((TKey)key);
            }
       
            return false;
        }
    
        IDictionaryEnumerator IDictionary.GetEnumerator() {
            return new Enumerator(this, Enumerator.DictEntry);
        }
    
        void IDictionary.Remove(object key) {            
            if(IsCompatibleKey(key)) {
                Remove((TKey)key);
            }
        }

        [Serializable]
        public struct Enumerator: IEnumerator<KeyValuePair<TKey,TValue>>,
            IDictionaryEnumerator
        {
            /// <summary>
            /// ��ֵ���ֵ�
            /// </summary>
            private Dictionary<TKey,TValue> dictionary;
            /// <summary>
            /// �汾
            /// </summary>
            private int version;
            /// <summary>
            /// ����
            /// </summary>
            private int index;
            /// <summary>
            /// ��ǰ��ֵ��
            /// </summary>
            private KeyValuePair<TKey,TValue> current;
            private int getEnumeratorRetType;  // What should Enumerator.Current return?
            
            /// <summary>
            /// �ֵ���Ŀ
            /// </summary>
            internal const int DictEntry = 1;
            /// <summary>
            /// ��ֵ��
            /// </summary>
            internal const int KeyValuePair = 2;

            /// <summary>
            /// ���캯��
            /// </summary>
            /// <param name="dictionary">��ֵ���ֵ�</param>
            /// <param name="getEnumeratorRetType">ö������</param>
            internal Enumerator(Dictionary<TKey,TValue> dictionary, int getEnumeratorRetType) {
                this.dictionary = dictionary;
                version = dictionary.version;
                index = 0;
                this.getEnumeratorRetType = getEnumeratorRetType;
                current = new KeyValuePair<TKey, TValue>();
            }

            /// <summary>
            /// ��ö�����ƽ������ϵ���һ��Ԫ��
            /// </summary>
            /// <returns>���ö�����ѳɹ����ƽ�����һ��Ԫ�أ���Ϊ true�����ö�������ݵ����ϵ�ĩβ����Ϊ false��</returns>
            public bool MoveNext() {
                if (version != dictionary.version) {
                    ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while ((uint)index < (uint)dictionary.count) {//��������
                    if (dictionary.entries[index].hashCode >= 0) {
                        current = new KeyValuePair<TKey, TValue>(dictionary.entries[index].key, dictionary.entries[index].value);//���õ�ǰö�ٶ���
                        index++;
                        return true;
                    }
                    index++;
                }

                index = dictionary.count + 1;//�������λ������ĩβ���򽫵�ǰö������Ϊ�����ݶ��󣬷���Ϊfalse
                current = new KeyValuePair<TKey, TValue>();
                return false;
            }

            public KeyValuePair<TKey,TValue> Current {
                get { return current; }
            }

            public void Dispose() {
            }

            object IEnumerator.Current {
                get { 
                    if( index == 0 || (index == dictionary.count + 1)) {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                    }      

                    if (getEnumeratorRetType == DictEntry) {
                        return new System.Collections.DictionaryEntry(current.Key, current.Value);
                    } else {
                        return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    }
                }
            }

            void IEnumerator.Reset() {
                if (version != dictionary.version) {
                    ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                }

                index = 0;
                current = new KeyValuePair<TKey, TValue>();    
            }

            DictionaryEntry IDictionaryEnumerator.Entry {
                get { 
                    if( index == 0 || (index == dictionary.count + 1)) {
                         ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                    }                        
                    
                    return new DictionaryEntry(current.Key, current.Value); 
                }
            }

            object IDictionaryEnumerator.Key {
                get { 
                    if( index == 0 || (index == dictionary.count + 1)) {
                         ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                    }                        
                    
                    return current.Key; 
                }
            }

            object IDictionaryEnumerator.Value {
                get { 
                    if( index == 0 || (index == dictionary.count + 1)) {
                         ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                    }                        
                    
                    return current.Value; 
                }
            }
        }

        /// <summary>
        /// ������
        /// </summary>
        [DebuggerTypeProxy(typeof(Mscorlib_DictionaryKeyCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]        
        [Serializable]
        public sealed class KeyCollection: ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>
        {
            private Dictionary<TKey,TValue> dictionary;//�ֵ��ֵ��

            /// <summary>
            /// ���캯��
            /// </summary>
            /// <param name="dictionary">�ֵ��ֵ��</param>
            public KeyCollection(Dictionary<TKey,TValue> dictionary) {
                if (dictionary == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
                }
                this.dictionary = dictionary;
            }

            /// <summary>
            /// ��ȡ������
            /// </summary>
            /// <returns></returns>
            public Enumerator GetEnumerator() {
                return new Enumerator(dictionary);
            }

            /// <summary>
            /// ���鸴��
            /// </summary>
            /// <param name="array"></param>
            /// <param name="index"></param>
            public void CopyTo(TKey[] array, int index) {
                if (array == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (index < 0 || index > array.Length) {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }
                
                int count = dictionary.count;
                Entry[] entries = dictionary.entries;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].key;
                }
            }

            public int Count {
                get { return dictionary.Count; }
            }

            bool ICollection<TKey>.IsReadOnly {
                get { return true; }
            }

            void ICollection<TKey>.Add(TKey item){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
            }
            
            void ICollection<TKey>.Clear(){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
            }

            bool ICollection<TKey>.Contains(TKey item){
                return dictionary.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
                return false;
            }
            
            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(dictionary);                
            }

            void ICollection.CopyTo(Array array, int index) {
                if (array==null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (array.Rank != 1) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
                }

                if( array.GetLowerBound(0) != 0 ) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
                }

                if (index < 0 || index > array.Length) {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }
                
                TKey[] keys = array as TKey[];
                if (keys != null) {
                    CopyTo(keys, index);
                }
                else {
                    object[] objects = array as object[];
                    if (objects == null) {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }
                                         
                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    try {
                        for (int i = 0; i < count; i++) {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].key;
                        }
                    }                    
                    catch(ArrayTypeMismatchException) {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized {
                get { return false; }
            }

            Object ICollection.SyncRoot { 
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            [Serializable]
            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator
            {
                private Dictionary<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TKey currentKey;
            
                internal Enumerator(Dictionary<TKey, TValue> dictionary) {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentKey = default(TKey);                    
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    if (version != dictionary.version) {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)index < (uint)dictionary.count) {
                        if (dictionary.entries[index].hashCode >= 0) {
                            currentKey = dictionary.entries[index].key;
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = dictionary.count + 1;
                    currentKey = default(TKey);
                    return false;
                }
                
                public TKey Current {
                    get {                        
                        return currentKey;
                    }
                }

                Object System.Collections.IEnumerator.Current {
                    get {                      
                        if( index == 0 || (index == dictionary.count + 1)) {
                             ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                        }                        
                        
                        return currentKey;
                    }
                }
                
                void System.Collections.IEnumerator.Reset() {
                    if (version != dictionary.version) {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);                        
                    }

                    index = 0;                    
                    currentKey = default(TKey);
                }
            }                        
        }

        [DebuggerTypeProxy(typeof(Mscorlib_DictionaryValueCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class ValueCollection: ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>
        {
            private Dictionary<TKey,TValue> dictionary;

            public ValueCollection(Dictionary<TKey,TValue> dictionary) {
                if (dictionary == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
                }
                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator() {
                return new Enumerator(dictionary);                
            }

            public void CopyTo(TValue[] array, int index) {
                if (array == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (index < 0 || index > array.Length) {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }
                
                int count = dictionary.count;
                Entry[] entries = dictionary.entries;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].value;
                }
            }

            public int Count {
                get { return dictionary.Count; }
            }

            bool ICollection<TValue>.IsReadOnly {
                get { return true; }
            }

            void ICollection<TValue>.Add(TValue item){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Remove(TValue item){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
                return false;
            }

            void ICollection<TValue>.Clear(){
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Contains(TValue item){
                return dictionary.ContainsValue(item);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(dictionary);                
            }

            void ICollection.CopyTo(Array array, int index) {
                if (array == null) {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (array.Rank != 1) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
                }

                if( array.GetLowerBound(0) != 0 ) {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
                }

                if (index < 0 || index > array.Length) { 
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                
                TValue[] values = array as TValue[];
                if (values != null) {
                    CopyTo(values, index);
                }
                else {
                    object[] objects = array as object[];
                    if (objects == null) {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }

                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    try {
                        for (int i = 0; i < count; i++) {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].value;
                        }
                    }
                    catch(ArrayTypeMismatchException) {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized {
                get { return false; }
            }

            Object ICollection.SyncRoot { 
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            [Serializable]
            public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator
            {
                private Dictionary<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TValue currentValue;
            
                internal Enumerator(Dictionary<TKey, TValue> dictionary) {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose() {
                }

                public bool MoveNext() {                    
                    if (version != dictionary.version) {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }
                    
                    while ((uint)index < (uint)dictionary.count) {
                        if (dictionary.entries[index].hashCode >= 0) {
                            currentValue = dictionary.entries[index].value;
                            index++;
                            return true;
                        }
                        index++;
                    }
                    index = dictionary.count + 1;
                    currentValue = default(TValue);
                    return false;
                }
                
                public TValue Current {
                    get {                        
                        return currentValue;
                    }
                }

                Object System.Collections.IEnumerator.Current {
                    get {                      
                        if( index == 0 || (index == dictionary.count + 1)) {
                             ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);                        
                        }                        
                        
                        return currentValue;
                    }
                }
                
                void System.Collections.IEnumerator.Reset() {
                    if (version != dictionary.version) {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }
                    index = 0;                    
                    currentValue = default(TValue);
                }
            }
        }
    }
}
