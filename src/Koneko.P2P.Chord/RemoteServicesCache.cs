using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

using Koneko.Common.Storage;

namespace Koneko.P2P.Chord {
	public class RemoteServicesCache<ServiceT> : IDisposable {
		private IDictionary<NodeDescriptor, RemoteServicesCacheEntry<ServiceT>> Cache { get; set; }
		
		public ServiceT LocalService { get; set; }
		public NodeDescriptor LocalServiceNode { get; set; }
		public string ServiceUrlPart { get; set; }

		public RemoteServicesCache() {
			Cache = new Dictionary<NodeDescriptor, RemoteServicesCacheEntry<ServiceT>>();
		}

		public RemoteServicesCacheEntry<ServiceT> GetRemoteNodeService(NodeDescriptor node) {
			// return local service for local nodes
			if (node.Equals(LocalServiceNode)) {
				return new RemoteServicesCacheEntry<ServiceT> { Service = LocalService, IsLocalService = true };
			}
			if (!Cache.ContainsKey(node)) {
                var srvFactory = new ChannelFactory<ServiceT>(
										new NetTcpBinding(),
										"net.tcp://" + node.IpAddress + ":" + node.Port + ServiceUrlPart
								);
                var srv = srvFactory.CreateChannel();
				Cache.Add(node, new RemoteServicesCacheEntry<ServiceT> { Service = srv });
			}
			return Cache[node];
		}

		public void Clear() {
			foreach (var s in Cache) {
				try {
					((ICommunicationObject)s.Value.Service).Close();
				} catch { }
			}
			Cache.Clear();
		}

		public void Dispose() {
			Clear();
		}
	}

	public class RemoteServicesCacheEntry<ServiceT> {
		public ServiceT Service { get; set; }
		public bool IsLocalService { get; set; }
		public bool IsUnavailable { 
			get { 
				// local instance is always available
				if (IsLocalService) {
					return false;
				} else {
					return ((ICommunicationObject)Service).State == CommunicationState.Faulted 
						|| ((ICommunicationObject)Service).State == CommunicationState.Closed 
						|| ((ICommunicationObject)Service).State == CommunicationState.Closing;
				}
			}
		}

		public RemoteServicesCacheEntry() {
			IsLocalService = false;
		}
	}
}
