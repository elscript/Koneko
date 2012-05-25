using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

using Koneko.Common;

namespace Koneko.P2P.Chord {
	public class CommunicationManager<ServiceT>: IDisposable  {
		private ServiceHost ServiceHost { get; set; }

		public ServiceT LocalService { get; set; }
		public NodeDescriptor LocalServiceNode { get; set; }

		public string ServiceUrlPart { 
			get {
				return "/NodeService/" + LocalServiceNode.RingLevel;
			}
		}

		public void StartCommunication() {
			ServiceHost = new ServiceHost(LocalService);
			ServiceHost.AddServiceEndpoint(
				typeof(INodeService),
				new NetTcpBinding(),
				"net.tcp://" + NetworkHelper.GetLocalIpAddress() + ":" + LocalServiceNode.Port + ServiceUrlPart
			);
			ServiceHost.Open();
		}

		public void StopCommunication() {
			if (ServiceHost.State != CommunicationState.Closed) {
				ServiceHost.Close();
			}
			((IDisposable)ServiceHost).Dispose();
		}

		void IDisposable.Dispose() {
			StopCommunication();
		}
	}
}
