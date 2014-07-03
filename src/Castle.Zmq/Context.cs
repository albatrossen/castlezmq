﻿using System.IO;
using System.IO.Compression;

namespace Castle.Zmq
{
	using System;

	/// <summary>
	/// Holds a zmq context. See http://api.zeromq.org/4-0:zmq-ctx-new
	/// </summary>
	public class Context : IZmqContext, IDisposable
	{
		private volatile bool _disposed;
		
		internal readonly IntPtr contextPtr;

		public const int DefaultIoThreads = 1;
		public const int DefaultMaxSockets = 1024;

	    static Context()
	    {
	        //EnsureZmqLibrary();
	    }

	    public static void EnsureZmqLibrary()
	    {
	        IntPtr libPtr = Native.LoadLibrary("libzmq");
	        if (libPtr == IntPtr.Zero)
	        {
	            LoadEmbeddedLibary();
	        }
	    }

	    private static void LoadEmbeddedLibary()
	    {
	        var bitnessPath = String.Format("x{0}", Environment.Is64BitProcess ? 64 : 86);

	        var asm = typeof (Context).Assembly;
            string resourceName = String.Format(
                "Castle.Zmq.Native.lib.{0}.libzmq.dll.gz", 
                bitnessPath);

	        var dir = String.Format("castle-zmq-{0}", typeof(Context).Assembly.GetName().Version);

	        dir = Path.Combine(Path.GetTempPath(), dir);
	        dir = Path.Combine(dir, bitnessPath);

            if(!Directory.Exists(dir))
	            Directory.CreateDirectory(dir);

	        var tempFile = Path.Combine(dir, "libzmq.dll");

            // Try to delete and recreate, in case of file corruption
	        if (File.Exists(tempFile))
	        {
	            try
	            {
                    File.Delete(tempFile);
	            }
	            catch{ } // Might be in use by another process; likely not corrupted
	        }

	        if (!File.Exists(tempFile)) // if delete was successful, create it
	        {
	            using (var stream = asm.GetManifestResourceStream(resourceName))
	            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
	            using (var file = File.Create(tempFile))
	            {
	                gzip.CopyTo(file);
	                file.Flush(true);
	            }
	        }

	        IntPtr libPtr = Native.LoadLibrary(tempFile);
	        if (libPtr == IntPtr.Zero)
	        {
	            throw new InvalidOperationException("Unable to load libzmq.");
	        }
	    }

	    /// <summary>
		/// Creates a new context
		/// </summary>
		/// <param name="ioThreads">How many dedicated threds for IO operations</param>
		/// <param name="maxSockets">Max sockets allowed for this context</param>
		public Context(int ioThreads, int maxSockets)
		{
			if (ioThreads < 0) throw new ArgumentException("ioThreads can't be less than zero", "ioThreads");
			if (maxSockets < 1) throw new ArgumentException("maxSockets can't be less than one", "maxSockets");

			this.contextPtr = Native.Context.zmq_ctx_new();

			if (this.contextPtr == IntPtr.Zero)
			{
				throw new ZmqException("Could not allocate a Zmq Context");
			}

			// Just in case to avoid memory leaks
			if (ioThreads != DefaultIoThreads || maxSockets != DefaultMaxSockets)
			{
				InternalConfigureContext(ioThreads, maxSockets);
			} 
		}

		public Context() :this(DefaultIoThreads, DefaultMaxSockets)
		{
		}

		~Context()
		{
			InternalDispose(false);
		}

		public IZmqSocket CreateSocket(SocketType type)
		{
			return new Socket(this, type);
		}

		public void Dispose()
		{
			this.InternalDispose(true);
		}

		private void InternalConfigureContext(int ioThreads, int maxSockets)
		{
			try
			{
				if (ioThreads != DefaultIoThreads)
				{
					var res = Native.Context.zmq_ctx_set(this.contextPtr, Native.Context.IO_THREADS, ioThreads);
					if (res == Native.ErrorCode) Native.ThrowZmqError();
				}
				if (maxSockets != DefaultMaxSockets)
				{
					var res = Native.Context.zmq_ctx_set(this.contextPtr, Native.Context.MAX_SOCKETS, maxSockets);
					if (res == Native.ErrorCode) Native.ThrowZmqError();
				}
			}
			catch (Exception)
			{
				this.InternalDispose(true);
				throw;
			}
		}

		private void InternalDispose(bool isDispose)
		{
			if (this._disposed) return;

			if (isDispose)
			{
				GC.SuppressFinalize(this);
			}

			this._disposed = true;

			if (this.contextPtr != IntPtr.Zero)
			{
				Native.Context.zmq_ctx_shutdown(this.contextPtr); // discard any error

				var error = Native.Context.zmq_ctx_term(this.contextPtr);
				if (error == Native.ErrorCode)
				{
					// Not good, but we can't throw an exception in the Dispose
					var msg = "Error disposing context: " + Native.LastErrorString();
					System.Diagnostics.Trace.TraceError(msg);
					System.Diagnostics.Debug.WriteLine(msg);
				}
			}
		}
	}
}
