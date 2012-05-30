using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Net;
using System.Net.Sockets;

using NReco.Logging;

using Koneko.Common;
using Koneko.Common.Storage;
using Koneko.Common.Hashing;
using Koneko.P2P.Chord;
using Koneko.P2P.Chord.Storage;

namespace Koneko.P2P.Chord.ConsoleApp {
	public class Program {
		private static ILog Log = LogManager.GetLogger(typeof(Program));
		
		[STAThread]
		public static void Main(string[] args) {
			try {
				// forcing english language exception
				Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture ("en-US"); 
				Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");

				// log4net init
				NReco.Logging.LogManager.Configure(new NReco.Log4Net.Logger());

				var ringLevels = args.Length > 0 ? Convert.ToInt32(args[0]) : 1;
				var ringLength = args.Length > 1 ? Convert.ToInt32(args[1]) : 8;
				var localPort = args.Length > 2 ? Convert.ToInt32(args[2]) : new Random().Next(10000, 20000);

				var hashKeyPrv = new SHA1ObjectHashKeyProvider() { Modulo = (ulong)Math.Pow(2, ringLength) };

				var localInstances = new List<LocalInstance>();

				for (int currRingLvl = 1; currRingLvl <= ringLevels; ++currRingLvl) {
					// init instance service
					var localInstance = new LocalInstance(
											ringLength: ringLength, 
											ringLevel: currRingLvl, 
											localPort: localPort, 
											hashKeyPrv: hashKeyPrv
										);
					localInstances.Add(localInstance);
				}

				try {
					ProcessCommands(localInstances);
				} finally {
					foreach (var loc in localInstances) {
						((IDisposable)loc).Dispose();
					}
				}
			} catch (Exception ex) {
				Log.Write(LogEvent.Error, ex.ToString());
				Console.ReadLine();
			}
		}

		private static void ProcessCommands(IList<LocalInstance> localInstances) {
			CancellationTokenSource showLocalInfoTs = null;
			while (true) {
				var cmd = Console.ReadLine(); 
				if (cmd.StartsWith("join")) {
					var cmdParts = cmd.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
					var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent, TaskContinuationOptions.None);
					if (cmdParts.Length > 1) {
						var nodeInfo = cmdParts[1].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
						foreach (var inst in localInstances) {
							// TODO: it is possible to wrap each join in a different thread and then wait on all threads so all joins will perform simultaneously
							inst.Join(
									new NodeDescriptor(
										ipAddress: nodeInfo[0], 
										port: Convert.ToInt32(nodeInfo[1]), 
										ringLevel: inst.LocalNode.Endpoint.RingLevel, 
										hashKeyPrv: inst.ObjectHashKeyPrv
							));
						}
					} else {
						foreach (var inst in localInstances) {
							// TODO: it is possible to wrap each join in a different thread and then wait on all threads so all joins will perform simultaneously
							inst.Join();
						}
					}
				} else if (cmd == "info") {
					ShowLocalInfo(localInstances);
				} else if (cmd == "startinfo") {
					showLocalInfoTs = StartShowLocalInfo(localInstances);
				} else if (cmd == "stopinfo") {
					StopShowLocalInfo(showLocalInfoTs);
				} else if (cmd == "leave") {
					StopShowLocalInfo(showLocalInfoTs);

					foreach (var inst in localInstances) {
						inst.SignalEvent(LocalInstanceEvent.LeaveRequested);
					}
					// wait until all instance insternal threads are done
					foreach (var inst in localInstances) {
						inst.WaitUntilCurrentEventProcessed();
					}
				} else if (cmd == "exit") {
					StopShowLocalInfo(showLocalInfoTs);

					foreach (var inst in localInstances) {
						inst.SignalEvent(LocalInstanceEvent.ExitRequested);
					}
					// wait until all instance insternal threads are done
					foreach (var inst in localInstances) {
						inst.WaitUntilCurrentEventProcessed();
					}
					break;
				} else {
					Console.WriteLine("join <ipaddress>:<port> : Join the network with known node \r\n info: Show info about local node \r\n join : Create new network \r\n leave: Leave the network \r\n exit : Exit the application");
				}
			}
		}

		private static CancellationTokenSource StartShowLocalInfo(IList<LocalInstance> localInstances) {
			var ts = new CancellationTokenSource();
			Task.Factory.StartNew(
				() => {
					while (!ts.IsCancellationRequested) {
						ShowLocalInfo(localInstances);
						Thread.Sleep(3000);
					}
				},
				ts.Token
			);
			return ts;
		}

		private static void StopShowLocalInfo(CancellationTokenSource ts) {
			if (ts != null) {
				ts.Cancel();
				ts.Token.WaitHandle.WaitOne();
				ts = null;
			}
		}

		private static void ShowLocalInfo(IList<LocalInstance> localInstances) {
			Console.WriteLine("===============LOCAL INFO BEGIN=================");
			foreach (var inst in localInstances) {
				Console.WriteLine("===== INSTANCE LEVEL {0} BEGIN =====", inst.LocalNode.Endpoint.RingLevel);
				Console.WriteLine("**** Node {0} ****", inst.LocalNode.Endpoint);
				Console.WriteLine("^^^^ Ring position: {0} ^^^^", inst.LocalNode.Id);

				if (inst.LocalNode.State == NodeState.Disconnected) {
					Console.WriteLine("DISCONNECTED");
				} else {
					Console.WriteLine("Bootstrapper: {0}", inst.LocalNode.InitEndpoint != null ? inst.LocalNode.InitEndpoint.ToShortString() : "n/a");
					Console.WriteLine("Successor: {0}", inst.LocalNode.Successor != null ? inst.LocalNode.Successor.ToShortString() : "n/a");
					Console.WriteLine("Predecessor: {0}", inst.LocalNode.Predecessor != null ? inst.LocalNode.Predecessor.ToShortString() : "n/a");
					Console.WriteLine("Fingers:");
					foreach (var f in inst.LocalNode.Fingers) {
						Console.WriteLine("\t Start: {0}, Node: {1}", f.Key, f.Value.ToShortString());
					}
					Console.WriteLine("Cached successors:");
					for (int i = 0; i < inst.LocalNode.SuccessorCache.Length; ++i) {
						Console.WriteLine("\t Position: {0}, Node: {1}", i, inst.LocalNode.SuccessorCache[i].ToShortString());
					}
				}

				Console.WriteLine("===== INSTANCE LEVEL {0} END =====", inst.LocalNode.Endpoint.RingLevel);
			}
			Console.WriteLine("================LOCAL INFO END==================");
		}
	}
}
