// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
namespace System {
    
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// �ṩ�������ֵ��ʽ��Ϊ�ַ�����ʾ��ʽ�Ĺ��ܡ�
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
#if CONTRACTS_FULL
    [ContractClass(typeof(IFormattableContract))]
#endif // CONTRACTS_FULL
    public interface IFormattable
    {
        /// <summary>
        /// ʹ��ָ���ĸ�ʽ��ʽ����ǰʵ����ֵ��
        /// </summary>
        /// <param name="format">Ҫʹ�õĸ�ʽ��</param>
        /// <param name="formatProvider">Ҫ��������ֵ��ʽ���ṩ����</param>
        /// <returns>ʹ��ָ����ʽ�ĵ�ǰʵ����ֵ��</returns>
        [Pure]
        String ToString(String format, IFormatProvider formatProvider);
    }

#if CONTRACTS_FULL
    [ContractClassFor(typeof(IFormattable))]
    internal abstract class IFormattableContract : IFormattable
    {
       String IFormattable.ToString(String format, IFormatProvider formatProvider)
       {
           Contract.Ensures(Contract.Result<String>() != null);
 	       throw new NotImplementedException();
       }
    }
#endif // CONTRACTS_FULL
}
