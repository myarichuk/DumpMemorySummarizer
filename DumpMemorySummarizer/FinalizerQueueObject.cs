using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumpMemorySummarizer
{
	public class FinalizerQueueObject
	{
		public string TypeName { get; set; }
		public int Size { get; set; }

		public string PropertyData { get; set; }

		public string PropertyName { get; set; }
	}
}
