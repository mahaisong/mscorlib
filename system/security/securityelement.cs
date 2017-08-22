// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// <OWNER>[....]</OWNER>
// 

namespace System.Security
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Security.Util;
    using System.Text;
    using System.Globalization;
    using System.IO;
    using System.Security.Permissions;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// ��ȫԪ������
    /// </summary>
    internal enum SecurityElementType
    {
        /// <summary>
        /// ���ڵ�
        /// </summary>
        Regular = 0,
        /// <summary>
        /// ��ʽ
        /// </summary>
        Format = 1,
        /// <summary>
        /// ����
        /// </summary>
        Comment = 2
    }

    /// <summary>
    /// SecurityElement�Ľӿڹ���
    /// </summary>
    internal interface ISecurityElementFactory
    {
        /// <summary>
        /// ����SecurityElement����
        /// </summary>
        /// <returns></returns>
        SecurityElement CreateSecurityElement();

        /// <summary>
        /// ��ȡ��ǰ���󸱱�
        /// </summary>
        /// <returns></returns>
        Object Copy();

        /// <summary>
        /// ��ȡ��ǩ
        /// </summary>
        /// <returns></returns>
        String GetTag();

        /// <summary>
        /// ��ȡ�����ַ���
        /// </summary>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        String Attribute( String attributeName );
    }

    //  ���������ڰ�ȫϵͳ�м� XML ����ģ�͵�����ʵ�֣�����Ϊ���� XML ����ģ��ʹ�á�
    //  ע�⣺�������ܷ����ԭ��ֻ������ XML �ı���ʽ��Ԫ�ر���ʱ�Ž����ַ���Ч�Լ�飬�����Ƕ�ÿ�����Ի򷽷��������м�顣��̬��������������λ�ý�����ʽ��顣
    //
    /// <summary>
    /// ��ʾ���밲ȫ����� XML ����ģ�͡����಻�ܱ��̳С�
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    sealed public class SecurityElement : ISecurityElementFactory
    {
        /// <summary>
        /// ��ǩ�ַ�
        /// </summary>
        internal String  m_strTag;
        /// <summary>
        /// �����ֶ�
        /// </summary>
        internal String  m_strText;
        /// <summary>
        /// ��Ԫ������
        /// </summary>
        private ArrayList m_lChildren;
        /// <summary>
        /// Ԫ������
        /// </summary>
        internal ArrayList m_lAttributes;
        /// <summary>
        /// Ԫ�ظ�ʽ
        /// </summary>
        internal SecurityElementType m_type = SecurityElementType.Regular;
        
        private static readonly char[] s_tagIllegalCharacters = new char[] { ' ', '<', '>' };
        private static readonly char[] s_textIllegalCharacters = new char[] { '<', '>' };
        private static readonly char[] s_valueIllegalCharacters = new char[] { '<', '>', '\"' };
        private const String s_strIndent = "   ";
        
        /// <summary>
        /// ��־������
        /// </summary>
        private const int c_AttributesTypical = 4 * 2;  // 4�����ԣ�ÿ�����Ե�2���ַ���
        private const int c_ChildrenTypical = 1;  

        private static readonly String[] s_escapeStringPairs = new String[]
            {
                // these must be all once character escape sequences or a new escaping algorithm is needed
                "<", "&lt;",
                ">", "&gt;",
                "\"", "&quot;",
                "\'", "&apos;",
                "&", "&amp;"
            };

        private static readonly char[] s_escapeChars = new char[] { '<', '>', '\"', '\'', '&' };
        
        //-------------------------- Constructors ---------------------------
        
        internal SecurityElement()
        {
        }

////// ISecurityElementFactory implementation

        SecurityElement ISecurityElementFactory.CreateSecurityElement()
        {
            return this;
        }

        String ISecurityElementFactory.GetTag()
        {
            return ((SecurityElement)this).Tag;
        }

        Object ISecurityElementFactory.Copy()
        {
            return ((SecurityElement)this).Copy();
        }

        String ISecurityElementFactory.Attribute( String attributeName )
        {
            return ((SecurityElement)this).Attribute( attributeName );
        }

//////////////

#if FEATURE_CAS_POLICY
        public static SecurityElement FromString( String xml )
        {
            if (xml == null)
                throw new ArgumentNullException( "xml" );
            Contract.EndContractBlock();

            return new Parser( xml ).GetTopElement();
        }
#endif // FEATURE_CAS_POLICY
    
        public SecurityElement( String tag )
        {
            if (tag == null)
                throw new ArgumentNullException( "tag" );
        
            if (!IsValidTag( tag ))
                throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementTag" ), tag ) );
            Contract.EndContractBlock();
        
            m_strTag = tag;
            m_strText = null;
        }

        public SecurityElement( String tag, String text )
        {
            if (tag == null)
                throw new ArgumentNullException( "tag" );
        
            if (!IsValidTag( tag ))
                throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementTag" ), tag ) );

            if (text != null && !IsValidText( text ))
                throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementText" ), text ) );
            Contract.EndContractBlock();
        
            m_strTag = tag;
            m_strText = text;
        }
    
        //-------------------------- Properties -----------------------------

        /// <summary>
        /// ��ȡ������ XML Ԫ�صı�����ơ�
        /// </summary>
        public String Tag
        {
            [Pure]
            get
            {
                return m_strTag;
            }
            
            set
            {
                if (value == null)
                    throw new ArgumentNullException( "Tag" );
        
                if (!IsValidTag( value ))
                    throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementTag" ), value ) );
                Contract.EndContractBlock();
            
                m_strTag = value;
            }
        }

        /// <summary>
        /// ������/ֵ����ʽ��ȡ������ XML Ԫ�����ԡ�
        /// </summary>
        public Hashtable Attributes
        {
            get
            {
                if (m_lAttributes == null || m_lAttributes.Count == 0)//�������Ϊ�գ������Եĳ���Ϊ0.�򷵻�Ϊ��
                {
                    return null;
                }
                else
                {
                    Hashtable hashtable = new Hashtable(m_lAttributes.Count / 2);//��ʼ������Ϊ���Գ���/2��Hashtable
                                        
                    int iMax = m_lAttributes.Count;
                    Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
                    for (int i = 0; i < iMax; i += 2)
                    {
                        hashtable.Add( m_lAttributes[i], m_lAttributes[i+1]);
                    }
                                        
                    return hashtable;
                }
            }
            
            set
            {
                if (value == null || value.Count == 0)
                {
                    m_lAttributes = null;
                }
                else
                {
                    ArrayList list = new ArrayList(value.Count);
                    
                    System.Collections.IDictionaryEnumerator enumerator = (System.Collections.IDictionaryEnumerator)value.GetEnumerator();
                    
                    while (enumerator.MoveNext())
                    {
                        String attrName = (String)enumerator.Key;
                        String attrValue = (String)enumerator.Value;
                    
                        if (!IsValidAttributeName( attrName ))
                            throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementName" ), (String)enumerator.Current ) );
                        
                        if (!IsValidAttributeValue( attrValue ))
                            throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementValue" ), (String)enumerator.Value ) );
                    
                        list.Add(attrName);
                        list.Add(attrValue);
                    }
                    
                    m_lAttributes = list;
                }
            }
        }

        /// <summary>
        /// ��ȡ������ XML Ԫ���е��ı���
        /// </summary>
        public String Text
        {
            get
            {
                return Unescape( m_strText );
            }
            
            set
            {
                if (value == null)
                {
                    m_strText = null;
                }
                else
                {
                    if (!IsValidText( value ))
                        throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementTag" ), value ) );
                        
                    m_strText = value;
                }
            }
        }

        /// <summary>
        /// ��ȡ������ XML Ԫ����Ԫ�ص�����
        /// </summary>
        public ArrayList Children
        {
            get//��ȡXML Ԫ����Ԫ����Ԫ��SecurityElement
            {
                ConvertSecurityElementFactories();
                return m_lChildren;
            }

            set//����XML Ԫ����Ԫ��
            {
                if (value != null)//���ֵΪ�գ���ʼ��XMLԪ����Ԫ��
                {
                    IEnumerator enumerator = value.GetEnumerator();
                    
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current == null)
                            throw new ArgumentException( Environment.GetResourceString( "ArgumentNull_Child" ) );
                    }
                }
            
                m_lChildren = value;
            }
        }

        /// <summary>
        /// ת����ȫԪ�ع�������ISecurityElementFactoriesת��ΪSecurityElement
        /// </summary>
        internal void ConvertSecurityElementFactories()
        {
            if (m_lChildren == null)//��� XML Ԫ����Ԫ�ص�����Ϊ�գ��򷵻�Ϊ��
                return;

            for (int i = 0; i < m_lChildren.Count; ++i)
            {
                ISecurityElementFactory iseFactory = m_lChildren[i] as ISecurityElementFactory;//����Ԫ��ת��Ϊxml��ȫԪ�ع���ʵ��
                if (iseFactory != null && !(m_lChildren[i] is SecurityElement))//���Ԫ�ع�����Ϊ�գ�������Ԫ�ؿ���ת��ΪSecurityElement����
                    m_lChildren[i] = iseFactory.CreateSecurityElement();//�������SecurityElement����ֵ����Ԫ��������
            }
        }

        /// <summary>
        /// ��ȡ�ڲ���Ԫ������
        /// </summary>
        internal ArrayList InternalChildren
        {
            get
            {
                // С��!��������б���԰���SecurityElements������ISecurityElementFactories�����������һ��һ�µ�SecurityElement����,����get_Children��
                return m_lChildren;
            }
        }  
   
        //-------------------------- Public Methods -----------------------------
        
        /// <summary>
        /// �԰�ȫ����ʽ������/ֵ������ӵ� XML Ԫ�ء�
        /// </summary>
        /// <param name="name">��</param>
        /// <param name="value">ֵ</param>
        internal void AddAttributeSafe( String name, String value )
        {
            if (m_lAttributes == null)
            {
                m_lAttributes = new ArrayList( c_AttributesTypical  );
            }
            else
            {
                int iMax = m_lAttributes.Count;
                Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
                for (int i = 0; i < iMax; i += 2)
                {
                    String strAttrName = (String)m_lAttributes[i];
                    
                    if (String.Equals(strAttrName, name))
                        throw new ArgumentException( Environment.GetResourceString( "Argument_AttributeNamesMustBeUnique" ) );
                }
            }
        
            m_lAttributes.Add(name);
            m_lAttributes.Add(value);
        }        
        
        /// <summary>
        /// ������/ֵ������ӵ� XML Ԫ�ء�
        /// </summary>
        /// <param name="name">��</param>
        /// <param name="value">ֵ</param>
        public void AddAttribute( String name, String value )
        {   
            if (name == null)
                throw new ArgumentNullException( "name" );
                
            if (value == null)
                throw new ArgumentNullException( "value" );
        
            if (!IsValidAttributeName( name ))
                throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementName" ), name ) );
                
            if (!IsValidAttributeValue( value ))
                throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, Environment.GetResourceString( "Argument_InvalidElementValue" ), value ) );
            Contract.EndContractBlock();
        
            AddAttributeSafe( name, value );
        }

        public void AddChild( SecurityElement child )
        {   
            if (child == null)
                throw new ArgumentNullException( "child" );
            Contract.EndContractBlock();
        
            if (m_lChildren == null)
                m_lChildren = new ArrayList( c_ChildrenTypical  );
                
            m_lChildren.Add( child );
        }

        internal void AddChild( ISecurityElementFactory child )
        {
            if (child == null)
                throw new ArgumentNullException( "child" );
            Contract.EndContractBlock();
        
            if (m_lChildren == null)
                m_lChildren = new ArrayList( c_ChildrenTypical  );
             
            m_lChildren.Add( child );
        }            

        internal void AddChildNoDuplicates( ISecurityElementFactory child )
        {   
            if (child == null)
                throw new ArgumentNullException( "child" );
            Contract.EndContractBlock();
        
            if (m_lChildren == null)
            {
                m_lChildren = new ArrayList( c_ChildrenTypical  );
                m_lChildren.Add( child );
            }
            else
            {
                for (int i = 0; i < m_lChildren.Count; ++i)
                {
                    if (m_lChildren[i] == child)
                        return;
                }
                m_lChildren.Add( child );
            }
        }

        public bool Equal( SecurityElement other )
        {
            if (other == null)
                return false;
        
            // Check if the tags are the same
            if (!String.Equals(m_strTag, other.m_strTag))
                return false;

            // Check if the text is the same
            if (!String.Equals(m_strText, other.m_strText))
                return false;

            // Check if the attributes are the same and appear in the same
            // order.
            
            // Maybe we can get away by only checking the number of attributes
            if (m_lAttributes == null || other.m_lAttributes == null)
            {
                if (m_lAttributes != other.m_lAttributes)
                    return false;
            }
            else 
            {                
                int iMax = m_lAttributes.Count;
                Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );

                if (iMax != other.m_lAttributes.Count)
                    return false;
                          
                for (int i = 0; i < iMax; i++)
                {
                    String lhs = (String)m_lAttributes[i];
                    String rhs = (String)other.m_lAttributes[i];
                    
                    if (!String.Equals(lhs, rhs))
                        return false;
                }
            }

            // Finally we must check the child and make sure they are
            // equal and in the same order
            
            // Maybe we can get away by only checking the number of children
            if (m_lChildren == null || other.m_lChildren == null) 
            {
                if (m_lChildren != other.m_lChildren)
                    return false;
            } 
            else 
            {
                if (m_lChildren.Count != other.m_lChildren.Count)
                    return false;

                this.ConvertSecurityElementFactories();
                other.ConvertSecurityElementFactories();

                // Okay, we'll need to go through each one of them
                IEnumerator lhs = m_lChildren.GetEnumerator();
                IEnumerator rhs = other.m_lChildren.GetEnumerator();

                SecurityElement e1, e2;
                while (lhs.MoveNext())
                {
                    rhs.MoveNext();
                    e1 = (SecurityElement)lhs.Current;
                    e2 = (SecurityElement)rhs.Current;       
                    if (e1 == null || !e1.Equal(e2))                
                        return false;
                }
            }
            return true;
        }

        [System.Runtime.InteropServices.ComVisible(false)]
        public SecurityElement Copy()
        {
            SecurityElement element = new SecurityElement( this.m_strTag, this.m_strText );
            element.m_lChildren = this.m_lChildren == null ? null : new ArrayList( this.m_lChildren );
            element.m_lAttributes = this.m_lAttributes == null ? null : new ArrayList(this.m_lAttributes);

            return element;
        }

        [Pure]
        public static bool IsValidTag( String tag )
        {
            if (tag == null)
                return false;
                
            return tag.IndexOfAny( s_tagIllegalCharacters ) == -1;
        }

        [Pure]
        public static bool IsValidText( String text )
        {
            if (text == null)
                return false;
                
            return text.IndexOfAny( s_textIllegalCharacters ) == -1;
        }

        [Pure]
        public static bool IsValidAttributeName( String name )
        {
            return IsValidTag( name );
        }
        
        [Pure]
        public static bool IsValidAttributeValue( String value )
        {
            if (value == null)
                return false;
                
            return value.IndexOfAny( s_valueIllegalCharacters ) == -1;
        }

        private static String GetEscapeSequence( char c )
        {
            int iMax = s_escapeStringPairs.Length;
            Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
            for (int i = 0; i < iMax; i += 2)
            {
                String strEscSeq = s_escapeStringPairs[i];
                String strEscValue = s_escapeStringPairs[i+1];

                if (strEscSeq[0] == c)
                    return strEscValue;
            }

            Contract.Assert( false, "Unable to find escape sequence for this character" );
            return c.ToString();
        }

        public static String Escape( String str )
        {
            if (str == null)
                return null;

            StringBuilder sb = null;

            int strLen = str.Length;
            int index; // Pointer into the string that indicates the location of the current '&' character
            int newIndex = 0; // Pointer into the string that indicates the start index of the "remaining" string (that still needs to be processed).


            do
            {
                index = str.IndexOfAny( s_escapeChars, newIndex );

                if (index == -1)
                {
                    if (sb == null)
                        return str;
                    else
                    {
                        sb.Append( str, newIndex, strLen - newIndex );
                        return sb.ToString();
                    }
                }
                else
                {
                    if (sb == null)
                        sb = new StringBuilder();                    

                    sb.Append( str, newIndex, index - newIndex );
                    sb.Append( GetEscapeSequence( str[index] ) );

                    newIndex = ( index + 1 );
                }
            }
            while (true);
            
            // no normal exit is possible
        }

        private static String GetUnescapeSequence( String str, int index, out int newIndex )
        {
            int maxCompareLength = str.Length - index;

            int iMax = s_escapeStringPairs.Length;
            Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
            for (int i = 0; i < iMax; i += 2)
            {
                String strEscSeq = s_escapeStringPairs[i];
                String strEscValue = s_escapeStringPairs[i+1];
                
                int length = strEscValue.Length;

                if (length <= maxCompareLength && String.Compare( strEscValue, 0, str, index, length, StringComparison.Ordinal) == 0)
                {
                    newIndex = index + strEscValue.Length;
                    return strEscSeq;
                }
            }

            newIndex = index + 1;
            return str[index].ToString();
        }
            

        private static String Unescape( String str )
        {
            if (str == null)
                return null;

            StringBuilder sb = null;

            int strLen = str.Length;
            int index; // Pointer into the string that indicates the location of the current '&' character
            int newIndex = 0; // Pointer into the string that indicates the start index of the "remainging" string (that still needs to be processed).

            do
            {
                index = str.IndexOf( '&', newIndex );

                if (index == -1)
                {
                    if (sb == null)
                        return str;
                    else
                    {
                        sb.Append( str, newIndex, strLen - newIndex );
                        return sb.ToString();
                    }
                }
                else
                {
                    if (sb == null)
                        sb = new StringBuilder();

                    sb.Append(str,  newIndex, index - newIndex);
                    sb.Append( GetUnescapeSequence( str, index, out newIndex ) ); // updates the newIndex too

                }
            }
            while (true);

            // C# reports a warning if I leave this in, but I still kinda want to just in case.
            // Contract.Assert( false, "If you got here, the execution engine or compiler is really confused" );
            // return str;
        }

        private delegate void ToStringHelperFunc( Object obj, String str );

        private static void ToStringHelperStringBuilder( Object obj, String str )
        {
            ((StringBuilder)obj).Append( str );
        }
        
        private static void ToStringHelperStreamWriter( Object obj, String str )
        {
            ((StreamWriter)obj).Write( str );
        }

        public override String ToString ()
        {
            StringBuilder sb = new StringBuilder();

            ToString( "", sb, new ToStringHelperFunc( ToStringHelperStringBuilder ) );

            return sb.ToString();
        }

        internal void ToWriter( StreamWriter writer )
        {
            ToString( "", writer, new ToStringHelperFunc( ToStringHelperStreamWriter ) );
        }
        
        private void ToString( String indent, Object obj, ToStringHelperFunc func )
        {
            // First add the indent
            
            // func( obj, indent );                       
            
            // Add in the opening bracket and the tag.
            
            func( obj, "<" );

            switch (m_type)
            {
                case SecurityElementType.Format:
                    func( obj, "?" );
                    break;

                case SecurityElementType.Comment:
                    func( obj, "!" );
                    break;

                default:
                    break;
            }

            func( obj, m_strTag );
            
            // If there are any attributes, plop those in.
            
            if (m_lAttributes != null && m_lAttributes.Count > 0)
            {
                func( obj, " " );

                int iMax = m_lAttributes.Count;
                Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
                for (int i = 0; i < iMax; i += 2)
                {
                    String strAttrName = (String)m_lAttributes[i];
                    String strAttrValue = (String)m_lAttributes[i+1];
                    
                    func( obj, strAttrName );
                    func( obj, "=\"" );
                    func( obj, strAttrValue );
                    func( obj, "\"" );

                    if (i != m_lAttributes.Count - 2)
                    {
                        if (m_type == SecurityElementType.Regular)
                        {
                            func( obj, Environment.NewLine );
                        }
                        else
                        {
                            func( obj, " " );
                        }
                    }
                }
            }
         
            if (m_strText == null && (m_lChildren == null || m_lChildren.Count == 0))
            {
                // If we are a single tag with no children, just add the end of tag text.
    
                switch (m_type)
                {
                    case SecurityElementType.Comment:
                        func( obj, ">" );
                        break;

                    case SecurityElementType.Format:
                        func( obj, " ?>" );
                        break;

                    default:
                        func( obj, "/>" );
                        break;
                }
                func( obj, Environment.NewLine );
            }
            else
            {
                // Close the current tag.
            
                func( obj, ">" );
                
                // Output the text
                
                func( obj, m_strText );
                
                // Output any children.
                
                if (m_lChildren != null)
                {
                    this.ConvertSecurityElementFactories();

                    func( obj, Environment.NewLine );
                
                    // String childIndent = indent + s_strIndent;
                
                    for (int i = 0; i < m_lChildren.Count; ++i)                    
                    {
                        ((SecurityElement)m_lChildren[i]).ToString( "", obj, func );
                    }

                    // In the case where we have children, the close tag will not be on the same line as the
                    // opening tag, so we need to indent.

                    // func( obj, indent );
                }
                
                // Output the closing tag
                
                func( obj, "</" );
                func( obj, m_strTag );
                func( obj, ">" );
                func( obj, Environment.NewLine );
            }
        }
                
                

        public String Attribute( String name )
        {
            if (name == null)
                throw new ArgumentNullException( "name" );
            Contract.EndContractBlock();
                
            // Note: we don't check for validity here because an
            // if an invalid name is passed we simply won't find it.
        
            if (m_lAttributes == null)
                return null;
                            
            // Go through all the attribute and see if we know about
            // the one we are asked for

            int iMax = m_lAttributes.Count;
            Contract.Assert( iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly" );
                          
            for (int i = 0; i < iMax; i += 2)
            {
                String strAttrName = (String)m_lAttributes[i];
                
                if (String.Equals(strAttrName, name))
                {
                    String strAttrValue = (String)m_lAttributes[i+1];
                    
                    return Unescape(strAttrValue);
                }
            }

            // In the case where we didn't find it, we are expected to
            // return null
            return null;
        }

        public SecurityElement SearchForChildByTag( String tag )
        {
            // Go through all the children and see if we can
            // find the one are are asked for (matching tags)

            if (tag == null)
                throw new ArgumentNullException( "tag" );
            Contract.EndContractBlock();
                
            // Note: we don't check for a valid tag here because
            // an invalid tag simply won't be found.    
                
            if (m_lChildren == null)
                return null;

            IEnumerator enumerator = m_lChildren.GetEnumerator();

            while (enumerator.MoveNext())
            {
                SecurityElement current = (SecurityElement)enumerator.Current;
            
                if (current != null && String.Equals(current.Tag, tag))
                    return current;
            }
            return null;
        }

#if FEATURE_CAS_POLICY
        internal IPermission ToPermission(bool ignoreTypeLoadFailures)
        {
            IPermission ip = XMLUtil.CreatePermission( this, PermissionState.None, ignoreTypeLoadFailures );
            if (ip == null)
                return null;
            ip.FromXml(this);

            // Get the permission token here to ensure that the token
            // type is updated appropriately now that we've loaded the type.
            PermissionToken token = PermissionToken.GetToken( ip );
            Contract.Assert((token.m_type & PermissionTokenType.DontKnow) == 0, "Token type not properly assigned");

            return ip;            
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal Object ToSecurityObject()
        {
            switch (m_strTag)
            {
                case "PermissionSet":
                    PermissionSet pset = new PermissionSet(PermissionState.None);
                    pset.FromXml(this);
                    return pset;

                default:
                    return ToPermission(false);
            }
        }
#endif // FEATURE_CAS_POLICY

        internal String SearchForTextOfLocalName(String strLocalName) 
        {
            // Search on each child in order and each
            // child's child, depth-first
            
            if (strLocalName == null)
                throw new ArgumentNullException( "strLocalName" );
            Contract.EndContractBlock();
                
            // Note: we don't check for a valid tag here because
            // an invalid tag simply won't be found.    
            
            // First we check this.
            
            if (m_strTag == null) return null;            
            if (m_strTag.Equals( strLocalName ) || m_strTag.EndsWith( ":" + strLocalName, StringComparison.Ordinal ))
                return Unescape( m_strText );               
            if (m_lChildren == null)
                return null;

            IEnumerator enumerator = m_lChildren.GetEnumerator();

            while (enumerator.MoveNext())
            {
                String current = ((SecurityElement)enumerator.Current).SearchForTextOfLocalName( strLocalName );
            
                if (current != null)
                    return current;
            }
            return null;            
        }

        public String SearchForTextOfTag( String tag )
        {
            // Search on each child in order and each
            // child's child, depth-first
            
            if (tag == null)
                throw new ArgumentNullException( "tag" );
            Contract.EndContractBlock();
                
            // Note: we don't check for a valid tag here because
            // an invalid tag simply won't be found.    
            
            // First we check this.
            
            if (String.Equals(m_strTag, tag))
                return Unescape( m_strText );              
            if (m_lChildren == null)
                return null;

            IEnumerator enumerator = m_lChildren.GetEnumerator();

            this.ConvertSecurityElementFactories();

            while (enumerator.MoveNext())
            {
                String current = ((SecurityElement)enumerator.Current).SearchForTextOfTag( tag );
            
                if (current != null)
                    return current;
            }
            return null;
        } 
    }                        
}
