using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Chord {
	public class Network {
		private readonly Random Rnd;

		public int Length { get; set; }

		public Network() {
			Rnd = new Random();
		}

		public bool IsInCircularInterval(long value, long left, long right, bool includeLeft = false, bool includeRight = false) {
			if (right == left) {
				return value == right;
			}
			if (right > left) {
				if (includeLeft) {
					left = left - 1;
				}
				if (includeRight) {
					right = right + 1;
				}
				return value > left && value < right;
			} else {
				return !IsInCircularInterval(value, right, left, !includeLeft, !includeRight);
			}
		}

		public long GetFingerTableKey(long nodeId, int position) {
			long result = nodeId + (long)Math.Pow(2, position);
			return result > Length
					? result % (long)Math.Pow(2, Length)
					: result;
		}

		public int GetRandomFingerTableIndex() {
			return Rnd.Next(1, Length);
		}

		public Node FindSuccessorById(long nodeId, Node initialNode) {
			if (nodeId > initialNode.Id) {
				if (initialNode.Successor != null && nodeId <= initialNode.Successor.Id) {
					return initialNode.Successor;
				}
			} 

			var predecessorNode = FindPredecessorById(nodeId, initialNode);
			return predecessorNode.Successor;
		}

		public Node FindPredecessorById(long nodeId, Node initialNode) {
			Node result = initialNode;
			while (!IsInCircularInterval(nodeId, result.Id, result.Successor.Id, includeRight: true)) {
				result = FindClosestPrecedingFingerById(nodeId, initialNode);
			}
			return result;
		}

		private Node FindClosestPrecedingFingerById(long nodeId, Node initialNode) {
			for (int i = Length - 1; i >= 0; --i) {
				if (IsInCircularInterval(initialNode.Fingers[i].Value.Id, initialNode.Id, nodeId)) {
					return initialNode.Fingers[i].Value;
				}
			}
			return initialNode;
		}
	}
}
