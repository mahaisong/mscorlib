// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  Stream
** 
** <OWNER>gpaperin</OWNER>
**
**
** Purpose: Abstract base class for all Streams.  Provides
** default implementations of asynchronous reads & writes, in
** terms of the synchronous reads & writes (and vice versa).
**
**
===========================================================*/
using System;
using System.Threading;
#if FEATURE_ASYNC_IO
using System.Threading.Tasks;
#endif

using System.Runtime;
using System.Runtime.InteropServices;
#if NEW_EXPERIMENTAL_ASYNC_IO
using System.Runtime.CompilerServices;
#endif
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Permissions;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace System.IO {
    [Serializable]
    [ComVisible(true)]
#if CONTRACTS_FULL
    [ContractClass(typeof(StreamContract))]
#endif
#if FEATURE_REMOTING
    public abstract class Stream : MarshalByRefObject, IDisposable {
#else // FEATURE_REMOTING
    public abstract class Stream : IDisposable {
#endif // FEATURE_REMOTING

        /// <summary>
        /// �޺󱸴洢���� Stream��
        /// </summary>
        public static readonly Stream Null = new NullStream();

        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant(��Ч��)
        // improvement in Copy performance(����).
        /// <summary>
        /// Ĭ�ϵĸ��ƻ����С
        /// </summary>
        private const int _DefaultCopyBufferSize = 81920;

#if NEW_EXPERIMENTAL_ASYNC_IO
        // To implement Async IO operations on streams that don't support async IO
        // Ϊ��ʵ���첽IO���� streams��֧���첽io

        /// <summary>
        /// ��д����
        /// </summary>
        [NonSerialized]
        private ReadWriteTask _activeReadWriteTask;
        /// <summary>
        /// ΢С���ź�
        /// </summary>
        [NonSerialized]
        private SemaphoreSlim _asyncActiveSemaphore;

        /// <summary>
        /// ��֤�첽��Ϊ�źų�ʼ��
        /// </summary>
        /// <returns></returns>
        internal SemaphoreSlim EnsureAsyncActiveSemaphoreInitialized()
        {
            // Lazily-initialize _asyncActiveSemaphore.  As we're never accessing the SemaphoreSlim's
            // WaitHandle, we don't need to worry about Disposing it.
            // Lazily-initialize _asyncActiveSemaphore.���Ǵ���û�з���SemaphoreSlim WaitHandle,���ǲ���Ҫ���Ĵ�������
            return LazyInitializer.EnsureInitialized(ref _asyncActiveSemaphore, () => new SemaphoreSlim(1, 1));
        }
#endif
        /// <summary>
        /// ��������������дʱ����ȡָʾ��ǰ���Ƿ�֧�ֶ�ȡ��ֵ��
        /// </summary>
        public abstract bool CanRead {
            [Pure]
            get;
        }

        // If CanSeek is false, Position, Seek, Length, and SetLength should throw.
        /// <summary>
        /// ��������������дʱ����ȡָʾ��ǰ���Ƿ�֧�ֲ��ҹ��ܵ�ֵ��
        /// </summary>
        public abstract bool CanSeek {
            [Pure]
            get;
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵȷ����ǰ���Ƿ���Գ�ʱ��
        /// </summary>
        [ComVisible(false)]
        public virtual bool CanTimeout {
            [Pure]
            get {
                return false;
            }
        }

        /// <summary>
        /// ��������������дʱ����ȡָʾ��ǰ���Ƿ�֧��д�빦�ܵ�ֵ��
        /// </summary>
        public abstract bool CanWrite {
            [Pure]
            get;
        }

        /// <summary>
        /// ��������������дʱ����ȡ�����ȣ����ֽ�Ϊ��λ����
        /// </summary>
        public abstract long Length {
            get;
        }

        /// <summary>
        /// ��������������дʱ����ȡ�����õ�ǰ���е�λ�á�
        /// </summary>
        public abstract long Position {
            get;
            set;
        }

        /// <summary>
        /// ��ȡ������һ��ֵ���Ժ���Ϊ��λ������ֵȷ�����ڳ�ʱǰ���Զ�ȡ�೤ʱ�䡣
        /// </summary>
        [ComVisible(false)]
        public virtual int ReadTimeout {
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            }
            set {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            }
        }

        /// <summary>
        /// ��ȡ������һ��ֵ���Ժ���Ϊ��λ������ֵȷ�����ڳ�ʱǰ����д��೤ʱ�䡣
        /// </summary>
        [ComVisible(false)]
        public virtual int WriteTimeout {
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            }
            set {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            }
        }

#if FEATURE_ASYNC_IO
        /// <summary>
        /// �ӵ�ǰ�����첽��ȡ�ֽڲ�����д�뵽��һ�����С�
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <returns>��ʾ�첽���Ʋ���������</returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public Task CopyToAsync(Stream destination)
        {
            return CopyToAsync(destination, _DefaultCopyBufferSize);
        }

        /// <summary>
        /// ʹ��ָ���Ļ�������С���ӵ�ǰ�����첽��ȡ�ֽڲ�����д�뵽��һ���С�
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <param name="bufferSize">�������Ĵ�С�����ֽ�Ϊ��λ����  ��ֵ��������㡣  Ĭ�ϴ�СΪ 81920��  </param>
        /// <returns>��ʾ�첽���Ʋ���������</returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public Task CopyToAsync(Stream destination, Int32 bufferSize)
        {
            return CopyToAsync(destination, bufferSize, CancellationToken.None);
        }

        /// <summary>
        /// ʹ��ָ���Ļ�������С��ȡ�����ƣ��ӵ�ǰ�����첽��ȡ�ֽڲ�����д�뵽��һ�����С�
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <param name="bufferSize">�������Ĵ�С�����ֽ�Ϊ��λ����  ��ֵ��������㡣  Ĭ�ϴ�СΪ 81920��  </param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�  Ĭ��ֵΪ None��  </param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public virtual Task CopyToAsync(Stream destination, Int32 bufferSize, CancellationToken cancellationToken)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize", Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));
            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("destination", Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!CanRead)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnreadableStream"));
            if (!destination.CanWrite)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnwritableStream"));
            Contract.EndContractBlock();

            return CopyToAsyncInternal(destination, bufferSize, cancellationToken);
        }

        /// <summary>
        /// �ڲ�ʵ���첽����
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <param name="bufferSize">�������Ĵ�С�����ֽ�Ϊ��λ����  ��ֵ��������㡣  Ĭ�ϴ�СΪ 81920��  </param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�  Ĭ��ֵΪ None��  </param>
        /// <returns></returns>
        private async Task CopyToAsyncInternal(Stream destination, Int32 bufferSize, CancellationToken cancellationToken)
        {
            Contract.Requires(destination != null);
            Contract.Requires(bufferSize > 0);
            Contract.Requires(CanRead);
            Contract.Requires(destination.CanWrite);

            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            // ʹ�������첽����һ�����첽��ȡ������һ�����첽д������ѭ��������д��
            while ((bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
        }
#endif // FEATURE_ASYNC_IO

        // Reads the bytes from the current stream and writes the bytes to
        // the destination stream until all bytes are read, starting at
        // the current position.
        /// <summary>
        /// �ӵ�ǰ���ж�ȡ�ֽڲ�����д�뵽��һ���С�
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        public void CopyTo(Stream destination)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("destination", Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!CanRead)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnreadableStream"));
            if (!destination.CanWrite)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnwritableStream"));
            Contract.EndContractBlock();

            InternalCopyTo(destination, _DefaultCopyBufferSize);
        }

        /// <summary>
        /// ʹ��ָ���Ļ�������С���ӵ�ǰ���ж�ȡ�ֽڲ�����д�뵽��һ���С�
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <param name="bufferSize">�������Ĵ�С����ֵ��������㡣Ĭ�ϴ�СΪ 81920��</param>
        public void CopyTo(Stream destination, int bufferSize)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize",
                        Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));
            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("destination", Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!CanRead)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnreadableStream"));
            if (!destination.CanWrite)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnwritableStream"));
            Contract.EndContractBlock();

            InternalCopyTo(destination, bufferSize);
        }

        /// <summary>
        /// �ڲ�CopyToʵ��
        /// </summary>
        /// <param name="destination">��ǰ�������ݽ����Ƶ�������</param>
        /// <param name="bufferSize">�������Ĵ�С����ֵ��������㡣Ĭ�ϴ�СΪ 81920��</param>
        private void InternalCopyTo(Stream destination, int bufferSize)
        {
            Contract.Requires(destination != null);
            Contract.Requires(CanRead);
            Contract.Requires(destination.CanWrite);
            Contract.Requires(bufferSize > 0);
            
            byte[] buffer = new byte[bufferSize];
            int read;
            while ((read = Read(buffer, 0, buffer.Length)) != 0)
                destination.Write(buffer, 0, read);
        }


        // Stream used to require that all cleanup logic went into Close(),
        // which was thought up before we invented IDisposable.  However, we
        // need to follow the IDisposable pattern so that users can write 
        // sensible subclasses without needing to inspect all their base 
        // classes, and without worrying about version brittleness, from a
        // base class switching to the Dispose pattern.  We're moving
        // Stream to the Dispose(bool) pattern - that's where all subclasses 
        // should put their cleanup starting in V2.
        //������Ҫ�����������߼�����ر�(),������Ϊ������IDisposable�����ġ�
        //Ȼ��,������Ҫ��ѭIDisposableģʽ,�Ա��û����Ա�д���������,
        //������Ҫ������еĻ���,�����õ��İ汾����,��һ�������л�������ģʽ��
        //��������������(bool)ģʽ����������������඼Ӧ�ð����������V2��
        /// <summary>
        /// �رյ�ǰ�����ͷ���֮������������Դ�����׽��ֺ��ļ����������ֱ�ӵ��ô˷�������Ӧȷ����������ȷ�ͷš�
        /// </summary>
        public virtual void Close()
        {
            /* These are correct, but we'd have to fix PipeStream & NetworkStream very carefully.
            ��Щ������ȷ��,�����Ǳ�����PipeStream & NetworkStream��С�ġ�
            Contract.Ensures(CanRead == false);
            Contract.Ensures(CanWrite == false);
            Contract.Ensures(CanSeek == false);
            */

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// �ͷ��� Stream ʹ�õ�������Դ��
        /// </summary>
        public void Dispose()
        {
            /* These are correct, but we'd have to fix PipeStream & NetworkStream very carefully.
            Contract.Ensures(CanRead == false);
            Contract.Ensures(CanWrite == false);
            Contract.Ensures(CanSeek == false);
            */

            Close();
        }

        /// <summary>
        /// �ͷ��� Stream ռ�õķ��й���Դ���������ͷ��й���Դ��
        /// </summary>
        /// <param name="disposing">��Ҫ�ͷ��й���Դ�ͷ��й���Դ����Ϊ true�������ͷŷ��й���Դ����Ϊ false��</param>
        protected virtual void Dispose(bool disposing)
        {
            // Note: Never change this to call other virtual methods on Stream
            // like Write, since the state on subclasses has already been 
            // torn down.  This is the last code to run on cleanup for a stream.
            //Ӧͨ��ָ���ͷ�������Դ true Ϊ disposing���� disposing �� true,��������ȷ������ˢ�µ������Ļ��������������������ս�Ķ����ⲻ���ܴ�һ���ս���������ȱ���ս���֮��˳����е���ʱ��
            //���������ʹ�ò���ϵͳ���������Դ����ͨ�ţ��뿼��ʹ�õ����� SafeHandle Ϊ��Ŀ�ġ�
            //���ô˷����ɹ��� Dispose ������ Finalize ������ Dispose �����ܱ��� Dispose �����滻 disposing ��������Ϊ true�� Finalize ���� Dispose �� disposing ����Ϊ false��

        }

        /// <summary>
        /// ��������������дʱ����������������л���������ʹ�����л������ݱ�д�뵽�����豸��
        /// </summary>
        public abstract void Flush();

#if FEATURE_ASYNC_IO
        /// <summary>
        /// �첽������������л��������������л������ݶ�д������豸�С�
        /// </summary>
        /// <returns>��ʾ�첽ˢ�²���������</returns>
        [HostProtection(ExternalThreading=true)]
        [ComVisible(false)]
        public Task FlushAsync()
        {
            return FlushAsync(CancellationToken.None);
        }

        /// <summary>
        /// �첽��������������л���������ʹ���л�������д������豸�����Ҽ��ȡ������
        /// </summary>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�Ĭ��ֵΪ None��</param>
        /// <returns>��ʾ�첽ˢ�²���������</returns>
        [HostProtection(ExternalThreading=true)]
        [ComVisible(false)]
        public virtual Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(state => ((Stream)state).Flush(), this,
                cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
#endif // FEATURE_ASYNC_IO

        [Obsolete("CreateWaitHandle will be removed eventually.  Please use \"new ManualResetEvent(false)\" instead.")]
        protected virtual WaitHandle CreateWaitHandle()
        {
            Contract.Ensures(Contract.Result<WaitHandle>() != null);
            return new ManualResetEvent(false);
        }

        /// <summary>
        /// ��ʼ�첽��������������ʹ�� ReadAsync �����滻����μ�����ע�����֡���
        /// </summary>
        /// <param name="buffer">���ݶ���Ļ�������</param>
        /// <param name="offset">buffer �е��ֽ�ƫ�������Ӹ�ƫ������ʼд������ж�ȡ�����ݡ�</param>
        /// <param name="count">����ȡ���ֽ�����</param>
        /// <param name="callback">��ѡ���첽�ص�������ɶ�ȡʱ���á�</param>
        /// <param name="state">һ���û��ṩ�Ķ����������ض����첽��ȡ����������������������</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading=true)]
        public virtual IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);
            return BeginReadInternal(buffer, offset, count, callback, state, serializeAsynchronously: false);
            //bool serializeAsynchronously = false;
            //return BeginReadInternal(buffer, offset, count, callback, state, serializeAsynchronously);
        }

        /// <summary>
        /// �ڲ�ʵ��BeginRead
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <param name="serializeAsynchronously">trueΪ�첽��falseΪͬ��</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        internal IAsyncResult BeginReadInternal(byte[] buffer, int offset, int count, AsyncCallback callback, Object state, bool serializeAsynchronously)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);
            if (!CanRead) __Error.ReadNotSupported();// ������ܶ��Ļ�������ʾ��֧�ֶ�

#if !NEW_EXPERIMENTAL_ASYNC_IO
            return BlockingBeginRead(buffer, offset, count, callback, state);
#else

            // Mango did not do Async IO.
            // ���ж�WP8�ļ������ж�
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
            {
                return BlockingBeginRead(buffer, offset, count, callback, state);
            }

            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread if it does a second IO request until the first one completes.
            // �����ź����������߳̾��������Ĵ���
            var semaphore = EnsureAsyncActiveSemaphoreInitialized();
            Task semaphoreTask = null;//�źŽ���
            // �Ƿ��첽���л�
            if (serializeAsynchronously)
            {
                semaphoreTask = semaphore.WaitAsync();//���� SemaphoreSlim ���첽�ȴ���
            }
            else
            {
                semaphore.Wait();// ��ֹ��ǰ�̣߳�ֱ�����ɽ��� SemaphoreSlim Ϊֹ��
            }

            // Create the task to asynchronously do a Read.  This task serves both
            // as the asynchronous work item and as the IAsyncResult returned to the user.
            // �����첽��ȡ�������������������첽������ͽ�IAsyncResult���ظ��û�
            var asyncResult = new ReadWriteTask(true /*isRead*/, delegate
            {
                // The ReadWriteTask stores all of the parameters to pass to Read.
                // As we're currently inside of it, we can get the current task
                // and grab the parameters from it.
                // ReadWriteTask�洢����Read�����в���
                // ���������ڴ�����ʱ�����ǽ���ȡ��ǰ����ʹ����л�ȡ����
                // ��ȡ��ǰִ��Taskʵ��
                var thisTask = Task.InternalCurrent as ReadWriteTask;
                Contract.Assert(thisTask != null, "Inside ReadWriteTask, InternalCurrent should be the ReadWriteTask");

                // Do the Read and return the number of bytes read
                // ��ȡ�����ض�ȡbytes��Ŀ
                var bytesRead = thisTask._stream.Read(thisTask._buffer, thisTask._offset, thisTask._count);
                thisTask.ClearBeginState(); // �����ǰ�������һЩ�ڴ�ѹ��
                return bytesRead;
            }, state, this, buffer, offset, count, callback);

            // ��������
            if (semaphoreTask != null)
                RunReadWriteTaskWhenReady(semaphoreTask, asyncResult);
            else
                RunReadWriteTask(asyncResult);

            
            return asyncResult; // return it
#endif
        }

        /// <summary>
        /// �ȴ�������첽��ȡ��ɡ�������ʹ�� ReadAsync �����滻����μ�����ע�����֡���
        /// </summary>
        /// <param name="asyncResult">��Ҫ��ɵĹ����첽��������á�</param>
        /// <returns>�����ж�ȡ���ֽ����������� (0) ����������ֽ���֮�䡣����������β������ (0)�������������� 1 ���ֽڿ���֮ǰӦһֱ������ֹ��</returns>
        public virtual int EndRead(IAsyncResult asyncResult)
        {
            if (asyncResult == null)// �ж�IAsyncResult�Ƿ�Ϊ��
                throw new ArgumentNullException("asyncResult");
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.EndContractBlock();

#if !NEW_EXPERIMENTAL_ASYNC_IO
            return BlockingEndRead(asyncResult);
#else
            // Mango did not do async IO.
            // �����WP8�Ŀ鼶EndRead
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
            {
                return BlockingEndRead(asyncResult);
            }
            // ��ȡ��ǰ�Ķ�ȡ��д������readTask��
            var readTask = _activeReadWriteTask;

            if (readTask == null)
            {
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple"));
            }
            else if (readTask != asyncResult)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple"));
            }
            else if (!readTask._isRead)
            {
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple"));
            }
            
            try 
            {
                return readTask.GetAwaiter().GetResult(); // block until completion, then get result / propagate any exception ֱ����ɣ����ؽ�� / �����κ��쳣
            }
            finally
            {
                _activeReadWriteTask = null;// ����ǰ�Ķ�ȡ��д���������
                Contract.Assert(_asyncActiveSemaphore != null, "Must have been initialized in order to get here.");
                _asyncActiveSemaphore.Release();// ��ͬ�����ͷ�
            }
#endif
        }


#if FEATURE_ASYNC_IO
        /// <summary>
        /// �ӵ�ǰ���첽��ȡ�ֽ����У��������е�λ��������ȡ���ֽ�����
        /// </summary>
        /// <param name="buffer">����д��Ļ�������</param>
        /// <param name="offset">buffer �е��ֽ�ƫ�������Ӹ�ƫ������ʼд������ж�ȡ�����ݡ�</param>
        /// <param name="count">����ȡ���ֽ�����</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public Task<int> ReadAsync(Byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None);
        }

        /// <summary>
        /// �ӵ�ǰ���첽��ȡ�ֽڵ����У������е�λ��������ȡ���ֽ�����������ȡ������
        /// </summary>
        /// <param name="buffer">����д��Ļ�������</param>
        /// <param name="offset">buffer �е��ֽ�ƫ�������Ӹ�ƫ������ʼд������ж�ȡ�����ݡ�</param>
        /// <param name="count">����ȡ���ֽ�����</param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�  Ĭ��ֵΪ None��</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public virtual Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // If cancellation was requested, bail early with an already completed task.
            // Otherwise, return a task that represents the Begin/End methods.
            // ���cancellation���󣬱��������Ѿ���ɵ�����
            // ���򣬷���һ������Begin/End����������
            return cancellationToken.IsCancellationRequested
                        ? Task.FromCancellation<int>(cancellationToken)
                        : BeginEndReadAsync(buffer, offset, count);
        }

        /// <summary>
        /// ��ʼ�ͽ����첽��ȡ
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private Task<Int32> BeginEndReadAsync(Byte[] buffer, Int32 offset, Int32 count)
        {
            return TaskFactory<Int32>.FromAsyncTrim(this,
                        new ReadWriteParameters { Buffer = buffer, Offset = offset, Count = count },
                        (stream, args, callback, state) => stream.BeginRead(args.Buffer, args.Offset, args.Count, callback, state),
                        (stream, asyncResult) => stream.EndRead(asyncResult)
                );// ���������
        }

        private struct ReadWriteParameters // struct for arguments to Read and Write calls
        {
            internal byte[] Buffer;
            internal int Offset;
            internal int Count;
        }
#endif //FEATURE_ASYNC_IO


        /// <summary>
        /// ��ʼ�첽д������������ʹ�� WriteAsync �����滻����μ�����ע�����֡���
        /// </summary>
        /// <param name="buffer">����д�����ݵĻ�������</param>
        /// <param name="offset">buffer �е��ֽ�ƫ�������Ӵ˴���ʼд�롣 </param>
        /// <param name="count">���д����ֽ�����</param>
        /// <param name="callback">��ѡ���첽�ص��������д��ʱ���á�</param>
        /// <param name="state">һ���û��ṩ�Ķ����������ض����첽д������������������������</param>
        /// <returns>��ʾ�첽д��� IAsyncResult�������Դ��ڹ���״̬����</returns>
        [HostProtection(ExternalThreading=true)]
        public virtual IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);
            return BeginWriteInternal(buffer, offset, count, callback, state, serializeAsynchronously: false);
        }

        /// <summary>
        /// �ڲ�ʵ��BeginWrite
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <param name="serializeAsynchronously"></param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        internal IAsyncResult BeginWriteInternal(byte[] buffer, int offset, int count, AsyncCallback callback, Object state, bool serializeAsynchronously)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);
            if (!CanWrite) __Error.WriteNotSupported();
#if !NEW_EXPERIMENTAL_ASYNC_IO
            return BlockingBeginWrite(buffer, offset, count, callback, state);
#else

            // Mango did not do Async IO.
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
            {
                return BlockingBeginWrite(buffer, offset, count, callback, state);
            }

            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread if it does a second IO request until the first one completes.
            var semaphore = EnsureAsyncActiveSemaphoreInitialized();
            Task semaphoreTask = null;
            if (serializeAsynchronously)
            {
                semaphoreTask = semaphore.WaitAsync(); // kick off the asynchronous wait, but don't block
            }
            else
            {
                semaphore.Wait(); // synchronously wait here
            }

            // Create the task to asynchronously do a Write.  This task serves both
            // as the asynchronous work item and as the IAsyncResult returned to the user.
            var asyncResult = new ReadWriteTask(false /*isRead*/, delegate
            {
                // The ReadWriteTask stores all of the parameters to pass to Write.
                // As we're currently inside of it, we can get the current task
                // and grab the parameters from it.
                var thisTask = Task.InternalCurrent as ReadWriteTask;
                Contract.Assert(thisTask != null, "Inside ReadWriteTask, InternalCurrent should be the ReadWriteTask");

                // Do the Write
                thisTask._stream.Write(thisTask._buffer, thisTask._offset, thisTask._count);  
                thisTask.ClearBeginState(); // just to help alleviate some memory pressure
                return 0; // not used, but signature requires a value be returned
            }, state, this, buffer, offset, count, callback);

            // Schedule it
            if (semaphoreTask != null)
                RunReadWriteTaskWhenReady(semaphoreTask, asyncResult);
            else
                RunReadWriteTask(asyncResult);

            return asyncResult; // return it
#endif
        }

#if NEW_EXPERIMENTAL_ASYNC_IO
        /// <summary>
        /// ���ж�д����
        /// </summary>
        /// <param name="asyncWaiter">�첽�ȴ�����</param>
        /// <param name="readWriteTask">��д����</param>
        private void RunReadWriteTaskWhenReady(Task asyncWaiter, ReadWriteTask readWriteTask)
        {
            Contract.Assert(readWriteTask != null);  // Should be Contract.Requires, but CCRewrite is doing a poor job with
                                                     // preconditions���Ⱦ������� in async methods that await.  Mike & Manuel are aware(��ʶ��). (10/6/2011, bug 290222)
            Contract.Assert(asyncWaiter != null);    // Ditto

            // If the wait has already complete, run the task.
            // ����ȴ�������ɣ�����������
            if (asyncWaiter.IsCompleted)
            {
                Contract.Assert(asyncWaiter.IsRanToCompletion, "The semaphore wait should always complete successfully.");
                RunReadWriteTask(readWriteTask);
            }
            else  // ����,�ȴ��ֵ�����,Ȼ�������������
            {
                asyncWaiter.ContinueWith((t, state) =>
                    {
                        Contract.Assert(t.IsRanToCompletion, "The semaphore wait should always complete successfully.");
                        var tuple = (Tuple<Stream,ReadWriteTask>)state;
                        tuple.Item1.RunReadWriteTask(tuple.Item2); // RunReadWriteTask(readWriteTask);
                    }, Tuple.Create<Stream,ReadWriteTask>(this, readWriteTask),
                default(CancellationToken),
                TaskContinuationOptions.ExecuteSynchronously, 
                TaskScheduler.Default);
            }
        }

        /// <summary>
        /// ���ж�д����
        /// </summary>
        /// <param name="readWriteTask"></param>
        private void RunReadWriteTask(ReadWriteTask readWriteTask)
        {
            Contract.Requires(readWriteTask != null);
            Contract.Assert(_activeReadWriteTask == null, "Expected no other readers or writers");

            // Schedule the task.  ScheduleAndStart must happen after the write to _activeReadWriteTask to avoid a race.
            // Internally, we're able to directly call ScheduleAndStart rather than Start, avoiding
            // two interlocked operations.  However, if ReadWriteTask is ever changed to use
            // a cancellation token, this should be changed to use Start.
            // ���ŵ�����ScheduleAndStart���뷢��д_activeReadWriteTask��,����һ��������
            // ���ڲ�,�����ܹ�ֱ�ӵ���ScheduleAndStart�����ǿ�ʼ,������������������
            // Ȼ��,���ReadWriteTask��Ϊʹ��ȡ������,��Ӧ�ø�Ϊʹ�ÿ�ʼ��
            _activeReadWriteTask = readWriteTask; // store the task so that EndXx can validate it's given the right one 
            readWriteTask.m_taskScheduler = TaskScheduler.Default;
            readWriteTask.ScheduleAndStart(needsProtection: false);
        }
#endif
        /// <summary>
        /// �����첽д������������ʹ�� WriteAsync �����滻����μ�����ע�����֡���
        /// </summary>
        /// <param name="asyncResult">��δ��ɵ��첽 I/O ��������á�</param>
        public virtual void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult==null)
                throw new ArgumentNullException("asyncResult");
            Contract.EndContractBlock();

#if !NEW_EXPERIMENTAL_ASYNC_IO
            BlockingEndWrite(asyncResult);
#else            

            // Mango did not do Async IO.
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
            {
                BlockingEndWrite(asyncResult);
                return;
            }            

            var writeTask = _activeReadWriteTask;
            if (writeTask == null)
            {
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple"));
            }
            else if (writeTask != asyncResult)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple"));
            }
            else if (writeTask._isRead)
            {
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple"));
            }

            try 
            {
                writeTask.GetAwaiter().GetResult(); // block until completion, then propagate any exceptions
                Contract.Assert(writeTask.Status == TaskStatus.RanToCompletion);
            }
            finally
            {
                _activeReadWriteTask = null;
                Contract.Assert(_asyncActiveSemaphore != null, "Must have been initialized in order to get here.");
                _asyncActiveSemaphore.Release();
            }
#endif
        }

#if NEW_EXPERIMENTAL_ASYNC_IO
        // Task used by BeginRead / BeginWrite to do Read / Write asynchronously.
        // A single instance of this task serves(����) four purposes(Ŀ��):
        // 1. The work item scheduled to run the Read / Write operation
        // 2. The state holding the arguments to be passed to Read / Write
        // 3. The IAsyncResult returned from BeginRead / BeginWrite
        // 4. The completion action that runs to invoke the user-provided callback.
        // This last item is a bit tricky.  Before the AsyncCallback is invoked, the
        // IAsyncResult must have completed, so we can't just invoke the handler
        // from within the task, since it is the IAsyncResult, and thus it's not
        // yet completed.  Instead, we use AddCompletionAction to install this
        // task as its own completion handler.  That saves the need to allocate
        // a separate(����) completion handler, it guarantees(��֤) that the task will
        // have completed by the time the handler is invoked, and it allows
        // the handler to be invoked synchronously upon the completion of the
        // task.  This all enables BeginRead / BeginWrite to be implemented
        // with a single allocation.
        private sealed class ReadWriteTask : Task<int>, ITaskCompletionAction
        {
            internal readonly bool _isRead;
            internal Stream _stream;
            internal byte [] _buffer;
            internal int _offset;
            internal int _count;
            /// <summary>
            /// �첽�ص�ί��
            /// </summary>
            private AsyncCallback _callback;
            /// <summary>
            /// ��ǰ�̵߳�ִ��������
            /// </summary>
            private ExecutionContext _context;

            /// <summary>
            /// Used to allow the args to Read/Write to be made available(��Ч��) for GC
            /// �������������дΪGC����
            /// </summary>
            internal void ClearBeginState()
            {
                _stream = null;
                _buffer = null;
            }

            /// <summary>
            /// ����д����Ĺ��캯��
            /// </summary>
            /// <param name="isRead">�Ƿ��Ƕ�����</param>
            /// <param name="function">��д������</param>
            /// <param name="state"></param>
            /// <param name="stream">������</param>
            /// <param name="buffer">����</param>
            /// <param name="offset"></param>
            /// <param name="count"></param>
            /// <param name="callback">�첽�ص�</param>
            [SecuritySafeCritical] // necessary for EC.Capture(���������)
            [MethodImpl(MethodImplOptions.NoInlining)]
            public ReadWriteTask(
                bool isRead,
                Func<object,int> function, object state,
                Stream stream, byte[] buffer, int offset, int count, AsyncCallback callback) :
                base(function, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach)
            {
                Contract.Requires(function != null);
                Contract.Requires(stream != null);
                Contract.Requires(buffer != null);
                Contract.EndContractBlock();

                StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;

                // �洢����
                _isRead = isRead;
                _stream = stream;
                _buffer = buffer;
                _offset = offset;
                _count = count;

                // If a callback was provided, we need to:
                // ���һ���ص����ṩ��������Ҫ��
                // - Store the user-provided handler
                // - �洢�û��ṩ��handler
                // - Capture an ExecutionContext under which to invoke the handler
                // -�����ExecutionContext�ڵ��ô������
                // - Add this task as its own completion handler so that the Invoke method
                //   will run the callback when this task completes.
                // - ������������Ϊ���Լ����handler��˵��÷��������лص���������������
                if (callback != null)
                {
                    _callback = callback;
                    //�ӵ�ǰ�̲߳���ִ�������ģ�����ֵ
                    _context = ExecutionContext.Capture(ref stackMark, 
                        ExecutionContext.CaptureOptions.OptimizeDefaultCase | ExecutionContext.CaptureOptions.IgnoreSyncCtx);
                    base.AddCompletionAction(this);//��������Ϊ
                }
            }

            /// <summary>
            /// �����첽�ص�
            /// </summary>
            /// <param name="completedTask"></param>
            [SecurityCritical] // necessary for CoreCLR
            private static void InvokeAsyncCallback(object completedTask)
            {
                // ��objectת��ΪReadWriteTask������ȡ���Ļص���������
                var rwc = (ReadWriteTask)completedTask;
                var callback = rwc._callback;
                rwc._callback = null;
                callback(rwc);
            }

            /// <summary>
            /// �������Ļص��ķ���
            /// </summary>
            [SecurityCritical] // necessary for CoreCLR
            private static ContextCallback s_invokeAsyncCallback;
            
            /// <summary>
            /// ITaskCompletionAction.Invoke��ʽ�ӿ�ʵ��
            /// </summary>
            /// <param name="completingTask">�������</param>
            [SecuritySafeCritical] // necessary for ExecutionContext.Run
            void ITaskCompletionAction.Invoke(Task completingTask)
            {
                // Get the ExecutionContext.  If there is none, just run the callback
                // directly, passing in the completed task as the IAsyncResult.
                // If there is one, process it with ExecutionContext.Run.
                // ��ȡ��ǰ�̵߳������ģ����Ϊ�գ���ֱ�����лص�������IAsyncResult�������
                // ����еĻ�,��ExecutionContext.Run��������
                var context = _context;
                if (context == null) 
                {
                    var callback = _callback;
                    _callback = null;
                    callback(completingTask);
                }
                else 
                {
                    _context = null;
        
                    var invokeAsyncCallback = s_invokeAsyncCallback;
                    if (invokeAsyncCallback == null) s_invokeAsyncCallback = invokeAsyncCallback = InvokeAsyncCallback; // benign ----

                    using (context) ExecutionContext.Run(context, invokeAsyncCallback, this, true);// ʹ��ExecutionContext.Run���ú���
                }
            }
        }
#endif

#if !FEATURE_PAL && FEATURE_ASYNC_IO
        /// <summary>
        /// ���ֽ������첽д�뵱ǰ�����������ĵ�ǰλ������д����ֽ�����
        /// </summary>
        /// <param name="buffer">����д�����ݵĻ�������</param>
        /// <param name="offset">buffer �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ�������</param>
        /// <param name="count">���д����ֽ�����</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public Task WriteAsync(Byte[] buffer, int offset, int count)
        {
            return WriteAsync(buffer, offset, count, CancellationToken.None);
        }

        /// <summary>
        /// ���ֽڵ������첽д�뵱ǰ�����������еĵ�ǰλ����ǰ�ƶ�д����ֽ�����������ȡ������
        /// </summary>
        /// <param name="buffer">����д�����ݵĻ�������</param>
        /// <param name="offset">buffer �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ�������</param>
        /// <param name="count">���д����ֽ�����</param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�  Ĭ��ֵΪ None��  </param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        public virtual Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // If cancellation was requested, bail early with an already completed task.
            // Otherwise, return a task that represents the Begin/End methods.
            return cancellationToken.IsCancellationRequested
                        ? Task.FromCancellation(cancellationToken)
                        : BeginEndWriteAsync(buffer, offset, count);
        }


        private Task BeginEndWriteAsync(Byte[] buffer, Int32 offset, Int32 count)
        {            
            return TaskFactory<VoidTaskResult>.FromAsyncTrim(
                        this, new ReadWriteParameters { Buffer=buffer, Offset=offset, Count=count },
                        (stream, args, callback, state) => stream.BeginWrite(args.Buffer, args.Offset, args.Count, callback, state), // cached by compiler
                        (stream, asyncResult) => // cached by compiler
                        {
                            stream.EndWrite(asyncResult);
                            return default(VoidTaskResult);
                        });
        }
#endif // !FEATURE_PAL && FEATURE_ASYNC_IO

        /// <summary>
        /// ��������������дʱ�����õ�ǰ���е�λ�á�
        /// </summary>
        /// <param name="offset">����� origin �������ֽ�ƫ������</param>
        /// <param name="origin">SeekOrigin ���͵�ֵ��ָʾ���ڻ�ȡ��λ�õĲο��㡣</param>
        /// <returns>��ǰ���е���λ�á�</returns>
        public abstract long Seek(long offset, SeekOrigin origin);

        /// <summary>
        /// ��������������дʱ�����õ�ǰ���ĳ��ȡ�
        /// </summary>
        /// <param name="value">����ĵ�ǰ���ĳ��ȣ����ֽڱ�ʾ����</param>
        public abstract void SetLength(long value);

        /// <summary>
        /// ��������������дʱ���ӵ�ǰ����ȡ�ֽ����У����������е�λ��������ȡ���ֽ�����
        /// </summary>
        /// <param name="buffer">�ֽ����顣�˷�������ʱ���û���������ָ�����ַ����飬������� offset �� (offset + count -1) ֮���ֵ�ɴӵ�ǰԴ�ж�ȡ���ֽ��滻��</param>
        /// <param name="offset">buffer �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ�洢�ӵ�ǰ���ж�ȡ�����ݡ�</param>
        /// <param name="count">Ҫ�ӵ�ǰ��������ȡ���ֽ�����</param>
        /// <returns></returns>
        public abstract int Read([In, Out] byte[] buffer, int offset, int count);

        // Reads one byte from the stream by calling Read(byte[], int, int). 
        // Will return an unsigned byte cast to an int or -1 on end of stream.
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are reading one byte at a time.
        /// <summary>
        /// �����ж�ȡһ���ֽڣ��������ڵ�λ����ǰ����һ���ֽڣ���������ѵ�������β���򷵻� -1��
        /// </summary>
        /// <returns>ǿ��ת��Ϊ Int32 ���޷����ֽڣ������������ĩβ����Ϊ -1��</returns>
        public virtual int ReadByte()
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < 256);

            byte[] oneByteArray = new byte[1];
            int r = Read(oneByteArray, 0, 1);
            if (r==0)
                return -1;
            return oneByteArray[0];
        }

        /// <summary>
        /// ��������������дʱ����ǰ����д���ֽ����У����������еĵ�ǰλ������д����ֽ�����
        /// </summary>
        /// <param name="buffer">�ֽ����顣�˷����� count ���ֽڴ� buffer ���Ƶ���ǰ����</param>
        /// <param name="offset">buffer �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ���ǰ����</param>
        /// <param name="count">Ҫд�뵱ǰ�����ֽ�����</param>
        public abstract void Write(byte[] buffer, int offset, int count);

        // Writes one byte from the stream by calling Write(byte[], int, int).
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are writing one byte at a time.
        /// <summary>
        /// ��һ���ֽ�д�����ڵĵ�ǰλ�ã��������ڵ�λ����ǰ����һ���ֽڡ�
        /// </summary>
        /// <param name="value">Ҫд�����е��ֽڡ�</param>
        public virtual void WriteByte(byte value)
        {
            byte[] oneByteArray = new byte[1];
            oneByteArray[0] = value;
            Write(oneByteArray, 0, 1);
        }
        /// <summary>
        /// ��ָ���� Stream ������Χ�����̰߳�ȫ��ͬ������װ��
        /// </summary>
        /// <param name="stream">Ҫͬ���� Stream ����</param>
        /// <returns>һ���̰߳�ȫ�� Stream ����</returns>
        [HostProtection(Synchronization=true)]
        public static Stream Synchronized(Stream stream) 
        {
            if (stream==null)
                throw new ArgumentNullException("stream");
            Contract.Ensures(Contract.Result<Stream>() != null);
            Contract.EndContractBlock();
            if (stream is SyncStream)
                return stream;
            
            return new SyncStream(stream);
        }

#if !FEATURE_PAL  // This method shouldn't have been exposed in Dev10 (we revised object invariants after locking down).
        [Obsolete("Do not call or override this method.")]
        protected virtual void ObjectInvariant() 
        {
        }
#endif
        /// <summary>
        /// �鼶BeginRead(�����WP8���첽����)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        internal IAsyncResult BlockingBeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);

            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously.  
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult; 
            try {
                int numRead = Read(buffer, offset, count);
                asyncResult = new SynchronousAsyncResult(numRead, state);
            }
            catch (IOException ex) {
                asyncResult = new SynchronousAsyncResult(ex, state, isWrite: false);
            }
            
            if (callback != null) {
                callback(asyncResult);
            }

            return asyncResult;
        }

        internal static int BlockingEndRead(IAsyncResult asyncResult)
        {
            Contract.Ensures(Contract.Result<int>() >= 0);

            return SynchronousAsyncResult.EndRead(asyncResult);
        }

        internal IAsyncResult BlockingBeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);

            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously.  
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult;
            try {
                Write(buffer, offset, count);
                asyncResult = new SynchronousAsyncResult(state);
            }
            catch (IOException ex) {
                asyncResult = new SynchronousAsyncResult(ex, state, isWrite: true);
            }

            if (callback != null) {
                callback(asyncResult);
            }

            return asyncResult;
        }

        internal static void BlockingEndWrite(IAsyncResult asyncResult)
        {
            SynchronousAsyncResult.EndWrite(asyncResult);
        }

        /// <summary>
        /// ����
        /// </summary>
        [Serializable]
        private sealed class NullStream : Stream
        {
            internal NullStream() {}

            public override bool CanRead {
                [Pure]
                get { return true; }
            }

            public override bool CanWrite {
                [Pure]
                get { return true; }
            }

            public override bool CanSeek {
                [Pure]
                get { return true; }
            }

            public override long Length {
                get { return 0; }
            }

            public override long Position {
                get { return 0; }
                set {}
            }

            protected override void Dispose(bool disposing)
            {
                // Do nothing - we don't want NullStream singleton (static) to be closable
                // �����κ��� - ���ǲ��뵥��NullStream�ر�
            }

            public override void Flush()
            {
            }

#if FEATURE_ASYNC_IO
            [ComVisible(false)]
            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return cancellationToken.IsCancellationRequested ?
                    Task.FromCancellation(cancellationToken) :
                    Task.CompletedTask;
            }
#endif // FEATURE_ASYNC_IO

            [HostProtection(ExternalThreading = true)]
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                if (!CanRead) __Error.ReadNotSupported();

                return BlockingBeginRead(buffer, offset, count, callback, state);
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException("asyncResult");
                Contract.EndContractBlock();

                return BlockingEndRead(asyncResult);
            }

            [HostProtection(ExternalThreading = true)]
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                if (!CanWrite) __Error.WriteNotSupported();

                return BlockingBeginWrite(buffer, offset, count, callback, state);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException("asyncResult");
                Contract.EndContractBlock();

                BlockingEndWrite(asyncResult);
            }

            public override int Read([In, Out] byte[] buffer, int offset, int count)
            {
                return 0;
            }

#if FEATURE_ASYNC_IO
            [ComVisible(false)]
            public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var nullReadTask = s_nullReadTask;
                if (nullReadTask == null) 
                    s_nullReadTask = nullReadTask = new Task<int>(false, 0, (TaskCreationOptions)InternalTaskOptions.DoNotDispose, CancellationToken.None); // benign ----
                return nullReadTask;
            }
            private static Task<int> s_nullReadTask;
#endif //FEATURE_ASYNC_IO

            public override int ReadByte()
            {
                return -1;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
            }

#if FEATURE_ASYNC_IO
            [ComVisible(false)]
            public override Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return cancellationToken.IsCancellationRequested ?
                    Task.FromCancellation(cancellationToken) :
                    Task.CompletedTask;
            }
#endif // FEATURE_ASYNC_IO

            public override void WriteByte(byte value)
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return 0;
            }

            public override void SetLength(long length)
            {
            }
        }


        /// <summary>����IAsyncResult����ʹ���첽IO�������ϵķ�����</summary>
        internal sealed class SynchronousAsyncResult : IAsyncResult {
            
            private readonly Object _stateObject;// ״̬����        
            private readonly bool _isWrite;// �Ƿ���д
            private ManualResetEvent _waitHandle;// �ȴ����
            private ExceptionDispatchInfo _exceptionInfo; // �쳣��Ϣ

            private bool _endXxxCalled;// ��������
            private Int32 _bytesRead;// ��ȡ�ֽ�

            internal SynchronousAsyncResult(Int32 bytesRead, Object asyncStateObject) {
                _bytesRead = bytesRead;
                _stateObject = asyncStateObject;
                //_isWrite = false;
            }

            internal SynchronousAsyncResult(Object asyncStateObject) {
                _stateObject = asyncStateObject;
                _isWrite = true;
            }

            internal SynchronousAsyncResult(Exception ex, Object asyncStateObject, bool isWrite) {
                _exceptionInfo = ExceptionDispatchInfo.Capture(ex);
                _stateObject = asyncStateObject;
                _isWrite = isWrite;                
            }

            public bool IsCompleted {
                // We never hand out objects of this type to the user before the synchronous IO completed:
                get { return true; }
            }

            public WaitHandle AsyncWaitHandle {
                get {
                    return LazyInitializer.EnsureInitialized(ref _waitHandle, () => new ManualResetEvent(true));                    
                }
            }

            public Object AsyncState {
                get { return _stateObject; }
            }

            public bool CompletedSynchronously {
                get { return true; }
            }

            /// <summary>
            /// ����������׳�
            /// </summary>
            internal void ThrowIfError() {
                if (_exceptionInfo != null)
                    _exceptionInfo.Throw();
            }                        

            /// <summary>
            /// ������ȡ
            /// </summary>
            /// <param name="asyncResult"></param>
            /// <returns></returns>
            internal static Int32 EndRead(IAsyncResult asyncResult) {

                SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult;
                if (ar == null || ar._isWrite)
                    __Error.WrongAsyncResult();

                if (ar._endXxxCalled)
                    __Error.EndReadCalledTwice();

                ar._endXxxCalled = true;

                ar.ThrowIfError();
                return ar._bytesRead;
            }

            /// <summary>
            /// ����д��
            /// </summary>
            /// <param name="asyncResult"></param>
            internal static void EndWrite(IAsyncResult asyncResult) {

                SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult;
                if (ar == null || !ar._isWrite)
                    __Error.WrongAsyncResult();

                if (ar._endXxxCalled)
                    __Error.EndWriteCalledTwice();

                ar._endXxxCalled = true;

                ar.ThrowIfError();
            }
        }   // class SynchronousAsyncResult


        // SyncStream is a wrapper around a stream that takes 
        // a lock for every operation making it thread safe.
        /// <summary>
        /// SyncStream��װ��,Ϊÿ��������Ҫһ�������̰߳�ȫ�ġ�
        /// </summary>
        [Serializable]
        internal sealed class SyncStream : Stream, IDisposable
        {
            private Stream _stream;
            [NonSerialized]
            private bool? _overridesBeginRead;
            [NonSerialized]
            private bool? _overridesBeginWrite;

            /// <summary>
            /// ���캯��
            /// </summary>
            /// <param name="stream">Ҫ����װstream</param>
            internal SyncStream(Stream stream)
            {
                if (stream == null)
                    throw new ArgumentNullException("stream");
                Contract.EndContractBlock();
                _stream = stream;
            }
            
            public override bool CanRead {
                [Pure]
                get { return _stream.CanRead; }
            }
        
            public override bool CanWrite {
                [Pure]
                get { return _stream.CanWrite; }
            }
        
            public override bool CanSeek {
                [Pure]
                get { return _stream.CanSeek; }
            }
        
            [ComVisible(false)]
            public override bool CanTimeout {
                [Pure]
                get {
                    return _stream.CanTimeout;
                }
            }

            public override long Length {
                get {
                    lock(_stream) {
                        return _stream.Length;
                    }
                }
            }
        
            public override long Position {
                get {
                    lock(_stream) {
                        return _stream.Position;
                    }
                }
                set {
                    lock(_stream) {
                        _stream.Position = value;
                    }
                }
            }

            [ComVisible(false)]
            public override int ReadTimeout {
                get {
                    return _stream.ReadTimeout;
                }
                set {
                    _stream.ReadTimeout = value;
                }
            }

            [ComVisible(false)]
            public override int WriteTimeout {
                get {
                    return _stream.WriteTimeout;
                }
                set {
                    _stream.WriteTimeout = value;
                }
            }

            // In the off chance that some wrapped stream has different 
            // semantics for Close vs. Dispose, let's preserve that.
            /// <summary>
            /// �رյ�ǰ�����ͷ���֮������������Դ�����׽��ֺ��ļ����������ֱ�ӵ��ô˷�������Ӧȷ����������ȷ�ͷš�
            /// </summary>
            public override void Close()
            {
                lock(_stream) {
                    try {
                        _stream.Close();
                    }
                    finally {
                        base.Dispose(true);
                    }
                }
            }
            
            protected override void Dispose(bool disposing)
            {
                lock(_stream) {
                    try {
                        // Explicitly pick up a potentially methodimpl'ed Dispose
                        if (disposing)
                            ((IDisposable)_stream).Dispose();
                    }
                    finally {
                        base.Dispose(disposing);
                    }
                }
            }
        
            public override void Flush()
            {
                lock(_stream)
                    _stream.Flush();
            }
        
            public override int Read([In, Out]byte[] bytes, int offset, int count)
            {
                lock(_stream)
                    return _stream.Read(bytes, offset, count);
            }
        
            public override int ReadByte()
            {
                lock(_stream)
                    return _stream.ReadByte();
            }

            /// <summary>
            /// ����BeginMethod�������ڲ�ʵ��
            /// </summary>
            /// <param name="stream"></param>
            /// <param name="methodName"></param>
            /// <returns></returns>
            private static bool OverridesBeginMethod(Stream stream, string methodName)
            {
                Contract.Requires(stream != null, "Expected a non-null stream.");
                Contract.Requires(methodName == "BeginRead" || methodName == "BeginWrite",
                    "Expected BeginRead or BeginWrite as the method name to check.");

                // Get all of the methods on the underlying stream
                // ��ȡ�ײ��������з���
                var methods = stream.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // If any of the methods have the desired name and are defined on the base Stream
                // Type, then the method was not overridden.  If none of them were defined on the
                // base Stream, then it must have been overridden.
                // ����κη�������Ļ������������ƺͶ���,Ȼ��û�и��ǵķ�����
                // ���û��һ�������ڻ���,��ô�����뱻���ǡ�
                foreach (var method in methods)
                {
                    if (method.DeclaringType == typeof(Stream) &&
                        method.Name == methodName)
                    {
                        return false;
                    }
                }
                return true;
            }
        
            [HostProtection(ExternalThreading=true)]
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                // Lazily-initialize whether the wrapped stream overrides BeginRead
                if (_overridesBeginRead == null)
                {
                    _overridesBeginRead = OverridesBeginMethod(_stream, "BeginRead");
                }

                lock (_stream)
                {
                    // If the Stream does have its own BeginRead implementation, then we must use that override.
                    // If it doesn't, then we'll use the base implementation, but we'll make sure that the logic
                    // which ensures only one asynchronous operation does so with an asynchronous wait rather
                    // than a synchronous wait.  A synchronous wait will result in a deadlock condition, because
                    // the EndXx method for the outstanding async operation won't be able to acquire the lock on
                    // _stream due to this call blocked while holding the lock.
                    // ��������Լ���BeginReadʵ��,��ô���Ǳ���ʹ�ø��ǡ����û��,��ô���ǽ�ʹ�û���ʵ��,
                    // �����ǻ�ȷ��ֻ��һ���첽�������߼����첽�ȴ�������,������һ��ͬ���ȡ�
                    // ͬ���ᵼ������������,��Ϊ������첽������EndXx�����޷������_stream��������������������е�����
                    return _overridesBeginRead.Value ?
                        _stream.BeginRead(buffer, offset, count, callback, state) :
                        _stream.BeginReadInternal(buffer, offset, count, callback, state, serializeAsynchronously: true);
                }
            }
        
            public override int EndRead(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException("asyncResult");
                Contract.Ensures(Contract.Result<int>() >= 0);
                Contract.EndContractBlock();

                lock(_stream)
                    return _stream.EndRead(asyncResult);
            }
        
            public override long Seek(long offset, SeekOrigin origin)
            {
                lock(_stream)
                    return _stream.Seek(offset, origin);
            }
        
            public override void SetLength(long length)
            {
                lock(_stream)
                    _stream.SetLength(length);
            }
        
            public override void Write(byte[] bytes, int offset, int count)
            {
                lock(_stream)
                    _stream.Write(bytes, offset, count);
            }
        
            public override void WriteByte(byte b)
            {
                lock(_stream)
                    _stream.WriteByte(b);
            }
        
            [HostProtection(ExternalThreading=true)]
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                // Lazily-initialize whether the wrapped stream overrides BeginWrite
                if (_overridesBeginWrite == null)
                {
                    _overridesBeginWrite = OverridesBeginMethod(_stream, "BeginWrite");
                }

                lock (_stream)
                {
                    // If the Stream does have its own BeginWrite implementation, then we must use that override.
                    // If it doesn't, then we'll use the base implementation, but we'll make sure that the logic
                    // which ensures only one asynchronous operation does so with an asynchronous wait rather
                    // than a synchronous wait.  A synchronous wait will result in a deadlock condition, because
                    // the EndXx method for the outstanding async operation won't be able to acquire the lock on
                    // _stream due to this call blocked while holding the lock.
                    return _overridesBeginWrite.Value ?
                        _stream.BeginWrite(buffer, offset, count, callback, state) :
                        _stream.BeginWriteInternal(buffer, offset, count, callback, state, serializeAsynchronously: true);
                }
            }
            
            public override void EndWrite(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException("asyncResult");
                Contract.EndContractBlock();

                lock(_stream)
                    _stream.EndWrite(asyncResult);
            }
        }
    }

#if CONTRACTS_FULL
    [ContractClassFor(typeof(Stream))]
    internal abstract class StreamContract : Stream
    {
        public override long Seek(long offset, SeekOrigin origin)
        {
            Contract.Ensures(Contract.Result<long>() >= 0);
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() <= count);
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Position {
            get {
                Contract.Ensures(Contract.Result<long>() >= 0);
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override bool CanRead {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite {
            get { throw new NotImplementedException(); }
        }

        public override bool CanSeek {
            get { throw new NotImplementedException(); }
        }

        public override long Length
        {
            get {
                Contract.Ensures(Contract.Result<long>() >= 0);
                throw new NotImplementedException();
            }
        }
    }
#endif  // CONTRACTS_FULL
}
