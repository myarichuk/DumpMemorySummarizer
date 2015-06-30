using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace DumpMemorySummarizer
{
	public class Thread
	{
		public uint OSThreadId { get; set; }

		public int ManagedThreadId { get; set; }

		public uint LockCount { get; set; }

		public bool IsAlive { get; set; }

		public bool IsGC { get; set; }

		public bool IsFinalizer { get; set; }

		public bool IsBackground { get; set; }

		public bool IsAborted { get; set; }

		public bool IsAbortRequested { get; set; }

		public bool IsGCSuspending { get; set; }

		public string CurrentExceptionMessage { get; set; }

		public IReadOnlyCollection<string> Stacktrace { get; set; }

		public Thread(ClrThread thread)
		{
			OSThreadId = thread.OSThreadId;
			ManagedThreadId = thread.ManagedThreadId;
			LockCount = thread.LockCount;
			IsAlive = thread.IsAlive;
			IsAborted = thread.IsAborted;
			IsGC = thread.IsGC;
			IsFinalizer = thread.IsFinalizer;
			IsBackground = thread.IsBackground;
			IsAborted = thread.IsAborted;
			IsAbortRequested = thread.IsAbortRequested;
			IsGCSuspending = thread.IsGCSuspendPending;
			IsThreadpoolCompletionPort = thread.IsThreadpoolCompletionPort;
			IsUserSuspended = thread.IsUserSuspended;
			IsSuspendingEE = thread.IsSuspendingEE;
			IsThreadpoolTimer = thread.IsThreadpoolTimer;
			IsThreadpoolWorker = thread.IsThreadpoolWorker;
			IsUnstarted = thread.IsUnstarted;
//			BlockingObjects = thread.BlockingObjects.Select(x =>
//				new BlockingObject
//				{
//					ObjectRef = x.Object, 
//					Reason = x.Reason,
//					RecursionCount = x.RecursionCount, 
//					Taken = x.Taken,
//				});
			if (thread.CurrentException != null)
				CurrentExceptionMessage = thread.CurrentException.Message;

			Stacktrace = thread.StackTrace.Select(frame => String.Format("{0} {1,12:X} {2}", frame.Kind, frame.StackPointer, frame.DisplayString)).ToList();
		}

		public class BlockingObject
		{
			public ulong ObjectRef { get; set; }
			public BlockingReason Reason { get; set; }
			public int RecursionCount { get; set; }
			public bool Taken { get; set; }
		}

		public object BlockingObjects { get; set; }

		public bool IsUnstarted { get; set; }

		public bool IsThreadpoolWorker { get; set; }

		public bool IsThreadpoolTimer { get; set; }

		public bool IsSuspendingEE { get; set; }

		public bool IsUserSuspended { get; set; }

		public bool IsThreadpoolCompletionPort { get; set; }
	}
}
