using System.Linq;
using Raven.Client.Indexes;

namespace DumpMemorySummarizer.Indexes
{
	public class HeapObjectCountByType : AbstractIndexCreationTask<HeapObject, HeapObjectCountByType.Result>
	{
		public class Result
		{
			public long Count { get; set; }

			public string Type { get; set; }

			public double AverageSize { get; set; }

			public double MaxSize { get; set; }

			public double MinSize { get; set; }
		}

		public HeapObjectCountByType()
		{
			Map = objects => from @object in objects
				select new
				{
					Count = 1,
					Type = @object.TypeName,
					AverageSize = @object.Size,
					MaxSize = 0,
					MinSize = 0
				};

			Reduce = results => from result in results
				group result by result.Type
				into g
				let count = g.Sum(x => x.Count)
				let totalSize = g.Sum(x => x.AverageSize)
				select new
				{
					Type = g.Key,
					AverageSize = totalSize / count,
					Count = count,
					MaxSize = g.Max(x => x.AverageSize),
					MinSize = g.Min(x => x.AverageSize),
				};
		}
	}
}
