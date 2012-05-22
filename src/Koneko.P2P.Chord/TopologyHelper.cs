﻿using System;
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

		public static ulong GetFingerTableKey(ulong nodeId, int position, int ringLength) {
			var result = nodeId + (ulong)Math.Pow(2, position);
			var modulo = (ulong)Math.Pow(2, ringLength);
			return result > modulo ? result % modulo : result;
		}
	}
}
