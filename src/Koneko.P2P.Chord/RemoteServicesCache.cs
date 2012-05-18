using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

using Koneko.Common.Storage;

namespace Koneko.P2P.Chord {
	public class RemoteServicesCache<ServiceT> : IDisposable {
		private IDictionary<NodeDescriptor, ServiceT> Cache { get; set; }

		public string ServiceUrlPart { get; set; }

		public RemoteServicesCache() {
			Cache = new Dictionary<NodeDescriptor, ServiceT>();
		}

		public ServiceT GetRemoteNodeService(NodeDescriptor node) {
			if (!Cache.ContainsKey(node)) {
                var srvFactory = new ChannelFactory<ServiceT>(
										new NetTcpBinding(),
										"net.tcp://" + node.IpAddress + ":" + node.Port//"net.tcp://" + node.IpAddress + ":" + node.Port + ServiceUrlPart
								);
                var srv = srvFactory.CreateChannel();
				Cache.Add(node, srv);
			}
			return Cache[node];
		}

		public void Clear() {
			foreach (var s in Cache) {
				try {
					((ICommunicationObject)s.Value).Close();
				} catch { }
			}
		}

		public void Dispose() {
			Clear();
		}
	}
}
