namespace Castle.Zmq.Extensions
{
	using System;
	using System.Threading;

	public class WorkerPool : SharedQueue
	{
		private readonly IZmqContext _ctx;
		private readonly string _backendEndpoint;
		private readonly Action<IZmqSocket> _proc;
		private readonly int _workers;
		private volatile bool _running;

		public WorkerPool(IZmqContext ctx, string frontEndEndpoint, string backendEndpoint, Action<IZmqSocket> proc, int workers)
			: base(ctx, frontEndEndpoint, backendEndpoint)
		{
			this._ctx = ctx;
			this._backendEndpoint = backendEndpoint;
			this._proc = proc;
			this._workers = workers;
		}

		public override void Start()
		{
			EnsureNotDisposed();

			this._running = true;

			for (int i = 0; i < _workers; i++)
			{
				ThreadPool.UnsafeQueueUserWorkItem(WorkerProc, null);
			}

			base.Start();
		}

		private void WorkerProc(object state)
		{
			try
			{
				using (var socket = this._ctx.CreateSocket(SocketType.Rep))
				{
					socket.Connect(this._backendEndpoint);

					var polling = new Polling(PollingEvents.RecvReady, socket);

					// once data is ready, we pass it along to the actual worker
					polling.RecvReady += _dummy => this._proc(socket);

					while (_running)
					{
						polling.PollForever();
					}
				}
			}
			catch (ZmqException e)
			{
				if (e.ZmqErrorCode != Native.ETERM) throw;
			}
		}

		protected override void DoDispose()
		{
			_running = false;
		}
	}
}