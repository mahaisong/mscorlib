using System.Diagnostics.Contracts;
// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  ListDictionaryInternal
** 
** <OWNER>[....]</OWNER>
**
**
** Purpose: List for exceptions.
**
** 
===========================================================*/

namespace System.Collections {
    ///    This is a simple implementation of IDictionary using a singly linked list. This
    ///    will be smaller and faster than a Hashtable if the number of elements is 10 or less.
    ///    This should not be used if performance is important for large numbers of elements.
    ///    ����һ���򵥵�ʵ��ʹ��һ��������IDictionary���⽫�Ǹ�С,��һ��Hashtable���Ԫ�ص�������10����١���Ӧʹ���������Ԫ�ص������Ǻ���Ҫ�ġ�
    [Serializable]
    internal class ListDictionaryInternal: IDictionary {
        DictionaryNode head;
        int version;
        int count;
        [NonSerialized]
        private Object _syncRoot;

        public ListDictionaryInternal() {
        }

        public Object this[Object key]
        {
            get
            {
                if(key == null)
                {
                    throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
                }
                Contract.EndContractBlock();
                DictionaryNode node = head;

                while(node != null)
                {
                    if(node.key.Equals(key))
                    {
                        return node.value;
                    }
                    node = node.next;
                }
                return null;
            }
            set
            {
                if(key == null)
                {
                    throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
                }
                Contract.EndContractBlock();

#if FEATURE_SERIALIZATION
                if (!key.GetType().IsSerializable)
                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "key");
                if((value !=null)&&(!value.GetType().IsSerializable))
                {
                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "value");
                }
#endif
                version++;
                DictionaryNode last = null;
                DictionaryNode node;
                for(node = head; node != null; node = node.next)
                {
                    if(node.key.Equals(key))
                    {
                        break;
                    }
                    last = node;
                }

                if(node!= null)
                {
                    node.value = value;
                    return;
                }

                DictionaryNode newNode = new DictionaryNode();
                newNode.key = key;
                newNode.value = value;
                if(last!= null)
                {
                    last.next = newNode;
                }
                else
                {
                    head = newNode;
                }
                count++;
            }
        }

//        public Object this[Object key] {
//            get {
//                if (key == null) {
//                    throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
//                }
//                Contract.EndContractBlock();
//                DictionaryNode node = head;

//                while (node != null) {
//                    if ( node.key.Equals(key) ) {
//                        return node.value;
//                    }
//                    node = node.next;
//                }
//                return null;
//            }
//            set {
//                if (key == null) {
//                    throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
//                }
//                Contract.EndContractBlock();

//#if FEATURE_SERIALIZATION
//                if (!key.GetType().IsSerializable)                 
//                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "key");                    

//                if( (value != null) && (!value.GetType().IsSerializable ) )
//                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "value");                    
//#endif
                
//                version++;
//                DictionaryNode last = null;
//                DictionaryNode node;
//                for (node = head; node != null; node = node.next) {
//                    if( node.key.Equals(key) ) {
//                        break;
//                    } 
//                    last = node;
//                }
//                if (node != null) {
//                    // Found it
//                    node.value = value;
//                    return;
//                }
//                // û�нڵ㣬���Ǿͼ���һ���µĽڵ�
//                DictionaryNode newNode = new DictionaryNode();
//                newNode.key = key;
//                newNode.value = value;
//                if (last != null) {
//                    last.next = newNode;
//                }
//                else {
//                    head = newNode;
//                }
//                count++;
//            }
//        }

        public int Count {
            get {
                return count;
            }
        }   

        public ICollection Keys {
            get {
                return new NodeKeyValueCollection(this, true);
            }
        }

        public bool IsReadOnly {
            get {
                return false;
            }
        }

        public bool IsFixedSize {
            get {
                return false;
            }
        }

        public bool IsSynchronized {
            get {
                return false;
            }
        }

        //public Object SyncRoot {
        //    get {
        //        if( _syncRoot == null) {
        //            System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);    
        //        }
        //        return _syncRoot; 
        //    }
        //}

        public Object SyncRoot
        {
            get
            {
                if(_syncRoot == null)
                {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                }
                return _syncRoot;
            }
        }

        public ICollection Values {
            get {
                return new NodeKeyValueCollection(this, false);
            }
        }

        /// <summary>
        /// ����ֵ����ӵ��ֵ��б��У�Object this[Object key] get ���Ƶ��������˶Լ���ֵ����֤��
        /// </summary>
        /// <param name="key">��</param>
        /// <param name="value">ֵ</param>
        public void Add(Object key, Object value) {
            if (key == null) {
                throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
            }
            Contract.EndContractBlock();

#if FEATURE_SERIALIZATION
            if (!key.GetType().IsSerializable)                 
                throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "key" );                    

            if( (value != null) && (!value.GetType().IsSerializable) )
                throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), "value");                    
#endif
            
            version++;
            DictionaryNode last = null;
            DictionaryNode node;
            for (node = head; node != null; node = node.next) {//�������ͬ�ļ��������׳������쳣
                if (node.key.Equals(key)) {
                    throw new ArgumentException(Environment.GetResourceString("Argument_AddingDuplicate__", node.key, key));
                } 
                last = node;
            }
            if (node != null) {
                // Found it
                node.value = value;
                return;
            }
            // ���δ���֣������һ���µĽڵ�
            DictionaryNode newNode = new DictionaryNode();
            newNode.key = key;
            newNode.value = value;
            if (last != null) {
                last.next = newNode;
            }
            else {
                head = newNode;
            }
            count++;
        }

        /// <summary>
        /// ���ֵ�������
        /// �������
        /// head��ʽ���
        /// </summary>
        public void Clear() {
            count = 0;
            head = null;
            version++;
        }

        ///// <summary>
        ///// �ж��Ƿ������
        ///// </summary>
        ///// <param name="key">��</param>
        ///// <returns></returns>
        //public bool Contains(Object key) {
        //    if (key == null) {
        //        throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
        //    }
        //    Contract.EndContractBlock();
        //    for (DictionaryNode node = head; node != null; node = node.next) {
        //        if (node.key.Equals(key)) {
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        public bool Contains(Object key)
        {
            if(key == null)
            {
                throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
            }
            Contract.EndContractBlock();
            for(DictionaryNode node = head;node!=null;node = node.next)
            {
                if(node.key.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="array"></param>
        ///// <param name="index"></param>
        //public void CopyTo(Array array, int index)  {
        //    if (array==null)
        //        throw new ArgumentNullException("array");
                
        //    if (array.Rank != 1)
        //        throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));

        //    if (index < 0)
        //            throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));

        //    if ( array.Length - index < this.Count ) 
        //        throw new ArgumentException( Environment.GetResourceString("ArgumentOutOfRange_Index"), "index");
        //    Contract.EndContractBlock();

        //    for (DictionaryNode node = head; node != null; node = node.next) {
        //        array.SetValue(new DictionaryEntry(node.key, node.value), index);
        //        index++;
        //    }
        //}

        public void CopyTo(Array array,int index)
        {
            if(array == null)
            {
                throw new ArgumentException("array");
            }
            if(array.Rank != 1)
            {
                throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
            }
            if(index < 0)
            {
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            }
            if(array.Length - index < this.count)
            {
                throw new ArgumentException(Environment.GetResourceString("ArgumentOutOfRange_Index"), "index");
            }

            Contract.EndContractBlock();
            for(DictionaryNode node = head; node != null;node = node.next)
            {
                array.SetValue(new DictionaryEntry(node.key, node.value), index);
                index++;
            }
        }

        //public IDictionaryEnumerator GetEnumerator() {
        //    return new NodeEnumerator(this);
        //}

        public IDictionaryEnumerator GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new NodeEnumerator(this);
        }

        //public void Remove(Object key) {
        //    if (key == null) {
        //        throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
        //    }
        //    Contract.EndContractBlock();
        //    version++;
        //    DictionaryNode last = null;
        //    DictionaryNode node;
        //    for (node = head; node != null; node = node.next) {
        //        if (node.key.Equals(key)) {
        //            break;
        //        } 
        //        last = node;
        //    }
        //    if (node == null) {
        //        return;
        //    }          
        //    if (node == head) {
        //        head = node.next;
        //    } else {
        //        last.next = node.next;
        //    }
        //    count--;
        //}

        public void Remove(Object key)
        {
            if(key == null)
            {
                throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
            }
            Contract.EndContractBlock();
            version++;
            DictionaryNode last = null;
            DictionaryNode node;
            for(node = head; node!= null; node= node.next)
            {
                if(node.key.Equals(key))
                {
                    break;
                }
                last = node;
            }
            if(node == null)
            {
                return;
            }

            if(node == head)
            {
                head = head.next;
            }
            else
            {
                last.next = node.next;
            }

            count--;
        }

        //private class NodeEnumerator : IDictionaryEnumerator {
        //    ListDictionaryInternal list;
        //    DictionaryNode current;
        //    int version;
        //    bool start;


        //    public NodeEnumerator(ListDictionaryInternal list) {
        //        this.list = list;
        //        version = list.version;
        //        start = true;
        //        current = null;
        //    }

        //    public Object Current {
        //        get {
        //            return Entry;
        //        }
        //    }

        //    public DictionaryEntry Entry {
        //        get {
        //            if (current == null) {
        //                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
        //            }
        //            return new DictionaryEntry(current.key, current.value);
        //        }
        //    }

        //    public Object Key {
        //        get {
        //            if (current == null) {
        //                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
        //            }
        //            return current.key;
        //        }
        //    }

        //    public Object Value {
        //        get {
        //            if (current == null) {
        //                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
        //            }
        //            return current.value;
        //        }
        //    }

        //    public bool MoveNext() {
        //        if (version != list.version) {
        //            throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
        //        }
        //        if (start) {
        //            current = list.head;
        //            start = false;
        //        }
        //        else {
        //            if( current != null ) {
        //                current = current.next;
        //            }
        //        }
        //        return (current != null);
        //    }

        //    public void Reset() {
        //        if (version != list.version) {
        //            throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
        //        }
        //        start = true;
        //        current = null;
        //    }
            
        //}

        private class NodeEnumerator : IDictionaryEnumerator
        {
            ListDictionaryInternal list;
            DictionaryNode current;
            int version;
            bool start;

            public NodeEnumerator(ListDictionaryInternal list)
            {
                this.list = list;
                version = list.version;
                start = true;
                current = null;
            }

            public DictionaryEntry Current
            {
                get
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry
            {
                get
                {
                    if (current == null)
                    {
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                    }
                    return new DictionaryEntry(current.key,current.value);
                }
            }

            public Object Key
            {
                get
                {
                    if(current==null)
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                    return current.key;
                }
            }

            public Object value
            {
                get
                {
                    if(current==null)
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                    return current.value;
                }
            }

            public bool MoveNext()
            {
                if(version!=list.version)
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
                if(start)
                {
                    current = list.head;
                    start = false;
                }
                else
                {
                    if(current!=null)
                    {
                        current = current.next;
                    }
                }
                return (current != null);
            }

            public void Reset()
            {
                if(version!=null)
                {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
                }
                start = true;
                current = null;
            }
        }


        //private class NodeKeyValueCollection : ICollection {
        //    ListDictionaryInternal list;
        //    bool isKeys;

        //    public NodeKeyValueCollection(ListDictionaryInternal list, bool isKeys) {
        //        this.list = list;
        //        this.isKeys = isKeys;
        //    }

        //    void ICollection.CopyTo(Array array, int index)  {
        //        if (array==null)
        //            throw new ArgumentNullException("array");
        //        if (array.Rank != 1)
        //            throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
        //        if (index < 0)
        //            throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
        //        Contract.EndContractBlock();
        //        if (array.Length - index < list.Count) 
        //            throw new ArgumentException(Environment.GetResourceString("ArgumentOutOfRange_Index"), "index");                
        //        for (DictionaryNode node = list.head; node != null; node = node.next) {
        //            array.SetValue(isKeys ? node.key : node.value, index);
        //            index++;
        //        }
        //    }

        //    int ICollection.Count {
        //        get {
        //            int count = 0;
        //            for (DictionaryNode node = list.head; node != null; node = node.next) {
        //                count++;
        //            }
        //            return count;
        //        }
        //    }   

        //    bool ICollection.IsSynchronized {
        //        get {
        //            return false;
        //        }
        //    }

        //    Object ICollection.SyncRoot {
        //        get {
        //            return list.SyncRoot;
        //        }
        //    }

        //    IEnumerator IEnumerable.GetEnumerator() {
        //        return new NodeKeyValueEnumerator(list, isKeys);
        //    }


        //    private class NodeKeyValueEnumerator: IEnumerator {
        //        ListDictionaryInternal list;
        //        DictionaryNode current;
        //        int version;
        //        bool isKeys;
        //        bool start;

        //        public NodeKeyValueEnumerator(ListDictionaryInternal list, bool isKeys) {
        //            this.list = list;
        //            this.isKeys = isKeys;
        //            this.version = list.version;
        //            this.start = true;
        //            this.current = null;
        //        }

        //        public Object Current {
        //            get {
        //                if (current == null) {
        //                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
        //                }
        //                return isKeys ? current.key : current.value;
        //            }
        //        }

        //        public bool MoveNext() {
        //            if (version != list.version) {
        //                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
        //            }
        //            if (start) {
        //                current = list.head;
        //                start = false;
        //            }
        //            else {
        //                if( current != null) {
        //                    current = current.next;
        //                }
        //            }
        //            return (current != null);
        //        }

        //        public void Reset() {
        //            if (version != list.version) {
        //                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
        //            }
        //            start = true;
        //            current = null;
        //        }
        //    }        
        //}

        private class NodeKeyValueCollection : ICollection
        {
            ListDictionaryInternal list;
            bool isKeys;

            public NodeKeyValueCollection(ListDictionaryInternal list,bool isKeys)
            {
                this.list = list;
                this.isKeys = isKeys;
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException("array");
                }
                if(array.Rank!=1)
                {
                    throw new ArgumentException(Environment.GetResourceString("Arg_RankMultiDimNotSupported"));
                }
                if(index < 0)
                {
                    throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                }
                Contract.EndContractBlock();
                if (array.Length - index < list.count)
                    throw new ArgumentException(Environment.GetResourceString("ArgumentOutOfRange_Index"), "index");
                for(DictionaryNode node = list.head;node != null;node = node.next)
                {
                    array.SetValue(isKeys ? node.key : node.value, index);
                }
            }

            int ICollection.Count
            {
                get {
                    int count = 0;
                    for(DictionaryNode node = list.head;node != null; node= node.next)
                    {
                        count++;
                    }
                    return count;
                }
            }

            object ICollection.SyncRoot
            {
                get { return false; }
            }

            bool ICollection.IsSynchronized
            {
                get { return list.IsSynchronized; }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return list.GetEnumerator();
            }

            private class NodeKeyValueEnumerator : IEnumerable
            {

                ListDictionaryInternal list;
                DictionaryNode current;
                int version;
                bool isKeys;
                bool start;

                public NodeKeyValueEnumerator(ListDictionaryInternal list,bool isKsys)
                {
                    this.list = list;
                    this.version = list.version;
                    this.isKeys = isKsys;
                    start = true;
                    this.current = null;
                }

                public IEnumerator GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                public Object Current
                {
                    get
                    {
                        if(current!= null)
                        {
                            throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumOpCantHappen"));
                        }

                        return isKeys ? current.key : current.value; 
                    }
                }

                public bool MoveNext()
                {
                    if(version != list.version)
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
                    if(start)
                    {
                        current = list.head;
                        start = false;
                    }else
                    {
                        if(current != null)
                        {
                            current = current.next;
                        }
                    }
                    return (current != null);
                }

                public void Reset()
                {
                    if(version != list.version)
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_EnumFailedVersion"));
                    start = true;
                    current = null;
                }
            }
        }

        /// <summary>
        /// �ֵ�ڵ�
        /// </summary>
        [Serializable]
        private class DictionaryNode {
            public Object key;
            public Object value;
            public DictionaryNode next;
        }
    }
}
