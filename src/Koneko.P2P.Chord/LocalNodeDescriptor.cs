using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Koneko.P2P.Chord {
	public class LocalNodeDescriptor {
		public IList<KeyValuePair<ulong, NodeDescriptor>> Fingers { get; set; }
		public int SuccessorCacheSize { get; set; }
		public NodeDescriptor Endpoint { get; set; }
		public int RingLength { get; set; }
		public NodeDescriptor InitEndpoint { get; set; }
		public NodeState State { get; set; }

		private readonly Random Rnd;

		// synchronization 
		private object _PredecessorLockObject = new object();
		public object PredecessorLockObject {
			get { return _PredecessorLockObject; }
		}

		private object _SuccessorLockObject = new object();
		public object SuccessorLockObject {
			get { return _SuccessorLockObject; }
		}

		private NodeDescriptor[] _SuccessorCache;
		public NodeDescriptor[] SuccessorCache {
			get {
				if (_SuccessorCache == null) {
					_SuccessorCache = new NodeDescriptor[SuccessorCacheSize];
				}
				return _SuccessorCache;
			}
			set {
				_SuccessorCache = value;
			}
		}

		private NodeDescriptor _Predecessor;
		public NodeDescriptor Predecessor { 
			get { lock(PredecessorLockObject) { return _Predecessor; } }
			set { lock(PredecessorLockObject) { _Predecessor = value; } }
		}

		public NodeDescriptor Successor {
			get { 
				lock (SuccessorLockObject) {
					if (Fingers.Any()) {
						return Fingers[0].Value;
					}
				}
				return null;
			} 
			set {
				lock (SuccessorLockObject) {
					Fingers[0] = new KeyValuePair<ulong,NodeDescriptor>(TopologyHelper.GetFingerTableKey(Endpoint.Id, 0, RingLength), value);
				}
			}
		}

		public ulong Id {
			get { return Endpoint.Id; }
		}

		public LocalNodeDescriptor(NodeDescriptor endpoint, int ringLength) {
			Rnd = new Random();

			Endpoint = endpoint;
			RingLength = ringLength;
			SuccessorCacheSize = 2;

			// dummy init for fingers table
			InitFingerTable();
			// dummy init for successor cache
			InitSuccessorCache();
		}

		public void InitFingerTable() {
			if (Fingers == null) {
				Fingers = new List<KeyValuePair<ulong, NodeDescriptor>>();
			}
			Fingers.Clear();
			for (int i = 0; i <= RingLength - 1; ++i) {
				Fingers.Add(new KeyValuePair<ulong, NodeDescriptor>(TopologyHelper.GetFingerTableKey(Id, i, RingLength), Endpoint));
			}
		}

		public void InitSuccessorCache() {
			SuccessorCache = new NodeDescriptor[SuccessorCacheSize];
			for (int i = 0; i <= SuccessorCacheSize - 1; ++i) {
				SuccessorCache[i] = Endpoint;
			}
		}

		public void Reset() {
			InitFingerTable();
			InitSuccessorCache();
			InitEndpoint = null;
			Predecessor = null;
		}

		public int GetRandomFingerTableIndex() {
			return Rnd.Next(1, (int)RingLength);
		}
	}
}
