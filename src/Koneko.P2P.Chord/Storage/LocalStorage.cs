using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

using NReco;

using Koneko.Common.Hashing;
using Koneko.Common.Storage;
using Koneko.P2P.Chord;

namespace Koneko.P2P.Chord.Storage {
	[ServiceBehavior(InstanceContextMode=InstanceContextMode.Single)]
	public class LocalStorage : IStorageService, IDisposable {
		public LocalInstance LocalInstance { get; set; }

		private RemoteServicesCache<IStorageService> _NodeServices;
		public RemoteServicesCache<IStorageService> NodeServices {
			get {
				if (_NodeServices == null) {
					_NodeServices = new RemoteServicesCache<IStorageService> { 
										ServiceUrlPart = GetRemoteServiceUrlPart()
									};
				}
				return _NodeServices;
			}
		}

		public void Join(IHashFunctionArgument[] vals) {
			foreach (var val in vals) {
				Put(val);
			}
		}

		public void Exit() {
			NodeServices.Clear();
		}

		public void Put(IHashFunctionArgument val) {
			var responsibleNode = LocalInstance.FindResponsibleNodeForValue(val);
			if (responsibleNode.Equals(LocalInstance.LocalNode.Endpoint)) {
				PutToLocalStorage(val);
			} else {
				var responsibleNodeSrv = NodeServices.GetRemoteNodeService(responsibleNode);
				responsibleNodeSrv.Put(val);
			}
		}

		public object Get(IStorageQuery q) {
			var responsibleNode = LocalInstance.FindResponsibleNodeForValue(q.GetArgument());
			if (responsibleNode.Equals(LocalInstance.LocalNode.Endpoint)) {
				return GetFromLocalStorage(q);
			} else {
				var responsibleNodeSrv = NodeServices.GetRemoteNodeService(responsibleNode);
				return responsibleNodeSrv.Get(q);
			}
		}

		private void PutToLocalStorage(object val) {
			// TODO
		}

		private object GetFromLocalStorage(IStorageQuery q) {
			// TODO
			return "test";
		}

		public string GetRemoteServiceUrlPart() {
			return "/StorageService/" + LocalInstance.LocalNode.Endpoint.RingLevel;
		}

		public void Dispose() {
			if (NodeServices is IDisposable) {
				((IDisposable)NodeServices).Dispose();
			}
		}
	}
}
