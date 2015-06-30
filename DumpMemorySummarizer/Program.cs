using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClrMD.Extensions;
using DumpMemorySummarizer.Indexes;
using Fclp;
using Microsoft.Diagnostics.Runtime;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Imports.Newtonsoft.Json;
using Raven.Json.Linq;

namespace DumpMemorySummarizer
{
	public class Program
	{
		private static string dumpFilename;
		private static string url;
		private static string databaseName;
		private readonly static Dictionary<ulong,List<ClrRoot>> gcRootsByObjRef = new Dictionary<ulong, List<ClrRoot>>();
		private readonly static Dictionary<ulong,ClrRoot> gcRootsByRef = new Dictionary<ulong, ClrRoot>(); 
		static void Main(string[] args)
		{
			var p = new FluentCommandLineParser();

			p.SetupHelp("help","h")
			 .Callback(() => Console.WriteLine("Usage: \n DumpMemorySummarizer.exe ( -dump=[Dump filename] or -url=[remote RavenDB url] -databaseName=[database name])"))
			 .UseForEmptyArgs();

			p.Setup<string>("dump")
			 .Callback(record => dumpFilename = record)
			 .Required()
			 .WithDescription("Dump filename full path");

			p.Setup<string>("url")
			 .Callback(record => url = record)			
 			 .Required()
			 .WithDescription("URL of RavenDB server");

			p.Setup<string>("databaseName")
			 .Callback(record => databaseName = record)
			 .SetDefault("DumpMemorySummary")
			 .WithDescription("Destination database name");			

			p.IsCaseSensitive = false;

			var parseResult = p.Parse(args);
			if (parseResult.HasErrors || String.IsNullOrWhiteSpace(dumpFilename))
			{
				Console.WriteLine(parseResult.ErrorText);
				return;
			}

			IDocumentStore store;
			
			if (String.IsNullOrWhiteSpace(url))
			{
				store = new EmbeddableDocumentStore
				{
					RunInMemory = false,
					DefaultDatabase = "DumpMemorySummary",
					UseEmbeddedHttpServer = true
				};
			}
			else
			{
				store = new DocumentStore
				{
					Url = url,
					DefaultDatabase = databaseName					
				};
			}

			using (store)
			using (var session = ClrMDSession.LoadCrashDump(dumpFilename))
			{		
				store.Initialize();
				store.DatabaseCommands.GlobalAdmin.EnsureDatabaseExists(databaseName);

				new GcRootCountByKind().Execute(store);
				new GcRootsCountByKindAndType().Execute(store);
				new HeapObjectCountByType().Execute(store);
				new HeapObjectStatisticsByType().Execute(store);

				var heap = session.Heap;

				if(heap.CanWalkHeap == false)
					throw new ApplicationException("Cannot walk heap, aborting.");

				using (var bulkInsert = store.BulkInsert(databaseName))
				{
					long rootCount = 0;
					bulkInsert.Store(session.Runtime.GetThreadPool(),"threadpool/1");					

					Console.Write("Writing threads...");
					int threadId = 1;
					foreach (var thread in session.Runtime.Threads)
					{						
						bulkInsert.Store(new Thread(thread));
					}
					Console.WriteLine("done");

					Console.Write("Writing objects from finalizer queue...");
					foreach (var finalizerObj in session.Runtime.EnumerateFinalizerQueue())
					{
						var clrType = heap.GetObjectType(finalizerObj);

						if (clrType == null)
							continue;

						if (clrType.Name.Contains("Stream"))
						{
							HandleStreamObject(clrType, bulkInsert, finalizerObj);
						}
						else if (clrType.Name.Equals("Microsoft.Isam.Esent.Interop.Table"))
						{
							HandleEsentTable(clrType, bulkInsert, finalizerObj);
						}
						else
							bulkInsert.Store(new FinalizerQueueObject
							{
								TypeName = clrType.Name,
								Size = clrType.BaseSize,
							});
					}
					Console.WriteLine("done");

					foreach (var root in heap.EnumerateRoots())
					{
						if (root != null && root.Type != null)
						{
							var clrType = root.Type;
							var rootInfo = new GcRoot
							{
								Address = root.Address,
								Name = root.Name,
								RootKind = root.Kind,
								ObjectRefThatRootKeepsAlive = root.Object,
								TypeName = (clrType != null) ? clrType.Name : "<No Type>"
							};
							bulkInsert.Store(rootInfo);

							List<ClrRoot> objectRoots;
							if (gcRootsByObjRef.TryGetValue(root.Object, out objectRoots))
								objectRoots.Add(root);
							else
								gcRootsByObjRef.Add(root.Object, new List<ClrRoot> {root});

							Console.WriteLine("#{0}, {1}", ++rootCount, rootInfo);
						}
					}

					long objectsEnumerated = 0;
					foreach (var objRef in heap.EnumerateObjects())
					{
						objectsEnumerated++;
						Console.WriteLine("Enumerated {0} objects from the heap", objectsEnumerated);
						var clrType = heap.GetObjectType(objRef);
						if (clrType == null)
						{
							Console.WriteLine("Warning: heap corrupted, could not determine object type");
							continue;
						}
						var size = clrType.GetSize(objRef);
						var generation = heap.GetGeneration(objRef);
						var objectInfo = new HeapObject
						{
							ObjRef = objRef,
							Generation = generation,
							Size = size,
							TypeName = clrType.Name,
							IsArray = clrType.IsArray,
							ArrayLength = clrType.IsArray ? clrType.GetArrayLength(objRef) : -1,
							IsInLOH = heap.IsInHeap(objRef),
							GcRootPaths = new Dictionary<ulong, List<string>>()
						};

//						List<ClrRoot> clrRoots;
//						if (gcRootsByObjRef.TryGetValue(objRef, out clrRoots))
//							foreach (var root in clrRoots)
//							{
//								//var path = PathToGcRoots(heap, objRef, root);
//								//objectInfo.GcRootPaths.Add(root.Address, path);
//							}

						Console.WriteLine("#{0}, {1}", ++objectsEnumerated, objectInfo);
						bulkInsert.Store(objectInfo);
					}
				}
			}
		}

		private static void HandleEsentTable(ClrType clrType, BulkInsertOperation bulkInsert, ulong finalizerObj)
		{
			const string propName = "name";
			var tableNameField = clrType.GetFieldByName(propName);
			if (tableNameField == null)
			{
				bulkInsert.Store(new FinalizerQueueObject
				{
					TypeName = clrType.Name,
					Size = clrType.BaseSize,
					PropertyData = "<no table name field>",
					PropertyName = propName
				});
			}
			else
			{
				var tableName = tableNameField.GetValue(finalizerObj);
				var name = (tableName != null) ? tableName.ToString() : "<empty table name field>";

				bulkInsert.Store(new FinalizerQueueObject
				{
					TypeName = clrType.Name,
					Size = clrType.BaseSize,
					PropertyData = String.IsNullOrWhiteSpace(name) ? "<empty table name field>" : name,
					PropertyName = propName
				});
			}
		}

		private static void HandleStreamObject(ClrType clrType, BulkInsertOperation bulkInsert, ulong finalizerObj)
		{
			const string propName = "_fileName";
			var filenameField = clrType.GetFieldByName(propName);
			if (filenameField == null)
			{
				bulkInsert.Store(new FinalizerQueueObject
				{
					TypeName = clrType.Name,
					Size = clrType.BaseSize,
					PropertyData = "<no filename field>",
					PropertyName = propName
				});
			}
			else
			{
				var filename = filenameField.GetValue(finalizerObj) ?? string.Empty;

				bulkInsert.Store(new FinalizerQueueObject
				{
					TypeName = clrType.Name,
					Size = clrType.BaseSize,
					PropertyData = filename.ToString(),
					PropertyName = propName
				});
			}
		}

		private static Dictionary<ulong,List<ulong>> PathToGcRoots(ClrHeap heap, ulong objRef, ClrRoot root)
		{
			const int maxSteps = 65536;
			var sourceNode = new ObjectGraphNode(objRef, heap.GetObjectType(objRef).Name);
			var considered = new HashSet<ulong>();
			var gcRootNodes = new HashSet<ulong>();
			var count = 0;
			var eval = new Stack<ObjectGraphNode>();

			eval.Push(sourceNode);

			while (eval.Count > 0)
			{
				var node = eval.Pop();
				if (considered.Contains(node.ObjRef))
					continue;

				considered.Add(node.ObjRef);

				if (gcRootsByRef.ContainsKey(node.ObjRef))
				{
					gcRootNodes.Add(node.ObjRef);
					continue;					
				}

				var type = heap.GetObjectType(node.ObjRef);
				if (type == null)
					continue;

				count++;
				if (count >= maxSteps)
					return null;
					
				type.EnumerateRefsOfObject(node.ObjRef, (child, offset) =>
				{
					if (child != 0 && !considered.Contains(child))
					{
						var typeName = heap.GetObjectType(child).Name;
						eval.Push(new ObjectGraphNode(child,typeName));
					}
				});
			}

			return null;
		}

		public class ObjectGraphNode : IEquatable<ObjectGraphNode>
		{
			public List<ObjectGraphNode> Adjacents { get; set; }

			public string TypeName { get; set; }

			public ulong ObjRef { get; set; }

			public ObjectGraphNode(ulong objRef, string typeName)
			{
				ObjRef = objRef;
				TypeName = typeName;
			}

			public bool Equals(ObjectGraphNode other)
			{
				if (ReferenceEquals(null, other)) return false;
				if (ReferenceEquals(this, other)) return true;
				return Equals(Adjacents, other.Adjacents) && string.Equals(TypeName, other.TypeName) && ObjRef == other.ObjRef;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				if (ReferenceEquals(this, obj)) return true;
				if (obj.GetType() != this.GetType()) return false;
				return Equals((ObjectGraphNode) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = (Adjacents != null ? Adjacents.GetHashCode() : 0);
					hashCode = (hashCode*397) ^ (TypeName != null ? TypeName.GetHashCode() : 0);
					hashCode = (hashCode*397) ^ ObjRef.GetHashCode();
					return hashCode;
				}
			}

			public static bool operator ==(ObjectGraphNode left, ObjectGraphNode right)
			{
				return Equals(left, right);
			}

			public static bool operator !=(ObjectGraphNode left, ObjectGraphNode right)
			{
				return !Equals(left, right);
			}
		}
	
	}
}
