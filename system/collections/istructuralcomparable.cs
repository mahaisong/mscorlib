using System;

namespace System.Collections {

    /// <summary>
    /// �ṹ��Ƚ����ӿ�
    /// </summary>
    public interface IStructuralComparable {
        Int32 CompareTo(Object other, IComparer comparer);
    }
}
