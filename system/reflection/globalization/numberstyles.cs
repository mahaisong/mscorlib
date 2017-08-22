// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Enum:  NumberStyles.cs
**
**
** Purpose: Contains valid formats for Numbers recognized by
** the Number class' parsing code.
**
**
===========================================================*/
namespace System.Globalization {
    
    using System;
    /// <summary>
    /// ȷ�������ַ����������������ʽ����Щ�����Ѵ��ݵ������͸��������͵� Parse �� TryParse ������
    /// </summary>
    [Serializable]
    [Flags]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum NumberStyles {
        // Bit flag indicating that leading whitespace is allowed. Character values
        // 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, and 0x0020 are considered to be
        // whitespace.
        /// <summary>
        /// ��
        /// </summary>
        None                  = 0x00000000, 
        /// <summary>
        /// ָʾ���������ַ����п��Դ���ǰ���հ��ַ�
        /// </summary>
        AllowLeadingWhite     = 0x00000001, 
        /// <summary>
        /// ָʾ���������ַ����п��Դ��ڽ�β�հ��ַ�
        /// </summary>
        AllowTrailingWhite    = 0x00000002, //Bitflag indicating trailing whitespace is allowed.
        /// <summary>
        /// ָʾ�����ַ������Ծ���ǰ�����š�
        /// </summary>
        AllowLeadingSign      = 0x00000004, //Can the number start with a sign char.  
                                            //Specified by NumberFormatInfo.PositiveSign and NumberFormatInfo.NegativeSign
        /// <summary>
        /// ָʾ�����ַ������Ծ��н�β���š�
        /// </summary>
        AllowTrailingSign     = 0x00000008, //Allow the number to end with a sign char

        /// <summary>
        /// ָʾ�����ַ������Ծ���һ�Խ����������������š�
        /// </summary>
        AllowParentheses      = 0x00000010, //Allow the number to be enclosed in parens
        /// <summary>
        /// ָʾ�����ַ������Ծ���С���㡣
        /// </summary>
        AllowDecimalPoint     = 0x00000020, //Allow a decimal point
        /// <summary>
        /// ָʾ�����ַ������Ծ�����ָ��������罫��λ��ǧλ�ָ������ķ��š�
        /// </summary>
        AllowThousands        = 0x00000040, //Allow thousands separators (more properly, allow group separators)
        /// <summary>
        /// ָʾ�����ַ�������ָ�������С�
        /// </summary>
        AllowExponent         = 0x00000080, //Allow an exponent
        /// <summary>
        /// ָʾ�����ַ����ɰ������ҷ��š�
        /// </summary>
        AllowCurrencySymbol   = 0x00000100, //Allow a currency symbol.
        /// <summary>
        /// ָʾ��ֵ�ַ�����ʾһ��ʮ������ֵ��
        /// </summary>
        AllowHexSpecifier     = 0x00000200, //Allow specifiying hexadecimal.
        //Common uses.  These represent some of the most common combinations of these flags.
    
        /// <summary>
        /// ָʾʹ�� AllowLeadingWhite��AllowTrailingWhite �� AllowLeadingSign ��ʽ�� ���Ǹ���������ʽ��
        /// </summary>
        Integer  = AllowLeadingWhite | AllowTrailingWhite | AllowLeadingSign,
        /// <summary>
        /// ָʾʹ�� AllowLeadingWhite��AllowTrailingWhite �� AllowHexSpecifier ��ʽ�� ���Ǹ���������ʽ�� 
        /// </summary>
        HexNumber = AllowLeadingWhite | AllowTrailingWhite | AllowHexSpecifier,
        /// <summary>
        /// ָʾʹ�� AllowLeadingWhite��AllowTrailingWhite��AllowLeadingSign��AllowTrailingSign��AllowDecimalPoint �� AllowThousands ��ʽ�� ���Ǹ���������ʽ�� 
        /// </summary>
        Number   = AllowLeadingWhite | AllowTrailingWhite | AllowLeadingSign | AllowTrailingSign |
                   AllowDecimalPoint | AllowThousands,
        /// <summary>
        /// ָʾʹ�� AllowLeadingWhite��AllowTrailingWhite��AllowLeadingSign��AllowDecimalPoint �� AllowExponent ��ʽ�� ���Ǹ���������ʽ�� 
        /// </summary>
        Float    = AllowLeadingWhite | AllowTrailingWhite | AllowLeadingSign | 
                   AllowDecimalPoint | AllowExponent,
        /// <summary>
        /// ָʾʹ�ó� AllowExponent �� AllowHexSpecifier �����������ʽ�� ���Ǹ���������ʽ�� 
        /// </summary>
        Currency = AllowLeadingWhite | AllowTrailingWhite | AllowLeadingSign | AllowTrailingSign |
                   AllowParentheses  | AllowDecimalPoint | AllowThousands | AllowCurrencySymbol,
        /// <summary>
        /// ָʾʹ�ó� AllowHexSpecifier �����������ʽ�� ���Ǹ���������ʽ��
        /// </summary>
        Any      = AllowLeadingWhite | AllowTrailingWhite | AllowLeadingSign | AllowTrailingSign |
                   AllowParentheses  | AllowDecimalPoint | AllowThousands | AllowCurrencySymbol | AllowExponent,
             
    }
}
