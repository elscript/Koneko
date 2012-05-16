using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;

using Koneko.P2P.Chord;

namespace Koneko.P2P.Chord.ConsoleApp {
	public class Program {
		public static void Main(string[] args) {
			var localInstance = new LocalInstance(new NodeDescriptor("127.0.0.1", 9999)) { 
									Length = 5
								};
			using (localInstance) {
				var srv = new NodeService { LocalInstance = localInstance };

				using (var srvHost = new ServiceHost(srv)) {
					srvHost.AddServiceEndpoint(
						typeof(INodeService),
						new NetTcpBinding(),
						"net.tcp://" + localInstance.LocalNode.Endpoint.IpAddress + ":" + localInstance.LocalNode.Endpoint.Port
					);
					srvHost.Open();

					while (true) {
						var cmd = Console.ReadLine();
						if (cmd.StartsWith("join")) {
							var cmdParts = cmd.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
							if (cmdParts.Length > 1) {
								var nodeInfo = cmdParts[1].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
								localInstance.Join(new NodeDescriptor(nodeInfo[0], Convert.ToInt32(nodeInfo[1])));
							} else {
								localInstance.Join();
							}
						} else if (cmd == "leave") {
							localInstance.Leave();
						} else if (cmd == "exit") {
							localInstance.Exit();
							srvHost.Close();
							break;
						} else {
							Console.WriteLine(
									@"	join <ipaddress>:<port> : Joins the network with known node \r\n
										join : Create new network \r\n	
										leave : Leave the network \r\n
										exit : Exit the application \r\n
									"
							);
						}
					}
				}
			}
		}
	}
}
