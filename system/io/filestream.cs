// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  FileStream
** 
** <OWNER>[....]</OWNER>
**
**
** Purpose: Exposes a Stream around a file, with full 
** synchronous and asychronous support, and buffering.
**
**
===========================================================*/
using System;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Security;
#if FEATURE_MACL
using System.Security.AccessControl;
#endif
using System.Security.Permissions;
using System.Threading;
#if FEATURE_ASYNC_IO
using System.Threading.Tasks;
#endif
using System.Runtime.InteropServices;
#if FEATURE_REMOTING
using System.Runtime.Remoting.Messaging;
#endif
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;
using System.Diagnostics.Tracing;

/*
 * FileStream supports different modes of accessing the disk - async mode
 * and [....] mode.  They are two completely different codepaths in the
 * [....] & async methods (ie, Read/Write vs. BeginRead/BeginWrite).  File
 * handles in NT can be opened in only [....] or overlapped (async) mode,
 * and we have to deal with this pain.  Stream has implementations of
 * the [....] methods in terms of the async ones, so we'll
 * call through to our base class to get those methods when necessary.
 *
 * Also buffering is added into FileStream as well. Folded in the
 * code from BufferedStream, so all the comments about it being mostly
 * aggressive (and the possible perf improvement) apply to FileStream as 
 * well.  Also added some buffering to the async code paths.
 *
 * Class Invariants:
 * The class has one buffer, shared for reading & writing.  It can only be
 * used for one or the other at any point in time - not both.  The following
 * should be true:
 *   0 <= _readPos <= _readLen < _bufferSize
 *   0 <= _writePos < _bufferSize
 *   _readPos == _readLen && _readPos > 0 implies the read buffer is valid, 
 *     but we're at the end of the buffer.
 *   _readPos == _readLen == 0 means the read buffer contains garbage.
 *   Either _writePos can be greater than 0, or _readLen & _readPos can be
 *     greater than zero, but neither can be greater than zero at the same time.
 *
 */

namespace System.IO {

    // This is an internal object implementing IAsyncResult with fields
    // for all of the relevant data necessary to complete the IO operation.
    // This is used by AsyncFSCallback and all of the async methods.
    // We should probably make this a nested type of FileStream. But 
    // I don't know how to define a nested class in mscorlib.h
    // ����һ��ʵ����IAsyncResult�ӿ��ڲ�����������IO�����������塣
    // ���Ǳ�AsyncFSCallback��ȫ�����첽���÷������õġ�
    // ����Ӧ������һ��Ƕ�����͵��ļ����������Ҳ�֪����ζ���һ��Ƕ�׵�����mscorlib.h


    // Ideally we should make this type windows only (!FEATURE_PAL). But to make that happen
    // we need to do a lot of untangling in the VM code.
    // ����������������Ӧ�����������͵Ĵ���ֻ��(! FEATURE_PAL)��
    // ��Ҫ������һ��,������Ҫ���ܶ�⿪VM���롣
    /// <summary>
    /// �ļ������첽���
    /// </summary>
    unsafe internal sealed class FileStreamAsyncResult : IAsyncResult
    {
        // README:
        // If you modify the order of these fields, make sure to update 
        // the native VM definition of this class as well!!! 
#if FEATURE_ASYNC_IO
        // User code callback
        private AsyncCallback _userCallback;
#endif
        private Object _userStateObject;
        /// <summary>
        /// �ļ���
        /// </summary>
        private ManualResetEvent _waitHandle;
        [SecurityCritical]
        private SafeFileHandle _handle;      // For cancellation support.
#if !FEATURE_PAL
        [SecurityCritical]
        private NativeOverlapped* _overlapped;
        internal NativeOverlapped* OverLapped { [SecurityCritical]get { return _overlapped; } }
        internal bool IsAsync { [SecuritySafeCritical]get { return _overlapped != null; } }
#endif
        /// <summary>
        /// �����Ƿ��Ѿ�����Endxxx.
        /// </summary>
        internal int _EndXxxCalled;
        private int _numBytes;     // number of bytes read OR written
        internal int NumBytes { get { return _numBytes; } }

        private int _errorCode;
        internal int ErrorCode { get { return _errorCode; } }

        private int _numBufferedBytes;
        internal int NumBufferedBytes { get { return _numBufferedBytes; } }

        /// <summary>
        /// ��ȡ��ȡ���ֽ���
        /// </summary>
        internal int NumBytesRead { get { return _numBytes + _numBufferedBytes; } }

        private bool _isWrite;     // Whether this is a read or a write
        internal bool IsWrite { get { return _isWrite; } }

        private bool _isComplete;  // Value for IsCompleted property        
        private bool _completedSynchronously;  // Which thread called callback

        // The NativeOverlapped struct keeps a GCHandle to this IAsyncResult object.
        // So if the user doesn't call EndRead/EndWrite, a finalizer won't help because
        // it'll never get called. 

        // Overlapped class will take care of the async IO operations in progress 
        // when an appdomain unload occurs.

#if FEATURE_ASYNC_IO
        [System.Security.SecurityCritical] // auto-generated
        private unsafe static IOCompletionCallback s_IOCallback;

        /// <summary>
        /// �ļ������첽���
        /// </summary>
        /// <param name="numBufferedBytes">�����ֽ���</param>
        /// <param name="bytes">��������</param>
        /// <param name="handle">�ļ����</param>
        /// <param name="userCallback">�û��ص�</param>
        /// <param name="userStateObject">�û�״̬����</param>
        /// <param name="isWrite">�Ƿ��Ƕ�</param>
        [SecuritySafeCritical]
        internal FileStreamAsyncResult(
            int numBufferedBytes,
            byte[] bytes,
            SafeFileHandle handle,
            AsyncCallback userCallback,
            Object userStateObject,
            bool isWrite)
        {
            _userCallback = userCallback;
            _userStateObject = userStateObject;
            _isWrite = isWrite;
            _numBufferedBytes = numBufferedBytes;
            _handle = handle;

            // For Synchronous IO, I could go with either a callback and using
            // the managed Monitor class, or I could create a handle and wait on it.
            // ����ͬ��IO���ҿ�����һ���ص���ʹ�ü�ع����࣬�������ǿ��Դ���һ������͵ȴ���
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            _waitHandle = waitHandle;

            // Create a managed overlapped class
            // We will set the file offsets later
            // ����һ������overlapped�࣬���ǽ����������ļ�ƫ����
            Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, this);

            // Pack the Overlapped class, and store it in the async result
            if (userCallback != null)
            {
                var ioCallback = s_IOCallback; // cached static delegate; delay initialized due to it being SecurityCritical
                if (ioCallback == null) s_IOCallback = ioCallback = new IOCompletionCallback(AsyncFSCallback);
                _overlapped = overlapped.Pack(ioCallback, bytes);
            }
            else
            {
                _overlapped = overlapped.UnsafePack(null, bytes);
            }

            Contract.Assert(_overlapped != null, "Did Overlapped.Pack or Overlapped.UnsafePack just return a null?");
        }

        /// <summary>
        /// ���������ȡ���
        /// </summary>
        /// <param name="numBufferedBytes"></param>
        /// <param name="userCallback"></param>
        /// <param name="userStateObject"></param>
        /// <param name="isWrite"></param>
        /// <returns></returns>
        internal static FileStreamAsyncResult CreateBufferedReadResult(int numBufferedBytes, AsyncCallback userCallback, Object userStateObject, bool isWrite)
        {
            FileStreamAsyncResult asyncResult = new FileStreamAsyncResult(numBufferedBytes, userCallback, userStateObject, isWrite);
            asyncResult.CallUserCallback();
            return asyncResult;
        }

        // This creates a synchronous Async Result. We should consider making this a separate class and maybe merge it with 
        // System.IO.Stream.SynchronousAsyncResult
        private FileStreamAsyncResult(int numBufferedBytes, AsyncCallback userCallback, Object userStateObject, bool isWrite)
        {
            _userCallback = userCallback;
            _userStateObject = userStateObject;
            _isWrite = isWrite;
            _numBufferedBytes = numBufferedBytes;
        }
#endif // FEATURE_ASYNC_IO

        /// <summary>
        /// ��ȡ�û�����Ķ������޶�����������첽��������Ϣ��
        /// </summary>
        public Object AsyncState
        {
            get { return _userStateObject; }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ�첽�����Ƿ�����ɡ�
        /// </summary>
        public bool IsCompleted
        {
            get { return _isComplete; }
        }

        /// <summary>
        /// ��ȡ���ڵȴ��첽������ɵ� WaitHandle��
        /// </summary>
        public WaitHandle AsyncWaitHandle
        {
#if !FEATURE_PAL
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ResourceExposure(ResourceScope.None)]
            [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
            get {
                // Consider uncommenting this someday soon - the EventHandle 
                // in the Overlapped struct is really useless half of the 
                // time today since the OS doesn't signal it.  If users call
                // EndXxx after the OS call happened to complete, there's no
                // reason to create a synchronization primitive here.  Fixing
                // this will save us some perf, assuming we can correctly
                // initialize the ManualResetEvent.  
                // �ⲻ�õĽ�������ȡ������EventHandle�ص��ṹʵ�������õĽ���һ���ʱ��,
                // ��Ϊ����ϵͳ������ζ���������OS���÷�������ɺ�,û�����ɴ���ͬ��ԭ�
                // �û�����EndXxx���޸��⽫��������һЩ����,�������ǿ�����ȷ�س�ʼ��ManualResetEvent��
                if (_waitHandle == null) {
                    ManualResetEvent mre = new ManualResetEvent(false);
                    if (_overlapped != null && _overlapped->EventHandle != IntPtr.Zero) {
                        mre.SafeWaitHandle = new SafeWaitHandle(_overlapped->EventHandle, true);
                    }

                    // make sure only one thread sets _waitHandle
                    if (Interlocked.CompareExchange<ManualResetEvent>(ref _waitHandle, mre, null) == null) {
                        if (_isComplete)
                            _waitHandle.Set();
                    }
                    else {
                        // There's a slight but acceptable ---- if we weren't
                        // the thread that set _waitHandle and this code path
                        // returns before the code in the if statement 
                        // executes (on the other thread). However, the 
                        // caller is waiting for the wait handle to be set, 
                        // which will still happen.
                        mre.Close();
                    }
                }
                return _waitHandle;
            }
#else
            get { return null; }
#endif //!FEATURE_PAL
        }

        // Returns true iff the user callback was called by the thread that 
        // called BeginRead or BeginWrite.  If we use an async delegate or
        // threadpool thread internally, this will be false.  This is used
        // by code to determine whether a successive call to BeginRead needs 
        // to be done on their main thread or in their callback to avoid a
        // stack overflow on many reads or writes.
        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ�첽�����Ƿ�ͬ����ɡ�
        /// </summary>
        public bool CompletedSynchronously
        {
            get { return _completedSynchronously; }
        }

#if FEATURE_ASYNC_IO
        /// <summary>
        /// �����û�callback����
        /// </summary>
        private void CallUserCallbackWorker()
        {
            _isComplete = true;

            // ensure _isComplete is set before reading _waitHandle
            // ȷ���ڶ�ȡ_waitHandle֮ǰ_isComplete����ֵ
            Thread.MemoryBarrier();
            if (_waitHandle != null)
                _waitHandle.Set();

            _userCallback(this);
        }

        /// <summary>
        /// �����û�callback
        /// </summary>
        internal void CallUserCallback()
        {
            // Convenience method for me, since I have to do this in a number 
            // of places in the buffering code for fake IAsyncResults.   
            // AsyncFSCallback intentionally does not use this method.
            // �����ұ����ķ�����Ϊ��Ҫ������ط���һЩd���ڼ�IAsyncResultd�Ļ������
            // AsyncFSCallback���ⲻʹ�����ַ�����
            if (_userCallback != null) {
                // Call user's callback on a threadpool thread.  
                // Set completedSynchronously to false, since it's on another 
                // thread, not the main thread.
                // �����û���callback��һ���̳߳��̡߳�
                // ����completedSynchronouslyΪʧ�ܣ���Ϊ������һ���̣߳��������߳�
                _completedSynchronously = false;
                ThreadPool.QueueUserWorkItem(state => ((FileStreamAsyncResult)state).CallUserCallbackWorker(), this);
            }
            else {
                _isComplete = true;

                // ensure _isComplete is set before reading _waitHandle
                // ȷ���ڶ�ȡ_waitHandle֮ǰ_isComplete������
                Thread.MemoryBarrier();
                if (_waitHandle != null)
                    _waitHandle.Set();
            }
        }

        /// <summary>
        /// ����ڴ��GC���
        /// </summary>
        [SecurityCritical]
        internal void ReleaseNativeResource()
        {
            // Free memory & GC handles.
            if (this._overlapped != null)
                Overlapped.Free(_overlapped);
        }

        /// <summary>
        /// �ȴ�
        /// </summary>
        internal void Wait()
        {
            if (_waitHandle != null)
            {
                // We must block to ensure that AsyncFSCallback has completed,
                // and we should close the WaitHandle in here.  AsyncFSCallback
                // and the hand-ported imitation version in COMThreadPool.cpp 
                // are the only places that set this event.
                // ���Ǳ���ȷ��asyncfscallback���Ѿ���ɣ�����Ӧ��������ȴ������
                // asyncfscallback������ֲ�°���comthreadpool.cppʹ��һ�¼���Ψһ�ĵط���
                try
                {
                    _waitHandle.WaitOne();
                    Contract.Assert(_isComplete == true, "FileStreamAsyncResult::Wait - AsyncFSCallback  didn't set _isComplete to true!");
                }
                finally
                {
                    _waitHandle.Close();
                }
            }
        }
        
        // When doing IO asynchronously (ie, _isAsync==true), this callback is 
        // called by a free thread in the threadpool when the IO operation 
        // completes.  
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        unsafe private static void AsyncFSCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            BCLDebug.Log(String.Format("AsyncFSCallback called.  errorCode: " + errorCode + "  numBytes: " + numBytes));

            // Unpack overlapped
            Overlapped overlapped = Overlapped.Unpack(pOverlapped);
            // Free the overlapped struct in EndRead/EndWrite.

            // Extract async result from overlapped 
            FileStreamAsyncResult asyncResult =
                (FileStreamAsyncResult)overlapped.AsyncResult;
            asyncResult._numBytes = (int)numBytes;

            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled(EventLevel.Informational, FrameworkEventSource.Keywords.ThreadTransfer))
                FrameworkEventSource.Log.ThreadTransferReceive((long)(asyncResult.OverLapped), 2, string.Empty);

            // Handle reading from & writing to closed pipes.  While I'm not sure
            // this is entirely necessary anymore, maybe it's possible for 
            // an async read on a pipe to be issued and then the pipe is closed, 
            // returning this error.  This may very well be necessary.
            if (errorCode == FileStream.ERROR_BROKEN_PIPE || errorCode == FileStream.ERROR_NO_DATA)
                errorCode = 0;

            asyncResult._errorCode = (int)errorCode;

            // Call the user-provided callback.  It can and often should
            // call EndRead or EndWrite.  There's no reason to use an async 
            // delegate here - we're already on a threadpool thread.  
            // IAsyncResult's completedSynchronously property must return
            // false here, saying the user callback was called on another thread.
            asyncResult._completedSynchronously = false;
            asyncResult._isComplete = true;

            // ensure _isComplete is set before reading _waitHandle
            Thread.MemoryBarrier();

            // The OS does not signal this event.  We must do it ourselves.
            ManualResetEvent wh = asyncResult._waitHandle;
            if (wh != null)
            {
                Contract.Assert(!wh.SafeWaitHandle.IsClosed, "ManualResetEvent already closed!");
                bool r = wh.Set();
                Contract.Assert(r, "ManualResetEvent::Set failed!");
                if (!r) __Error.WinIOError();
            }

            AsyncCallback userCallback = asyncResult._userCallback;
            if (userCallback != null)
                userCallback(asyncResult);
        }

        [SecuritySafeCritical]
        [HostProtection(ExternalThreading = true)]
        internal void Cancel()
        {
            Contract.Assert(_handle != null, "_handle should not be null.");
            Contract.Assert(_overlapped != null, "Cancel should only be called on true asynchronous FileStreamAsyncResult, i.e. _overlapped is not null");

            if (IsCompleted)
                return;

            if (_handle.IsInvalid)
                return;

            bool r = Win32Native.CancelIoEx(_handle, _overlapped);
            if (!r)
            {
                int errorCode = Marshal.GetLastWin32Error();

                // ERROR_NOT_FOUND is returned if CancelIoEx cannot find the request to cancel.
                // This probably means that the IO operation has completed.
                if (errorCode != Win32Native.ERROR_NOT_FOUND)
                    __Error.WinIOError(errorCode, String.Empty);
            }
        }
#endif //FEATURE_ASYNC_IO
    }

    /// <summary>
    /// Ϊ�ļ��ṩ Stream����֧��ͬ����д������Ҳ֧���첽��д������
    /// </summary>
    [ComVisible(true)]
    public class FileStream : Stream
    {
        /// <summary>
        /// Ĭ�ϵĻ����С
        /// </summary>
        internal const int DefaultBufferSize = 4096;


#if FEATURE_LEGACYNETCF
        // Mango didn't do support Async IO.
        private static readonly bool _canUseAsync = !CompatibilitySwitches.IsAppEarlierThanWindowsPhone8;
#else
        private const bool _canUseAsync = true;
#endif //FEATURE_ASYNC_IO

        /// <summary>
        /// �������� ���ڷ����ȡ/д�뻺��
        /// </summary>
        private byte[] _buffer;   // Shared read/write buffer.  Alloc on first use.
        /// <summary>
        /// �ļ�����
        /// </summary>
        private String _fileName; // Fully qualified file name.
        /// <summary>
        /// �����Ƿ�����ص����������
        /// </summary>
        private bool _isAsync;    // Whether we opened the handle for overlapped IO
        private bool _canRead;
        private bool _canWrite;
        private bool _canSeek;
        /// <summary>
        /// ���������Ƿ���ʹ��������
        /// </summary>
        private bool _exposedHandle; // Could other code be using this handle?
        /// <summary>
        /// �Ƿ�����첽�������
        /// </summary>
        private bool _isPipe;     // Whether to disable async buffering code.
        /// <summary>
        /// �ڹ���������ȡָ��
        /// </summary>
        private int _readPos;     // Read pointer within shared buffer.
        /// <summary>
        /// ���ļ���������ȡ���ֽ���
        /// </summary>
        private int _readLen;     // Number of bytes read in buffer from file.
        /// <summary>
        /// �ڹ���������дָ��
        /// </summary>
        private int _writePos;    // Write pointer within shared buffer.
        /// <summary>
        /// �ڲ�����ĳ��ȣ����������Ļ�
        /// </summary>
        private int _bufferSize;  // Length of internal buffer, if it's allocated.
        /// <summary>
        /// �ļ��İ�ȫ���
        /// </summary>
        [System.Security.SecurityCritical] // auto-generated
        private SafeFileHandle _handle;
        /// <summary>
        /// �ļ��еĵ�ǰ��������
        /// </summary>
        private long _pos;        // Cache current location in the file.
        /// <summary>
        /// ����ӣ���ֹ�����ļ���
        /// </summary>
        private long _appendStart;// When appending, prevent overwriting file.
#if FEATURE_ASYNC_IO
        private static AsyncCallback s_endReadTask;
        private static AsyncCallback s_endWriteTask;
        private static Action<object> s_cancelReadHandler;
        private static Action<object> s_cancelWriteHandler;
#endif

        //This exists only to support IsolatedStorageFileStream.
        //Any changes to FileStream must include the corresponding changes in IsolatedStorage.
        // ��ֻ����֧��д��IsolatedStorageFileStream��
        // ��FileStream�κα仯���������Ӧ�ı�IsolatedStorage��
        internal FileStream() { 
        }
#if FEATURE_CORECLR
        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode) 
            : this(path, mode, (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), FileShare.Read, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false, false, true) {
#if FEATURE_LEGACYNETCF
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8) {
                System.Reflection.Assembly callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
                if(callingAssembly != null && !callingAssembly.IsProfileAssembly) {
                    string caller = new System.Diagnostics.StackFrame(1).GetMethod().FullName;
                    string callee = System.Reflection.MethodBase.GetCurrentMethod().FullName;
                    throw new MethodAccessException(String.Format(
                        CultureInfo.CurrentCulture,
                        Environment.GetResourceString("Arg_MethodAccessException_WithCaller"),
                        caller,
                        callee));
                }
            }
#endif // FEATURE_LEGACYNETCF
        }
    
        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access) 
            : this(path, mode, access, FileShare.Read, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false, false, true) {
#if FEATURE_LEGACYNETCF
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8) {
                System.Reflection.Assembly callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
                if(callingAssembly != null && !callingAssembly.IsProfileAssembly) {
                    string caller = new System.Diagnostics.StackFrame(1).GetMethod().FullName;
                    string callee = System.Reflection.MethodBase.GetCurrentMethod().FullName;
                    throw new MethodAccessException(String.Format(
                        CultureInfo.CurrentCulture,
                        Environment.GetResourceString("Arg_MethodAccessException_WithCaller"),
                        caller,
                        callee));
                }
            }
#endif // FEATURE_LEGACYNETCF
        }

        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share) 
            : this(path, mode, access, share, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false, false, true) {
#if FEATURE_LEGACYNETCF
            if(CompatibilitySwitches.IsAppEarlierThanWindowsPhone8) {
                System.Reflection.Assembly callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
                if(callingAssembly != null && !callingAssembly.IsProfileAssembly) {
                    string caller = new System.Diagnostics.StackFrame(1).GetMethod().FullName;
                    string callee = System.Reflection.MethodBase.GetCurrentMethod().FullName;
                    throw new MethodAccessException(String.Format(
                        CultureInfo.CurrentCulture,
                        Environment.GetResourceString("Arg_MethodAccessException_WithCaller"),
                        caller,
                        callee));
                }
            }
#endif // FEATURE_LEGACYNETCF
        }

        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize) 
            : this(path, mode, access, share, bufferSize, FileOptions.None, Path.GetFileName(path), false, false, true)
        {
        }

#else // FEATURE_CORECLR
        /// <summary>
        /// ��ʼ��FileStream�����ʵ����ָ����·�����ļ���ģʽ
        /// </summary>
        /// <param name="path">��ԵĻ�ǰFileStream���󽫷�װ���ļ��ľ���·��</param>
        /// <param name="mode">�ļ���ģʽ</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode) 
            : this(path, mode, (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), FileShare.Read, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false) {
        }

        /// <summary>
        /// ��ʼ��FileStream�����ʵ����ָ����·��������ģʽ���Ͷ�/дȨ�ޡ�
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access">��/дȨ��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access) 
            : this(path, mode, access, FileShare.Read, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false) {
        }

        /// <summary>
        /// ��ʼ��FileStream�����ʵ����ָ����·��������ģʽ����дȨ�޺͹���Ȩ�ޡ�
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share">����Ȩ��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share) 
            : this(path, mode, access, share, DefaultBufferSize, FileOptions.None, Path.GetFileName(path), false) {
        }

        /// <summary>
        /// ��ָ����·��������ģʽ����/д������Ȩ�޺ͻ�������С��ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        /// <param name="bufferSize">һ����������� Int32 ֵ����ʾ��������С��Ĭ�ϻ�������СΪ 4096��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize) 
            : this(path, mode, access, share, bufferSize, FileOptions.None, Path.GetFileName(path), false)
        {
        }
#endif // FEATURE_CORECLR

        /// <summary>
        /// ʹ��ָ����·��������ģʽ����/д�͹���Ȩ�ޡ����� FileStreams ���Ծ��еĶԴ��ļ��ķ���Ȩ�ޡ���������С�͸����ļ�ѡ���ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        /// <param name="bufferSize"></param>
        /// <param name="options">һ��ָ�������ļ�ѡ���ֵ��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)
            : this(path, mode, access, share, bufferSize, options, Path.GetFileName(path), false)
        {
        }


        /// <summary>
        /// ��ʼ��FileStream�����ʵ����ָ����·��������ģʽ����/д�͹���Ȩ�ޣ��������Ĵ�С����ͬ�����첽״̬��
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        /// <param name="bufferSize"></param>
        /// <param name="useAsync">�Ƿ�ʹ���첽</param>
#if FEATURE_CORECLR
        [System.Security.SecurityCritical] // auto-generated
#else
        [System.Security.SecuritySafeCritical]
        #endif
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) 
            : this(path, mode, access, share, bufferSize, (useAsync ? FileOptions.Asynchronous : FileOptions.None), Path.GetFileName(path), false)
        {
        }

#if FEATURE_MACL
        // This constructor is done differently to avoid loading a few more
        // classes, and more importantly, to build correctly on Rotor.
        // �˹��캯��������ͬ�ģ��Ա�����ؼ����࣬����Ҫ���ǣ�������ȷ��ת��.
        /// <summary>
        /// ʹ��ָ����·��������ģʽ������Ȩ�޺͹���Ȩ�ޡ���������С�������ļ�ѡ����ʿ��ƺ���˰�ȫ��ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="rights"></param>
        /// <param name="share"></param>
        /// <param name="bufferSize"></param>
        /// <param name="options"></param>
        /// <param name="fileSecurity">�ļ���ȫ</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options, FileSecurity fileSecurity)
        {
            Object pinningHandle;// �������
            Win32Native.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share, fileSecurity, out pinningHandle);// ��ȡ��ȫ����
            try {
                Init(path, mode, (FileAccess)0, (int)rights, true, share, bufferSize, options, secAttrs, Path.GetFileName(path), false, false, false);
            }
            finally {
                if (pinningHandle != null) {
                    GCHandle pinHandle = (GCHandle) pinningHandle;
                    pinHandle.Free();
                }
            }
        }

        /// <summary>
        /// ʹ��ָ����·��������ģʽ������Ȩ�޺͹���Ȩ�ޡ���������С�͸����ļ�ѡ���ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="rights">һ��������ȷ��Ϊ�ļ��������ʺ���˹���ʱҪʹ�õķ���Ȩ��</param>
        /// <param name="share"></param>
        /// <param name="bufferSize"></param>
        /// <param name="options"></param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(String path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options)
        {
            Win32Native.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share);
            Init(path, mode, (FileAccess)0, (int)rights, true, share, bufferSize, options, secAttrs, Path.GetFileName(path), false, false, false);
        }
#endif

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, String msgPath, bool bFromProxy)
        {
            Win32Native.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share);
            Init(path, mode, access, 0, false, share, bufferSize, options, secAttrs, msgPath, bFromProxy, false, false);
        }

        [System.Security.SecurityCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, String msgPath, bool bFromProxy, bool useLongPath)
        {
            Win32Native.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share);
            Init(path, mode, access, 0, false, share, bufferSize, options, secAttrs, msgPath, bFromProxy, useLongPath, false);
        }

        [System.Security.SecurityCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal FileStream(String path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, String msgPath, bool bFromProxy, bool useLongPath, bool checkHost)
        {
            Win32Native.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share);
            Init(path, mode, access, 0, false, share, bufferSize, options, secAttrs, msgPath, bFromProxy, useLongPath, checkHost);
        }

        // System.Security.AccessControl�����ռ���û�ж�����ת��
        /// <summary>
        /// ���ڳ�ʼ��FileStream
        /// </summary>
        /// <param name="path">ָ����·��</param>
        /// <param name="mode">ָ������ϵͳ���ļ��ķ�ʽ��</param>
        /// <param name="access">���������ļ���ȡ��д����ȡ/д�����Ȩ�޵ĳ�����</param>
        /// <param name="rights">����Ҫ�ڴ������ʺ���˹���ʱʹ�õķ���Ȩ�ޡ�</param>
        /// <param name="useRights">�Ƿ�ʹ��Ȩ��</param>
        /// <param name="share">�������ڿ������� FileStream �����ͬһ�ļ����Ծ��еķ������͵ĳ�����</param>
        /// <param name="bufferSize">���������С</param>
        /// <param name="options">��ʾ���ڴ��� FileStream ����ĸ߼�ѡ�</param>
        /// <param name="secAttrs">Native��ȫ����</param>
        /// <param name="msgPath">�ļ�������չ��</param>
        /// <param name="bFromProxy"></param>
        /// <param name="useLongPath">�Ƿ�ʹ�ó�·��</param>
        /// <param name="checkHost"></param>
        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        private void Init(String path, FileMode mode, FileAccess access, int rights, bool useRights, FileShare share, int bufferSize, FileOptions options, Win32Native.SECURITY_ATTRIBUTES secAttrs, String msgPath, bool bFromProxy, bool useLongPath, bool checkHost)
        {
            if (path == null)
                throw new ArgumentNullException("path", Environment.GetResourceString("ArgumentNull_Path"));
            if (path.Length == 0)
                throw new ArgumentException(Environment.GetResourceString("Argument_EmptyPath"));
            Contract.EndContractBlock();

#if FEATURE_MACL
            FileSystemRights fileSystemRights = (FileSystemRights)rights;// �����ļ�ϵͳȨ��
#endif  
            // msgPath must be safe to hand back to untrusted code.
            // msgpath���뽻�����������εĴ��밲ȫ��
            _fileName = msgPath;  // ������ɲ��ֹ���Ķ��������
            _exposedHandle = false;

#if FEATURE_PAL
            Contract.Assert(!useRights, "Specifying FileSystemRights is not supported on this platform!");
#endif

            // don't include inheritable in our bounds check for share
            // �����������ǵı߽�̳м�����
            FileShare tempshare = share & ~FileShare.Inheritable;
            String badArg = null;

            // �Դ����������������ж�������������� badArg
            if (mode < FileMode.CreateNew || mode > FileMode.Append)
                badArg = "mode";
            else if (!useRights && (access < FileAccess.Read || access > FileAccess.ReadWrite))
                badArg = "access";
#if FEATURE_MACL
            else if (useRights && (fileSystemRights < FileSystemRights.ReadData || fileSystemRights > FileSystemRights.FullControl))
                badArg = "rights";
#endif            
            else if (tempshare < FileShare.None || tempshare > (FileShare.ReadWrite | FileShare.Delete))
                badArg = "share";
            
            if (badArg != null)
                throw new ArgumentOutOfRangeException(badArg, Environment.GetResourceString("ArgumentOutOfRange_Enum"));
            
            // NOTE: any change to FileOptions enum needs to be matched here in the error validation
            if (options != FileOptions.None && (options & ~(FileOptions.WriteThrough | FileOptions.Asynchronous | FileOptions.RandomAccess | FileOptions.DeleteOnClose | FileOptions.SequentialScan | FileOptions.Encrypted | (FileOptions)0x20000000 /* NoBuffering */)) != 0)
                throw new ArgumentOutOfRangeException("options", Environment.GetResourceString("ArgumentOutOfRange_Enum"));

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize", Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));

            // Write access validation
            // дȨ����֤
#if FEATURE_MACL
            if ((!useRights && (access & FileAccess.Write) == 0) 
                || (useRights && (fileSystemRights & FileSystemRights.Write) == 0))
#else
            if (!useRights && (access & FileAccess.Write) == 0)
#endif //FEATURE_MACL
            {
                if (mode==FileMode.Truncate || mode==FileMode.CreateNew || mode==FileMode.Create || mode==FileMode.Append) {
                    // No write access
                    if (!useRights)
                        throw new ArgumentException(Environment.GetResourceString("Argument_InvalidFileMode&AccessCombo", mode, access));
#if FEATURE_MACL
                    else
                        throw new ArgumentException(Environment.GetResourceString("Argument_InvalidFileMode&RightsCombo", mode, fileSystemRights));
#endif //FEATURE_MACL
                }
            }

#if FEATURE_MACL
            // FileMode.Truncate only works with GENERIC_WRITE (FileAccess.Write), source:MSDN
            // FileMode.Truncateֻ����generic_write��FileAccess.Write����������Դ��MSDN
            // For backcomp use FileAccess.Write when FileSystemRights.Write is specified
            // ��FileSystemRights.Writeʱ��ʹ��FileAccess.Writeʱָ����
            if (useRights && (mode == FileMode.Truncate)) {
                if (fileSystemRights == FileSystemRights.Write) {
                    useRights = false;
                    access = FileAccess.Write;
                }
                else {
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidFileModeTruncate&RightsCombo", mode, fileSystemRights));
                }
            }
#endif

            int fAccess;
            if (!useRights) {// ��ʾΪFileSystemRights.Write
                fAccess = access == FileAccess.Read? GENERIC_READ:
                access == FileAccess.Write? GENERIC_WRITE:
                GENERIC_READ | GENERIC_WRITE;
            }
            else {
                fAccess = rights;
            }
            
            // Get absolute path - Security needs this to prevent something
            // like trying to create a file in c:\tmp with the name 
            // "..\WinNT\System32\ntoskrnl.exe".  Store it for user convenience.
            int maxPath = useLongPath ? Path.MaxLongPath : Path.MaxPath;
            String filePath = Path.NormalizePath(path, true, maxPath);// ����·���������Ϣ�����������ļ�·��

            _fileName = filePath;// ���ñ�FileStream��_fileName

            // Prevent access to your disk drives as raw block devices.
            if (filePath.StartsWith("\\\\.\\", StringComparison.Ordinal))
                throw new ArgumentException(Environment.GetResourceString("Arg_DevicesNotSupported"));

            // In 4.0, we always construct a FileIOPermission object below. 
            // If filePath contained a ':', we would throw a NotSupportedException in 
            // System.Security.Util.StringExpressionSet.CanonicalizePath. 
            // If filePath contained other illegal characters, we would throw an ArgumentException in 
            // FileIOPermission.CheckIllegalCharacters.
            // In 4.5 we on longer construct the FileIOPermission object in full trust.
            // To preserve the 4.0 behavior we do an explicit check for ':' here and also call Path.CheckInvalidPathChars.
            // Note that we need to call CheckInvalidPathChars before checking for ':' because that is what FileIOPermission does.

            Path.CheckInvalidPathChars(filePath, true);// �����Ƿ�����Ч�ַ���

            if (filePath.IndexOf( ':', 2 ) != -1)
                throw new NotSupportedException( Environment.GetResourceString( "Argument_PathFormatNotSupported" ) );

            bool read = false;

#if FEATURE_MACL
            // �ж��Ƿ��Ƕ������������ж϶�����FileMode�Ƿ���϶�����
            if ((!useRights && (access & FileAccess.Read) != 0) || (useRights && (fileSystemRights & FileSystemRights.ReadAndExecute) != 0))
#else
            if (!useRights && (access & FileAccess.Read) != 0)
#endif //FEATURE_MACL
            {
                if (mode == FileMode.Append)
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidAppendMode"));
                else
                    read = true;
            }

            // All demands in full trust domains are no-ops, so skip 
            // ����Ҫ����ȫ��������û���ж�����������
#if FEATURE_CAS_POLICY
            if (!CodeAccessSecurityEngine.QuickCheckForAllDemands()) 
#endif // FEATURE_CAS_POLICY
            {
                // Build up security permissions required, as well as validate we
                // have a sensible set of parameters.  IE, creating a brand new file
                // for reading doesn't make much sense.
                // ��������İ�ȫȨ�ޣ��Լ���֤������һ������Ĳ������á�
                // Ҳ����˵������һ��ȫ�µ��Ķ��ļ���û�ж�����塣
                FileIOPermissionAccess secAccess = FileIOPermissionAccess.NoAccess;

                // ���öԶ��������ļ�IO����Ȩ��
                if (read)
                {
                    Contract.Assert(mode != FileMode.Append);
                    secAccess = secAccess | FileIOPermissionAccess.Read;
                }

                // I can't think of any combos of FileMode we should disallow if we
                // don't have read access.  Writing would pretty much always be valid
                // in those cases.
                // ���벻���κ���ϵ��Ҳ�Ӧ�ò�����������������ж����ʡ�����������£�д��������Ч�ġ�

                // For any FileSystemRights other than ReadAndExecute, demand Write permission
                // This is probably bit overkill for TakeOwnership etc but we don't have any 
                // matching FileIOPermissionAccess to demand. It is better that we ask for Write permission.
                // 
#if FEATURE_MACL
                // FileMode.OpenOrCreate & FileSystemRights.Synchronize �ܴ���0 bytes�ļ�; demand write
                // �����ļ��ķ���Ȩ���Լ�д�������ж϶�FileIOPermissionAccess����
                if ((!useRights && (access & FileAccess.Write) != 0)
                    || (useRights && (fileSystemRights & (FileSystemRights.Write | FileSystemRights.Delete 
                                                | FileSystemRights.DeleteSubdirectoriesAndFiles 
                                                | FileSystemRights.ChangePermissions 
                                                | FileSystemRights.TakeOwnership)) != 0)
                    || (useRights && ((fileSystemRights & FileSystemRights.Synchronize) != 0) 
                                                && mode==FileMode.OpenOrCreate)
                   )
#else
                if (!useRights && (access & FileAccess.Write) != 0) 
#endif //FEATURE_MACL
                {
                    if (mode==FileMode.Append)
                        secAccess = secAccess | FileIOPermissionAccess.Append;
                    else
                        secAccess = secAccess | FileIOPermissionAccess.Write;
                }

#if FEATURE_MACL
                bool specifiedAcl;
                unsafe {
                    specifiedAcl = secAttrs != null && secAttrs.pSecurityDescriptor != null;
                }
                
                // ���öԿɱ�������Ĳ���
                AccessControlActions control = specifiedAcl ? AccessControlActions.Change : AccessControlActions.None;
                new FileIOPermission(secAccess, control, new String[] { filePath }, false, false).Demand();
#else
#if FEATURE_CORECLR
                if (checkHost) {
                    FileSecurityState state = new FileSecurityState(FileSecurityState.ToFileSecurityState(secAccess), path, filePath);
                    state.EnsureState();
                }
#else
                new FileIOPermission(secAccess, new String[] { filePath }, false, false).Demand();
#endif // FEATURE_CORECLR
#endif
            }

            // Our Inheritable bit was stolen from Windows, but should be set in
            // the security attributes class.  Don't leave this bit set.
            share &= ~FileShare.Inheritable;

            // �Ƿ�Ҫ��λ��ĩβ
            bool seekToEnd = (mode==FileMode.Append);
            // Must use a valid Win32 constant here...
            // ����ʹ��һ����ЧWin32����
            if (mode == FileMode.Append)
                mode = FileMode.OpenOrCreate;

            // WRT async IO, do the right thing for whatever platform we're on.
            // This way, someone can easily write code that opens a file 
            // asynchronously no matter what their platform is.  
            if (_canUseAsync && (options & FileOptions.Asynchronous) != 0)
                _isAsync = true;
            else
                options &= ~FileOptions.Asynchronous;

            int flagsAndAttributes = (int) options;

#if !FEATURE_PAL
            // For mitigating local elevation of privilege attack through named pipes
            // make sure we always call CreateFile with SECURITY_ANONYMOUS so that the
            // named pipe server can't impersonate a high privileged client security context

            //�����첽IO������ȷ���£�����ʲôƽ̨�����ǡ�
            //���������˿������ɱ�д���ļ��Ĵ���
            //�������ǵ�ƽ̨��ʲô��
            flagsAndAttributes |= (Win32Native.SECURITY_SQOS_PRESENT | Win32Native.SECURITY_ANONYMOUS);
#endif
            // Don't pop up a dialog for reading from an emtpy floppy drive
            int oldMode = Win32Native.SetErrorMode(Win32Native.SEM_FAILCRITICALERRORS);
            try {
                String tempPath = filePath;
                if (useLongPath)
                    tempPath = Path.AddLongPathPrefix(tempPath);
                // �����ļ����
                _handle = Win32Native.SafeCreateFile(tempPath, fAccess, share, secAttrs, mode, flagsAndAttributes, IntPtr.Zero);
                
                if (_handle.IsInvalid) {// �������Ϊtrue�����ʾ�˾����Ч
                                        // Return a meaningful exception, using the RELATIVE path to
                                        // the file to avoid returning extra information to the caller
                                        // unless they have path discovery permission, in which case
                                        // the full path is fine & useful.
                                        //����һ����������쳣��ʹ�����·��
                                        //���ļ��Ա��ⷵ�ظ����÷��Ķ�����Ϣ
                                        //����������·�����ֵ���ɣ������������
                                        //������·���Ƿ�������õġ�

                    // NT5 oddity - when trying to open "C:\" as a FileStream,
                    // we usually get ERROR_PATH_NOT_FOUND from the OS.  We should
                    // probably be consistent w/ every other directory.
                    // NT5����-����ͼ�򿪡�C��\����Ϊһ��FileStream��
                    // ����ͨ���Ӳ���ϵͳ��error_path_not_found������Ӧ��һ�µ�w /����Ŀ¼
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode==__Error.ERROR_PATH_NOT_FOUND && filePath.Equals(Directory.InternalGetDirectoryRoot(filePath)))
                        errorCode = __Error.ERROR_ACCESS_DENIED;

                    // We need to give an exception, and preferably it would include
                    // the fully qualified path name.  Do security check here.  If
                    // we fail, give back the msgPath, which should not reveal much.
                    // While this logic is largely duplicated in 
                    // __Error.WinIOError, we need this for 
                    // IsolatedStorageFileStream.
                    // ������Ҫ����һ�����⣬�����������������ȫ�޶���·������
                    // ����������ȫ��顣�������ʧ���ˣ���msgpath������͸¶̫�ࡣ
                    // ������߼���Ҫ���ظ�__error.winioerror��������Ҫ���д��IsolatedStorageFileStream��
                    bool canGiveFullPath = false;
                    
                    // ��IO����Ĵ���
                    if (!bFromProxy)
                    {
                        try {
#if !FEATURE_CORECLR
                            new FileIOPermission(FileIOPermissionAccess.PathDiscovery, new String[] { _fileName }, false, false ).Demand();
#endif
                            canGiveFullPath = true;
                        }
                        catch(SecurityException) {}
                    }
    
                    if (canGiveFullPath)
                        __Error.WinIOError(errorCode, _fileName);
                    else
                        __Error.WinIOError(errorCode, msgPath);
                }
            }
            finally {
                Win32Native.SetErrorMode(oldMode);
            }
                
            // Disallow access to all non-file devices from the FileStream
            // constructors that take a String.  Everyone else can call 
            // CreateFile themselves then use the constructor that takes an 
            // IntPtr.  Disallows "con:", "com1:", "lpt1:", etc.
            int fileType = Win32Native.GetFileType(_handle);
            if (fileType != Win32Native.FILE_TYPE_DISK) {// ������Ǵ����ļ���رվ�����׳��쳣
                _handle.Close();
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_FileStreamOnNonFiles"));
            }

#if FEATURE_ASYNC_IO
            // This is necessary for async IO using IO Completion ports via our 
            // managed Threadpool API's.  This (theoretically) calls the OS's 
            // BindIoCompletionCallback method, and passes in a stub for the 
            // LPOVERLAPPED_COMPLETION_ROUTINE.  This stub looks at the Overlapped
            // struct for this request and gets a delegate to a managed callback 
            // from there, which it then calls on a threadpool thread.  (We allocate
            // our native OVERLAPPED structs 2 pointers too large and store EE state
            // & GC handles there, one to an IAsyncResult, the other to a delegate.)
            if (_isAsync) {
                bool b = false;
                // BindHandle requires UnmanagedCode permission
                new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Assert();
                try {
                    b = ThreadPool.BindHandle(_handle);
                }
                finally {
                    CodeAccessPermission.RevertAssert();
                    if (!b) {
                        // We should close the handle so that the handle is not open until SafeFileHandle GC
                        Contract.Assert(!_exposedHandle, "Are we closing handle that we exposed/not own, how?");
                        _handle.Close();
                    }
                }
                if (!b) 
                    throw new IOException(Environment.GetResourceString("IO.IO_BindHandleFailed"));
            }
#endif // FEATURE_ASYNC_IO
            if (!useRights) {// �ж��Ƿ���Զ�д
                _canRead = (access & FileAccess.Read) != 0;
                _canWrite = (access & FileAccess.Write) != 0;
            }
#if FEATURE_MACL
            else {
                _canRead = (fileSystemRights & FileSystemRights.ReadData) != 0;
                _canWrite = ((fileSystemRights & FileSystemRights.WriteData) != 0) 
                            || ((fileSystemRights & FileSystemRights.AppendData) != 0);
            }
#endif //FEATURE_MACL
            
            // ����Stream�����ֵ
            _canSeek = true;
            _isPipe = false;
            _pos = 0;
            _bufferSize = bufferSize;
            _readPos = 0;
            _readLen = 0;
            _writePos = 0;

            // ����Appendģʽ...
            if (seekToEnd) {
                _appendStart = SeekCore(0, SeekOrigin.End);
            }
            else {
                _appendStart = -1;
            }
        }

        [Obsolete("This constructor has been deprecated.  Please use new FileStream(SafeFileHandle handle, FileAccess access) instead.  http://go.microsoft.com/fwlink/?linkid=14202")]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(IntPtr handle, FileAccess access) 
            : this(handle, access, true, DefaultBufferSize, false) {
        }

        [Obsolete("This constructor has been deprecated.  Please use new FileStream(SafeFileHandle handle, FileAccess access) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed.  http://go.microsoft.com/fwlink/?linkid=14202")]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(IntPtr handle, FileAccess access, bool ownsHandle) 
            : this(handle, access, ownsHandle, DefaultBufferSize, false) {
        }

        [Obsolete("This constructor has been deprecated.  Please use new FileStream(SafeFileHandle handle, FileAccess access, int bufferSize) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed.  http://go.microsoft.com/fwlink/?linkid=14202")]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize)
            : this(handle, access, ownsHandle, bufferSize, false) {
        }

        // We explicitly do a Demand, not a LinkDemand here.
        [System.Security.SecuritySafeCritical]  // auto-generated
        [Obsolete("This constructor has been deprecated.  Please use new FileStream(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed.  http://go.microsoft.com/fwlink/?linkid=14202")]
#pragma warning disable 618
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags=SecurityPermissionFlag.UnmanagedCode)]
#pragma warning restore 618
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize, bool isAsync) 
            : this(new SafeFileHandle(handle, ownsHandle), access, bufferSize, isAsync) {
        }

        /// <summary>
        /// ʹ��ָ���Ķ�/дȨ��Ϊָ�����ļ������ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="handle">��ǰ FileStream ���󽫷�װ���ļ����ļ������</param>
        /// <param name="access">һ���������������� FileStream ����� CanRead �� CanWrite ���ԡ�</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(SafeFileHandle handle, FileAccess access)
            : this(handle, access, DefaultBufferSize, false) {
        }

        /// <summary>
        /// ʹ��ָ���Ķ�/дȨ�޺ͻ�������СΪָ�����ļ������ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="handle">��ǰ FileStream ���󽫷�װ���ļ����ļ������</param>
        /// <param name="access">һ�� FileAccess ������������ FileStream ����� CanRead �� CanWrite ���ԡ�</param>
        /// <param name="bufferSize">һ����������� Int32 ֵ����ʾ��������С��Ĭ�ϻ�������СΪ 4096��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileStream(SafeFileHandle handle, FileAccess access, int bufferSize)
            : this(handle, access, bufferSize, false) {
        }

        /// <summary>
        /// ʹ��ָ���Ķ�/дȨ�ޡ���������С��ͬ�����첽״̬Ϊָ�����ļ������ʼ�� FileStream �����ʵ����
        /// </summary>
        /// <param name="handle">�� FileStream ���󽫷�װ���ļ����ļ������</param>
        /// <param name="access">һ���������������� FileStream ����� CanRead �� CanWrite ���ԡ�</param>
        /// <param name="bufferSize">һ����������� Int32 ֵ����ʾ��������С��Ĭ�ϻ�������СΪ 4096��</param>
        /// <param name="isAsync">����첽�򿪾���������ص��� I/O ģʽ������Ϊ true������Ϊ false��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
#pragma warning disable 618
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags=SecurityPermissionFlag.UnmanagedCode)]
#pragma warning restore 618
        public FileStream(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) {
            // To ensure we don't leak a handle, put it in a SafeFileHandle first
            if (handle.IsInvalid)// �ж��Ƿ�����Ч�ľ��
                throw new ArgumentException(Environment.GetResourceString("Arg_InvalidHandle"), "handle");
            Contract.EndContractBlock();

            _handle = handle;
            _exposedHandle = true;// ��ʾ���

            // Now validate arguments.
            if (access < FileAccess.Read || access > FileAccess.ReadWrite)
                throw new ArgumentOutOfRangeException("access", Environment.GetResourceString("ArgumentOutOfRange_Enum"));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize", Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));

            int handleType = Win32Native.GetFileType(_handle);
            Contract.Assert(handleType == Win32Native.FILE_TYPE_DISK || handleType == Win32Native.FILE_TYPE_PIPE || handleType == Win32Native.FILE_TYPE_CHAR, "FileStream was passed an unknown file type!");

            // ������Ե�����
            _isAsync = isAsync && _canUseAsync;
            _canRead = 0 != (access & FileAccess.Read);
            _canWrite = 0 != (access & FileAccess.Write);
            _canSeek = handleType == Win32Native.FILE_TYPE_DISK;
            _bufferSize = bufferSize;
            _readPos = 0;
            _readLen = 0;
            _writePos = 0;
            _fileName = null;
            _isPipe = handleType == Win32Native.FILE_TYPE_PIPE;

#if !FEATURE_PAL
            // This is necessary for async IO using IO Completion ports via our 
            // managed Threadpool API's.  This calls the OS's 
            // BindIoCompletionCallback method, and passes in a stub for the 
            // LPOVERLAPPED_COMPLETION_ROUTINE.  This stub looks at the Overlapped
            // struct for this request and gets a delegate to a managed callback 
            // from there, which it then calls on a threadpool thread.  (We allocate
            // our native OVERLAPPED structs 2 pointers too large and store EE 
            // state & a handle to a delegate there.)
#if !FEATURE_CORECLR
            if (_isAsync) {
                bool b = false;
                try {
                    b = ThreadPool.BindHandle(_handle);
                }
                catch (ApplicationException) {
                    // If you passed in a synchronous handle and told us to use
                    // it asynchronously, throw here.
                    // �������һ��ͬ�������д��ݣ����Ҹ�������ʹ�����������׳���
                    throw new ArgumentException(Environment.GetResourceString("Arg_HandleNotAsync"));
                }
                if (!b) {
                    throw new IOException(Environment.GetResourceString("IO.IO_BindHandleFailed"));
                }
            }
            else {
#endif // FEATURE_CORECLR
                if (handleType != Win32Native.FILE_TYPE_PIPE)
                    VerifyHandleIsSync();// ��֤����Ƿ���ͬ����
#if !FEATURE_CORECLR
            }
#endif // FEATURE_CORECLR
#else
                if (handleType != Win32Native.FILE_TYPE_PIPE)
                    VerifyHandleIsSync();
#endif //!FEATURE_PAL
            if (_canSeek)
                SeekCore(0, SeekOrigin.Current);
            else
                _pos = 0;
        }

        /// <summary>
        /// ����FileShare��ȡ��ȫ����
        /// </summary>
        /// <param name="share"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private static Win32Native.SECURITY_ATTRIBUTES GetSecAttrs(FileShare share)
        {
            Win32Native.SECURITY_ATTRIBUTES secAttrs = null;
            if ((share & FileShare.Inheritable) != 0) {
                secAttrs = new Win32Native.SECURITY_ATTRIBUTES();
                secAttrs.nLength = (int)Marshal.SizeOf(secAttrs);

                secAttrs.bInheritHandle = 1;
            }
            return secAttrs;
        }

#if FEATURE_MACL
        // If pinningHandle is not null, caller must free it AFTER the call to
        // CreateFile has returned.
        // ���pinninghandle���ǿգ��ͷź����CreateFile���ء�
        /// <summary>
        /// ��ȡ�ļ��İ�ȫ����
        /// </summary>
        /// <param name="share"></param>
        /// <param name="fileSecurity"></param>
        /// <param name="pinningHandle"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe static Win32Native.SECURITY_ATTRIBUTES GetSecAttrs(FileShare share, FileSecurity fileSecurity, out Object pinningHandle)
        {
            pinningHandle = null;
            Win32Native.SECURITY_ATTRIBUTES secAttrs = null;
            if ((share & FileShare.Inheritable) != 0 || fileSecurity != null) {// �ж��ӳ����Ƿ���Լ̳��ļ�������Լ��ļ������Ƿ�Ϊ��
                secAttrs = new Win32Native.SECURITY_ATTRIBUTES();// �԰�ȫ���Ե����Խ��и�ֵ
                secAttrs.nLength = (int)Marshal.SizeOf(secAttrs);

                if ((share & FileShare.Inheritable) != 0) {
                    secAttrs.bInheritHandle = 1;
                }

                // For ACL's, get the security descriptor from the FileSecurity.
                if (fileSecurity != null) {
                    byte[] sd = fileSecurity.GetSecurityDescriptorBinaryForm();// ��fileSecurity�л�ȡ��ȫ˵����
                    pinningHandle = GCHandle.Alloc(sd, GCHandleType.Pinned);// ΪpinningHandle����ָ���ľ��
                    fixed (byte* pSecDescriptor = sd)
                        secAttrs.pSecurityDescriptor = pSecDescriptor;// ���ð�ȫ������
                }
            }
            return secAttrs;// ����Win32Native�İ�ȫ����
        }
#endif

        // Verifies that this handle supports synchronous IO operations (unless you
        // didn't open it for either reading or writing).
        /// <summary>
        /// ��֤������֧��ͬ��IO����(������û�д��������������Ƕ���д)
        /// </summary>
        /// <exception cref="ArgumentException">Arg_HandleNotSync ��֧���쳣</exception>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe void VerifyHandleIsSync()
        {
            // Do NOT use this method on pipes.  Reading or writing to a pipe may
            // cause an app to block incorrectly, introducing a deadlock (depending
            // on whether a write will wake up an already-blocked thread or this
            // FileStream's thread).
            // ��Ҫʹ�����ַ����ڹܵ��ϡ�
            // ��ȡ��д��ܵ����ܻᵼ��Ӧ�ó����Ĵ�������������ȡ�����Ƿ�д�������Ѿ�����ֹ���̻߳�FileStream���̣߳���

            // Do NOT change this to use a byte[] of length 0, or test test won't
            // work.  Our ReadFile & WriteFile methods are special cased to return
            // for arrays of length 0, since we'd get an IndexOutOfRangeException 
            // while using C#'s fixed syntax.
            // ��Ҫʹ��һ���ֽڳ���0������Բ���ı����������
            // ���ǵ�ReadFile��WriteFile���������������
            // ����Ϊ0�����飬��Ϊ���ǻ�õ�һ��IndexOutOfRangeException
            // ����C #�̶����﷨��
            byte[] bytes = new byte[1];
            int hr = 0;
            int r = 0;

            // If the handle is a pipe, ReadFile will block until there
            // has been a write on the other end.  We'll just have to deal with it,
            // For the read end of a pipe, you can mess up and 
            // accidentally read synchronously from an async pipe.
            // ��������һ���ܵ���Ȼ����ļ�������ֱ����һ��д����һ�ˡ�
            // ����ֻ��ȥ��������Թܵ��Ķ��ˣ�����Ը���ż��ͬ���첽�ܵ���
            if (CanRead) {
                r = ReadFileNative(_handle, bytes, 0, 0, null, out hr);
            }
            else if (CanWrite) {
                r = WriteFileNative(_handle, bytes, 0, 0, null, out hr);
            }

            if (hr==ERROR_INVALID_PARAMETER)
                throw new ArgumentException(Environment.GetResourceString("Arg_HandleNotSync"));
            if (hr == Win32Native.ERROR_INVALID_HANDLE)
                __Error.WinIOError(hr, "<OS handle>");
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ��ǰ���Ƿ�֧�ֶ�ȡ��(��� Stream.CanRead��)
        /// </summary>
        public override bool CanRead {
            [Pure]
            get { return _canRead; }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ��ǰ���Ƿ�֧��д�롣
        /// </summary>
        public override bool CanWrite {
            [Pure]
            get { return _canWrite; }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ��ǰ���Ƿ�֧�ֲ��ҡ�
        /// </summary>
        public override bool CanSeek {
            [Pure]
            get { return _canSeek; }
        }

        /// <summary>
        /// ��ȡһ��ֵ����ֵָʾ FileStream ���첽����ͬ���򿪵ġ�
        /// </summary>
        public virtual bool IsAsync {
            get { return _isAsync; }
        }

        /// <summary>
        /// ��ȡ���ֽڱ�ʾ�������ȡ�
        /// </summary>
        public override long Length {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                if (_handle.IsClosed) __Error.FileNotOpen();// �ж��ļ�����Ƿ�ر�
                if (!CanSeek) __Error.SeekNotSupported();// �ж��ļ��Ƿ�֧�ֲ���
                int hi = 0, lo = 0;

                lo = Win32Native.GetFileSize(_handle, out hi);// ��ȡ�ļ�����

                if (lo==-1)
                {  // ����κδ����4GB��1�ֽڵ��ļ���
                    int hr = Marshal.GetLastWin32Error();
                    if (hr != 0)
                        __Error.WinIOError(hr, String.Empty);
                }
                long len = (((long)hi) << 32) | ((uint) lo);
                // If we're writing near the end of the file, we must include our
                // internal buffer in our Length calculation.  Don't flush because
                // we use the length of the file in our async write method.
                // ����������ļ��Ľ�β��д�Ļ������Ǳ����ڳ��ȼ����а����ڲ���������
                // ��Ҫ��ϴ����Ϊ����ʹ�õ��ļ��ĳ��������ǵ��첽д�뷽����
                if (_writePos > 0 && _pos + _writePos > len)
                    len = _writePos + _pos;
                return len;
            }
        }

        /// <summary>
        /// ��ȡ���ݸ����캯���� FileStream �����ơ�
        /// </summary>
        public String Name {
                [SecuritySafeCritical]
            get {
                if (_fileName == null)
                    return Environment.GetResourceString("IO_UnknownFileName");
#if FEATURE_CORECLR
                FileSecurityState sourceState = new FileSecurityState(FileSecurityStateAccess.PathDiscovery, String.Empty, _fileName);
                sourceState.EnsureState();
#else
                new FileIOPermission(FileIOPermissionAccess.PathDiscovery, new String[] { _fileName }, false, false).Demand();
#endif
                return _fileName;
            }
        }

        /// <summary>
        /// �ڲ���ȡ���캯����FileStream������
        /// </summary>
        internal String NameInternal {
            get {
                if (_fileName == null)
                    return "<UnknownFileName>";
                return _fileName;
            }
        }

        /// <summary>
        /// ��ȡ�����ô����ĵ�ǰλ�á�
        /// </summary>
        public override long Position {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                if (_handle.IsClosed) __Error.FileNotOpen();
                if (!CanSeek) __Error.SeekNotSupported();

                Contract.Assert((_readPos == 0 && _readLen == 0 && _writePos >= 0) || (_writePos == 0 && _readPos <= _readLen), "We're either reading or writing, but not both.");

                // Verify that internal position is in [....] with the handle
                // ͨ��������֤�ڲ�������[....]��
                if (_exposedHandle)
                    VerifyOSHandlePosition();

                // Compensate for buffer that we read from the handle (_readLen) Vs what the user
                // read so far from the internel buffer (_readPos). Of course add any unwrittern  
                // buffered data
                // �����Ļ����������ǴӴ���_readlen�����û�������ĿǰΪֹ���ڲ���������_readpos����
                // ��Ȼ������κ�unwrittern��������
                return _pos + (_readPos - _readLen + _writePos);
            }
            set {
                if (value < 0) throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
                Contract.EndContractBlock();
                if (_writePos > 0) FlushWrite(false);
                _readPos = 0;
                _readLen = 0;
                Seek(value, SeekOrigin.Begin);
            }
        }

#if FEATURE_MACL
        /// <summary>
        /// ��ȡ FileSecurity ���󣬸ö����װ��ǰ FileStream �������������ļ��ķ��ʿ����б� (ACL) ��
        /// </summary>
        /// <returns>һ�����󣬸ö����װ��ǰ FileStream �������������ļ��ķ��ʿ������á�</returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public FileSecurity GetAccessControl()
        {
            if (_handle.IsClosed) __Error.FileNotOpen();
            return new FileSecurity(_handle, _fileName, AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
        }

        /// <summary>
        /// �� FileSecurity �����������ķ��ʿ����б� (ACL) ��Ӧ���ڵ�ǰ FileStream �������������ļ���
        /// </summary>
        /// <param name="fileSecurity">����ҪӦ���ڵ�ǰ�ļ��� ACL ��Ķ���</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public void SetAccessControl(FileSecurity fileSecurity)
        {
            if (fileSecurity == null)
                throw new ArgumentNullException("fileSecurity");
            Contract.EndContractBlock();

            if (_handle.IsClosed) __Error.FileNotOpen();

            fileSecurity.Persist(_handle, _fileName);
        }
#endif
        /// <summary>
        /// �ͷ��� FileStream ռ�õķ��й���Դ���������������ͷ��й���Դ��
        /// </summary>
        /// <param name="disposing">��Ҫ�ͷ��й���Դ�ͷ��й���Դ����Ϊ true�������ͷŷ��й���Դ����Ϊ false��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected override void Dispose(bool disposing)
        {
            // Nothing will be done differently based on whether we are 
            // disposing vs. finalizing.  This is taking advantage of the
            // weak ordering between normal finalizable objects & critical
            // finalizable objects, which I included in the SafeHandle 
            // design for FileStream, which would often "just work" when 
            // finalized.
            try {
                if (_handle != null && !_handle.IsClosed) {
                    // Flush data to disk iff we were writing.  After 
                    // thinking about this, we also don't need to flush
                    // our read position, regardless of whether the handle
                    // was exposed to the user.  They probably would NOT 
                    // want us to do this.
                    if (_writePos > 0) {
                        FlushWrite(!disposing);
                    }
                }
            }
            finally {
                if (_handle != null && !_handle.IsClosed)
                    _handle.Dispose();
                
                _canRead = false;
                _canWrite = false;
                _canSeek = false;
                // Don't set the buffer to null, to avoid a NullReferenceException
                // when users have a race condition in their code (ie, they call
                // Close when calling another method on Stream like Read).
                //_buffer = null;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// ��������
        /// </summary>
        [System.Security.SecuritySafeCritical]  // auto-generated
        ~FileStream()
        {
            if (_handle != null) {
                BCLDebug.Correctness(_handle.IsClosed, "You didn't close a FileStream & it got finalized.  Name: \""+_fileName+"\"");
                Dispose(false);
            }
        }

        /// <summary>
        /// ��������Ļ�������ʹ�����л������ݶ�д�뵽�ļ���
        /// </summary>
        public override void Flush()
        {
            Flush(false);
        }

        /// <summary>
        /// ��������Ļ������������л������ݶ�д�뵽�ļ��У�����Ҳ��������м��ļ���������
        /// </summary>
        /// <param name="flushToDisk">���ˢ�������м��ļ�����������Ϊ true������Ϊ false</param>
        [System.Security.SecuritySafeCritical]
        public virtual void Flush(Boolean flushToDisk)
        {
            // This code is duplicated in Dispose
            if (_handle.IsClosed) __Error.FileNotOpen();

            FlushInternalBuffer();

            if (flushToDisk && CanWrite)
            {
                FlushOSBuffer();
            }
        }

        /// <summary>
        /// ˢ���ڲ�����
        /// </summary>
        private void FlushInternalBuffer()
        {
            // ���������д���涼���
            if (_writePos > 0)
            {
                FlushWrite(false);
            }
            else if (_readPos < _readLen && CanSeek)
            {
                FlushRead();
            }
        }

        /// <summary>
        /// ˢ��ϵͳ����
        /// </summary>
        [System.Security.SecuritySafeCritical]
        private void FlushOSBuffer()
        {
            if (!Win32Native.FlushFileBuffers(_handle))
            {
                __Error.WinIOError();
            }
        }

        // Reading is done by blocks from the file, but someone could read
        // 1 byte from the buffer then write.  At that point, the OS's file
        // pointer is out of [....] with the stream's position.  All write 
        // functions should call this function to preserve the position in the file.
        /// <summary>
        /// ��ն�ȡ����
        /// </summary>
        private void FlushRead() {
            Contract.Assert(_writePos == 0, "FileStream: Write buffer must be empty in FlushRead!");
            if (_readPos - _readLen != 0) {// FileStream��ʧȥ�����ȡ���ݡ�
                Contract.Assert(CanSeek, "FileStream will lose buffered read data now.");
                SeekCore(_readPos - _readLen, SeekOrigin.Current);
            }
            _readPos = 0;
            _readLen = 0;
        }

        // Writes are buffered.  Anytime the buffer fills up 
        // (_writePos + delta > _bufferSize) or the buffer switches to reading
        // and there is left over data (_writePos > 0), this function must be called.
        /// <summary>
        /// ���д�뻺��
        /// </summary>
        /// <param name="calledFromFinalizer">�Ƿ��Ǵ��ս�������</param>
        /// <remarks>д�뻺�塣�ڻ����������򻺳����л����Ķ������������ݣ�����������뱻���á�</remarks>
        private void FlushWrite(bool calledFromFinalizer) {
            Contract.Assert(_readPos == 0 && _readLen == 0, "FileStream: Read buffer must be empty in FlushWrite!");

#if FEATURE_ASYNC_IO
            if (_isAsync) {// �ж��Ƿ����첽���
                // ������������д�뵽stream
                IAsyncResult asyncResult = BeginWriteCore(_buffer, 0, _writePos, null, null);
                // With our Whidbey async IO & overlapped support for AD unloads,
                // we don't strictly need to block here to release resources
                // since that support takes care of the pinning & freeing the 
                // overlapped struct.  We need to do this when called from
                // Close so that the handle is closed when Close returns, but
                // we do't need to call EndWrite from the finalizer.  
                // Additionally, if we do call EndWrite, we block forever 
                // because AD unloads prevent us from running the managed 
                // callback from the IO completion port.  Blocking here when 
                // called from the finalizer during AD unload is clearly wrong, 
                // but we can't use any sort of test for whether the AD is 
                // unloading because if we weren't unloading, an AD unload 
                // could happen on a separate thread before we call EndWrite.
                if (!calledFromFinalizer)
                    EndWrite(asyncResult);
            }
            else
#endif // FEATURE_ASYNC_IO
                WriteCore(_buffer, 0, _writePos);

            _writePos = 0;
        }


        [Obsolete("This property has been deprecated.  Please use FileStream's SafeFileHandle property instead.  http://go.microsoft.com/fwlink/?linkid=14202")]
        public virtual IntPtr Handle {
            [System.Security.SecurityCritical]  // auto-generated_required
#if !FEATURE_CORECLR
            [SecurityPermissionAttribute(SecurityAction.InheritanceDemand, Flags=SecurityPermissionFlag.UnmanagedCode)]
#endif
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)]
            get { 
                Flush();
                // Explicitly dump any buffered data, since the user could move our
                // position or write to the file.
                _readPos = 0;
                _readLen = 0;
                _writePos = 0;
                _exposedHandle = true;

                return _handle.DangerousGetHandle();
            }
        }

        /// <summary>
        /// ��ȡ SafeFileHandle ����������ǰ FileStream ��������װ���ļ��Ĳ���ϵͳ�ļ����
        /// </summary>
        public virtual SafeFileHandle SafeFileHandle {
            [System.Security.SecurityCritical]  // auto-generated_required
#if !FEATURE_CORECLR
            [SecurityPermissionAttribute(SecurityAction.InheritanceDemand, Flags=SecurityPermissionFlag.UnmanagedCode)]
#endif
            get { 
                Flush();
                // Explicitly dump any buffered data, since the user could move our
                // position or write to the file.
                // ��ʽת���κλ�������ݣ���Ϊ�û����Խ����ǵ�λ�û�д���ļ���
                _readPos = 0;
                _readLen = 0;
                _writePos = 0;
                _exposedHandle = true;

                return _handle;
            }
        }

        /// <summary>
        /// ��������ֵ����Ϊ���ĳ���
        /// </summary>
        /// <param name="value"></param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.EndContractBlock();

            if (_handle.IsClosed) __Error.FileNotOpen();
            if (!CanSeek) __Error.SeekNotSupported();
            if (!CanWrite) __Error.WriteNotSupported();

            // Handle buffering updates.
            // ���������
            if (_writePos > 0) {
                FlushWrite(false);
            }
            else if (_readPos < _readLen) {
                FlushRead();
            }
            _readPos = 0;
            _readLen = 0;

            if (_appendStart != -1 && value < _appendStart)
                throw new IOException(Environment.GetResourceString("IO.IO_SetLengthAppendTruncate"));
            SetLengthCore(value);
        }

        // We absolutely need this method broken out so that BeginWriteCore can call
        // a method without having to go through buffering code that might call
        // FlushWrite.
        /// <summary>
        /// ���ó���(Core)
        /// </summary>
        /// <param name="value">�µĳ���ֵ</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private void SetLengthCore(long value)
        {
            Contract.Assert(value >= 0, "value >= 0");
            long origPos = _pos;// ����ǰλ������Ϊԭʼλ��

            if (_exposedHandle)
                VerifyOSHandlePosition();
            if (_pos != value)
                SeekCore(value, SeekOrigin.Begin);
            if (!Win32Native.SetEndOfFile(_handle)) {
                int hr = Marshal.GetLastWin32Error();
                if (hr==__Error.ERROR_INVALID_PARAMETER)
                    throw new ArgumentOutOfRangeException("value", Environment.GetResourceString("ArgumentOutOfRange_FileLengthTooBig"));
                __Error.WinIOError(hr, String.Empty);
            }
            // �����ļ�ָ�������ó���֮ǰ
            if (origPos != value) {
                if (origPos < value) // ���ԭʼλ��С�����ڵĳ���ֵ����λ��ԭʼλ��
                    SeekCore(origPos, SeekOrigin.Begin);
                else // ���ԭʼλ�ô������ڵĳ��ȣ���λ����� 
                    SeekCore(0, SeekOrigin.End);
            }
        }

        /// <summary>
        /// �����ж�ȡ�ֽڿ鲢��������д������������С�
        /// </summary>
        /// <param name="array">�˷�������ʱ����ָ�����ֽ����飬������ offset �� (offset + count - 1) ֮���ֵ�ɴӵ�ǰԴ�ж�ȡ���ֽ��滻��</param>
        /// <param name="offset">array �е��ֽ�ƫ���������ڴ˴����ö�ȡ���ֽڡ�</param>
        /// <param name="count">����ȡ���ֽ�����</param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override int Read([In, Out] byte[] array, int offset, int count) {
            if (array==null)
                throw new ArgumentNullException("array", Environment.GetResourceString("ArgumentNull_Buffer"));
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (array.Length - offset < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();
            
            if (_handle.IsClosed) __Error.FileNotOpen();
            
            Contract.Assert((_readPos==0 && _readLen==0 && _writePos >= 0) || (_writePos==0 && _readPos <= _readLen), "We're either reading or writing, but not both.");

            bool isBlocked = false;
            int n = _readLen - _readPos;
            // if the read buffer is empty, read into either user's array or our
            // buffer, depending on number of bytes user asked for and buffer size.
            // ������������ǿյģ������κ��û�����������ǵĻ���������ȡ�����û�Ҫ����ֽ����ͻ�������С��
            if (n == 0) {
                if (!CanRead) __Error.ReadNotSupported();
                if (_writePos > 0) FlushWrite(false);//���д����
                if (!CanSeek || (count >= _bufferSize)) {
                    n = ReadCore(array, offset, count);
                    // ���������塣
                    _readPos = 0;
                    _readLen = 0;
                    return n;
                }
                if (_buffer == null) _buffer = new byte[_bufferSize];
                // 
                n = ReadCore(_buffer, 0, _bufferSize);
                if (n == 0) return 0;
                isBlocked = n < _bufferSize;// �Ƿ�����������
                _readPos = 0;
                _readLen = n;
            }
            // Now copy min of count or numBytesAvailable (ie, near EOF) to array.
            // ��ȡ������ȡ����Ч��ȡ�ֽ�
            if (n > count) n = count;
            // ��_buffer�������鸴�Ƶ�array������
            Buffer.InternalBlockCopy(_buffer, _readPos, array, offset, n);
            _readPos += n;

            // We may have read less than the number of bytes the user asked 
            // for, but that is part of the Stream contract.  Reading again for
            // more data may cause us to block if we're using a device with 
            // no clear end of file, such as a serial port or pipe.  If we
            // blocked here & this code was used with redirected pipes for a
            // process's standard output, this can lead to deadlocks involving
            // two processes. But leave this here for files to avoid what would
            // probably be a breaking change.         -- 
            // ���ǿ����Ѿ��������ֽ������û�Ҫ��ģ�������һ������ͬ��һ���֡�
            // �������ʹ��һ��û����ȷ���ļ��������豸�����紮�ڻ�ܵ�����ȡ��������ݿ��ܻᵼ�����ǵ����ݿ顣
            // ������Ƿ������������ʹ���ض���ܵ�һ�����̵ı�׼�������ᵼ���漰��������������
            // ����������������ļ����Ա��������һ�����Ƶı仯��

            // If we are reading from a device with no clear EOF like a 
            // serial port or a pipe, this will cause us to block incorrectly.
            // ������Ǵ�û����ȷ��EOF���ж˿ڻ�ܵ�װ�ã��⽫ʹ���ǵĿ����
            if (!_isPipe) {
                // If we hit the end of the buffer and didn't have enough bytes, we must
                // read some more from the underlying stream.  However, if we got
                // fewer bytes from the underlying stream than we asked for (ie, we're 
                // probably blocked), don't ask for more bytes.
                // ������ǻ��л������Ľ�����û���㹻���ֽڣ����Ǳ���ӻ����������ж�ȡ��������ݡ�
                // Ȼ����������Ǵӵײ������ֽ���������Ҫ��ģ��������ǿ��ܱ�����������ҪҪ�������ֽڡ�
                if (n < count && !isBlocked) {
                    Contract.Assert(_readPos == _readLen, "Read buffer should be empty!");                
                    int moreBytesRead = ReadCore(array, offset + n, count - n);
                    n += moreBytesRead;
                    // We've just made our buffer inconsistent with our position 
                    // pointer.  We must throw away the read buffer.
                    _readPos = 0;
                    _readLen = 0;
                }
            }

            return n;
        }

        /// <summary>
        /// read(Core)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe int ReadCore(byte[] buffer, int offset, int count) {
            Contract.Assert(!_handle.IsClosed, "!_handle.IsClosed");
            Contract.Assert(CanRead, "CanRead");

            Contract.Assert(buffer != null, "buffer != null");
            Contract.Assert(_writePos == 0, "_writePos == 0");
            Contract.Assert(offset >= 0, "offset is negative");
            Contract.Assert(count >= 0, "count is negative");
#if FEATURE_ASYNC_IO
            if (_isAsync) {// ������첽��ȡ
                IAsyncResult result = BeginReadCore(buffer, offset, count, null, null, 0);
                return EndRead(result);
            }
#endif //FEATURE_ASYNC_IO

            // Make sure we are reading from the right spot
            if (_exposedHandle)
                VerifyOSHandlePosition();

            int hr = 0;
            int r = ReadFileNative(_handle, buffer, offset, count, null, out hr);
            if (r == -1) {
                // For pipes, ERROR_BROKEN_PIPE is the normal end of the pipe.
                if (hr == ERROR_BROKEN_PIPE) {
                    r = 0;
                }
                else {
                    if (hr == ERROR_INVALID_PARAMETER)
                        throw new ArgumentException(Environment.GetResourceString("Arg_HandleNotSync"));
                    
                    __Error.WinIOError(hr, String.Empty);
                }
            }
            Contract.Assert(r >= 0, "FileStream's ReadCore is likely broken.");
            _pos += r;

            return r;
        }

        /// <summary>
        /// �������ĵ�ǰλ������Ϊ����ֵ
        /// </summary>
        /// <param name="offset">�������ԭ�㣬���п�ʼѰ�ҡ�</param>
        /// <param name="origin">ָ���Ŀ�ʼ����������ǰλ�ã�ʹ��һ��SeekOriginֵ��Ϊһ������ֵ</param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override long Seek(long offset, SeekOrigin origin) {
            if (origin<SeekOrigin.Begin || origin>SeekOrigin.End)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidSeekOrigin"));
            Contract.EndContractBlock();
            if (_handle.IsClosed) __Error.FileNotOpen();
            if (!CanSeek) __Error.SeekNotSupported();

            Contract.Assert((_readPos==0 && _readLen==0 && _writePos >= 0) || (_writePos==0 && _readPos <= _readLen), "We're either reading or writing, but not both.");

            // If we've got bytes in our buffer to write, write them out.
            // If we've read in and consumed some bytes, we'll have to adjust
            // our seek positions ONLY IF we're seeking relative to the current
            // position in the stream.  This simulates doing a seek to the new
            // position, then a read for the number of bytes we have in our buffer.
            if (_writePos > 0) {
                FlushWrite(false);
            }
            else if (origin == SeekOrigin.Current) {
                // Don't call FlushRead here, which would have caused an infinite
                // loop.  Simply adjust the seek origin.  This isn't necessary
                // if we're seeking relative to the beginning or end of the stream.
                // ��������FlushRead����ᵼ������ѭ�����򵥵ص���Ѱ��ԭ�㡣����û�б�Ҫ�ģ��������Ѱ��Ŀ�ʼ�����������
                offset -= (_readLen - _readPos);
            }

            // Verify that internal position is in [....] with the handle
            if (_exposedHandle)
                VerifyOSHandlePosition();

            long oldPos = _pos + (_readPos - _readLen);
            long pos = SeekCore(offset, origin);

            // Prevent users from overwriting data in a file that was opened in
            // append mode.
            if (_appendStart != -1 && pos < _appendStart) {
                SeekCore(oldPos, SeekOrigin.Begin);
                throw new IOException(Environment.GetResourceString("IO.IO_SeekAppendOverwrite"));
            }

            // We now must update the read buffer.  We can in some cases simply
            // update _readPos within the buffer, copy around the buffer so our 
            // Position property is still correct, and avoid having to do more 
            // reads from the disk.  Otherwise, discard the buffer's contents.
            if (_readLen > 0) {
                // We can optimize the following condition:
                // oldPos - _readPos <= pos < oldPos + _readLen - _readPos
                if (oldPos == pos) {
                    if (_readPos > 0) {
                        //Console.WriteLine("Seek: seeked for 0, adjusting buffer back by: "+_readPos+"  _readLen: "+_readLen);
                        Buffer.InternalBlockCopy(_buffer, _readPos, _buffer, 0, _readLen - _readPos);
                        _readLen -= _readPos;
                        _readPos = 0;
                    }
                    // If we still have buffered data, we must update the stream's 
                    // position so our Position property is correct.
                    if (_readLen > 0)
                        SeekCore(_readLen, SeekOrigin.Current);
                }
                else if (oldPos - _readPos < pos && pos < oldPos + _readLen - _readPos) {
                    int diff = (int)(pos - oldPos);
                    //Console.WriteLine("Seek: diff was "+diff+", readpos was "+_readPos+"  adjusting buffer - shrinking by "+ (_readPos + diff));
                    Buffer.InternalBlockCopy(_buffer, _readPos+diff, _buffer, 0, _readLen - (_readPos + diff));
                    _readLen -= (_readPos + diff);
                    _readPos = 0;
                    if (_readLen > 0)
                        SeekCore(_readLen, SeekOrigin.Current);
                }
                else {
                    // Lose the read buffer.
                    _readPos = 0;
                    _readLen = 0;
                }
                Contract.Assert(_readLen >= 0 && _readPos <= _readLen, "_readLen should be nonnegative, and _readPos should be less than or equal _readLen");
                Contract.Assert(pos == Position, "Seek optimization: pos != Position!  Buffer math was mangled.");
            }
            return pos;
        }

        // This doesn't do argument checking.  Necessary for SetLength, which must
        // set the file pointer beyond the end of the file. This will update the 
        // internal position
        // �ⲻ��������顣�б�Ҫ���û������ĳ��ȣ����������ļ�ָ�����ļ���ĩβ���⽫�����ڲ�λ��
        /// <summary>
        /// seek����
        /// </summary>
        /// <param name="offset">�������ԭ�㣬���п�ʼѰ�ҡ�</param>
        /// <param name="origin">ָ���Ŀ�ʼ����������ǰλ�òο�seekorigin�͡�</param>
        /// <returns>���ص�ǰ��λ��</returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private long SeekCore(long offset, SeekOrigin origin) {
            Contract.Assert(!_handle.IsClosed && CanSeek, "!_handle.IsClosed && CanSeek");
            Contract.Assert(origin>=SeekOrigin.Begin && origin<=SeekOrigin.End, "origin>=SeekOrigin.Begin && origin<=SeekOrigin.End");
            int hr = 0;
            long ret = 0;
            
            ret = Win32Native.SetFilePointer(_handle, offset, origin, out hr);
            if (ret == -1) {
                // #errorInvalidHandle
                // If ERROR_INVALID_HANDLE is returned, it doesn't suffice to set 
                // the handle as invalid; the handle must also be closed.
                // 
                // Marking the handle as invalid but not closing the handle
                // resulted in exceptions during finalization and locked column 
                // values (due to invalid but unclosed handle) in SQL FileStream 
                // scenarios.
                // 
                // A more mainstream scenario involves accessing a file on a 
                // network share. ERROR_INVALID_HANDLE may occur because the network 
                // connection was dropped and the server closed the handle. However, 
                // the client side handle is still open and even valid for certain 
                // operations.
                //
                // Note that Dispose doesn't throw so we don't need to special case. 
                // SetHandleAsInvalid only sets _closed field to true (without 
                // actually closing handle) so we don't need to call that as well.
                if (hr == Win32Native.ERROR_INVALID_HANDLE)
                    _handle.Dispose();
                __Error.WinIOError(hr, String.Empty);
            }
            
            _pos = ret;
            return ret;
        }

        // Checks the position of the OS's handle equals what we expect it to.
        // This will fail if someone else moved the FileStream's handle or if
        // we've hit a bug in FileStream's position updating code.
        /// <summary>
        /// ������ϵͳ���ֱ�λ�õ���������������
        /// �⽫�����ƶ�FileStream���ֱ������������FileStream��λ�ø��´������ʧ�ܡ�
        /// </summary>
        private void VerifyOSHandlePosition()
        {
            if (!CanSeek)
                return;

            // SeekCore will override the current _pos, so save it now
            // SeekCore�����ص�ǰ_pos,�����������Ҫ������
            long oldPos = _pos;
            long curPos = SeekCore(0, SeekOrigin.Current);
            
            if (curPos != oldPos) {
                // For reads, this is non-fatal but we still could have returned corrupted 
                // data in some cases. So discard the internal buffer. Potential MDA 
                _readPos = 0;  
                _readLen = 0;
                if(_writePos > 0) {
                    // ���������������û�֪��
                    _writePos = 0;
                    throw new IOException(Environment.GetResourceString("IO.IO_FileStreamHandlePosition"));
                }
            }
        }

        /// <summary>
        /// ���ֽڿ�д���ļ�����
        /// </summary>
        /// <param name="array">����Ҫд����������ݵĻ�������</param>
        /// <param name="offset">array �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ�������</param>
        /// <param name="count">���д����ֽ�����</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void Write(byte[] array, int offset, int count) {
            if (array==null)
                throw new ArgumentNullException("array", Environment.GetResourceString("ArgumentNull_Buffer"));
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (array.Length - offset < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            if (_handle.IsClosed) __Error.FileNotOpen();

            if (_writePos == 0)
            {
                // ȷ�����ǿ���д��������׼���õĻ�������
                if (!CanWrite) __Error.WriteNotSupported();
                if (_readPos < _readLen) FlushRead();
                _readPos = 0;
                _readLen = 0;
            }

            // If our buffer has data in it, copy data from the user's array into
            // the buffer, and if we can fit it all there, return.  Otherwise, write
            // the buffer to disk and copy any remaining data into our buffer.
            // The assumption here is memcpy is cheaper than disk (or net) IO.
            // (10 milliseconds to disk vs. ~20-30 microseconds for a 4K memcpy)
            // So the extra copying will reduce the total number of writes, in 
            // non-pathological cases (ie, write 1 byte, then write for the buffer 
            // size repeatedly)
            // ������ǵĻ����������ݣ����û������鸴�Ƶ��������У�������ǿ��Խ���Żأ����ء�
            // ����д������̺͸����κ�ʣ������ݵ�������������ļ�����memcpy�Ǳ��˱ȴ���IO��������
            //��10���������~ 20-30΢��4K memcpy����˶��⿽������������д�룬�ڷǲ�������£���д1���ֽڣ�Ȼ�󷴸�д��������С��
            if (_writePos > 0) {
                int numBytes = _bufferSize - _writePos;   // �������еĿո�
                if (numBytes > 0) {
                    if (numBytes > count)
                        numBytes = count;
                    Buffer.InternalBlockCopy(array, offset, _buffer, _writePos, numBytes);// ��ֵarray�еĿո񣬲�д��ո�
                    _writePos += numBytes;
                    if (count==numBytes) return;// ���ʣ��ȫ�ǿո��򲻽��и���
                    // Ϊ�ڶ���д����׼��
                    offset += numBytes;
                    count -= numBytes;
                }
                // Reset our buffer.  We essentially want to call FlushWrite
                // without calling Flush on the underlying Stream.
                // �������ǵĻ����������ǻ������ǲ�����flushwriteˢ�»�������
#if FEATURE_ASYNC_IO

                if (_isAsync) {
                    IAsyncResult result = BeginWriteCore(_buffer, 0, _writePos, null, null);
                    EndWrite(result);
                }
                else
                {
                    WriteCore(_buffer, 0, _writePos);
                }
#else
                WriteCore(_buffer, 0, _writePos);
#endif // FEATURE_ASYNC_IO
                _writePos = 0;
            }
            // �������������д����ȫ���⻺�塣
            if (count >= _bufferSize) {
                Contract.Assert(_writePos == 0, "FileStream cannot have buffered data to write here!  Your stream will be corrupted.");
                WriteCore(array, offset, count);
                return;
            }
            else if (count == 0)
                return;  // Don't allocate a buffer then call memcpy for 0 bytes.
            if (_buffer==null) _buffer = new byte[_bufferSize];
            // ����ʣ����ֽڵ������������Ժ��ʱ����д��
            Buffer.InternalBlockCopy(array, offset, _buffer, _writePos, count);
            _writePos = count;
            return;
        }

        /// <summary>
        /// д��(Core)
        /// </summary>
        /// <param name="buffer">����Ҫд����������ݵĻ�������</param>
        /// <param name="offset">array �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ�������</param>
        /// <param name="count">���д����ֽ�����</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe void WriteCore(byte[] buffer, int offset, int count) {
            Contract.Assert(!_handle.IsClosed, "!_handle.IsClosed");
            Contract.Assert(CanWrite, "CanWrite");

            Contract.Assert(buffer != null, "buffer != null");
            Contract.Assert(_readPos == _readLen, "_readPos == _readLen");
            Contract.Assert(offset >= 0, "offset is negative");
            Contract.Assert(count >= 0, "count is negative");
#if FEATURE_ASYNC_IO
            if (_isAsync) { 
                IAsyncResult result = BeginWriteCore(buffer, offset, count, null, null);
                EndWrite(result);
                return;
            }
#endif //FEATURE_ASYNC_IO

            // Make sure we are writing to the position that we think we are
            if (_exposedHandle)
                VerifyOSHandlePosition();
            
            int hr = 0;
            int r = WriteFileNative(_handle, buffer, offset, count, null, out hr);// ���ñ���д���ļ�
            if (r == -1) {
                // For pipes, ERROR_NO_DATA is not an error, but the pipe is closing.
                if (hr == ERROR_NO_DATA) {
                    r = 0;
                }
                else {
                    // ERROR_INVALID_PARAMETER may be returned for writes
                    // where the position is too large (ie, writing at Int64.MaxValue 
                    // on Win9x) OR for synchronous writes to a handle opened 
                    // asynchronously.
                    if (hr == ERROR_INVALID_PARAMETER)
                        throw new IOException(Environment.GetResourceString("IO.IO_FileTooLongOrHandleNotSync"));
                    __Error.WinIOError(hr, String.Empty);
                }
            }
            Contract.Assert(r >= 0, "FileStream's WriteCore is likely broken.");
            _pos += r;
            return;
        }

        /// <summary>
        /// ��ʼ��ȡ
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset"></param>
        /// <param name="numBytes"></param>
        /// <param name="userCallback"></param>
        /// <param name="stateObject"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [HostProtection(ExternalThreading = true)]
        public override IAsyncResult BeginRead(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            if (array==null)
                throw new ArgumentNullException("array");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (numBytes < 0)
                throw new ArgumentOutOfRangeException("numBytes", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (array.Length - offset < numBytes)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            if (_handle.IsClosed) __Error.FileNotOpen();

#if FEATURE_ASYNC_IO
            if (!_isAsync)// ��������첽��ȡ����ִ�л����BeginRead
                return base.BeginRead(array, offset, numBytes, userCallback, stateObject);
            else
                return BeginReadAsync(array, offset, numBytes, userCallback, stateObject);
#else
            return base.BeginRead(array, offset, numBytes, userCallback, stateObject);
#endif // FEATURE_ASYNC_IO
        }

#if FEATURE_ASYNC_IO
        /// <summary>
        /// ��ʼ�첽��ȡ
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset"></param>
        /// <param name="numBytes"></param>
        /// <param name="userCallback"></param>
        /// <param name="stateObject"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [HostProtection(ExternalThreading = true)]
        private FileStreamAsyncResult BeginReadAsync(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            Contract.Assert(_isAsync);

            if (!CanRead) __Error.ReadNotSupported();

            Contract.Assert((_readPos == 0 && _readLen == 0 && _writePos >= 0) || (_writePos == 0 && _readPos <= _readLen), "We're either reading or writing, but not both.");

            if (_isPipe)// ���ڹܵ��Ĵ���
            {
                // Pipes are ----ed up, at least when you have 2 different pipes
                // that you want to use simultaneously.  When redirecting stdout
                // & stderr with the Process class, it's easy to deadlock your
                // parent & child processes when doing writes 4K at a time.  The
                // OS appears to use a 4K buffer internally.  If you write to a
                // pipe that is full, you will block until someone read from 
                // that pipe.  If you try reading from an empty pipe and 
                // FileStream's BeginRead blocks waiting for data to fill it's 
                // internal buffer, you will be blocked.  In a case where a child
                // process writes to stdout & stderr while a parent process tries
                // reading from both, you can easily get into a deadlock here.
                // To avoid this deadlock, don't buffer when doing async IO on
                // pipes.  But don't completely ignore buffered data either.  
                if (_readPos < _readLen)
                {
                    int n = _readLen - _readPos;
                    if (n > numBytes) n = numBytes;
                    Buffer.InternalBlockCopy(_buffer, _readPos, array, offset, n);
                    _readPos += n;

                    // Return a synchronous FileStreamAsyncResult
                    return FileStreamAsyncResult.CreateBufferedReadResult(n, userCallback, stateObject, false);
                }
                else
                {
                    Contract.Assert(_writePos == 0, "FileStream must not have buffered write data here!  Pipes should be unidirectional.");
                    return BeginReadCore(array, offset, numBytes, userCallback, stateObject, 0);
                }
            }

            Contract.Assert(!_isPipe, "Should not be a pipe.");

            // ������
            if (_writePos > 0) FlushWrite(false);
            if (_readPos == _readLen)
            {
                // I can't see how to handle buffering of async requests when 
                // filling the buffer asynchronously, without a lot of complexity.
                // The problems I see are issuing an async read, we do an async 
                // read to fill the buffer, then someone issues another read 
                // (either synchronously or asynchronously) before the first one 
                // returns.  This would involve some sort of complex buffer locking
                // that we probably don't want to get into, at least not in V1.
                // If we did a [....] read to fill the buffer, we could avoid the
                // problem, and any async read less than 64K gets turned into a
                // synchronous read by NT anyways...       -- 

                if (numBytes < _bufferSize)// ����ֽ�����С�ڻ�������
                {
                    if (_buffer == null) _buffer = new byte[_bufferSize];
                    IAsyncResult bufferRead = BeginReadCore(_buffer, 0, _bufferSize, null, null, 0);
                    _readLen = EndRead(bufferRead);
                    // ��ȡ��ȡ�ֽڳ���
                    int n = _readLen;
                    if (n > numBytes) n = numBytes;
                    // ��ȡ�������ʵ����
                    Buffer.InternalBlockCopy(_buffer, 0, array, offset, n);
                    _readPos = n;

                    // ����ͬ��FileStreamAsyncResult
                    return FileStreamAsyncResult.CreateBufferedReadResult(n, userCallback, stateObject, false);
                }
                else
                {
                    // Here we're making our position pointer inconsistent
                    // with our read buffer.  Throw away the read buffer's contents.
                    _readPos = 0;
                    _readLen = 0;
                    return BeginReadCore(array, offset, numBytes, userCallback, stateObject, 0);
                }
            }
            else
            {// ����
                int n = _readLen - _readPos;
                if (n > numBytes) n = numBytes;
                Buffer.InternalBlockCopy(_buffer, _readPos, array, offset, n);
                _readPos += n;

                if (n >= numBytes)
                {
                    // Return a synchronous FileStreamAsyncResult
                    return FileStreamAsyncResult.CreateBufferedReadResult(n, userCallback, stateObject, false);
                }
                else
                {
                    // For streams with no clear EOF like serial ports or pipes
                    // we cannot read more data without causing an app to block
                    // incorrectly.  Pipes don't go down this path 
                    // though.  This code needs to be fixed.
                    // Throw away read buffer.
                    _readPos = 0;
                    _readLen = 0;
                    return BeginReadCore(array, offset + n, numBytes - n, userCallback, stateObject, n);
                }
                // WARNING: all state on asyncResult objects must be set before
                // we call ReadFile in BeginReadCore, since the OS can run our
                // callback & the user's callback before ReadFile returns.
            }
        }
#endif // FEATURE_ASYNC_IO

#if FEATURE_ASYNC_IO
        /// <summary>
        /// ��ʼ�첽��ȡ
        /// </summary>
        /// <param name="bytes">�˷�������ʱ����ָ�����ֽ����飬������ offset �� (offset + count - 1) ֮���ֵ�ɴӵ�ǰԴ�ж�ȡ���ֽ��滻��</param>
        /// <param name="offset">array �е��ֽ�ƫ���������ڴ˴����ö�ȡ���ֽڡ�</param>
        /// <param name="numBytes">����ȡ���ֽ�����</param>
        /// <param name="userCallback">�û��ص�</param>
        /// <param name="stateObject">״̬����</param>
        /// <param name="numBufferedBytesRead"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.AppDomain, ResourceScope.AppDomain)]
        unsafe private FileStreamAsyncResult BeginReadCore(byte[] bytes, int offset, int numBytes, AsyncCallback userCallback, Object stateObject, int numBufferedBytesRead)
        {
            Contract.Assert(!_handle.IsClosed, "!_handle.IsClosed");
            Contract.Assert(CanRead, "CanRead");
            Contract.Assert(bytes != null, "bytes != null");
            Contract.Assert(_writePos == 0, "_writePos == 0");
            Contract.Assert(_isAsync, "BeginReadCore doesn't work on synchronous file streams!");
            Contract.Assert(offset >= 0, "offset is negative");
            Contract.Assert(numBytes >= 0, "numBytes is negative");

            // Create and store async stream class library specific data in the async result
            // �����ʹ洢���첽�����ľ����������첽���

            // Must pass in _numBufferedBytes here to ensure all the state on the IAsyncResult 
            // object is set before we call ReadFile, which gives the OS an
            // opportunity to run our callback (including the user callback &
            // the call to EndRead) before ReadFile has returned.   
            FileStreamAsyncResult asyncResult = new FileStreamAsyncResult(numBufferedBytesRead, bytes, _handle, userCallback, stateObject, false);
            NativeOverlapped* intOverlapped = asyncResult.OverLapped;

            // Calculate position in the file we should be at after the read is done
            // �ڶ�ȡ�ļ�������Ӧ�ü������ļ��е�λ��
            if (CanSeek) {
                long len = Length;
                
                // Make sure we are reading from the position that we think we are
                if (_exposedHandle)
                    VerifyOSHandlePosition();
                
                if (_pos + numBytes > len) {// ���Ҫ��ȡ��ĩβ�����ļ����ȣ���Ҫ���¼���
                    if (_pos <= len)
                        numBytes = (int) (len - _pos);
                    else
                        numBytes = 0;
                }

                // Now set the position to read from in the NativeOverlapped struct
                // For pipes, we should leave the offset fields set to 0.
                intOverlapped->OffsetLow = unchecked((int)_pos);
                intOverlapped->OffsetHigh = (int)(_pos>>32);

                // When using overlapped IO, the OS is not supposed to 
                // touch the file pointer location at all.  We will adjust it 
                // ourselves. This isn't threadsafe.

                // WriteFile should not update the file pointer when writing
                // in overlapped mode, according to MSDN.  But it does update 
                // the file pointer when writing to a UNC path!   
                // So changed the code below to seek to an absolute 
                // location, not a relative one.  ReadFile seems consistent though.
                SeekCore(numBytes, SeekOrigin.Current);// ��λ�����������ĩβ
            }

            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled(EventLevel.Informational, FrameworkEventSource.Keywords.ThreadTransfer))
                FrameworkEventSource.Log.ThreadTransferSend((long)(asyncResult.OverLapped), 2, string.Empty, false);

            // queue an async ReadFile operation and pass in a packed overlapped
            // �����첽ReadFile������һ�������overlapped
            int hr = 0;
            int r = ReadFileNative(_handle, bytes, offset, numBytes, intOverlapped, out hr);
            // ReadFile, the OS version, will return 0 on failure.  But
            // my ReadFileNative wrapper returns -1.  My wrapper will return
            // the following:
            // On error, r==-1.
            // On async requests that are still pending, r==-1 w/ hr==ERROR_IO_PENDING
            // on async requests that completed sequentially, r==0
            // You will NEVER RELIABLY be able to get the number of bytes
            // read back from this call when using overlapped structures!  You must
            // not pass in a non-null lpNumBytesRead to ReadFile when using 
            // overlapped structures!  This is by design NT behavior.
            if (r==-1 && numBytes!=-1) {// �Դ������ش���
                
                // For pipes, when they hit EOF, they will come here.
                if (hr == ERROR_BROKEN_PIPE) {
                    // Not an error, but EOF.  AsyncFSCallback will NOT be 
                    // called.  Call the user callback here.

                    // We clear the overlapped status bit for this special case.
                    // Failure to do so looks like we are freeing a pending overlapped later.
                    intOverlapped->InternalLow = IntPtr.Zero;
                    asyncResult.CallUserCallback();                 
                    // EndRead����ȷ�ͷ�OverLapped�ṹ��
                }
                else if (hr != ERROR_IO_PENDING) {
                    if (!_handle.IsClosed && CanSeek)  // Update Position - It could be anywhere.
                        SeekCore(0, SeekOrigin.Current);

                    if (hr == ERROR_HANDLE_EOF)
                        __Error.EndOfFile();
                    else
                        __Error.WinIOError(hr, String.Empty);
                }
            }
            else {
                // Due to a workaround for a race condition in NT's ReadFile & 
                // WriteFile routines, we will always be returning 0 from ReadFileNative
                // when we do async IO instead of the number of bytes read, 
                // irregardless of whether the operation completed 
                // synchronously or asynchronously.  We absolutely must not
                // set asyncResult._numBytes here, since will never have correct
                // results.  
                //Console.WriteLine("ReadFile returned: "+r+" (0x"+Int32.Format(r, "x")+")  The IO completed synchronously, but the user callback was called on a separate thread");
            }

            return asyncResult;
        }
#endif //FEATURE_ASYNC_IO
        /// <summary>
        /// �ȴ�������첽��������ɡ�
        /// </summary>
        /// <param name="asyncResult"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // Although the unsafe code is only required in PAL, the block is wide scoped. Leave it here for desktop to ensure it's reviewed.
        public unsafe override int EndRead(IAsyncResult asyncResult)
        {
            // There are 3 significantly different IAsyncResults we'll accept
            // here.  One is from Stream::BeginRead.  The other two are variations
            // on our FileStreamAsyncResult.  One is from BeginReadCore,
            // while the other is from the BeginRead buffering wrapper.
            if (asyncResult==null)
                throw new ArgumentNullException("asyncResult");
            Contract.EndContractBlock();

#if FEATURE_ASYNC_IO
            if (!_isAsync)// ��������첽�����������ػ����EndRead
                return base.EndRead(asyncResult);

            FileStreamAsyncResult afsar = asyncResult as FileStreamAsyncResult;
            if (afsar==null || afsar.IsWrite)
                __Error.WrongAsyncResult();

            // Ensure we can't get into any ----s by doing an interlocked
            // CompareExchange here.  Avoids corrupting memory via freeing the
            // NativeOverlapped class or GCHandle twice.  -- 
            if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                __Error.EndReadCalledTwice();

            // Obtain the WaitHandle, but don't use public property in case we
            // delay initialize the manual reset event in the future.
            // ���WaitHandle������Ҫʹ�ù����Ʋ�������£����ǳٳ�δ����ʼ���ֶ������¼���
            afsar.Wait();

            // Free memory & GC handles.
            afsar.ReleaseNativeResource();

            // Now check for any error during the read.
            if (afsar.ErrorCode != 0)
                __Error.WinIOError(afsar.ErrorCode, String.Empty);

            return afsar.NumBytesRead;
#else
            return base.EndRead(asyncResult);
#endif // FEATURE_ASYNC_IO
        }

        // Reads a byte from the file stream.  Returns the byte cast to an int
        // or -1 if reading from the end of the stream.
        /// <summary>
        /// ��ȡһ���ֽ�
        /// </summary>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override int ReadByte() {
            if (_handle.IsClosed) __Error.FileNotOpen();
            if (_readLen==0 && !CanRead) __Error.ReadNotSupported();
            Contract.Assert((_readPos==0 && _readLen==0 && _writePos >= 0) || (_writePos==0 && _readPos <= _readLen), "We're either reading or writing, but not both.");
            if (_readPos == _readLen) {
                if (_writePos > 0) FlushWrite(false);
                Contract.Assert(_bufferSize > 0, "_bufferSize > 0");
                if (_buffer == null) _buffer = new byte[_bufferSize];
                _readLen = ReadCore(_buffer, 0, _bufferSize);// ��ȡ�ļ����ݵ�������
                _readPos = 0;
            }
            if (_readPos == _readLen)// ��ʾ��ȡ���ļ�ĩβ
                return -1;

            int result = _buffer[_readPos];
            _readPos++;
            return result;
        }

        /// <summary>
        /// ��ʼ�첽д��
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset"></param>
        /// <param name="numBytes"></param>
        /// <param name="userCallback"></param>
        /// <param name="stateObject"></param>
        /// <returns></returns>
        [System.Security.SecuritySafeCritical]  // auto-generated
        [HostProtection(ExternalThreading=true)]
        public override IAsyncResult BeginWrite(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            if (array==null)
                throw new ArgumentNullException("array");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (numBytes < 0)
                throw new ArgumentOutOfRangeException("numBytes", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (array.Length - offset < numBytes)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            if (_handle.IsClosed) __Error.FileNotOpen();

#if FEATURE_ASYNC_IO
            if (!_isAsync)
                return base.BeginWrite(array, offset, numBytes, userCallback, stateObject);
            else
                return BeginWriteAsync(array, offset, numBytes, userCallback, stateObject);
#else
            return base.BeginWrite(array, offset, numBytes, userCallback, stateObject);
#endif // FEATURE_ASYNC_IO
        }

#if FEATURE_ASYNC_IO
        [System.Security.SecuritySafeCritical]  // auto-generated
        [HostProtection(ExternalThreading = true)]
        private FileStreamAsyncResult BeginWriteAsync(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            Contract.Assert(_isAsync);

            if (!CanWrite) __Error.WriteNotSupported();

            Contract.Assert((_readPos == 0 && _readLen == 0 && _writePos >= 0) || (_writePos == 0 && _readPos <= _readLen), "We're either reading or writing, but not both.");

            if (_isPipe)
            {
                // Pipes are ----ed up, at least when you have 2 different pipes
                // that you want to use simultaneously.  When redirecting stdout
                // & stderr with the Process class, it's easy to deadlock your
                // parent & child processes when doing writes 4K at a time.  The
                // OS appears to use a 4K buffer internally.  If you write to a
                // pipe that is full, you will block until someone read from 
                // that pipe.  If you try reading from an empty pipe and 
                // FileStream's BeginRead blocks waiting for data to fill it's 
                // internal buffer, you will be blocked.  In a case where a child
                // process writes to stdout & stderr while a parent process tries
                // reading from both, you can easily get into a deadlock here.
                // To avoid this deadlock, don't buffer when doing async IO on
                // pipes.   
                Contract.Assert(_readPos == 0 && _readLen == 0, "FileStream must not have buffered data here!  Pipes should be unidirectional.");

                if (_writePos > 0)
                    FlushWrite(false);

                return BeginWriteCore(array, offset, numBytes, userCallback, stateObject);
            }

            // Handle buffering.
            if (_writePos == 0)
            {
                if (_readPos < _readLen) FlushRead();
                _readPos = 0;
                _readLen = 0;
            }

            int n = _bufferSize - _writePos;
            if (numBytes <= n)// ���д���ֽ�С�ڻ�����ʣ���ֽ�
            {
                if (_writePos == 0) _buffer = new byte[_bufferSize];
                Buffer.InternalBlockCopy(array, offset, _buffer, _writePos, numBytes);
                _writePos += numBytes;// �����µĻ�����д��ָ��

                // Return a synchronous FileStreamAsyncResult
                return FileStreamAsyncResult.CreateBufferedReadResult(numBytes, userCallback, stateObject, true);
            }

            if (_writePos > 0)// ��������ʣ����ֽ�д�뵽����
                FlushWrite(false);

            return BeginWriteCore(array, offset, numBytes, userCallback, stateObject);
        }
#endif // FEATURE_ASYNC_IO

#if FEATURE_ASYNC_IO
        /// <summary>
        /// ��ʼд��(Core)�첽��
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="numBytes"></param>
        /// <param name="userCallback">�û��ص�</param>
        /// <param name="stateObject">״̬����</param>
        /// <returns></returns>
        [SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.AppDomain, ResourceScope.AppDomain)]
        unsafe private FileStreamAsyncResult BeginWriteCore(byte[] bytes, int offset, int numBytes, AsyncCallback userCallback, Object stateObject) 
        {
            Contract.Assert(!_handle.IsClosed, "!_handle.IsClosed");
            Contract.Assert(CanWrite, "CanWrite");
            Contract.Assert(bytes != null, "bytes != null");
            Contract.Assert(_readPos == _readLen, "_readPos == _readLen");
            Contract.Assert(_isAsync, "BeginWriteCore doesn't work on synchronous file streams!");
            Contract.Assert(offset >= 0, "offset is negative");
            Contract.Assert(numBytes >= 0, "numBytes is negative");

            // �����ʹ洢���첽�����ľ����������첽���
            FileStreamAsyncResult asyncResult = new FileStreamAsyncResult(0, bytes, _handle, userCallback, stateObject, true);
            NativeOverlapped* intOverlapped = asyncResult.OverLapped;

            if (CanSeek)
            {// ����FileStreamAsyncResult
                // Make sure we set the length of the file appropriately.
                // ȷ�������ʵ��������ļ��ĳ��ȡ�
                long len = Length;
                //Console.WriteLine("BeginWrite - Calculating end pos.  pos: "+pos+"  len: "+len+"  numBytes: "+numBytes);

                // Make sure we are writing to the position that we think we are
                // ������д��������Ϊ���ǵ�λ��
                if (_exposedHandle)
                    VerifyOSHandlePosition();
                
                //�����ļ����ȣ���ֹ��д�볤�Ȳ���
                if (_pos + numBytes > len) {
                    //Console.WriteLine("BeginWrite - Setting length to: "+(pos + numBytes));
                    SetLengthCore(_pos + numBytes);
                }

                // Now set the position to read from in the NativeOverlapped struct
                // For pipes, we should leave the offset fields set to 0.
                intOverlapped->OffsetLow = (int)_pos;
                intOverlapped->OffsetHigh = (int)(_pos>>32);

                // When using overlapped IO, the OS is not supposed to 
                // touch the file pointer location at all.  We will adjust it 
                // ourselves.  This isn't threadsafe.
                // ��ʹ���ص����������������ϵͳ�ǲ�Ӧ�ô������ļ�ָ��λ�á�
                // ���ǽ��������Լ����ⲻ���̰߳�ȫ�ġ�

                SeekCore(numBytes, SeekOrigin.Current);
            }

            //Console.WriteLine("BeginWrite finishing.  pos: "+pos+"  numBytes: "+numBytes+"  _pos: "+_pos+"  Position: "+Position);

            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled(EventLevel.Informational, FrameworkEventSource.Keywords.ThreadTransfer))
                FrameworkEventSource.Log.ThreadTransferSend((long)(asyncResult.OverLapped), 2, string.Empty, false);

            int hr = 0;
            // queue an async WriteFile operation and pass in a packed overlapped
            int r = WriteFileNative(_handle, bytes, offset, numBytes, intOverlapped, out hr);

            // WriteFile, the OS version, will return 0 on failure.  But
            // my WriteFileNative wrapper returns -1.  My wrapper will return
            // the following:
            // On error, r==-1.
            // On async requests that are still pending, r==-1 w/ hr==ERROR_IO_PENDING
            // On async requests that completed sequentially, r==0
            // You will NEVER RELIABLY be able to get the number of bytes
            // written back from this call when using overlapped IO!  You must
            // not pass in a non-null lpNumBytesWritten to WriteFile when using 
            // overlapped structures!  This is ByDesign NT behavior.
            if (r==-1 && numBytes!=-1) {
                //Console.WriteLine("WriteFile returned 0;  Write will complete asynchronously (if hr==3e5)  hr: 0x{0:x}", hr);
                
                // For pipes, when they are closed on the other side, they will come here.
                if (hr == ERROR_NO_DATA) {
                    // Not an error, but EOF.  AsyncFSCallback will NOT be 
                    // called.  Call the user callback here.
                    asyncResult.CallUserCallback();
                    // EndWrite will free the Overlapped struct correctly.
                }
                else if (hr != ERROR_IO_PENDING) {
                    if (!_handle.IsClosed && CanSeek)  // Update Position - It could be anywhere.
                        SeekCore(0, SeekOrigin.Current);

                    if (hr == ERROR_HANDLE_EOF)
                        __Error.EndOfFile();
                    else
                        __Error.WinIOError(hr, String.Empty);
                }
            }
            else {
                // Due to a workaround for a race condition in NT's ReadFile & 
                // WriteFile routines, we will always be returning 0 from WriteFileNative
                // when we do async IO instead of the number of bytes written, 
                // irregardless of whether the operation completed 
                // synchronously or asynchronously.  We absolutely must not
                // set asyncResult._numBytes here, since will never have correct
                // results.  
                //Console.WriteLine("WriteFile returned: "+r+" (0x"+Int32.Format(r, "x")+")  The IO completed synchronously, but the user callback was called on another thread.");
            }
            
            return asyncResult;
        }
#endif //FEATURE_ASYNC_IO
        /// <summary>
        /// �����첽д��
        /// </summary>
        /// <param name="asyncResult"></param>
        [System.Security.SecuritySafeCritical]  // Although the unsafe code is only required in PAL, the block is wide scoped. Leave it here for desktop to ensure it's reviewed.
        public unsafe override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult==null)
                throw new ArgumentNullException("asyncResult");
            Contract.EndContractBlock();

#if FEATURE_ASYNC_IO
            if (!_isAsync) {
                base.EndWrite(asyncResult);
                return;
            }

            FileStreamAsyncResult afsar = asyncResult as FileStreamAsyncResult;
            if (afsar==null || !afsar.IsWrite)
                __Error.WrongAsyncResult();

            // Ensure we can't get into any ----s by doing an interlocked
            // CompareExchange here.  Avoids corrupting memory via freeing the
            // NativeOverlapped class or GCHandle twice.  -- 
            if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                __Error.EndWriteCalledTwice();

            // Obtain the WaitHandle, but don't use public property in case we
            // delay initialize the manual reset event in the future.
            afsar.Wait();

            // Free memory & GC handles.
            afsar.ReleaseNativeResource();

            // Now check for any error during the write.
            if (afsar.ErrorCode != 0)
                __Error.WinIOError(afsar.ErrorCode, String.Empty);

            // Number of bytes written is afsar._numBytes + afsar._numBufferedBytes.
            return;
#else
            base.EndWrite(asyncResult);
#endif // FEATURE_ASYNC_IO
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void WriteByte(byte value)
        {
            if (_handle.IsClosed) __Error.FileNotOpen();
            if (_writePos==0) {
                if (!CanWrite) __Error.WriteNotSupported();
                if (_readPos < _readLen) FlushRead();
                _readPos = 0;
                _readLen = 0;
                Contract.Assert(_bufferSize > 0, "_bufferSize > 0");
                if (_buffer==null) _buffer = new byte[_bufferSize];
            }
            if (_writePos == _bufferSize)// ����Ѿ���ȡ����������β�����򽫻���������д�뵽stream
                FlushWrite(false);

            _buffer[_writePos] = value;
            _writePos++;
        }

        /// <summary>
        /// ��ֹ�������̶�ȡ��д�� FileStream��
        /// </summary>
        /// <param name="position">Ҫ�����ķ�Χ����ʼ�����˲�����ֵ������ڻ������ (0)��</param>
        /// <param name="length">Ҫ�����ķ�Χ��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public virtual void Lock(long position, long length) {
            if (position < 0 || length < 0)
                throw new ArgumentOutOfRangeException((position < 0 ? "position" : "length"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.EndContractBlock();
            if (_handle.IsClosed) __Error.FileNotOpen();

            int positionLow     = unchecked((int)(position      ));
            int positionHigh    = unchecked((int)(position >> 32));
            int lengthLow       = unchecked((int)(length        ));
            int lengthHigh      = unchecked((int)(length   >> 32));
            
            if (!Win32Native.LockFile(_handle, positionLow, positionHigh, lengthLow, lengthHigh))
                __Error.WinIOError();
        }

        /// <summary>
        /// �����������̷�����ǰ������ĳ���ļ���ȫ���򲿷֡�
        /// </summary>
        /// <param name="position">Ҫȡ�������ķ�Χ�Ŀ�ʼ����</param>
        /// <param name="length">Ҫȡ�������ķ�Χ��</param>
        [System.Security.SecuritySafeCritical]  // auto-generated
        public virtual void Unlock(long position, long length) {
            if (position < 0 || length < 0)
                throw new ArgumentOutOfRangeException((position < 0 ? "position" : "length"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.EndContractBlock();
            if (_handle.IsClosed) __Error.FileNotOpen();

            int positionLow     = unchecked((int)(position      ));
            int positionHigh    = unchecked((int)(position >> 32));
            int lengthLow       = unchecked((int)(length        ));
            int lengthHigh      = unchecked((int)(length   >> 32));

            if (!Win32Native.UnlockFile(_handle, positionLow, positionHigh, lengthLow, lengthHigh))
                __Error.WinIOError();
        }

        // Windows API definitions, from winbase.h and others
        
        private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const int FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const int FILE_FLAG_OVERLAPPED = 0x40000000;
        internal const int GENERIC_READ = unchecked((int)0x80000000);
        private const int GENERIC_WRITE = 0x40000000;
    
        private const int FILE_BEGIN = 0;
        private const int FILE_CURRENT = 1;
        private const int FILE_END = 2;

        // Error codes (not HRESULTS), from winerror.h
        internal const int ERROR_BROKEN_PIPE = 109;
        internal const int ERROR_NO_DATA = 232;
        private const int ERROR_HANDLE_EOF = 38;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_IO_PENDING = 997;


        // __ConsoleStream also uses this code. 
        /// <summary>
        /// ���ض�ȡ�ļ�
        /// </summary>
        /// <param name="handle">�ļ����</param>
        /// <param name="bytes">��������</param>
        /// <param name="offset">����</param>
        /// <param name="count">��ȡ����</param>
        /// <param name="overlapped"></param>
        /// <param name="hr"></param>
        /// <returns></returns>
        [System.Security.SecurityCritical]  // auto-generated
        private unsafe int ReadFileNative(SafeFileHandle handle, byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr)
        {
            Contract.Requires(handle != null, "handle != null");
            Contract.Requires(offset >= 0, "offset >= 0");
            Contract.Requires(count >= 0, "count >= 0");
            Contract.Requires(bytes != null, "bytes != null");
            // Don't corrupt memory when multiple threads are erroneously writing
            // to this stream simultaneously.
            // �ڶ���߳��б������д�������ʱ��,���ƻ��ڴ档
            if (bytes.Length - offset < count)
                throw new IndexOutOfRangeException(Environment.GetResourceString("IndexOutOfRange_IORaceCondition"));
            Contract.EndContractBlock();

            Contract.Assert((_isAsync && overlapped != null) || (!_isAsync && overlapped == null), "Async IO parameter ----up in call to ReadFileNative.");

            // You can't use the fixed statement on an array of length 0.
            // �㲻����һ������ĳ���Ϊ0�Ĺ̶���䡣
            if (bytes.Length==0) {
                hr = 0;
                return 0;
            }

            int r = 0;
            int numBytesRead = 0;

            fixed(byte* p = bytes) {
                if (_isAsync)
                    r = Win32Native.ReadFile(handle, p + offset, count, IntPtr.Zero, overlapped);
                else
                    r = Win32Native.ReadFile(handle, p + offset, count, out numBytesRead, IntPtr.Zero);
            }

            if (r==0) {// �����ȡʧ�ܣ�������صĴ�����Ϣ��������
                hr = Marshal.GetLastWin32Error();// ��ȡ����Ĵ���code
                // We should never silently ---- an error here without some
                // extra work.  We must make sure that BeginReadCore won't return an 
                // IAsyncResult that will cause EndRead to block, since the OS won't
                // call AsyncFSCallback for us.  
                if (hr == ERROR_BROKEN_PIPE || hr == Win32Native.ERROR_PIPE_NOT_CONNECTED) {
                    // This handle was a pipe, and it's done. Not an error, but EOF.
                    // However, the OS will not call AsyncFSCallback!
                    // Let the caller handle this, since BeginReadCore & ReadCore 
                    // need to do different things.
                    return -1;
                }

                // See code:#errorInvalidHandle in "private long SeekCore(long offset, SeekOrigin origin)".
                if (hr == Win32Native.ERROR_INVALID_HANDLE)
                    _handle.Dispose();

                return -1;
            }
            else
                hr = 0;
            return numBytesRead;
        }

        /// <summary>
        /// ����д�ļ� 
        /// </summary>
        /// <param name="handle">�ļ����</param>
        /// <param name="bytes">��������</param>
        /// <param name="offset">����</param>
        /// <param name="count">��ȡ����</param>
        /// <param name="overlapped"></param>
        /// <param name="hr"></param>
        /// <returns></returns>
        [System.Security.SecurityCritical]  // auto-generated
        private unsafe int WriteFileNative(SafeFileHandle handle, byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr) {
            Contract.Requires(handle != null, "handle != null");
            Contract.Requires(offset >= 0, "offset >= 0");
            Contract.Requires(count >= 0, "count >= 0");
            Contract.Requires(bytes != null, "bytes != null");
            // Don't corrupt memory when multiple threads are erroneously writing
            // to this stream simultaneously.  (the OS is reading from
            // the array we pass to WriteFile, but if we read beyond the end and
            // that memory isn't allocated, we could get an AV.)
            if (bytes.Length - offset < count)
                throw new IndexOutOfRangeException(Environment.GetResourceString("IndexOutOfRange_IORaceCondition"));
            Contract.EndContractBlock();

            Contract.Assert((_isAsync && overlapped != null) || (!_isAsync && overlapped == null), "Async IO parameter ----up in call to WriteFileNative.");

            // You can't use the fixed statement on an array of length 0.
            if (bytes.Length==0) {
                hr = 0;
                return 0;
            }

            int numBytesWritten = 0;
            int r = 0;
            
            // ������ת��Ϊָ�룬��ʼָ��λ��Ϊָ���ƫ����
            fixed(byte* p = bytes) {
                if (_isAsync)
                    r = Win32Native.WriteFile(handle, p + offset, count, IntPtr.Zero, overlapped);
                else
                    r = Win32Native.WriteFile(handle, p + offset, count, out numBytesWritten, IntPtr.Zero);
            }

            if (r==0) {
                hr = Marshal.GetLastWin32Error();
                // We should never silently ---- an error here without some
                // extra work.  We must make sure that BeginWriteCore won't return an 
                // IAsyncResult that will cause EndWrite to block, since the OS won't
                // call AsyncFSCallback for us.  

                if (hr==ERROR_NO_DATA) {
                    // This handle was a pipe, and the pipe is being closed on the 
                    // other side.  Let the caller handle this, since BeginWriteCore 
                    // & WriteCore need to do different things.
                    return -1;
                }
                
                // See code:#errorInvalidHandle in "private long SeekCore(long offset, SeekOrigin origin)".
                if (hr == Win32Native.ERROR_INVALID_HANDLE)
                    _handle.Dispose();

                return -1;
            }
            else
                hr = 0;
            return numBytesWritten;          
        }


#if FEATURE_ASYNC_IO
        /// <summary>
        /// �ӵ�ǰ���첽��ȡ�ֽڵ����У������е�λ��������ȡ���ֽ�����������ȡ������
        /// </summary>
        /// <param name="buffer">����д��Ļ�������</param>
        /// <param name="offset">buffer �е��ֽ�ƫ�������Ӹ�ƫ������ʼд������ж�ȡ�����ݡ�</param>
        /// <param name="count">����ȡ���ֽ�����</param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        [SecuritySafeCritical]
        public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (buffer.Length - offset < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to Read() or BeginRead() which a subclass might have overriden.  
            // To be safe we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into Read/BeginRead) when we are not sure.
            // ��������Ѽ̳е����࣬�����ʵ�ֿ����Ǵ������Ϊ�����е�read()��beginread()�������д��
            // Ϊ�˰�ȫ��������ǽ�ֻʹ�����ʵ�ֵ�����£�����֪���������ǰ�ȫ�ģ���������ࣨ�⽫���ö�BeginRead�������ǲ�ȷ����
            if (this.GetType() != typeof(FileStream))
                return base.ReadAsync(buffer, offset, count, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCancellation<int>(cancellationToken);

            if (_handle.IsClosed)
                __Error.FileNotOpen();

            // If async IO is not supported on this platform or 
            // if this FileStream was not opened with FileOptions.Asynchronous.
            if (!_isAsync)
                return base.ReadAsync(buffer, offset, count, cancellationToken);

            var readTask = new FileStreamReadWriteTask<int>(cancellationToken);// ���ڴ���첽����������Ϣ
            var endReadTask = s_endReadTask;
            if (endReadTask == null) s_endReadTask = endReadTask = EndReadTask; // ��ʼ���첽�������� ----
            readTask._asyncResult = BeginReadAsync(buffer, offset, count, endReadTask, readTask);// ��ʼ����첽����

            if (readTask._asyncResult.IsAsync && cancellationToken.CanBeCanceled)
            {
                var cancelReadHandler = s_cancelReadHandler;
                if (cancelReadHandler == null) s_cancelReadHandler = cancelReadHandler = CancelTask<int>; // benign initialization ----
                readTask._registration = cancellationToken.Register(cancelReadHandler,  readTask);// ע��cancel��������

                // ��������������£�������ȡ���ص�ǰ��ɡ�
                if (readTask._asyncResult.IsCompleted)
                    readTask._registration.Dispose();
            }

            return readTask;
        }

        /// <summary>
        /// ���ֽڵ������첽д�뵱ǰ�����������еĵ�ǰλ����ǰ�ƶ�д����ֽ�����������ȡ������
        /// </summary>
        /// <param name="buffer">����д�����ݵĻ�������</param>
        /// <param name="offset">buffer �еĴ��㿪ʼ���ֽ�ƫ�������Ӵ˴���ʼ���ֽڸ��Ƶ�������</param>
        /// <param name="count">���д����ֽ�����</param>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        [SecuritySafeCritical]
        public override Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (buffer.Length - offset < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to Write() or BeginWrite() which a subclass might have overriden.  
            // To be safe we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into Write/BeginWrite) when we are not sure.
            if (this.GetType() != typeof(FileStream))
                return base.WriteAsync(buffer, offset, count, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCancellation(cancellationToken);

            if (_handle.IsClosed)
                __Error.FileNotOpen();

            // If async IO is not supported on this platform or 
            // if this FileStream was not opened with FileOptions.Asynchronous.
            if (!_isAsync)
                return base.WriteAsync(buffer, offset, count, cancellationToken);

            var writeTask = new FileStreamReadWriteTask<VoidTaskResult>(cancellationToken);
            var endWriteTask = s_endWriteTask;
            if (endWriteTask == null) s_endWriteTask = endWriteTask = EndWriteTask; // benign initialization ----
            writeTask._asyncResult = BeginWriteAsync(buffer, offset, count, endWriteTask, writeTask);

            if (writeTask._asyncResult.IsAsync && cancellationToken.CanBeCanceled)
            {
                var cancelWriteHandler = s_cancelWriteHandler;
                if (cancelWriteHandler == null) s_cancelWriteHandler = cancelWriteHandler = CancelTask<VoidTaskResult>; // benign initialization ----
                writeTask._registration = cancellationToken.Register(cancelWriteHandler, writeTask);

                // In case the task is completed right before we register the cancellation callback.
                if (writeTask._asyncResult.IsCompleted)
                    writeTask._registration.Dispose();
            }

            return writeTask;
        }

        // The task instance returned from ReadAsync and WriteAsync.
        // Also stores all of the state necessary for those calls to avoid closures and extraneous delegate allocations.
        /// <summary>
        /// ��readasync��writeasync��������ʵ�����洢������Щ�������⵹�պ������Ĵ�������Ҫ��״̬��
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private sealed class FileStreamReadWriteTask<T> : Task<T>
        {
            internal CancellationToken _cancellationToken;
            internal CancellationTokenRegistration _registration;
            internal FileStreamAsyncResult _asyncResult; // initialized after Begin call completes

            internal FileStreamReadWriteTask(CancellationToken cancellationToken) : base()
            {
                _cancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// ȡ����readasync��writeasync�ص���
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state"></param>
        [SecuritySafeCritical]
        private static void CancelTask<T>(object state)
        {
            var task = state as FileStreamReadWriteTask<T>;
            Contract.Assert(task != null);
            FileStreamAsyncResult asyncResult = task._asyncResult;

            // This method is used as both the completion callback and the cancellation callback.
            // We should try to cancel the operation if this is running as the completion callback
            // or if cancellation is not applicable:
            // 1. asyncResult is not a FileStreamAsyncResult
            // 2. asyncResult.IsAsync is false: asyncResult is a "synchronous" FileStreamAsyncResult.
            // 3. The asyncResult is completed: this should never happen.
            Contract.Assert((!asyncResult.IsWrite && typeof(T) == typeof(int)) ||
                            (asyncResult.IsWrite && typeof(T) == typeof(VoidTaskResult)));
            Contract.Assert(asyncResult != null);
            Contract.Assert(asyncResult.IsAsync);

            try
            {
                // Cancel the overlapped read and set the task to cancelled state.
                if (!asyncResult.IsCompleted)
                    asyncResult.Cancel();
            }
            catch (Exception ex)
            {
                task.TrySetException(ex);
            }
        }

        // Completion callback for ReadAsync
        [SecuritySafeCritical]
        private static void EndReadTask(IAsyncResult iar)
        {
            FileStreamAsyncResult asyncResult = iar as FileStreamAsyncResult;
            Contract.Assert(asyncResult != null);
            Contract.Assert(asyncResult.IsCompleted, "How can we end up in the completion callback if the IAsyncResult is not completed?");

            var readTask = asyncResult.AsyncState as FileStreamReadWriteTask<int>;
            Contract.Assert(readTask != null);

            try
            {
                if (asyncResult.IsAsync)
                {
                    asyncResult.ReleaseNativeResource();

                    // release the resource held by CancellationTokenRegistration
                    readTask._registration.Dispose();
                }

                if (asyncResult.ErrorCode == Win32Native.ERROR_OPERATION_ABORTED)
                {
                    var cancellationToken = readTask._cancellationToken;
                    Contract.Assert(cancellationToken.IsCancellationRequested, "How can the IO operation be aborted if cancellation was not requested?");
                    readTask.TrySetCanceled(cancellationToken);
                }
                else
                    readTask.TrySetResult(asyncResult.NumBytesRead);
            }
            catch (Exception ex)
            {
                readTask.TrySetException(ex);
            }
        }

        // Completion callback for WriteAsync
        [SecuritySafeCritical]
        private static void EndWriteTask(IAsyncResult iar)
        {   
            var asyncResult = iar as FileStreamAsyncResult;
            Contract.Assert(asyncResult != null);
            Contract.Assert(asyncResult.IsCompleted, "How can we end up in the completion callback if the IAsyncResult is not completed?");

            var writeTask = iar.AsyncState as FileStreamReadWriteTask<VoidTaskResult>;
            Contract.Assert(writeTask != null);

            try
            {
                if (asyncResult.IsAsync)
                {
                    asyncResult.ReleaseNativeResource();

                    // release the resource held by CancellationTokenRegistration
                    writeTask._registration.Dispose();
                }

                if (asyncResult.ErrorCode == Win32Native.ERROR_OPERATION_ABORTED)
                {
                    var cancellationToken = writeTask._cancellationToken;
                    Contract.Assert(cancellationToken.IsCancellationRequested, "How can the IO operation be aborted if cancellation was not requested?");
                    writeTask.TrySetCanceled(cancellationToken);
                }
                else
                    writeTask.TrySetResult(default(VoidTaskResult));
            }
            catch (Exception ex)
            {
                writeTask.TrySetException(ex);
            }
        }

        // Unlike Flush(), FlushAsync() always flushes to disk. This is intentional.
        // Legend is that we chose not to flush the OS file buffers in Flush() in fear of 
        // perf problems with frequent, long running FlushFileBuffers() calls. But we don't 
        // have that problem with FlushAsync() because we will call FlushFileBuffers() in the background.
        /// <summary>
        /// �첽������������л��������������л������ݶ�д������豸��
        /// </summary>
        /// <param name="cancellationToken">Ҫ����ȡ������ı�ǡ�</param>
        /// <returns></returns>
        [HostProtection(ExternalThreading = true)]
        [ComVisible(false)]
        [System.Security.SecuritySafeCritical]
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to Flush() which a subclass might have overriden.  To be safe 
            // we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into Flush) when we are not sure.
            if (this.GetType() != typeof(FileStream))
                return base.FlushAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCancellation(cancellationToken);

            if (_handle.IsClosed)
                __Error.FileNotOpen();

            // The always synchronous data transfer between the OS and the internal buffer is intentional 
            // because this is needed to allow concurrent async IO requests. Concurrent data transfer
            // between the OS and the internal buffer will result in race conditions. Since FlushWrite and
            // FlushRead modify internal state of the stream and transfer data between the OS and the 
            // internal buffer, they cannot be truly async. We will, however, flush the OS file buffers
            // asynchronously because it doesn't modify any internal state of the stream and is potentially 
            // a long running process.
            try
            {
                FlushInternalBuffer();
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }

            if (CanWrite)
                return Task.Factory.StartNew(
                    state => ((FileStream)state).FlushOSBuffer(),
                    this,
                    cancellationToken,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);
            else
                return Task.CompletedTask;
        }
#endif //FEATURE_ASYNC_IO

    }
}
