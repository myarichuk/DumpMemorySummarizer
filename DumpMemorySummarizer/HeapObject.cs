using System;
using System.Collections.Generic;

namespace DumpMemorySummarizer
{
	public class HeapObject
	{
		public ulong ObjRef { get; set; }

		public ulong Size { get; set; }

		public int Generation { get; set; }

		public string TypeName { get; set; }

		public bool IsInLOH { get; set; }

		public bool IsArray { get; set; }

		public int ArrayLength { get; set; }


		public Dictionary<ulong, List<string>> GcRootPaths { get; set; } 

		public override string ToString()
		{
			return String.Format("ObjRef: {0}, Size: {1}, Generation: {2}, TypeName: {3}", ObjRef, Size, Generation, TypeName);
		}
	}
}