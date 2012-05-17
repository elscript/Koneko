using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Net;
using System.Net.Sockets;

using Koneko.Common.Storage;
using Koneko.Common.Hashing;
using Koneko.P2P.Chord;
using Koneko.P2P.Chord.Storage;

namespace Koneko.P2P.Chord.ConsoleApp {
	public class Program {
		public static void Main(string[] args) {
			var ringLevels = args.Length > 0 ? Convert.ToInt32(args[0]) : 1;
			var ringLength = args.Length > 1 ? Convert.ToInt32(args[1]) : 5;
			var localPort = args.Length > 2 ? Convert.ToInt32(args[2]) : 9999;
			var localIp = GetLocalIpAddress();

			var hashKeyPrv = new SHA1ObjectHashKeyProvider() { Modulo = (ulong)Math.Pow(2, ringLength) };

			var localInstances = new List<LocalInstance>();
			var localStorages = new List<LocalStorage>();
			var srvHosts = new List<ServiceHost>(); 

			for (int i = 1; i <= ringLevels; ++i) {
				// init instance service
				var localInstance = new LocalInstance(
										ringLength: ringLength, 
										ringLevel: i, 
										ipAddress: localIp,
										localPort: localPort, 
										hashKeyPrv: hashKeyPrv
									);
				localInstances.Add(localInstance);

				var nodeSrvHost = new ServiceHost(localInstance, new Uri("net.tcp://127.0.0.1:" + localPort));
				nodeSrvHost.AddServiceEndpoint(
					typeof(INodeService),
					new NetTcpBinding(),
					localInstance.GetRemoteServiceUrlPart()
				);
				srvHosts.Add(nodeSrvHost);

				// init storage service
				var localStorage = new LocalStorage() {
										LocalInstance = localInstance
									};
				localStorages.Add(localStorage);

				var storageSrvHost = new ServiceHost(localStorage, new Uri("net.tcp://127.0.0.1:" + (localPort + 1)));
				storageSrvHost.AddServiceEndpoint(
					typeof(IStorageService),
					new NetTcpBinding(),
					localStorage.GetRemoteServiceUrlPart()
				);
				srvHosts.Add(storageSrvHost);
			}

			try {
				foreach (var host in srvHosts) {
					host.Open();
				}
				ProcessCommands(localInstances, localStorages);
			} finally {
				foreach (var host in srvHosts) {
					if (host.State != CommunicationState.Closed) {
						host.Close();
					}
					((IDisposable)host).Dispose();
				}
				foreach (var inst in localInstances) {
					((IDisposable)inst).Dispose();
				}
				foreach (var stor in localStorages) {
					((IDisposable)stor).Dispose();
				}
			}
		}

		private static void ProcessCommands(IList<LocalInstance> localInstances, IList<LocalStorage> localStorages) {
			while (true) {
				var cmd = Console.ReadLine();
				if (cmd.StartsWith("join")) {
					var cmdParts = cmd.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
					if (cmdParts.Length > 1) {
						var nodeInfo = cmdParts[1].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
						foreach (var inst in localInstances) {
							inst.Join(
								new NodeDescriptor(
									ipAddress: nodeInfo[0], 
									port: Convert.ToInt32(nodeInfo[1]), 
									ringLevel: inst.LocalNode.Endpoint.RingLevel, 
									hashKeyPrv: inst.ObjectHashKeyPrv
								)
							);
						}
					} else {
						foreach (var inst in localInstances) {
							inst.Join();
						}
					}

					foreach (var stor in localStorages) {
						stor.Join(new HashableString[] { 
							"test", "foo", "baz", "bar"
						});
					}
				} else if (cmd == "exit") {
					break;
				} else {
					Console.WriteLine(
							@"	join <ipaddress>:<port> : Joins the network with known node \r\n
								join : Create new network \r\n	
								exit : Exit the application \r\n
							"
					);
				}
			}
		}

		private static string GetLocalIpAddress() {
			var host = Dns.GetHostEntry(Dns.GetHostName());
			var result = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
			return result != null ? result.ToString() : "127.0.0.1";
		}
	}
}
