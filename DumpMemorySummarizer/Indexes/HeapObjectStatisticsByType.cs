using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Client.Indexes;

namespace DumpMemorySummarizer.Indexes
{
	public class HeapObjectStatisticsByType : AbstractIndexCreationTask<HeapObject,HeapObjectStatisticsByType.Result>
	{
		public class Result
		{
			public long Generation0Count { get; set; }
			
			public long Generation1Count { get; set; }
			
			public long Generation2Count { get; set; }

			public long LOHCount { get; set; }

			public string Type { get; set; }

			public long Total { get; set; }
		};

		public HeapObjectStatisticsByType()
		{
			Map = objects => from heapObject in objects				
				select new
				{
					Generation0Count = (heapObject.Generation == 0) ? 1 : 0,
					Generation1Count = (heapObject.Generation == 1) ? 1 : 0,
					Generation2Count = (heapObject.Generation == 2) ? 1 : 0,
					LOHCount = (heapObject.IsInLOH) ? 1 : 0,
					Type = heapObject.TypeName,
					Total = 1
				};

			Reduce = results => from result in results
				group result by result.Type
				into g
				select new
				{
					Generation0Count = g.Sum(x => x.Generation0Count),
					Generation1Count = g.Sum(x => x.Generation1Count),
					Generation2Count = g.Sum(x => x.Generation2Count),
					LOHCount = g.Sum(x => x.LOHCount),
					Type = g.Key,
					Total = g.Sum(x => x.Total)
				};
		}
	}
}
