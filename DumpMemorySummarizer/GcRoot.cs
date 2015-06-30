using System;
using Microsoft.Diagnostics.Runtime;

namespace DumpMemorySummarizer
{
	public class GcRoot
	{
		public GCRootKind RootKind { get; set; }

		public ulong Address { get; set; }

		public string Name { get; set; }

		public string TypeName { get; set; }

		public ulong ObjectRefThatRootKeepsAlive { get; set; }

		public override string ToString()
		{
			return String.Format("RootKind: {0}, Address: {1}, Name: {2}, ObjectRefThatRootKeepsAlive: {3}, TypeName: {4}", RootKind, Address, Name, ObjectRefThatRootKeepsAlive, TypeName);
		}
	}
}