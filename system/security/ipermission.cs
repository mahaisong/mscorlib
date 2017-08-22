// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// IPermission.cs
// 
// <OWNER>ShawnFa</OWNER>
//
// Defines the interface that all Permission objects must support.
// 

namespace System.Security
{
    /// <summary>
    /// �����˽ӿ�,����֧�ֵ�������ɶ���
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface IPermission : ISecurityEncodable
    {
        // ע��:���ڶ���ĳ����ǰᵽPermissionsEnum.cs����CLS���ơ�

        // ��ȫϵͳ��������ȡ����һ���ֶθ��ƶ����Ա�����ж�������ò���¶������ʱ�����,����ʵ������Ȩ�޸��ơ�
        // ʹһ��׼ȷ����ɡ�
        /// <summary>
        /// ���������ص�ǰȨ�޵���ͬ������
        /// </summary>
        /// <returns></returns>
        IPermission Copy();

        /*
         * Methods to support the Installation, Registration, others... PolicyEngine
         */

        // ���ߺ����л���(����,����)��Ҫ��������Ȩ��֮�乲��״̬��һ���ֶΡ����û������ʵ��֮�乲��״̬,��ô����Ӧ�÷���null��
        //
        // ��GetCommonState���뵽�ķ���,�����뿪�ཻ,�Ա��ⲻ��Ҫ�����Ƹ��ġ�
        // 
        // ����һ���µ����permission-defined����������Ȩ�ޡ�ʮ��·��ͨ������Ϊ��Ȩ�����������⡱�͡�Ŀ�ꡱ�������Ŀ�ꡱ��null����������,����null��
        // 
        /// <summary>
        /// ����������һ��Ȩ�ޣ���Ȩ���ǵ�ǰȨ�޺�ָ��Ȩ�޵Ľ�����
        /// </summary>
        /// <param name="target">Ŀ��Ȩ��</param>
        /// <returns></returns>
        IPermission Intersect(IPermission target);

        // The runtime policy manager also requires a means of combining the
        // state contained within two permissions of the same type in a logical OR
        // construct.  (The Union of two permission of different type is not defined, 
        // except when one of the two is a CompoundPermission of internal type equal
        // to the type of the other permission.)
        // ����ʱ���Թ���ԱҲ��Ҫһ���ֶ����ϵ�Ȩ���а���������ͬ���͵�Ȩ���߼����졣
        // (���˵�������ͬ����û�ж������,��������һ���ڲ����͵�CompoundPermission����������ɵ�����)��
        //
        //
        /// <summary>
        /// ����һ��Ȩ�ޣ���Ȩ���ǵ�ǰȨ����ָ��Ȩ�޵Ĳ�����
        /// </summary>
        /// <param name="target">Ŀ��Ȩ��</param>
        /// <returns></returns>
        IPermission Union(IPermission target);

        // IsSubsetOf defines a standard mechanism for determining
        // relative safety between two permission demands of the same type.
        // If one demand x demands no more than some other demand y, then
        // x.IsSubsetOf(y) should return true. In this case, if the
        // demand for y is satisfied, then it is possible to assume that
        // the demand for x would also be satisfied under the same
        // circumstances. On the other hand, if x demands something that y
        // does not, then x.IsSubsetOf(y) should return false; the fact
        // that x is satisfied by the current security context does not
        // also imply that the demand for y will also be satisfied.
        // IsSubsetOf֮�䶨����һ����׼�Ļ�����ȷ����԰�ȫ������ͬ���͵�Ȩ��Ҫ��
        //
        // 
        // Returns true if 'this' Permission allows no more access than the
        // argument.
        /// <summary>
        /// ȷ����ǰȨ���Ƿ�Ϊָ��Ȩ�޵��Ӽ���
        /// </summary>
        /// <param name="target">Ŀ��Ȩ��</param>
        /// <returns></returns>
 
        bool IsSubsetOf(IPermission target);

        // The Demand method is the fundamental part of the IPermission
        // interface from a component developer's perspective. The
        // permission represents the demand that the developer wants
        // satisfied, and Demand is the means to invoke the demand.
        // For each type of permission, the mechanism to verify the
        // demand will be different. However, to the developer, all
        // permissions invoke that mechanism through the Demand interface.
        // Mark this method as requiring a security object on the caller's frame
        // so the caller won't be inlined (which would mess up stack crawling).
        /// <summary>
        /// ��������㰲ȫҪ�����������ʱ���� SecurityException��
        /// </summary>
        [DynamicSecurityMethodAttribute()]
        void Demand();

    }
}
