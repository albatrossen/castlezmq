﻿namespace Castle.Zmq.Extensions
{
	using System;

	public abstract class BasePublisher<T> : IDisposable where T : class
	{
		private readonly IZmqContext _context;
		private readonly string _endpoint;
		private readonly Func<T, byte[]> _serializer;
		private IZmqSocket _socket;
		private bool _started;
		private volatile bool _disposed;
		private readonly object _locker = new object();

		protected BasePublisher(IZmqContext context, string endpoint, Func<T, byte[]> serializer)
		{
			if (context == null) throw new ArgumentNullException("context");
			if (endpoint == null) throw new ArgumentNullException("endpoint");
			if (serializer == null) throw new ArgumentNullException("serializer");

			this._context = context;
			this._endpoint = endpoint;
			this._serializer = serializer;
		}

		/// <summary>
		/// Publishes message without topic
		/// </summary>
		/// <param name="message"></param>
		public void Publish(T message)
		{
			this.Publish(string.Empty, message);
		}

		/// <summary>
		/// Publishes with a topic
		/// </summary>
		/// <param name="topic"></param>
		/// <param name="message"></param>
		public void Publish(string topic, T message)
		{
			EnsureNotDisposed();
			if (!this._started) throw new InvalidOperationException("Needs to be Started before publishing");
			if (topic == null) throw new ArgumentNullException("topic");
			if (message == null) throw new ArgumentNullException("message");

			var serialized = this._serializer(message);

			lock (_locker)
			{
				this._socket.Send(topic, hasMoreToSend: true);
				this._socket.Send(serialized);
			}
		}

		public virtual void Start()
		{
			EnsureNotDisposed();

			if (this._socket == null)
			{
				this._socket = this._context.CreateSocket(SocketType.Pub);
			}
			if (!this._started)
			{
				this._socket.Bind(this._endpoint);
				this._started = true;
			}
		}

		public virtual void Stop()
		{
			EnsureNotDisposed();

			if (this._started)
			{
				this._socket.Unbind(this._endpoint);
				this._started = false;
			}
		}

		public void Dispose()
		{
			if (_disposed) return;

			try
			{
				this.Stop();
			}
			catch (Exception)
			{
			}

			if (this._socket != null)
			{
				this._socket.Dispose();
			}

			_disposed = true;
		}

		private void EnsureNotDisposed()
		{
			if (_disposed) throw new ObjectDisposedException("Publisher Disposed");
		}
	}
}