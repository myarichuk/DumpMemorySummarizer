using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Raven.Client.Indexes;

namespace DumpMemorySummarizer.Indexes
{
	public class GcRootsCountByKindAndType : AbstractIndexCreationTask<GcRoot, GcRootsCountByKindAndType.RootKindAndTypeResult>
	{
		public class RootKindAndTypeResult
		{
			public GCRootKind Kind { get; set; }

			public string Type { get; set; }

			public int Count { get; set; }
		}

		public GcRootsCountByKindAndType()
		{
			Map = docs => from root in docs
				select new
				{
					Kind = root.RootKind,
					Type = root.TypeName,
					Count = 1
				};

			Reduce = results => from result in results
				group result by new { result.Kind, result.Type }
				into g
				select new
				{
					g.Key.Kind, g.Key.Type,
					Count = g.Sum(x => x.Count)
				};

		}
	}
}
