using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Chord {
	public static class TopologyHelper {
		public static bool IsInCircularInterval(ulong value, ulong left, ulong right, bool includeLeft = false, bool includeRight = false) {
			if (right == left) {
				return value == right;
			}
			if (right > left) {
				return (includeLeft ? value >= left : value > left) && (includeRight ? value <= right : value < right);
			} else {
				return (includeLeft ? value >= left : value > left) || (includeRight ? value <= right : value < right);
			}
		}

		public static ulong GetFingerTableKey(ulong nodeId, int position, int ringLength) {
			var result = nodeId + (ulong)Math.Pow(2, position);
			var modulo = (ulong)Math.Pow(2, ringLength);
			return result > modulo ? result % modulo : result;
		}
	}
}
