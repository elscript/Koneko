using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

namespace Koneko.P2P.Chord {
	public class NodeServicesCache : IDisposable {
		private IDictionary<NodeDescriptor, INodeService> LocalCache { get; set; }

		public NodeServicesCache() {
			LocalCache = new Dictionary<NodeDescriptor, INodeService>();
		}

		public INodeService GetRemoteNodeService(NodeDescriptor node) {
			if (!LocalCache.ContainsKey(node)) {
                var srvFactory = new ChannelFactory<INodeService>(
									new NetTcpBinding(),
									"net.tcp://" + node.IpAddress + ":" + node.Port
							);
                var srv = srvFactory.CreateChannel();
				LocalCache.Add(node, srv);
			}
			return LocalCache[node];
		}

		public void Clear() {
			foreach (var c in LocalCache) {
				((ICommunicationObject)c.Value).Close();
			}
		}

		public void Dispose() {
			Clear();
		}
	}
}
