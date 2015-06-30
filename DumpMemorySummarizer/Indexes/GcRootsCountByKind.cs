using System.Linq;
using Microsoft.Diagnostics.Runtime;
using Raven.Client.Indexes;

namespace DumpMemorySummarizer.Indexes
{
	public class GcRootCountByKind : AbstractIndexCreationTask<GcRoot, GcRootCountByKind.RootKindResult>
	{
		public class RootKindResult
		{
			public GCRootKind Kind { get; set; }

			public int Count { get; set; }
		}

		public GcRootCountByKind()
		{
			Map = docs => from root in docs
				select new
				{
					Kind = root.RootKind,
					Count = 1
				};

			Reduce = results => from result in results
				group result by result.Kind
				into g
				select new
				{
					Kind = g.Key,
					Count = g.Sum(x => x.Count)
				};

		}
	}
}
