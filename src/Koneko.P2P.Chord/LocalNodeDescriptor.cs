﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Chord {
	public enum LocalNodeState {
		Disconnected, Joined
	}

	public class LocalNodeDescriptor {
		public NodeDescriptor Predecessor { get; set; }
		public IList<KeyValuePair<ulong, NodeDescriptor>> Fingers { get; set; }
		public NodeDescriptor Endpoint { get; set; }
		public LocalNodeState State { get; set; }
		public int RingLength { get; set; }

		public NodeDescriptor Successor {
			get { 
				if (Fingers.Any()) {
					return Fingers[0].Value;
				}
				return null;
			} 
			set {
				Fingers[0] = new KeyValuePair<ulong,NodeDescriptor>(TopologyHelper.GetFingerTableKey(Endpoint.Id, 0, RingLength), value);
			}
		}

		public ulong Id {
			get { return Endpoint.Id; }
		}

		public LocalNodeDescriptor() {
			Fingers = new List<KeyValuePair<ulong, NodeDescriptor>>();
			State = LocalNodeState.Disconnected;
		}
	}
}
