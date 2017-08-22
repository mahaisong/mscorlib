namespace System.Collections {

    /// <summary>
    /// ���巽����֧�ֶ���Ľṹ����ԱȽϡ�
    /// </summary>
    public interface IStructuralEquatable {
        /// <summary>
        /// ȷ��ĳ�������뵱ǰʵ���ڽṹ���Ƿ���ȡ�
        /// </summary>
        /// <param name="other">Ҫ�뵱ǰʵ�����бȽϵĶ���</param>
        /// <param name="comparer">һ����ȷ����ǰʵ���� other �Ƿ���ȵĶ���</param>
        /// <returns></returns>
        Boolean Equals(Object other, IEqualityComparer comparer);
        
        /// <summary>
        /// ���ص�ǰʵ���Ĺ�ϣ���롣
        /// </summary>
        /// <param name="comparer"></param>
        /// <returns></returns>
        int GetHashCode(IEqualityComparer comparer);
    }
}