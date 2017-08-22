// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  ArrayList
** 
** <OWNER>[....]</OWNER>
**
**
** Purpose: Implements a dynamically sized List as an array,
**          and provides many convenience methods for treating
**          an array as an IList.
**
** 
===========================================================*/
namespace System.Collections {
    using System;
    using System.Runtime;
    using System.Security;
    using System.Security.Permissions;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;

    // ʵ��һ����Ӧ�ɱ��б�,ʹ��һ�������������洢Ԫ�ء�
    // һ��ArrayList����,������ڲ�����ĳ��ȡ�
    // Ԫ�ر���ӵ�һ��ArrayList,ArrayList���Զ����ӵ�����Ҫ�����·����ڲ����顣
    /// <summary>
    /// ʹ�ô�С�������Ҫ��̬���ӵ�������ʵ�� IList �ӿڡ�
    /// </summary>
#if FEATURE_CORECLR
    [FriendAccessAllowed]
#endif
    [DebuggerTypeProxy(typeof(System.Collections.ArrayList.ArrayListDebugView))]   
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ArrayList : IList, ICloneable
    {
        /// <summary>
        /// items��������
        /// </summary>
        private Object[] _items;
        /// <summary>
        /// ʵ�ʰ�����Ԫ����
        /// </summary>
        [ContractPublicPropertyName("Count")]
        private int _size;
        /// <summary>
        /// ArrayList����汾
        /// </summary>
        private int _version;
        /// <summary>
        /// ͬ�����������л���
        /// </summary>
        [NonSerialized]
        private Object _syncRoot;
        
        /// <summary>
        /// Ĭ������
        /// </summary>
        private const int _defaultCapacity = 4;
        /// <summary>
        /// ��ʼ���ն�������
        /// </summary>
        private static readonly Object[] emptyArray = EmptyArray<Object>.Value; 
    
        /// <summary>
        /// ע��:������캯����һ����ٵĹ��캯��,��û����SyncArrayList����ʹ�á�
        /// </summary>
        /// <param name="trash"></param>
        internal ArrayList( bool trash )
        {
        }

        // ����һ��ArrayList������ǿյ�,������Ϊ�㡣�ڵ�һ��Ԫ����ӵ��б����������_defaultCapacity,Ȼ�������Ҫ���������ı�����
        /// <summary>
        /// ��ʼ�� ArrayList �����ʵ������ʵ��Ϊ�ղ��Ҿ���Ĭ�ϳ�ʼ������
        /// </summary>
        public ArrayList() {
            _items = emptyArray;  
        }

        // ����һ��������ĳ�ʼ����ArrayList���б�����ǿյ�,�����пռ����������Ԫ��֮ǰ��Ҫ���·��䡣
        /// <summary>
        /// ��ʼ�� ArrayList �����ʵ������ʵ��Ϊ�ղ��Ҿ���ָ���ĳ�ʼ������
        /// </summary>
        /// <param name="capacity">��ʼ����</param>
         public ArrayList(int capacity) {
             if (capacity < 0) //�����ʼ����С���㣬���׳�ArgumentOutOfRangeException�쳣
                 throw new ArgumentOutOfRangeException("capacity", Environment.GetResourceString("ArgumentOutOfRange_MustBeNonNegNum", "capacity"));
             Contract.EndContractBlock();

             if (capacity == 0)//�����ʼ����Ϊ�㣬������Ϊ������
                 _items = emptyArray;
             else//��ʼ��Object����
                 _items = new Object[capacity];
        }

         // ����һ��ArrayList,���Ƹ������ϵ����ݡ����б�Ĵ�С�������������ڸ������ϵĴ�С��
        /// <summary>
         /// ��ʼ�� ArrayList �����ʵ������ʵ��������ָ�����ϸ��Ƶ�Ԫ�أ������븴�Ƶ�Ԫ������ͬ�ĳ�ʼ������
        /// </summary>
        /// <param name="c"></param>
        public ArrayList(ICollection c) {
            if (c == null)//�������Ϊ�գ����׳�ArgumentNullException�쳣
                throw new ArgumentNullException("c", Environment.GetResourceString("ArgumentNull_Collection"));
            Contract.EndContractBlock();

            int count = c.Count;//��ȡ���ϵ�����
            if (count == 0)//�����������Ϊ�㣬���ʼ��Ϊ������
            {
                _items = emptyArray;
            }
            else {
                _items = new Object[count];//��ʼ��Object����
                AddRange(c);
            }
        }
    
        // ��ȡ����������б�������������Ĵ�СΪ�ڲ����������������Ŀ����������ʱ���ڲ��б����������������������
        /// <summary>
        /// ��ȡ������ ArrayList �ɰ�����Ԫ������
        /// </summary>
         public virtual int Capacity {
            get {
                Contract.Ensures(Contract.Result<int>() >= Count);
                return _items.Length;
            }
            set {
                if (value < _size) {
                    throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_SmallCapacity"));
                }
                Contract.Ensures(Capacity >= 0);
                Contract.EndContractBlock();
                //���ǲ�����°汾�ŵ����Ǹı��������һЩ���е�Ӧ�ó��������ڴˡ�
                if (value != _items.Length) {//��ֵ�����ڵ�ǰֵʱ�����������С
                    if (value > 0) {//���ֵ����0���������������
                        Object[] newItems = new Object[value];
                        if (_size > 0) { 
                            Array.Copy(_items, 0, newItems, 0, _size);//����Array�������ݸ���
                        }
                        _items = newItems;
                    }
                    else {//���С���㣬����������Ϊ����Ϊ0������
                        _items = new Object[_defaultCapacity];
                    }
                }            
            }
        }

        /// <summary>
         /// ��ȡ ArrayList ��ʵ�ʰ�����Ԫ������
        /// </summary>
        public virtual int Count {
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _size;
            }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ ArrayList �Ƿ���й̶���С��
        /// </summary>
        public virtual bool IsFixedSize {
            get { return false; }
        }

            
        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ ArrayList �Ƿ�Ϊֻ����
        /// </summary>
        public virtual bool IsReadOnly {
            get { return false; }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ�Ƿ�ͬ���� ArrayList �ķ��ʣ��̰߳�ȫ����
        /// </summary>
        public virtual bool IsSynchronized {
            get { return false; }
        }
    
        /// <summary>
        /// ��ȡ������ͬ���� ArrayList �ķ��ʵĶ���
        /// </summary>
        public virtual Object SyncRoot {
            get { 
                if( _syncRoot == null) {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);    
                }
                return _syncRoot; 
            }
        }
    
        /// <summary>
        /// ��ȡ������ָ����������Ԫ�ء�
        /// </summary>
        /// <param name="index">����</param>
        /// <returns>��ǰ����������</returns>
 
        public virtual Object this[int index] {
            get {
                if (index < 0 || index >= _size) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();
                return _items[index];
            }
            set {
                if (index < 0 || index >= _size) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();
                _items[index] = value;//����������ֵ
                _version++;//���Ӱ汾
            }
        }
    
        // ����һ���ض�IList ArrayList��װ��
        // �Ⲣ������IList������,��ֻ�а�װIList�ӿڡ�
        // ���ԶԵײ��б���κθ��Ľ�Ӱ��ArrayList��
        // �������ŤתIList���ӷ�Χ,����Ҫʹ��һ��ͨ��BinarySearch�򷽷�û��ʵ��һ���Լ�,�⽫�����õġ�
        // Ȼ��,������Щ������ͨ�õ�,���ܿ��ܲ�����ô�ò�������IList����
        /// <summary>
        /// ����һ���ض�IList ArrayList��װ����
        /// </summary>
        /// <param name="list">List�ӿ�</param>
        /// <returns></returns>
        public static ArrayList Adapter(IList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();
            return new IListWrapper(list);
        }
        
        /// <summary>
        /// ������������ӵ�����б��С��б�Ĵ�С����1�������Ҫ,�б�������������Ԫ��֮ǰ����һ����
        /// </summary>
        /// <param name="value">���Ӷ���</param>
        /// <returns>����ʵ�ʰ�����С</returns>
        public virtual int Add(Object value) {
            Contract.Ensures(Contract.Result<int>() >= 0);
            if (_size == _items.Length) EnsureCapacity(_size + 1);
            _items[_size] = value;
            _version++;
            return _size++;
        }

        //���������ϵ�Ԫ����ӵ����б�Ľ����������Ҫ,�б���������ӵ�����֮ǰ���������³ߴ�,�ĸ��Ƚϴ�
        /// <summary>
        /// ��� ICollection ��Ԫ�ص� ArrayList ��ĩβ��
        /// </summary>
        /// <param name="c">����</param>
        public virtual void AddRange(ICollection c) {
            InsertRange(_size, c);
        }
    
        // Searches a section of the list for a given element using a binary search
        // algorithm. Elements of the list are compared to the search value using
        // the given IComparer interface. If comparer is null, elements of
        // the list are compared to the search value using the IComparable
        // interface, which in that case must be implemented by all elements of the
        // list and the given search value. This method assumes that the given
        // section of the list is already sorted; if this is not the case, the
        // result will be incorrect.
        //
        // The method returns the index of the given value in the list. If the
        // list does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value. This is also the index at which
        // the search value should be inserted into the list in order for the list
        // to remain sorted.
        // 
        // The method uses the Array.BinarySearch method to perform the
        // search.
        // 
        /// <summary>
        /// ʹ��ָ���ıȽ���������������� ArrayList ������Ԫ�أ������ظ�Ԫ�ش��㿪ʼ��������
        /// </summary>
        /// <param name="index">����</param>
        /// <param name="count">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="comparer">�Ƚ���</param>
        /// <returns></returns>
        public virtual int BinarySearch(int index, int count, Object value, IComparer comparer) {
            if (index < 0)//�������С���㣬���׳�ArgumentOutOfRange_NeedNonNegNum index���쳣
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)//�����������count�����׳�ArgumentOutOfRange_NeedNonNegNum count���쳣
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.Ensures(Contract.Result<int>() < index + count);
            Contract.EndContractBlock();
    
            return Array.BinarySearch((Array)_items, index, count, value, comparer);//����ֵ��ArrayList�е�����
        }
        
        /// <summary>
        /// ʹ��Ĭ�ϱȽ��������������������б��һ��Ԫ�ز�����Ԫ�صĴ��㿪ʼ��������
        /// </summary>
        /// <param name="value">ֵ</param>
        /// <returns></returns>
        public virtual int BinarySearch(Object value)
        {
            Contract.Ensures(Contract.Result<int>() < Count);
            return BinarySearch(0, Count, value, null);
        }

        /// <summary>
        /// ʹ�ø����Ƚ��������������������б��һ��Ԫ�ز�����Ԫ�صĴ��㿪ʼ��������
        /// </summary>
        /// <param name="value">ֵ</param>
        /// <param name="comparer">�Ƚ���</param>
        /// <returns>����</returns>
        public virtual int BinarySearch(Object value, IComparer comparer)
        {
            Contract.Ensures(Contract.Result<int>() < Count);
            return BinarySearch(0, Count, value, comparer);
        }

    
        /// <summary>
        /// �� ArrayList ���Ƴ�����Ԫ�ء�
        /// </summary>
        public virtual void Clear() {
            if (_size > 0)
            {
                Array.Clear(_items, 0, _size); // ����Ҫҽ��,����������Ա�gc���յ�Ԫ�����á�
                _size = 0;
            }
            _version++;
        }

        //��¡���ArrayList,��һ��ǳ������(һ�������ж���������ArrayList,����ָ��Ķ����ǿ�¡)��
        /// <summary>
        /// ����һ��ArrayList��ǳ������(��ֻ��������)
        /// </summary>
        /// <returns>ArrayList</returns>
        public virtual Object Clone()
        {
            Contract.Ensures(Contract.Result<Object>() != null);
            ArrayList la = new ArrayList(_size);//�����µ�ArrayList
            la._size = _size;
            la._version = _version;
            Array.Copy(_items, 0, la._items, 0, _size);//����������������
            return la;
        }
    
        // ��������true,���ָ����Ԫ����ArrayList��һ������,O(n)������ƽ�����ɵ���item.Equals()��
        /// <summary>
        /// ȷ��ĳԪ���Ƿ��� ArrayList �С�
        /// </summary>
        /// <param name="item">Ԫ�ض���</param>
        /// <returns></returns>
        public virtual bool Contains(Object item) {
            if (item==null) {//���Ԫ�ض���Ϊ�գ����ж�ArrayList���Ƿ��ÿ�Ԫ��
                for(int i=0; i<_size; i++)
                    if (_items[i]==null)
                        return true;
                return false;
            }
            else {
                for(int i=0; i<_size; i++)
                    if ( (_items[i] != null) && (_items[i].Equals(item)) )//Ԫ�ز�Ϊ�գ���Ԫ�ض�����ͬ
                        return true;
                return false;
            }
        }

        // ���������б��Ƶ�����,��������������͡�
        /// <summary>
        /// ��Ŀ������Ŀ�ͷ��ʼ�������� ArrayList ���Ƶ�һά���� Array��
        /// </summary>
        /// <param name="array"></param>
        public virtual void CopyTo(Array array) {
            CopyTo(array, 0);
        }

        // ���������б��Ƶ�����,��������������͡�
        /// <summary>
        /// ���������б��Ƶ�һ�����ݵ�һά����,��Ŀ���ָ���������顣
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public virtual void CopyTo(Array array, int arrayIndex) {
            if ((array != null) && (array.Rank != 1))//���arrayΪ�գ���array��ά�Ȳ�Ϊ1�����׳�Arg_RankMultiDimNotSupported
                throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
            Contract.EndContractBlock();
            // ί������Array.Copy�����顣
            Array.Copy(_items, 0, array, arrayIndex, _size);
        }

        // ������ArrayList����һά�����е�Ԫ��,��Ŀ���ָ���������顣
        // 
        /// <summary>
        /// ��Ŀ�������ָ����������ʼ����һ����Χ��Ԫ�ش� System.Collections.ArrayList ���Ƶ����ݵ�һά System.Array��
        /// </summary>
        /// <param name="index">Դ System.Collections.ArrayList �и��ƿ�ʼλ�õĴ��㿪ʼ��������</param>
        /// <param name="array">��Ϊ�� System.Collections.ArrayList ���Ƶ�Ԫ�ص�Ŀ��λ�õ�һά System.Array��System.Array������д��㿪ʼ��������</param>
        /// <param name="arrayIndex">array �д��㿪ʼ�����������ڴ˴���ʼ���ơ�</param>
        /// <param name="count">Ҫ���Ƶ�Ԫ������</param>
        public virtual void CopyTo(int index, Array array, int arrayIndex, int count) {
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            if ((array != null) && (array.Rank != 1))
                throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
            Contract.EndContractBlock();
            // Delegate rest of error checking to Array.Copy.
            Array.Copy(_items, index, array, arrayIndex, count);
        }

        // ȷ������б�����������Ǹ�������Сֵ�����С����Сֵ�б�ĵ�����������,�����ǵ�ǰ������������ӵ�����,�ĸ��Ƚϴ�
        /// <summary>
        /// ��֤���������ϸ�
        /// </summary>
        /// <param name="min">��С��������</param>
        private void EnsureCapacity(int min) {
            if (_items.Length < min) {//���Obejct����С����Сֵ
                int newCapacity = _items.Length == 0? _defaultCapacity: _items.Length * 2;//����������ݲ�Ϊ�㣬�������С����Ϊԭ��������
                // ���������֮ǰ�������б����ӵ���󣬲�����µ��������ݴ�С
                if ((uint)newCapacity > Array.MaxArrayLength) newCapacity = Array.MaxArrayLength;
                if (newCapacity < min) newCapacity = min;
                Capacity = newCapacity;//�����µ��������ݴ�С
            }
        }

        // ���ذ�װ���̶��ڵ�ǰ�б�Ĵ�С����ӻ�ɾ����Ŀ�Ĳ����ͻ�ʧ��,����,�滻��Ʒ�Ǳ�����ġ�
        /// <summary>
        /// ����һ��IList��װ�̶���С��
        /// </summary>
        /// <param name="list">Ҫ��װ�� System.Collections.IList��</param>
        /// <returns>���й̶���С�� System.Collections.IList ��װ��</returns>
        /// <exception cref="System.ArgumentNullException">list Ϊ null��</exception>
        public static IList FixedSize(IList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<IList>() != null);
            Contract.EndContractBlock();
            return new FixedSizeList(list);
        }

        /// <summary>
        /// ���ؾ��й̶���С�� System.Collections.ArrayList ��װ��
        /// </summary>
        /// <param name="list">Ҫ��װ�� System.Collections.ArrayList</param>
        /// <returns>���й̶���С�� System.Collections.ArrayList ��װ��</returns>
        /// <exception cref="System.ArgumentNullException"> list Ϊ null��</exception>
        public static ArrayList FixedSize(ArrayList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();
            return new FixedSizeArrayList(list);
        }

        //����һ��ö����,����б������ɾ��Ԫ�ء�����޸��ڽ���,��Ȼö���б�ö������MoveNext��GetObject�������׳��쳣��
        /// <summary>
        /// ������������ ArrayList ��ö������
        /// </summary>
        /// <returns>�������� System.Collections.ArrayList �� System.Collections.IEnumerator��</returns>
        public virtual IEnumerator GetEnumerator() {
            Contract.Ensures(Contract.Result<IEnumerator>() != null);
            return new ArrayListEnumeratorSimple(this);
        }
    
        /// <summary>
        /// ���� System.Collections.ArrayList ��ĳ����Χ�ڵ�Ԫ�ص�ö������
        /// </summary>
        /// <param name="index">ö����Ӧ���õ� System.Collections.ArrayList ���ִ��㿪ʼ����ʼ������</param>
        /// <param name="count">ö����Ӧ���õ� System.Collections.ArrayList �����е�Ԫ������</param>
        /// <returns>System.Collections.ArrayList ��ָ����Χ�ڵ�Ԫ�ص� System.Collections.IEnumerator��</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index С���㡣- �� -count С���㡣</exception>
        public virtual IEnumerator GetEnumerator(int index, int count) {
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.Ensures(Contract.Result<IEnumerator>() != null);
            Contract.EndContractBlock();
    
            return new ArrayListEnumerator(this, index, count);
        }
    
        /// <summary>
        /// ����ָ���� System.Object������������ System.Collections.ArrayList �е�һ��ƥ����Ĵ��㿪ʼ��������
        /// </summary>
        /// <param name="value">Ҫ��ArrayList�в��ҵ�Object.��ֵ����Ϊnull</param>
        /// <returns>���������ArrayList���ҵ�value�ĵ�һ��ƥ����򷵻ظ���Ĵ��㿪ʼ������������Ϊ-1</returns>
        public virtual int IndexOf(Object value) {
            Contract.Ensures(Contract.Result<int>() < Count);
            return Array.IndexOf((Array)_items, value, 0, _size);
        }
    
        // ���ص�һ�γ��ֵ�ָ���ĸ���ֵ�б���ǰ�����б�ʱ,��ָ��startIndex,������Ԫ�ص��������б��Ԫ��ʹ�ö������ȷ�������ֵ���бȽϡ�
        // This method uses the Array.IndexOf method to perform the
        // search.
        /// <summary>
        /// ����ָ���� System.Object�������� System.Collections.ArrayList �д�ָ�����������һ��Ԫ�ص�Ԫ�ط�Χ�ڵ�һ��ƥ����Ĵ��㿪ʼ��������
        /// </summary>
        /// <param name="value">Ҫ��ArrayList�в��ҵ�Object����ֵ����Ϊnull</param>
        /// <param name="startIndex">���㿪ʼ����������ʼ���������б��� 0���㣩Ϊ��Чֵ��</param>
        /// <returns>����� System.Collections.ArrayList �д� startIndex �����һ��Ԫ�ص�Ԫ�ط�Χ���ҵ� value �ĵ�һ��ƥ�����Ϊ����Ĵ��㿪ʼ������������Ϊ-1</returns>
        /// <exception cref="ArgumentOutOfRange_Index">startIndex ���� System.Collections.ArrayList ����Ч������Χ�ڡ�</exception>
        public virtual int IndexOf(Object value, int startIndex) {
            if (startIndex > _size)
                throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();
            return Array.IndexOf((Array)_items, value, startIndex, _size - startIndex);
        }

        /// <summary>
        /// ����ָ���� Object�������� ArrayList �д�ָ��������ʼ������ָ��Ԫ�������ⲿ��Ԫ���е�һ��ƥ����Ĵ��㿪ʼ������
        /// </summary>
        /// <param name="value">Ҫ�� System.Collections.ArrayList �в��ҵ� System.Object����ֵ����Ϊ null��</param>
        /// <param name="startIndex">���㿪ʼ����������ʼ���������б��� 0���㣩Ϊ��Чֵ��</param>
        /// <param name="count">Ҫ�����Ĳ����е�Ԫ������</param>
        /// <returns>����� System.Collections.ArrayList �д� startIndex ��ʼ������ count ��Ԫ�ص�Ԫ�ط�Χ���ҵ� value�ĵ�һ��ƥ�����Ϊ����Ĵ��㿪ʼ������������Ϊ -1��</returns>
        /// <exception cref="ArgumentOutOfRange_Index">startIndex ���� System.Collections.ArrayList ����Ч������Χ�ڡ�- �� -count С���㡣- �� -startIndex�� count δָ�� System.Collections.ArrayList �е���Ч���֡�</exception>
        public virtual int IndexOf(Object value, int startIndex, int count) {
            if (startIndex > _size)
                throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            if (count <0 || startIndex > _size - count) throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_Count"));
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();
            return Array.IndexOf((Array)_items, value, startIndex, count);
        }
    
        // Inserts an element into this list at a given index. The size of the list
        // is increased by one. If required, the capacity of the list is doubled
        // before inserting the new element.
        // 
        /// <summary>
        /// ��Ԫ�ز��� System.Collections.ArrayList ��ָ����������
        /// </summary>
        /// <param name="index">���㿪ʼ��������Ӧ�ڸ�λ�ò��� value��</param>
        /// <param name="value">Ҫ����� System.Object����ֵ����Ϊ null��</param>
        /// <exception cref="ArgumentOutOfRangeException">index С���㡣- �� -index ���� System.Collections.ArrayList.Count��</exception>
        public virtual void Insert(int index, Object value) {
            // Note that insertions at the end are legal.
            if (index < 0 || index > _size) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_ArrayListInsert"));
            //Contract.Ensures(Count == Contract.OldValue(Count) + 1);
            Contract.EndContractBlock();

            if (_size == _items.Length) EnsureCapacity(_size + 1);//��������ﵽ���ʱ����չ����
            if (index < _size) {//���������ݽ���ǳ���ƣ���index����Ϊ����������ƶ�
                Array.Copy(_items, index, _items, index + 1, _size - index);
            }
            _items[index] = value;//��index����λ���и�ֵ
            _size++;//����ʵ������
            _version++;//���Ӱ汾��
        }

        //��������ļ��ϵ�Ԫ���ڸ��������������Ҫ,�б���������ӵ�����֮ǰ���������³ߴ�,�ĸ��Ƚϴ󡣷�Χ���ܱ���ӵ��б�����ͨ���������������б�Ĵ�С��
        /// <summary>
        /// ��������ļ��ϵ�Ԫ���ڸ���������
        /// </summary>
        /// <param name="index">��index������Ϊ����</param>
        /// <param name="c">����</param>
        public virtual void InsertRange(int index, ICollection c) {
            if (c == null)//�������Ϊ�գ����׳�ArgumentNullException�쳣
                throw new ArgumentNullException("c", Environment.GetResourceString("ArgumentNull_Collection"));
            if (index < 0 || index > _size) //�������С��0�򳬹������С�����׳�ArgumentOutOfRangeException�쳣
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            //Contract.Ensures(Count == Contract.OldValue(Count) + c.Count);
            Contract.EndContractBlock();

            int count = c.Count;//��ȡ���ϴ�С
            if (count > 0) {//������ݴ�С������
                EnsureCapacity(_size + count);                
                // shift existing items
                if (index < _size) {//�������С�ڴ�С����������鸳ֵ
                    Array.Copy(_items, index, _items, index + count, _size - index);
                }

                Object[] itemsToInsert = new Object[count];//��ʼ����������
                c.CopyTo(itemsToInsert, 0);//���������ݸ�ֵ������������
                itemsToInsert.CopyTo(_items, index);//�������������ݲ��뵽_items�еĵ�index����
                _size += count;//����ʵ�ʰ�����Ԫ����
                _version++;//���Ӱ汾
            }
        }
    
        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at the end 
        // and ending at the first element in the list. The elements of the list 
        // are compared to the given value using the Object.Equals method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        // 
        public virtual int LastIndexOf(Object value)
        {
            Contract.Ensures(Contract.Result<int>() < _size);
            return LastIndexOf(value, _size - 1, _size);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // startIndex and ending at the first element in the list. The 
        // elements of the list are compared to the given value using the 
        // Object.Equals method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        /// <summary>
        /// ����ָ���� System.Object�������� System.Collections.ArrayList �а���ָ����Ԫ��������ָ��������������Ԫ�ط�Χ�����һ��ƥ����Ĵ��㿪ʼ��������
        /// </summary>
        /// <param name="value">Ҫ�� System.Collections.ArrayList �в��ҵ� System.Object����ֵ����Ϊ null��</param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        public virtual int LastIndexOf(Object value, int startIndex)
        {
            if (startIndex >= _size)
                throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();
            return LastIndexOf(value, startIndex, startIndex + 1);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // startIndex and upto count elements. The elements of
        // the list are compared to the given value using the Object.Equals
        // method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        /// <summary>
        /// ����ָ���� System.Object�������� System.Collections.ArrayList �а���ָ����Ԫ��������ָ��������������Ԫ�ط�Χ�����һ��ƥ����Ĵ��㿪ʼ��������
        /// </summary>
        /// <param name="value">Ҫ�� System.Collections.ArrayList �в��ҵ� System.Object����ֵ����Ϊ null��</param>
        /// <param name="startIndex">��������Ĵ��㿪ʼ����ʼ������</param>
        /// <param name="count">Ҫ�����Ĳ����е�Ԫ������</param>
        /// <returns>����� System.Collections.ArrayList �а��� count ��Ԫ�ء��� startIndex ����β��Ԫ�ط�Χ���ҵ� value�����һ��ƥ�����Ϊ����Ĵ��㿪ʼ������������Ϊ -1��</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">startIndex ���� System.Collections.ArrayList ����Ч������Χ�ڡ�- �� -count С���㡣- �� -startIndex�� count δָ�� System.Collections.ArrayList �е���Ч���֡�</exception>
        public virtual int LastIndexOf(Object value, int startIndex, int count) {
            if (Count != 0 && (startIndex < 0 || count < 0))
                throw new ArgumentOutOfRangeException((startIndex<0 ? "startIndex" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();

            if (_size == 0)  // Special case for an empty list
                return -1;

            if (startIndex >= _size || count > startIndex + 1) 
                throw new ArgumentOutOfRangeException((startIndex>=_size ? "startIndex" : "count"), Environment.GetResourceString("ArgumentOutOfRange_BiggerThanCollection"));

            return Array.LastIndexOf((Array)_items, value, startIndex, count);
        }
    
        /// <summary>
        /// ����ֻ���� System.Collections.IList ��װ��
        /// </summary>
        /// <param name="list">Ҫ��װ�� System.Collections.IList��</param>
        /// <returns>list ��Χ��ֻ�� System.Collections.IList ��װ��</returns>
        /// <exception cref="System.ArgumentNullException">list Ϊ null</exception>
#if FEATURE_CORECLR
        [FriendAccessAllowed]
#endif
        public static IList ReadOnly(IList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<IList>() != null);
            Contract.EndContractBlock();
            return new ReadOnlyList(list);
        }

        /// <summary>
        /// ����ֻ���� System.Collections.ArrayList ��װ��
        /// </summary>
        /// <param name="list">Ҫ��װ�� System.Collections.ArrayList��</param>
        /// <returns>list ��Χ��ֻ�� System.Collections.ArrayList ��װ��</returns>
        /// <exception cref="System.ArgumentNullException">list Ϊ null��</exception>
        public static ArrayList ReadOnly(ArrayList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();
            return new ReadOnlyArrayList(list);
        }
    
        // �Ƴ���������Ԫ�أ�list�Ĵ�С��һ
        /// <summary>
        /// �� System.Collections.ArrayList ���Ƴ��ض�����ĵ�һ��ƥ���
        /// </summary>
        /// <param name="obj">Ҫ�� System.Collections.ArrayList �Ƴ��� System.Object����ֵ����Ϊ null��</param>
        public virtual void Remove(Object obj) {
            Contract.Ensures(Count >= 0);

            int index = IndexOf(obj);//��ȡobj������λ��
            BCLDebug.Correctness(index >= 0 || !(obj is Int32), "You passed an Int32 to Remove that wasn't in the ArrayList." + Environment.NewLine + "Did you mean RemoveAt?  int: "+obj+"  Count: "+Count);
            if (index >=0) 
                RemoveAt(index);
        }
    
        /// <summary>
        /// �Ƴ� System.Collections.ArrayList ��ָ����������Ԫ�ء�
        /// </summary>
        /// <param name="index">Ҫ�Ƴ���Ԫ�صĴ��㿪ʼ��������</param>
        /// <exception cref="System.ArgumentOutOfRangeException">index С���㡣- �� -index ���ڻ���� System.Collections.ArrayList.Count��</exception>
        /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�- �� -System.Collections.ArrayList ���й̶���С��</exception>
        public virtual void RemoveAt(int index) {
            if (index < 0 || index >= _size) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            Contract.Ensures(Count >= 0);
            //Contract.Ensures(Count == Contract.OldValue(Count) - 1);
            Contract.EndContractBlock();

            _size--;//����list��С
            if (index < _size) {//����indexλ�Ժ��Ԫ����ǰ��һλ
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }
            _items[_size] = null;//����_sizeλ����Ϊnull
            _version++;//�汾��1
        }
    
        /// <summary>
        /// �� System.Collections.ArrayList ���Ƴ�һ����Χ��Ԫ�ء�
        /// </summary>
        /// <param name="index">Ҫ�Ƴ���Ԫ�صķ�Χ���㿪ʼ����ʼ������</param>
        /// <param name="count">Ҫ�Ƴ���Ԫ������</param>
        /// <exception cref="System.ArgumentOutOfRangeException:"> index С���㡣- �� -count С���㡣</exception>
        /// <exception cref="System.ArgumentException:">index �� count ����ʾ System.Collections.ArrayList ��Ԫ�ص���Ч��Χ��</exception>
        /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�- �� -System.Collections.ArrayList ���й̶���С��</exception>
        public virtual void RemoveRange(int index, int count) {
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.Ensures(Count >= 0);
            //Contract.Ensures(Count == Contract.OldValue(Count) - count);
            Contract.EndContractBlock();
    
            if (count > 0) {
                int i = _size;//��ȡ_sizeֵ
                _size -= count;//����ArrayListʵ�ʰ�����С
                if (index < _size) {//������Ԫ����ǰ�ƶ�countλ
                    Array.Copy(_items, index + count, _items, index, _size - index);
                }
                while (i > _size) _items[--i] = null;//������_sizeΪ��Ԫ������Ϊnull
                _version++;//�汾��1
            }
        }
    
        /// <summary>
        /// ���� System.Collections.ArrayList������Ԫ����ָ��ֵ�ĸ�����
        /// </summary>
        /// <param name="value">Ҫ���� System.Collections.ArrayList �ж�����ж�θ��Ƶ� System.Object����ֵ����Ϊ null��</param>
        /// <param name="count">value Ӧ�����ƵĴ�����</param>
        /// <returns>���� count ��ָ����Ԫ������ System.Collections.ArrayList�����е�����Ԫ�ض��� value �ĸ�����</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">count С���㡣</exception>
        public static ArrayList Repeat(Object value, int count) {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();

            ArrayList list = new ArrayList((count>_defaultCapacity)?count:_defaultCapacity);//���count����Ĭ����������������������Ϊcount����������ΪĬ������
            for(int i=0; i<count; i++)//��count��value��������list��
                list.Add(value);
            return list;
        }

        /// <summary>
        /// ������ System.Collections.ArrayList ��Ԫ�ص�˳��ת��
        /// </summary>
        public virtual void Reverse() {
            Reverse(0, Count);
        }

        // ��ת����б��Ԫ�ط�Χ��һ��Ԫ�ص��ø÷�����,�������ķ�Χ�ͼ�����ǰλ�����������ڽ�λ����������+(ָ��+����- i - 1)�� 
        /// <summary>
        /// ��ָ����Χ��Ԫ�ص�˳��ת��
        /// </summary>
        /// <param name="index">Ҫ��ת�ķ�Χ�Ĵ��㿪ʼ����ʼ������</param>
        /// <param name="count">Ҫ��ת�ķ�Χ�ڵ�Ԫ������</param>
        /// <exception cref="System.ArgumentOutOfRangeException:">index С���㡣- �� -count С���㡣</exception>
        /// <exception cref="System.ArgumentException:">index �� count ����ʾ System.Collections.ArrayList ��Ԫ�ص���Ч��Χ��</exception>
        /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�</exception>
        public virtual void Reverse(int index, int count) {
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();
            Array.Reverse(_items, index, count);
            _version++;
        }

        // ��Ԫ�شӸ�������������Ϊ�����ļ��ϵ�Ԫ�ء�
        /// <summary>
        /// �������е�Ԫ�ظ��Ƶ� System.Collections.ArrayList ��һ����Χ��Ԫ���ϡ�
        /// </summary>
        /// <param name="index">���㿪ʼ�� System.Collections.ArrayList �������Ӹ�λ�ÿ�ʼ���� c ��Ԫ�ء�</param>
        /// <param name="c">System.Collections.ICollection��Ҫ����Ԫ�ظ��Ƶ� System.Collections.ArrayList �С����ϱ�����Ϊnull���������԰���Ϊ null ��Ԫ�ء�</param>
        /// <exception cref="System.ArgumentOutOfRangeException:">index С���㡣- �� -index ���� c �е�Ԫ�������� System.Collections.ArrayList.Count��</exception>
        /// <exception cref="System.ArgumentNullException:">c Ϊ null��</exception>
        public virtual void SetRange(int index, ICollection c) {
            if (c==null) throw new ArgumentNullException("c", Environment.GetResourceString("ArgumentNull_Collection"));
            Contract.EndContractBlock();
            int count = c.Count;
            if (index < 0 || index > _size - count) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
            
            if (count > 0) {
                c.CopyTo(_items, index);
                _version++;
            }
        }
        
        /// <summary>
        /// ���� System.Collections.ArrayList������ʾԴ System.Collections.ArrayList ��Ԫ�ص��Ӽ���
        /// </summary>
        /// <param name="index">��Χ��ʼ���Ĵ��㿪ʼ�� System.Collections.ArrayList ������</param>
        /// <param name="count">��Χ�е�Ԫ������</param>
        /// <returns>System.Collections.ArrayList������ʾԴ System.Collections.ArrayList ��Ԫ�ص��Ӽ���</returns>
        /// <exception cref="System.ArgumentOutOfRangeException:">index С���㡣- �� -count С���㡣</exception>
        /// <exception cref="System.ArgumentException:">index �� count ����ʾ System.Collections.ArrayList ��Ԫ�ص���Ч��Χ��</exception>
        public virtual ArrayList GetRange(int index, int count) {
            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();
            return new Range(this,index, count);//��ȡ��indexλ��ArrayList�Ӽ�
        }
        
        /// <summary>
        /// ʹ��ÿ��Ԫ�ص� System.IComparable ʵ�ֶ����� System.Collections.ArrayList �е�Ԫ�ؽ�������
        /// </summary>
        public virtual void Sort()
        {
            Sort(0, Count, Comparer.Default);
        }

        /// <summary>
        /// ʹ��ָ���ıȽ��������� System.Collections.ArrayList �е�Ԫ�ؽ�������
        /// </summary>
        /// <param name="comparer">�Ƚ�Ԫ��ʱҪʹ�õ� System.Collections.IComparer ʵ�֡�- �� -null ���ã�Visual Basic ��Ϊ Nothing����ʹ��ÿ��Ԫ����System.IComparable ʵ�֡�</param>
        /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�</exception>
        public virtual void Sort(IComparer comparer)
        {
            Sort(0, Count, comparer);
        }

        // ����б��Ԫ�ز��֡������໥�Ƚϵ�Ԫ��ʹ�ø���IComparer�ӿڡ������Ƚ���,�Ƚϵ�Ԫ��ʹ��IComparable�ӿ�,����������±���ʵ�ֵ�����Ԫ�ص��б�
        // This method uses the Array.Sort method to sort the elements.
        /// <summary>
        /// ʹ��ָ���ıȽ����� System.Collections.ArrayList ��ĳ����Χ�ڵ�Ԫ�ؽ�������
        /// </summary>
        /// <param name="index">Ҫ����ķ�Χ�Ĵ��㿪ʼ����ʼ������</param>
        /// <param name="count">Ҫ����ķ�Χ�ĳ��ȡ�</param>
        /// <param name="comparer">�Ƚ�Ԫ��ʱҪʹ�õ� System.Collections.IComparer ʵ�֡�- �� -null ���ã�Visual Basic ��Ϊ Nothing����ʹ��ÿ��Ԫ����System.IComparable ʵ�֡�</param>
        /// <exception cref="System.ArgumentOutOfRangeException:">index С���㡣- �� -count С���㡣</exception>
        /// <exception cref="System.ArgumentException:">index �� count δָ�� System.Collections.ArrayList �е���Ч��Χ��</exception>
        /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�</exception>
        public virtual void Sort(int index, int count, IComparer comparer) {
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (_size - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();
            
            Array.Sort(_items, index, count, comparer);
            _version++;
        }
    
        /// <summary>
        /// ����ͬ���ģ��̰߳�ȫ��System.Collections.IList ��װ��
        /// </summary>
        /// <param name="list">Ҫͬ���� System.Collections.IList��</param>
        /// <returns>ͬ���ģ��̰߳�ȫ��System.Collections.IList ��װ��</returns>
        [HostProtection(Synchronization=true)]
        public static IList Synchronized(IList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<IList>() != null);
            Contract.EndContractBlock();
            return new SyncIList(list);
        }
    
        /// <summary>
        /// ����ͬ���ģ��̰߳�ȫ��System.Collections.ArrayList ��װ��
        /// </summary>
        /// <param name="list">Ҫͬ���� System.Collections.ArrayList��</param>
        /// <returns>ͬ���ģ��̰߳�ȫ��System.Collections.ArrayList ��װ��</returns>
        [HostProtection(Synchronization=true)]
        public static ArrayList Synchronized(ArrayList list) {
            if (list==null)
                throw new ArgumentNullException("list");
            Contract.Ensures(Contract.Result<ArrayList>() != null);
            Contract.EndContractBlock();
            return new SyncArrayList(list);
        }
    
        /// <summary>
        /// �� System.Collections.ArrayList ��Ԫ�ظ��Ƶ��� System.Object �����С�
        /// </summary>
        /// <returns>System.Object ���飬������ System.Collections.ArrayList ��Ԫ�صĸ�����</returns>
        public virtual Object[] ToArray() {
            Contract.Ensures(Contract.Result<Object[]>() != null);

            Object[] array = new Object[_size];
            Array.Copy(_items, 0, array, 0, _size);
            return array;
        }
        //ToArray����һ���ض����͵�������,���а���������ArrayList��
        //����Ҫ����ArrayList��������������ת�����е�Ԫ�ء������������ʧ��,��һ��O(n)���㡣
        //���ڲ�,���ʵ�ֵ���Array.Copy��
        /// <summary>
        /// �� System.Collections.ArrayList ��Ԫ�ظ��Ƶ�ָ��Ԫ�����͵��������С�
        /// </summary>
        /// <param name="type">Ҫ���������临��Ԫ�ص�Ŀ�������Ԫ�� System.Type��</param>
        /// <returns>ָ��Ԫ�����͵����飬������ System.Collections.ArrayList ��Ԫ�صĸ�����</returns>
        [SecuritySafeCritical]
        public virtual Array ToArray(Type type) {
            if (type==null)
                throw new ArgumentNullException("type");
            Contract.Ensures(Contract.Result<Array>() != null);
            Contract.EndContractBlock();
            Array array = Array.UnsafeCreateInstance(type, _size);
            Array.Copy(_items, 0, array, 0, _size);
            return array;
        }

        // ������������б�Ĵ�С��
        // �÷�����������С��һ���б���ڴ濪������������֪,û����Ԫ�ؽ�����ӵ��б��С�
        // ��ȫ�������б���ͷ������ڴ������б�,ִ����������:
        // list.Clear();
        // list.TrimToSize();
        /// <summary>
        /// ����������Ϊ System.Collections.ArrayList ��Ԫ�ص�ʵ����Ŀ��
        /// </summary>
        /// <exception cref="System.NotSupportedException">System.Collections.ArrayList ��ֻ���ġ�- �� -System.Collections.ArrayList ���й̶���С</exception>
        public virtual void TrimToSize() {
            Capacity = _size;
        }


        // �����װһ��IList,��¶������Ϊһ��ArrayListע������Ҫ����ʵ��һ��ArrayList����
        /// <summary>
        /// IList��װ��
        /// </summary>
        [Serializable]
        private class IListWrapper : ArrayList
        {
            private IList _list;
            
            /// <summary>
            /// IList��װ��
            /// </summary>
            /// <param name="list">�̳�IList����</param>
            internal IListWrapper(IList list) {
                _list = list;
                _version = 0; // list�������汾��
            }
            
            /// <summary>
            /// ��ȡ������list����
            /// </summary>
             public override int Capacity {
                get { return _list.Count; }//����list������
                set {
                    if (value < Count) throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_SmallCapacity"));
                    Contract.EndContractBlock();
                }
            }
            
            /// <summary>
             /// ��ȡ ICollection �а�����Ԫ���������� ICollection �̳С���
            /// </summary>
            public override int Count { 
                get { return _list.Count; }
            }
            
            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾ IList �Ƿ�Ϊֻ����
            /// </summary>
            public override bool IsReadOnly { 
                get { return _list.IsReadOnly; }
            }

            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾ IList �Ƿ���й̶���С��
            /// </summary>
            public override bool IsFixedSize {
                get { return _list.IsFixedSize; }
            }

            
            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾ�Ƿ�ͬ���� ICollection �ķ��ʣ��̰߳�ȫ�������� ICollection �̳С���
            /// </summary>
            public override bool IsSynchronized { 
                get { return _list.IsSynchronized; }
            }
            
            /// <summary>
            /// ��ȡ������λ��ָ����������Ԫ�ء�
            /// </summary>
            /// <param name="index">����</param>
            /// <returns>����������</returns>
             public override Object this[int index] {
                get {
                    return _list[index];
                }
                set {
                    _list[index] = value;
                    _version++;
                }
            }
            
            /// <summary>
            /// ��ȡ������ͬ���� ICollection �ķ��ʵĶ��󡣣��� ICollection �̳С���
            /// </summary>
            public override Object SyncRoot {
                get { return _list.SyncRoot; }
            }
            
            /// <summary>
            /// ��ĳ����ӵ� IList �С�
            /// </summary>
            /// <param name="obj">��Ӷ���</param>
            /// <returns>��Ӷ��������</returns>
            public override int Add(Object obj) {
                int i = _list.Add(obj);
                _version++;
                return i;
            }
            
            /// <summary>
            /// ��Ӽ��Ϸ�Χ�ڵ�Ԫ��
            /// </summary>
            /// <param name="c">����</param>
            public override void AddRange(ICollection c) {
                InsertRange(Count, c);//���뷶Χ
            }
    
            /// <summary>
            /// ʹ�ø����Ƚ��������������������б��һ��Ԫ�ز�����Ԫ�صĴ��㿪ʼ������
            /// </summary>
            /// <param name="index">��������</param>
            /// <param name="count">��������</param>
            /// <param name="value">��������</param>
            /// <param name="comparer">�Ƚ���</param>
            /// <returns>Ԫ�����ڴ�����</returns>
            /// <exception cref="ArgumentOutOfRangeException">index �� count δָ�� System.Collections.ArrayList �е���Ч��Χ��</exception>
            /// <exception cref="ArgumentException"></exception>
            public override int BinarySearch(int index, int count, Object value, IComparer comparer) 
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (this.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();
                if (comparer == null)//����Ƚ���Ϊ�գ�������ΪĬ�ϱȽ���
                    comparer = Comparer.Default;
                
                int lo = index;//��ʼλ
                int hi = index + count - 1;//����λ
                int mid;
                while (lo <= hi) {//�����ʼλС�ڻ���ڽ���λ�������ѭ���ж�
                    mid = (lo+hi)/2;//��ȡ�м�λ
                    int r = comparer.Compare(value, _list[mid]);//ʹ�ñȽ����Ƚϴ�С
                    if (r == 0)
                        return mid;
                    if (r < 0)
                        hi = mid-1;
                    else 
                        lo = mid+1;
                }
                // return bitwise complement of the first element greater than value.
                // Since hi is less than lo now, ~lo is the correct item.
                return ~lo;//���ص�һ��Ԫ�ص�λ�����ڼ�ֵ��
            }
            
            /// <summary>
            /// �� IList ���Ƴ������
            /// </summary>
            /// <exception cref="NotSupportedException">System.Collections.ArrayList ��ֻ���ġ�- �� -System.Collections.ArrayList ���й̶���С</exception>
            public override void Clear() {
                //���_list��һ������,����֧����ȷ�ķ��������ǲ�Ӧ��������ȷ����FixedSized ArrayList
                if(_list.IsFixedSize) {
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
                }

                _list.Clear();//�Ƴ�������
                _version++;
            }
            
            /// <summary>
            /// ����һ��ArrayList��ǳ������(��ֻ��������)
            /// �Ⲣ����_list���һ��ArrayList��ǳ����!
            /// �����¡IListWrapper,������һ����װ����!
            /// </summary>
            /// <returns></returns>
            public override Object Clone() {
                // This does not do a shallow copy of _list into a ArrayList!
                // This clones the IListWrapper, creating another wrapper class!
                return new IListWrapper(_list);
            }
            
            /// <summary>
            /// ȷ�� IList �Ƿ�����ض�ֵ��
            /// </summary>
            /// <param name="obj">Ԫ�ض���</param>
            /// <returns></returns>
            public override bool Contains(Object obj) {
                return _list.Contains(obj);
            }
            
            /// <summary>
            /// ���ض��� System.Array ��������ʼ���� System.Collections.ICollection ��Ԫ�ظ��Ƶ�һ�� System.Array�С�
            /// </summary>
            /// <param name="array">��Ϊ�� System.Collections.ICollection ���Ƶ�Ԫ�ص�Ŀ��λ�õ�һά System.Array��System.Array������д��㿪ʼ��������</param>
            /// <param name="index">array �д��㿪ʼ�����������ڴ˴���ʼ���ơ�</param>
            public override void CopyTo(Array array, int index) {
                _list.CopyTo(array, index);
            }
    
            /// <summary>
            /// ���ض��� System.Array ��������ʼ���� System.Collections.ICollection ��Ԫ�ظ��Ƶ�һ�� System.Array��
            /// </summary>
            /// <param name="index">IList��ʼλ��</param>
            /// <param name="array">��Ϊ�� System.Collections.ICollection ���Ƶ�Ԫ�ص�Ŀ��λ�õ�һά System.Array��</param>
            /// <param name="arrayIndex">System.Array������д�arrayIndex��ʼ��������</param>
            /// <param name="count">��������</param>
            /// <exception cref="ArgumentNullException">arrayΪ��</exception>
            /// <exception cref="ArgumentOutOfRangeException">index��arrayIndex��countС����</exception>
            /// <exception cref="ArgumentException">count����Խ��</exception>
            public override void CopyTo(int index, Array array, int arrayIndex, int count) {
                if (array==null)
                    throw new ArgumentNullException("array");
                if (index < 0 || arrayIndex < 0)
                    throw new ArgumentOutOfRangeException((index < 0) ? "index" : "arrayIndex", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if( count < 0)
                    throw new ArgumentOutOfRangeException( "count" , Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));                 
                if (array.Length - arrayIndex < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                if (array.Rank != 1)
                    throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
                Contract.EndContractBlock();

                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                
                for(int i=index; i<index+count; i++)//��list�в������ݸ��Ƶ�array��
                    array.SetValue(_list[i], arrayIndex++);
            }
            
            /// <summary>
            /// ����ѭ�����ʼ��ϵ�ö���������� IEnumerable �̳С���
            /// </summary>
            /// <returns></returns>
            public override IEnumerator GetEnumerator() {
                return _list.GetEnumerator();
            }
            
            /// <summary>
            /// ����ѭ�����ʼ��ϵ�ö���������� IEnumerable �̳С���
            /// </summary>
            /// <param name="index">��ʼ����λ</param>
            /// <param name="count">ö������</param>
            /// <returns></returns>
            public override IEnumerator GetEnumerator(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
    
                return new IListWrapperEnumWrapper(this, index, count);
            }
            
            /// <summary>
            /// ȷ�� System.Collections.IList ���ض����������
            /// </summary>
            /// <param name="value">Ҫ�� System.Collections.IList �в��ҵĶ���</param>
            /// <returns>������б����ҵ� value����Ϊ���������������Ϊ -1��</returns>
            public override int IndexOf(Object value) {
                return _list.IndexOf(value);
            }

            /// <summary>
            /// ȷ�� System.Collections.IList ���ض����������
            /// </summary>
            /// <param name="value">Ҫ�� System.Collections.IList �в��ҵĶ���</param>
            /// <param name="startIndex">����ʼλ������</param>
            /// <returns>������б����ҵ� value����Ϊ���������������Ϊ -1��</returns>
            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // ��������Ĵ��������**AppCompatǱ�ڵ�����
            public override int IndexOf(Object value, int startIndex) {
                return IndexOf(value, startIndex, _list.Count - startIndex);
            }
    
            /// <summary>
            /// ȷ�� System.Collections.IList ���ض����������
            /// </summary>
            /// <param name="value">Ҫ��IList���ض���Ķ���</param>
            /// <param name="startIndex">����ʼλ������</param>
            /// <param name="count">��������</param>
            /// <returns>������б����ҵ�value����Ϊ��������������Ϊ-1��</returns>
            /// <exception cref="ArgumentOutOfRangeException">startIndexС�������ڰ���Ԫ��������count�����߽�</exception>
            public override int IndexOf(Object value, int startIndex, int count) {
                if (startIndex < 0 || startIndex > this.Count) throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                if (count < 0 || startIndex > this.Count - count) throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_Count"));
                Contract.EndContractBlock();

                int endIndex = startIndex + count;//��ȡ����������
                if (value == null) {//���������Ϊ�գ����жϷ�Χ���Ƿ���Ϊ�յ�ֵ
                    for(int i=startIndex; i<endIndex; i++)
                        if (_list[i] == null)
                            return i;
                    return -1;
                } else {//�����Ϊ�գ����жϷ�Χ���Ƿ�����ȵ�ֵ
                    for(int i=startIndex; i<endIndex; i++)
                        if (_list[i] != null && _list[i].Equals(value))
                            return i;
                    return -1;
                }
            }
            
            /// <summary>
            /// ��Ԫ�ض�������ض�λ�ô�
            /// </summary>
            /// <param name="index">����Ϊ����</param>
            /// <param name="obj">����Ԫ�ض���</param>
            public override void Insert(int index, Object obj) {
                _list.Insert(index, obj);//����IList.Insert
                _version++;
            }
            
            /// <summary>
            /// ��List�в��뼯��
            /// </summary>
            /// <param name="index">��ʼλ����</param>
            /// <param name="c">��Ҫ����ļ���</param>
            /// <exception cref="ArgumentNullException">c����Ϊ��</exception>
            /// <exception cref="ArgumentOutOfRangeException">index����������Χ</exception>
            public override void InsertRange(int index, ICollection c) {
                if (c==null)
                    throw new ArgumentNullException("c", Environment.GetResourceString("ArgumentNull_Collection"));
                if (index < 0 || index > this.Count) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();

                if( c.Count > 0) {//�������Ϊ�գ��򲻽��в��������򽫼��ϲ��뵽list��
                    ArrayList al = _list as ArrayList;//��IListת��ΪArrayList,������ɹ�����ʹ��ö�ٵ�������
                    if( al != null) { //���al��Ϊ�գ������ArrayList.InsertRange
                        // ������Ҫ����ArrayList��
                        // һϵ��_list cʱ,������Ҫ��һ������ķ�ʽ����������⡣
                        // ����ArrayList��InsertRange���顣
                        al.InsertRange(index, c);    
                    }
                    else {
                        IEnumerator en = c.GetEnumerator();//ö�ٵ�������
                        while(en.MoveNext()) {
                            _list.Insert(index++, en.Current);
                        }                   
                    }
                    _version++;//�汾��1
                }
            }
            
            /// <summary>
            /// ����ָ���� System.Object�������� System.Collections.ArrayList �а���ָ����Ԫ��������ָ��������������Ԫ�ط�Χ�����һ��ƥ����Ĵ��㿪ʼ������(������ArrayList)
            /// </summary>
            /// <param name="value">��������</param>
            /// <returns>�������ڴ���������������ڣ��򷵻�Ϊ-1</returns>
            public override int LastIndexOf(Object value) {
                 return LastIndexOf(value,_list.Count - 1, _list.Count);
            }

            /// <summary>
            /// ����ָ���� System.Object�������� System.Collections.ArrayList �а���ָ����Ԫ��������ָ��������������Ԫ�ط�Χ�����һ��ƥ����Ĵ��㿪ʼ������(������ArrayList)
            /// </summary>
            /// <param name="value">��������</param>
            /// <param name="startIndex">��ʼ������</param>
            /// <returns>�������ڴ���������������ڣ��򷵻�Ϊ-1</returns>
            [SuppressMessage("Microsoft.Contracts", "CC1055")]  //  ��������Ĵ��������**AppCompatǱ�ڵ�����
            public override int LastIndexOf(Object value, int startIndex) {
                return LastIndexOf(value, startIndex, startIndex + 1);
            }

            /// <summary>
            /// ����ָ���� System.Object�������� System.Collections.ArrayList �а���ָ����Ԫ��������ָ��������������Ԫ�ط�Χ�����һ��ƥ����Ĵ��㿪ʼ������(������ArrayList)
            /// </summary>
            /// <param name="value">��������</param>
            /// <param name="startIndex">��ʼ������</param>
            /// <param name="count">��������</param>
            /// <returns>�������ڴ���������������ڣ��򷵻�Ϊ-1</returns>
            /// <exception cref="ArgumentOutOfRangeException">startIndex,count����Խ��</exception>
            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // ��������Ĵ��������**AppCompatǱ�ڵ�����
            public override int LastIndexOf(Object value, int startIndex, int count) {
                if (_list.Count == 0)//���List���������򷵻�-1
                    return -1;
   
                if (startIndex < 0 || startIndex >= _list.Count) throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                if (count < 0 || count > startIndex + 1) throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_Count"));

                int endIndex = startIndex - count + 1;//��ȡ����λ����
                if (value == null) {
                    for(int i=startIndex; i >= endIndex; i--)
                        if (_list[i] == null)
                            return i;
                    return -1;
                } else {
                    for(int i=startIndex; i >= endIndex; i--)
                        if (_list[i] != null && _list[i].Equals(value))
                            return i;
                    return -1;
                }
            }
            
            /// <summary>
            /// �� ArrayList ���Ƴ��ض�����ĵ�һ��ƥ���
            /// </summary>
            /// <param name="value">Ҫ�� System.Collections.IList ���Ƴ��Ķ���</param>
            public override void Remove(Object value) {
                int index = IndexOf(value);//��ȡ�Ƴ����������          
                if (index >=0) //����������ڵ���0�����Ƴ����������Ķ���
                    RemoveAt(index);
            }
            
            /// <summary>
            /// �Ƴ�ָ���������� System.Collections.IList �
            /// </summary>
            /// <param name="index">���㿪ʼ������������Ҫ�Ƴ������</param>
            /// <exception cref="System.NotSupportedException">System.Collections.IList ��ֻ���ġ�- �� -System.Collections.IList ���й̶���С��</exception>
            public override void RemoveAt(int index) {
                _list.RemoveAt(index);
                _version++;
            }
            
            /// <summary>
            /// �� ArrayList ���Ƴ�һ����Χ��Ԫ�ء�
            /// </summary>
            /// <param name="index">��ʼ����</param>
            /// <param name="count">��������</param>
            /// <exception cref="ArgumentOutOfRangeException">index,countС����</exception>
            /// <exception cref="ArgumentException">count������Χ</exception>
            public override void RemoveRange(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                
                if( count > 0)    // ��ArrayList��һ�µ�
                    _version++;

                while(count > 0) {//ͨ��count��������ɾ��indexλ������ֱ��countС��0
                    _list.RemoveAt(index);
                    count--;
                }
            }
    
            /// <summary>
            /// ��ָ����Χ��Ԫ�ص�˳��ת��
            /// </summary>
            /// <param name="index">Ҫ��ת�ķ�Χ�Ĵ��㿪ʼ����ʼ������</param>
            /// <param name="count">Ҫ��ת�ķ�Χ�ڵ�Ԫ������</param>
            /// <exception cref="System.ArgumentOutOfRangeExceptio">index С���㡣- �� -count С���㡣</exception>
            /// <exception cref="System.ArgumentException">index �� count ����ʾ System.Collections.ArrayList ��Ԫ�ص���Ч��Χ��</exception>
            /// <exception cref="System.NotSupportedException">System.Collections.ArrayList ��ֻ���ġ�</exception>
            public override void Reverse(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                
                // ��ȡ��ʼ�����ͽ�������
                int i = index;
                int j = index + count - 1;
                while (i < j)//����Ԫ��
                {
                    Object tmp = _list[i];
                    _list[i++] = _list[j];
                    _list[j--] = tmp;
                }
                _version++;
            }
    
            /// <summary>
            /// �������е�Ԫ�ظ��Ƶ� System.Collections.ArrayList ��һ����Χ��Ԫ���ϡ�
            /// </summary>
            /// <param name="index">���㿪ʼ�� System.Collections.ArrayList �������Ӹ�λ�ÿ�ʼ���� c ��Ԫ�ء�</param>
            /// <param name="c">System.Collections.ICollection��Ҫ����Ԫ�ظ��Ƶ� System.Collections.ArrayList �С����ϱ�����Ϊnull���������԰���Ϊ null ��Ԫ�ء�</param>
            /// <exception cref="System.ArgumentOutOfRangeException">index С���㡣- �� -index ���� c �е�Ԫ�������� System.Collections.ArrayList.Count��</exception>
            /// <exception cref="System.ArgumentNullException"> c Ϊ null</exception>
            /// <exception cref="System.NotSupportedException">System.Collections.ArrayList ��ֻ����</exception>
            public override void SetRange(int index, ICollection c) {
                if (c==null) {
                    throw new ArgumentNullException("c", Environment.GetResourceString("ArgumentNull_Collection"));
                }
                Contract.EndContractBlock();

                if (index < 0 || index > _list.Count - c.Count) {
                    throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));            
                }
                   
                if( c.Count > 0) {                                     
                    IEnumerator en = c.GetEnumerator();//��ȡ�����е�ö��
                    while(en.MoveNext()) {//���������е�Ԫ�أ�������list��
                        _list[index++] = en.Current;
                    }
                    _version++;
                }
            }
    
            /// <summary>
            /// ��ȡRange
            /// </summary>
            /// <param name="index">��ʼ����</param>
            /// <param name="count">����</param>
            /// <returns>ArrayList</returns>
            public override ArrayList GetRange(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                return new Range(this,index, count);
            }

            /// <summary>
            /// ʹ��ָ���ıȽ����� System.Collections.ArrayList ��ĳ����Χ�ڵ�Ԫ�ؽ�������
            /// </summary>
            /// <param name="index">Ҫ����ķ�Χ�Ĵ��㿪ʼ����ʼ������</param>
            /// <param name="count">Ҫ����ķ�Χ�ĳ��ȡ�</param>
            /// <param name="comparer">�Ƚ�Ԫ��ʱҪʹ�õ� System.Collections.IComparer ʵ�֡�- �� -null ���ã�Visual Basic ��Ϊ Nothing����ʹ��ÿ��Ԫ����System.IComparable ʵ�֡�</param>
            /// <exception cref="System.ArgumentOutOfRangeException:">index С���㡣- �� -count С���㡣</exception>
            /// <exception cref="System.ArgumentException:">index �� count δָ�� System.Collections.ArrayList �е���Ч��Χ��</exception>
            /// <exception cref="System.NotSupportedException:">System.Collections.ArrayList ��ֻ���ġ�</exception>
            public override void Sort(int index, int count, IComparer comparer) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_list.Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                
                Object [] array = new Object[count];//����һ���µ�Object����
                CopyTo(index, array, 0, count);//���������ݸ��Ƶ�Object������
                Array.Sort(array, 0, count, comparer);//ʹ�ñȽ�����object�������ݽ�������
                for(int i=0; i<count; i++)
                    _list[i+index] = array[i];//�������ݽ��и���

                _version++;
            }


            public override Object[] ToArray() {
                Object[] array = new Object[Count];
                _list.CopyTo(array, 0);
                return array;
            }

            [SecuritySafeCritical]
            public override Array ToArray(Type type)
            {
                if (type==null)
                    throw new ArgumentNullException("type");
                Contract.EndContractBlock();
                Array array = Array.UnsafeCreateInstance(type, _list.Count);
                _list.CopyTo(array, 0);
                return array;
            }

            public override void TrimToSize()
            {
                // Can't really do much here...
            }
    
        
            /// <summary>
            /// ����IList��ö������װ����һ�����ʵ��ArrayList�����з�����
            /// </summary>
            [Serializable]
            private sealed class IListWrapperEnumWrapper : IEnumerator, ICloneable
            {
                private IEnumerator _en;
                private int _remaining;     // ��ǰʣ���������
                private int _initialStartIndex;   // ��������
                private int _initialCount;        // ��������
                private bool _firstCall;       // ��һ�ε���MoveNext
    
                private IListWrapperEnumWrapper()
                {
                }

                /// <summary>
                /// ����IList��ö������װ����һ�����ʵ��ArrayList�����з�����
                /// </summary>
                /// <param name="listWrapper">IList��װ��</param>
                /// <param name="startIndex">��ʼ������</param>
                /// <param name="count">����</param>
                internal IListWrapperEnumWrapper(IListWrapper listWrapper, int startIndex, int count) 
                {
                    _en = listWrapper.GetEnumerator();//��ȡIEnumerator
                    _initialStartIndex = startIndex;//���ó�ʼ����ʼ����
                    _initialCount = count;//���ó�ʼ������
                    while(startIndex-- > 0 && _en.MoveNext());
                    _remaining = count;
                    _firstCall = true;
                }

                /// <summary>
                /// ��¡IListWrapperEnumWrapper����
                /// </summary>
                /// <returns></returns>
                public Object Clone() {
                    // We must clone the underlying enumerator, I think.
                    IListWrapperEnumWrapper clone = new IListWrapperEnumWrapper();
                    clone._en = (IEnumerator) ((ICloneable)_en).Clone();
                    clone._initialStartIndex = _initialStartIndex;
                    clone._initialCount = _initialCount;
                    clone._remaining = _remaining;
                    clone._firstCall = _firstCall;
                    return clone;
                }

                /// <summary>
                /// ��ö�����ƽ������ϵ���һ��Ԫ�ء�
                /// </summary>
                /// <returns>���ö�����ѳɹ����ƽ�����һ��Ԫ�أ���Ϊ true�����ö�������ݵ����ϵ�ĩβ����Ϊ false��</returns>
                public bool MoveNext() {
                    if (_firstCall) {//����ǵ�һ�ε���,��_firstCall����Ϊfalse
                        _firstCall = false;
                        //return _remaining-- > 0 && _en.MoveNext();
                    }
                    if (_remaining < 0)
                        return false;
                    bool r = _en.MoveNext();
                    return r && _remaining-- > 0;
                }
                
                /// <summary>
                /// ��ȡ��ǰ����
                /// </summary>
                /// <exception cref="InvalidOperationException">��һ�α�����</exception>
                /// <exception cref="InvalidOperationException">ʣ���������С����</exception>
                public Object Current {
                    get {
                        if (_firstCall)
                            throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumNotStarted));
                        if (_remaining < 0)
                            throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumEnded));
                        return _en.Current;
                    }
                }
    
                /// <summary>
                /// ��IListWrapperEnumWrapper����
                /// </summary>
                public void Reset() {
                    _en.Reset();
                    int startIndex = _initialStartIndex;//��ʼ����Ĭ������
                    while(startIndex-- > 0 && _en.MoveNext());
                    _remaining = _initialCount;//����ʼ����������Ϊʣ���������
                    _firstCall = true;
                }
            }
        }
    
    
        /// <summary>
        /// ͬ��ArrayList(֧�����л�)
        /// </summary>
        [Serializable]
        private class SyncArrayList : ArrayList
        {
            private ArrayList _list;
            private Object _root;//����ͬ��������

            /// <summary>
            /// ͬ�����캯��
            /// </summary>
            /// <param name="list">����ͬ����ArrayList</param>
            internal SyncArrayList(ArrayList list)
                : base( false )
            {
                _list = list;
                _root = list.SyncRoot;
            }
    
            /// <summary>
            /// ����ArrayList����������
            /// </summary>
            public override int Capacity {
                get {
                    lock(_root) {//��Ӷ�����������ArrayList.Capacity
                        return _list.Capacity;
                    }
                }
                [SuppressMessage("Microsoft.Contracts", "CC1055")]  // ��������Ĵ��������* * AppCompatǱ�����⡣
                set {
                    lock(_root) {
                        _list.Capacity = value;
                    }
                }
            }
    
            /// <summary>
            /// ��ȡ ArrayList ��ʵ�ʰ�����Ԫ��������֧��ͬ�����ʣ�
            /// </summary>
            public override int Count { 
                get { lock(_root) { return _list.Count; } }
            }
    
            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾArrayList�Ƿ�Ϊֻ��
            /// </summary>
            public override bool IsReadOnly {
                get { return _list.IsReadOnly; }
            }

            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾArrayList�Ƿ�Ϊ�̶���С
            /// </summary>
            public override bool IsFixedSize {
                get { return _list.IsFixedSize; }
            }

            /// <summary>
            /// ��ȡһ��ֵ����ֵָʾ�Ƿ�ͬ���� ArrayList �ķ��ʣ��̰߳�ȫ����
            /// SyncArrayList֧��ͬ�����ʣ�����Ϊtrue
            /// </summary>
            public override bool IsSynchronized { 
                get { return true; }
            }
            
            /// <summary>
            /// ��ȡ������λ��ָ����������Ԫ�ء���֧��ͬ�����ʣ�
            /// </summary>
            /// <param name="index">����</param>
            /// <returns>��ǰ����������</returns>
             public override Object this[int index] {
                get {
                    lock(_root) {
                        return _list[index];
                    }
                }
                set {
                    lock(_root) {
                        _list[index] = value;
                    }
                }
            }
    
            /// <summary>
            /// ��ȡͬ������
            /// </summary>
            public override Object SyncRoot {
                get { return _root; }
            }
    
            /// <summary>
            /// ������������ӵ�����б��С��б�Ĵ�С����1�������Ҫ,�б�������������Ԫ��֮ǰ����һ����
            /// </summary>
            /// <param name="value">��Ӷ���</param>
            /// <returns>����ʵ�ʰ�����С</returns>
            public override int Add(Object value) {
                lock(_root) {
                    return _list.Add(value);
                }
            }
    
            /// <summary>
            /// ��� ICollection ��Ԫ�ص� ArrayList ��ĩβ��
            /// </summary>
            /// <param name="c">����</param>
            public override void AddRange(ICollection c) {
                lock(_root) {
                    _list.AddRange(c);
                }
            }

            /// <summary>
            /// ʹ��Ĭ�ϱȽ��������������������б��һ��Ԫ�ز�����Ԫ�صĴ��㿪ʼ��������
            /// </summary>
            /// <param name="value">����Ԫ�ض���</param>
            /// <returns></returns>
            public override int BinarySearch(Object value) {
                lock(_root) {
                    return _list.BinarySearch(value);
                }
            }

            /// <summary>
            /// ʹ�ø����Ƚ��������������������б��һ��Ԫ�ز�����Ԫ�صĴ��㿪ʼ��������
            /// </summary>
            /// <param name="value">����Ԫ�ض���</param>
            /// <param name="comparer">�Ƚ���</param>
            /// <returns></returns>
            public override int BinarySearch(Object value, IComparer comparer) {
                lock(_root) {
                    return _list.BinarySearch(value, comparer);
                }
            }

            /// <summary>
            /// ʹ��ָ���ıȽ���������������� ArrayList ������Ԫ�أ������ظ�Ԫ�ش��㿪ʼ��������
            /// </summary>
            /// <param name="index">����</param>
            /// <param name="count">����</param>
            /// <param name="value">ֵ</param>
            /// <param name="comparer">�Ƚ���</param>
            /// <returns></returns>
            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int BinarySearch(int index, int count, Object value, IComparer comparer) {
                lock(_root) {
                    return _list.BinarySearch(index, count, value, comparer);
                }
            }

            /// <summary>
            /// �� ArrayList ���Ƴ�����Ԫ�ء�
            /// </summary>
            public override void Clear() {
                lock(_root) {
                    _list.Clear();
                }
            }

            /// <summary>
            /// ����һ��ArrayList��ǳ������(��ֻ��������)
            /// </summary>
            /// <returns></returns>
            public override Object Clone() {
                lock(_root) {
                    return new SyncArrayList((ArrayList)_list.Clone());
                }
            }
    
            /// <summary>
            /// ȷ��ĳԪ���Ƿ��� ArrayList �С�
            /// </summary>
            /// <param name="item">Ԫ�ض���</param>
            /// <returns></returns>
            public override bool Contains(Object item) {
                lock(_root) {
                    return _list.Contains(item);
                }
            }
    
            /// <summary>
            /// ��Ŀ������Ŀ�ͷ��ʼ�������� ArrayList ���Ƶ�һά���� Array��
            /// </summary>
            /// <param name="array"></param>
            public override void CopyTo(Array array) {
                lock(_root) {
                    _list.CopyTo(array);
                }
            }

            /// <summary>
            ///  ���������б��Ƶ�һ�����ݵ�һά����,��Ŀ���ָ���������顣
            /// </summary>
            /// <param name="array"></param>
            /// <param name="index"></param>
            public override void CopyTo(Array array, int index) {
                lock(_root) {
                    _list.CopyTo(array, index);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void CopyTo(int index, Array array, int arrayIndex, int count) {
                lock(_root) {
                    _list.CopyTo(index, array, arrayIndex, count);
                }
            }
    
            public override IEnumerator GetEnumerator() {
                lock(_root) {
                    return _list.GetEnumerator();
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override IEnumerator GetEnumerator(int index, int count) {
                lock(_root) {
                    return _list.GetEnumerator(index, count);
                }
            }
    
            public override int IndexOf(Object value) {
                lock(_root) {
                    return _list.IndexOf(value);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex) {
                lock(_root) {
                    return _list.IndexOf(value, startIndex);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex, int count) {
                lock(_root) {
                    return _list.IndexOf(value, startIndex, count);
                }
            }
    
            public override void Insert(int index, Object value) {
                lock(_root) {
                    _list.Insert(index, value);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void InsertRange(int index, ICollection c) {
                lock(_root) {
                    _list.InsertRange(index, c);
                }
            }
    
            public override int LastIndexOf(Object value) {
                lock(_root) {
                    return _list.LastIndexOf(value);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex) {
                lock(_root) {
                    return _list.LastIndexOf(value, startIndex);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex, int count) {
                lock(_root) {
                    return _list.LastIndexOf(value, startIndex, count);
                }
            }
    
            public override void Remove(Object value) {
                lock(_root) {
                    _list.Remove(value);
                }
            }
    
            public override void RemoveAt(int index) {
                lock(_root) {
                    _list.RemoveAt(index);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void RemoveRange(int index, int count) {
                lock(_root) {
                    _list.RemoveRange(index, count);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Reverse(int index, int count) {
                lock(_root) {
                    _list.Reverse(index, count);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void SetRange(int index, ICollection c) {
                lock(_root) {
                    _list.SetRange(index, c);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override ArrayList GetRange(int index, int count) {
                lock(_root) {
                    return _list.GetRange(index, count);
                }
            }

            public override void Sort() {
                lock(_root) {
                    _list.Sort();
                }
            }
    
            public override void Sort(IComparer comparer) {
                lock(_root) {
                    _list.Sort(comparer);
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Sort(int index, int count, IComparer comparer) {
                lock(_root) {
                    _list.Sort(index, count, comparer);
                }
            }
    
            public override Object[] ToArray() {
                lock(_root) {
                    return _list.ToArray();
                }
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override Array ToArray(Type type) {
                lock(_root) {
                    return _list.ToArray(type);
                }
            }
    
            public override void TrimToSize() {
                lock(_root) {
                    _list.TrimToSize();
                }
            }
        }
    
    
        /// <summary>
        /// ͬ��IList(֧�����л�)
        /// </summary>
        [Serializable]
        private class SyncIList : IList
        {
            private IList _list;
            private Object _root;//ͬ��������
    
            internal SyncIList(IList list) {
                _list = list;
                _root = list.SyncRoot;
            }
    
            public virtual int Count { 
                get { lock(_root) { return _list.Count; } }
            }
    
            public virtual bool IsReadOnly {
                get { return _list.IsReadOnly; }
            }

            public virtual bool IsFixedSize {
                get { return _list.IsFixedSize; }
            }

            
            public virtual bool IsSynchronized { 
                get { return true; }
            }
            
             public virtual Object this[int index] {
                get {
                    lock(_root) {
                        return _list[index];
                    }
                }
                set {
                    lock(_root) {
                        _list[index] = value;
                    }
                }
            }
    
            public virtual Object SyncRoot {
                get { return _root; }
            }
    
            public virtual int Add(Object value) {
                lock(_root) {
                    return _list.Add(value);
                }
            }
                 
    
            public virtual void Clear() {
                lock(_root) {
                    _list.Clear();
                }
            }
    
            public virtual bool Contains(Object item) {
                lock(_root) {
                    return _list.Contains(item);
                }
            }
    
            public virtual void CopyTo(Array array, int index) {
                lock(_root) {
                    _list.CopyTo(array, index);
                }
            }
    
            public virtual IEnumerator GetEnumerator() {
                lock(_root) {
                    return _list.GetEnumerator();
                }
            }
    
            public virtual int IndexOf(Object value) {
                lock(_root) {
                    return _list.IndexOf(value);
                }
            }
    
            public virtual void Insert(int index, Object value) {
                lock(_root) {
                    _list.Insert(index, value);
                }
            }
    
            public virtual void Remove(Object value) {
                lock(_root) {
                    _list.Remove(value);
                }
            }
    
            public virtual void RemoveAt(int index) {
                lock(_root) {
                    _list.RemoveAt(index);
                }
            }
        }
    
        /// <summary>
        /// �̶���СList�����������ӻ�ɾ����ֻ�����޸ģ�֧�����л���
        /// </summary>
        [Serializable]
        private class FixedSizeList : IList
        {
            private IList _list;
    
            internal FixedSizeList(IList l) {
                _list = l;
            }
    
            public virtual int Count { 
                get { return _list.Count; }
            }
    
            public virtual bool IsReadOnly {
                get { return _list.IsReadOnly; }
            }

            public virtual bool IsFixedSize {
                get { return true; }
            }

            public virtual bool IsSynchronized { 
                get { return _list.IsSynchronized; }
            }
            
             public virtual Object this[int index] {
                get {
                    return _list[index];
                }
                set {
                    _list[index] = value;
                }
            }
    
            public virtual Object SyncRoot {
                get { return _list.SyncRoot; }
            }
            
            /// <summary>
            /// ����Ԫ�ض���
            /// </summary>
            /// <param name="obj">Ԫ�ض���</param>
            /// <returns>�����쳣</returns>
            /// <exception cref="NotSupportedException">��֧������</exception>
            public virtual int Add(Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
        
            /// <summary>
            /// ���Ƴ�IList����
            /// </summary>
            /// <exception cref="NotSupportedException">��֧���Ƴ�</exception>
            public virtual void Clear() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public virtual bool Contains(Object obj) {
                return _list.Contains(obj);
            }
    
            public virtual void CopyTo(Array array, int index) {
                _list.CopyTo(array, index);
            }
    
            public virtual IEnumerator GetEnumerator() {
                return _list.GetEnumerator();
            }
    
            public virtual int IndexOf(Object value) {
                return _list.IndexOf(value);
            }
    
            public virtual void Insert(int index, Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public virtual void Remove(Object value) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public virtual void RemoveAt(int index) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
        }

        /// <summary>
        ///  �̶���СArrayList�����������ӻ�ɾ����ֻ�����޸ģ�֧�����л���
        /// </summary>
        [Serializable]
        private class FixedSizeArrayList : ArrayList
        {
            private ArrayList _list;
    
            internal FixedSizeArrayList(ArrayList l) {
                _list = l;
                _version = _list._version;
            }
    
            public override int Count { 
                get { return _list.Count; }
            }
    
            public override bool IsReadOnly {
                get { return _list.IsReadOnly; }
            }

            /// <summary>
            /// �Ƿ��ǹ̶���С�ģ�����Ϊ�棩
            /// </summary>
            public override bool IsFixedSize {
                get { return true; }
            }

            public override bool IsSynchronized { 
                get { return _list.IsSynchronized; }
            }
            
             public override Object this[int index] {
                get {
                    return _list[index];
                }
                set {
                    _list[index] = value;
                    _version = _list._version;
                }
            }
    
            public override Object SyncRoot {
                get { return _list.SyncRoot; }
            }
            
            public override int Add(Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public override void AddRange(ICollection c) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int BinarySearch(int index, int count, Object value, IComparer comparer) {
                return _list.BinarySearch(index, count, value, comparer);
            }

            public override int Capacity {
                get { return _list.Capacity; }
                [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
                set { throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection")); }
            }

            public override void Clear() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public override Object Clone() {
                FixedSizeArrayList arrayList = new FixedSizeArrayList(_list);
                arrayList._list = (ArrayList)_list.Clone();
                return arrayList;
            }

            public override bool Contains(Object obj) {
                return _list.Contains(obj);
            }
    
            public override void CopyTo(Array array, int index) {
                _list.CopyTo(array, index);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void CopyTo(int index, Array array, int arrayIndex, int count) {
                _list.CopyTo(index, array, arrayIndex, count);
            }

            public override IEnumerator GetEnumerator() {
                return _list.GetEnumerator();
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override IEnumerator GetEnumerator(int index, int count) {
                return _list.GetEnumerator(index, count);
            }

            public override int IndexOf(Object value) {
                return _list.IndexOf(value);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex) {
                return _list.IndexOf(value, startIndex);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex, int count) {
                return _list.IndexOf(value, startIndex, count);
            }
    
            /// <summary>
            /// ����Ԫ�ض���
            /// </summary>
            /// <param name="index">Ҫ������������</param>
            /// <param name="obj">����Ԫ��</param>
            public override void Insert(int index, Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void InsertRange(int index, ICollection c) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }

            public override int LastIndexOf(Object value) {
                return _list.LastIndexOf(value);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex) {
                return _list.LastIndexOf(value, startIndex);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex, int count) {
                return _list.LastIndexOf(value, startIndex, count);
            }

            public override void Remove(Object value) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
    
            public override void RemoveAt(int index) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void RemoveRange(int index, int count) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void SetRange(int index, ICollection c) {
                _list.SetRange(index, c);
                _version = _list._version;
            }

            public override ArrayList GetRange(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                return new Range(this,index, count);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Reverse(int index, int count) {
                _list.Reverse(index, count);
                _version = _list._version;
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Sort(int index, int count, IComparer comparer) {
                _list.Sort(index, count, comparer);
                _version = _list._version;
            }

            public override Object[] ToArray() {
                return _list.ToArray();
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override Array ToArray(Type type) {
                return _list.ToArray(type);
            }
    
            public override void TrimToSize() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FixedSizeCollection"));
            }
        }
    
        /// <summary>
        /// ֻ��IList
        /// </summary>
        [Serializable]
        private class ReadOnlyList : IList
        {
            private IList _list;
    
            /// <summary>
            /// ֻ����ͬһ�����з��ʵĹ��캯��
            /// </summary>
            /// <param name="l"></param>
            internal ReadOnlyList(IList l) {
                _list = l;
            }
    
            /// <summary>
            /// ��ȡ IList ��ʵ�ʰ�����Ԫ������
            /// </summary>
            public virtual int Count { 
                get { return _list.Count; }
            }
    
            /// <summary>
            /// ��ȡһ��ֵ�����ж�IList�Ƿ�Ϊֻ��
            /// ����Ϊtrue
            /// </summary>
            public virtual bool IsReadOnly {
                get { return true; }
            }

            public virtual bool IsFixedSize {
                get { return true; }
            }

            /// <summary>
            /// ��ȡһ��ֵ�����ж��Ƿ�֧��ͬ�����ʣ��̰߳�ȫ��
            /// </summary>
            public virtual bool IsSynchronized { 
                get { return _list.IsSynchronized; }
            }
            
             public virtual Object this[int index] {
                get {
                    return _list[index];
                }
                set {
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
                }
            }
    
            public virtual Object SyncRoot {
                get { return _list.SyncRoot; }
            }
            
            public virtual int Add(Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public virtual void Clear() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public virtual bool Contains(Object obj) {
                return _list.Contains(obj);
            }
            
            public virtual void CopyTo(Array array, int index) {
                _list.CopyTo(array, index);
            }
    
            public virtual IEnumerator GetEnumerator() {
                return _list.GetEnumerator();
            }
    
            public virtual int IndexOf(Object value) {
                return _list.IndexOf(value);
            }
    
            public virtual void Insert(int index, Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            public virtual void Remove(Object value) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public virtual void RemoveAt(int index) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
        }

        [Serializable]
        private class ReadOnlyArrayList : ArrayList
        {
            private ArrayList _list;
    
            internal ReadOnlyArrayList(ArrayList l) {
                _list = l;
            }
    
            public override int Count { 
                get { return _list.Count; }
            }
    
            public override bool IsReadOnly {
                get { return true; }
            }

            public override bool IsFixedSize {
                get { return true; }
            }

            public override bool IsSynchronized { 
                get { return _list.IsSynchronized; }
            }
            
             public override Object this[int index] {
                get {
                    return _list[index];
                }
                set {
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
                }
            }
    
            public override Object SyncRoot {
                get { return _list.SyncRoot; }
            }
            
            public override int Add(Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public override void AddRange(ICollection c) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int BinarySearch(int index, int count, Object value, IComparer comparer) {
                return _list.BinarySearch(index, count, value, comparer);
            }


            public override int Capacity {
                get { return _list.Capacity; }
                [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
                set { throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection")); }
            }

            public override void Clear() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            public override Object Clone() {
                ReadOnlyArrayList arrayList = new ReadOnlyArrayList(_list);
                arrayList._list = (ArrayList)_list.Clone();
                return arrayList;
            }
    
            public override bool Contains(Object obj) {
                return _list.Contains(obj);
            }
    
            public override void CopyTo(Array array, int index) {
                _list.CopyTo(array, index);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void CopyTo(int index, Array array, int arrayIndex, int count) {
                _list.CopyTo(index, array, arrayIndex, count);
            }

            public override IEnumerator GetEnumerator() {
                return _list.GetEnumerator();
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override IEnumerator GetEnumerator(int index, int count) {
                return _list.GetEnumerator(index, count);
            }

            public override int IndexOf(Object value) {
                return _list.IndexOf(value);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex) {
                return _list.IndexOf(value, startIndex);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int IndexOf(Object value, int startIndex, int count) {
                return _list.IndexOf(value, startIndex, count);
            }
    
            public override void Insert(int index, Object obj) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void InsertRange(int index, ICollection c) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            public override int LastIndexOf(Object value) {
                return _list.LastIndexOf(value);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex) {
                return _list.LastIndexOf(value, startIndex);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex, int count) {
                return _list.LastIndexOf(value, startIndex, count);
            }

            public override void Remove(Object value) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public override void RemoveAt(int index) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void RemoveRange(int index, int count) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void SetRange(int index, ICollection c) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
    
            public override ArrayList GetRange(int index, int count) {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index<0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (Count - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                return new Range(this,index, count);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Reverse(int index, int count) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void Sort(int index, int count, IComparer comparer) {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }

            public override Object[] ToArray() {
                return _list.ToArray();
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override Array ToArray(Type type) {
                return _list.ToArray(type);
            }
    
            public override void TrimToSize() {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ReadOnlyCollection"));
            }
        }

        /// <summary>
        /// ʵ����һ��ArrayList��ö������ö����ʹ�õ��ڲ��汾���б�,��ȷ��û���޸���Ȼö���б�
        /// </summary>
        [Serializable]
        private sealed class ArrayListEnumerator : IEnumerator, ICloneable
        {
            private ArrayList list;
            private int index;
            private int endIndex;       // Where to stop.
            private int version;
            private Object currentElement;
            private int startIndex;     // Save this for Reset.
    
            /// <summary>
            /// ��ArrayListת��Ϊö��
            /// </summary>
            /// <param name="list">����ת����ArrayList</param>
            /// <param name="index">��ʼ����</param>
            /// <param name="count">ת����Ŀ</param>
            internal ArrayListEnumerator(ArrayList list, int index, int count) {
                this.list = list;
                startIndex = index;
                this.index = index - 1;
                endIndex = this.index + count;  // last valid index
                version = list._version;
                currentElement = null;
            }

            /// <summary>
            /// ����һ��ArrayListEnumerator����
            /// </summary>
            /// <returns></returns>
            public Object Clone() {
                return MemberwiseClone();
            }
    
            /// <summary>
            /// ��ö�����ƽ������ϵ���һ��Ԫ�ء�
            /// </summary>
            /// <returns>���ö�����ѳɹ����ƽ�����һ��Ԫ�أ���Ϊ true�����ö�������ݵ����ϵ�ĩβ����Ϊ false��</returns>
            public bool MoveNext() {
                if (version != list._version) throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumFailedVersion));
                if (index < endIndex) {//�ж������Ƿ���磬���δ���磬�����õ�ǰ���������͵�ǰ����
                    currentElement = list[++index];
                    return true;
                }
                else {
                    index = endIndex + 1;
                }
                
                return false;
            }
    
            /// <summary>
            /// ��ȡ�����еĵ�ǰԪ�ء�
            /// </summary>
            public Object Current {
                get {
                    if (index < startIndex) 
                        throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumNotStarted));
                    else if (index > endIndex) {
                        throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumEnded));
                    }
                    return currentElement;
                }
            }
       
            public void Reset() {
                if (version != list._version) throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumFailedVersion));
                index = startIndex - 1;                
            }
        }

        /// <summary>
        ///  ʵ��һ��ͨ�õ��б���ӷ�Χ��������һ��ʵ����List.GetRange���ص�Ĭ��ʵ��
        /// </summary>
        [Serializable]
        private class Range: ArrayList
        {
            /// <summary>
            /// ����ArrayList
            /// </summary>
            private ArrayList _baseList;
            /// <summary>
            /// ��������
            /// </summary>
            private int _baseIndex;

            /// <summary>
            /// �����С
            /// </summary>
            [ContractPublicPropertyName("Count")]
            private int _baseSize;
            /// <summary>
            /// ����汾
            /// </summary>
            private int _baseVersion;
                 
            /// <summary>
            /// ͨ��ArrayList�����ڲ��ӷ���
            /// </summary>
            /// <param name="list">�������</param>
            /// <param name="index">��ʼ����</param>
            /// <param name="count">����</param>
            internal Range(ArrayList list, int index, int count) : base(false) {
                _baseList = list;
                _baseIndex = index;
                _baseSize = count;
                _baseVersion = list._version;
                // we also need to update _version field to make Range of Range work
                _version = list._version;                
            }

            /// <summary>
            /// �ڲ�����ArrayList�ӷ�Χ
            /// </summary>
            /// <exception cref="InvalidOperationException">InvalidOperation_UnderlyingArrayListChanged</exception>
            private void InternalUpdateRange()
            {
                if (_baseVersion != _baseList._version)
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnderlyingArrayListChanged"));
            }

            /// <summary>
            /// ���°汾�������Ӽ��ϣ�ArrayList�汾��
            /// </summary>
            private void InternalUpdateVersion() {
                _baseVersion++;
                _version++;                
            }
            
            /// <summary>
            /// ��������ӵ���Χ��
            /// </summary>
            /// <param name="value">Ԫ�ض���</param>
            /// <returns></returns>
            public override int Add(Object value) {
                InternalUpdateRange();//���¼���
                _baseList.Insert(_baseIndex + _baseSize, value);//���û���Insert����
                InternalUpdateVersion();//���°汾
                return _baseSize++;
            }

            /// <summary>
            /// �����ϲ��뵽�ӷ�Χ��()
            /// </summary>
            /// <param name="c">����</param>
            public override void AddRange(ICollection c) {
                if( c ==  null ) {
                    throw new ArgumentNullException("c");
                }
                Contract.EndContractBlock();

                InternalUpdateRange();
                int count = c.Count;
                if( count > 0) {
                    _baseList.InsertRange(_baseIndex + _baseSize, c);//����ArrayList.InsertRange
                    InternalUpdateVersion();
                    _baseSize += count;
                }
            }

            #region ��������ArrayList����
            public override int BinarySearch(int index, int count, Object value, IComparer comparer)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();
                InternalUpdateRange();

                int i = _baseList.BinarySearch(_baseIndex + index, count, value, comparer);
                if (i >= 0) return i - _baseIndex;
                return i + _baseIndex;
            }

            public override int Capacity
            {
                get
                {
                    return _baseList.Capacity;
                }

                set
                {
                    if (value < Count) throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_SmallCapacity"));
                    Contract.EndContractBlock();
                }
            }


            public override void Clear()
            {
                InternalUpdateRange();
                if (_baseSize != 0)
                {
                    _baseList.RemoveRange(_baseIndex, _baseSize);
                    InternalUpdateVersion();
                    _baseSize = 0;
                }
            }

            public override Object Clone()
            {
                InternalUpdateRange();
                Range arrayList = new Range(_baseList, _baseIndex, _baseSize);
                arrayList._baseList = (ArrayList)_baseList.Clone();
                return arrayList;
            }

            public override bool Contains(Object item)
            {
                InternalUpdateRange();
                if (item == null)
                {
                    for (int i = 0; i < _baseSize; i++)
                        if (_baseList[_baseIndex + i] == null)
                            return true;
                    return false;
                }
                else
                {
                    for (int i = 0; i < _baseSize; i++)
                        if (_baseList[_baseIndex + i] != null && _baseList[_baseIndex + i].Equals(item))
                            return true;
                    return false;
                }
            }

            public override void CopyTo(Array array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException("array");
                if (array.Rank != 1)
                    throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
                if (index < 0)
                    throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (array.Length - index < _baseSize)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.CopyTo(_baseIndex, array, index, _baseSize);
            }

            public override void CopyTo(int index, Array array, int arrayIndex, int count)
            {
                if (array == null)
                    throw new ArgumentNullException("array");
                if (array.Rank != 1)
                    throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (array.Length - arrayIndex < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.CopyTo(_baseIndex + index, array, arrayIndex, count);
            }

            public override int Count
            {
                get
                {
                    InternalUpdateRange();
                    return _baseSize;
                }
            }

            public override bool IsReadOnly
            {
                get { return _baseList.IsReadOnly; }
            }

            public override bool IsFixedSize
            {
                get { return _baseList.IsFixedSize; }
            }

            public override bool IsSynchronized
            {
                get { return _baseList.IsSynchronized; }
            }

            public override IEnumerator GetEnumerator()
            {
                return GetEnumerator(0, _baseSize);
            }

            public override IEnumerator GetEnumerator(int index, int count)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                return _baseList.GetEnumerator(_baseIndex + index, count);
            }

            public override ArrayList GetRange(int index, int count)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                return new Range(this, index, count);
            }

            public override Object SyncRoot
            {
                get
                {
                    return _baseList.SyncRoot;
                }
            }


            public override int IndexOf(Object value)
            {
                InternalUpdateRange();
                int i = _baseList.IndexOf(value, _baseIndex, _baseSize);
                if (i >= 0) return i - _baseIndex;
                return -1;
            }

            public override int IndexOf(Object value, int startIndex)
            {
                if (startIndex < 0)
                    throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (startIndex > _baseSize)
                    throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                int i = _baseList.IndexOf(value, _baseIndex + startIndex, _baseSize - startIndex);
                if (i >= 0) return i - _baseIndex;
                return -1;
            }

            public override int IndexOf(Object value, int startIndex, int count)
            {
                if (startIndex < 0 || startIndex > _baseSize)
                    throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));

                if (count < 0 || (startIndex > _baseSize - count))
                    throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_Count"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                int i = _baseList.IndexOf(value, _baseIndex + startIndex, count);
                if (i >= 0) return i - _baseIndex;
                return -1;
            }

            public override void Insert(int index, Object value)
            {
                if (index < 0 || index > _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.Insert(_baseIndex + index, value);
                InternalUpdateVersion();
                _baseSize++;
            }

            public override void InsertRange(int index, ICollection c)
            {
                if (index < 0 || index > _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                if (c == null)
                {
                    throw new ArgumentNullException("c");
                }
                Contract.EndContractBlock();

                InternalUpdateRange();
                int count = c.Count;
                if (count > 0)
                {
                    _baseList.InsertRange(_baseIndex + index, c);
                    _baseSize += count;
                    InternalUpdateVersion();
                }
            }

            public override int LastIndexOf(Object value)
            {
                InternalUpdateRange();
                int i = _baseList.LastIndexOf(value, _baseIndex + _baseSize - 1, _baseSize);
                if (i >= 0) return i - _baseIndex;
                return -1;
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex)
            {
                return LastIndexOf(value, startIndex, startIndex + 1);
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override int LastIndexOf(Object value, int startIndex, int count)
            {
                InternalUpdateRange();
                if (_baseSize == 0)
                    return -1;

                if (startIndex >= _baseSize)
                    throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                if (startIndex < 0)
                    throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));

                int i = _baseList.LastIndexOf(value, _baseIndex + startIndex, count);
                if (i >= 0) return i - _baseIndex;
                return -1;
            }

            // Don't need to override Remove

            public override void RemoveAt(int index)
            {
                if (index < 0 || index >= _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.RemoveAt(_baseIndex + index);
                InternalUpdateVersion();
                _baseSize--;
            }

            public override void RemoveRange(int index, int count)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                // RemoveRange�������Ϊ0������Ҫ����_bastList��
                // ����,�������Ϊ0��baseList����ı�vresion����
                if (count > 0)
                {
                    _baseList.RemoveRange(_baseIndex + index, count);
                    InternalUpdateVersion();
                    _baseSize -= count;
                }
            }

            public override void Reverse(int index, int count)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.Reverse(_baseIndex + index, count);
                InternalUpdateVersion();
            }

            [SuppressMessage("Microsoft.Contracts", "CC1055")]  // Skip extra error checking to avoid *potential* AppCompat problems.
            public override void SetRange(int index, ICollection c)
            {
                InternalUpdateRange();
                if (index < 0 || index >= _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                _baseList.SetRange(_baseIndex + index, c);
                if (c.Count > 0)
                {
                    InternalUpdateVersion();
                }
            }

            public override void Sort(int index, int count, IComparer comparer)
            {
                if (index < 0 || count < 0)
                    throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                if (_baseSize - index < count)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
                Contract.EndContractBlock();

                InternalUpdateRange();
                _baseList.Sort(_baseIndex + index, count, comparer);
                InternalUpdateVersion();
            }

            public override Object this[int index]
            {
                get
                {
                    InternalUpdateRange();
                    if (index < 0 || index >= _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                    return _baseList[_baseIndex + index];
                }
                set
                {
                    InternalUpdateRange();
                    if (index < 0 || index >= _baseSize) throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_Index"));
                    _baseList[_baseIndex + index] = value;
                    InternalUpdateVersion();
                }
            }

            public override Object[] ToArray()
            {
                InternalUpdateRange();
                Object[] array = new Object[_baseSize];
                Array.Copy(_baseList._items, _baseIndex, array, 0, _baseSize);
                return array;
            }

            [SecuritySafeCritical]
            public override Array ToArray(Type type)
            {
                if (type == null)
                    throw new ArgumentNullException("type");
                Contract.EndContractBlock();

                InternalUpdateRange();
                Array array = Array.UnsafeCreateInstance(type, _baseSize);
                _baseList.CopyTo(_baseIndex, array, 0, _baseSize);
                return array;
            }

            public override void TrimToSize()
            {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_RangeCollection"));
            } 
            #endregion
        }

        [Serializable]
        private sealed class ArrayListEnumeratorSimple : IEnumerator, ICloneable {
            private ArrayList list;
            private int index;
            private int version;
            private Object currentElement;
            [NonSerialized]
            private bool isArrayList;
            // this object is used to indicate enumeration has not started or has terminated
            static Object dummyObject = new Object();  
                            
            internal ArrayListEnumeratorSimple(ArrayList list) {
                this.list = list;
                this.index = -1;
                version = list._version;
                isArrayList = (list.GetType() == typeof(ArrayList));
                currentElement = dummyObject;                
            }
            
            public Object Clone() {
                return MemberwiseClone();
            }
    
            public bool MoveNext() {
                if (version != list._version) {
                    throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumFailedVersion));
                }

                if( isArrayList) {  // avoid calling virtual methods if we are operating on ArrayList to improve performance
                    if (index < list._size - 1) {
                        currentElement = list._items[++index];
                        return true;
                    }
                    else {
                        currentElement = dummyObject;
                        index =list._size;
                        return false;
                    }                    
                }
                else {                    
                    if (index < list.Count - 1) {
                        currentElement = list[++index];
                        return true;
                    }
                    else {
                        index = list.Count;
                        currentElement = dummyObject;
                        return false;
                    }
                }
            }
    
            public Object Current {
                get {
                    object temp = currentElement;
                    if(dummyObject == temp) { // check if enumeration has not started or has terminated
                        if (index == -1) {
                            throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumNotStarted));
                        }
                        else {                    
                            throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumEnded));                        
                        }
                    }

                    return temp;
                }
            }
    
            public void Reset() {
                if (version != list._version) {
                    throw new InvalidOperationException(Environment.GetResourceString(ResId.InvalidOperation_EnumFailedVersion));
                }    
                
                currentElement = dummyObject;
                index = -1;
            }
        }

        internal class ArrayListDebugView {
            private ArrayList arrayList; 
        
            public ArrayListDebugView( ArrayList arrayList) {
                if( arrayList == null)
                    throw new ArgumentNullException("arrayList");

                this.arrayList = arrayList;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public Object[] Items { 
                get {
                    return arrayList.ToArray();
                }
            }
        }
    }    
}
