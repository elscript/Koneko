/*public void Join(Network network, Node knownNode = null) {
			NodesNetwork = network;

			if (knownNode == null) {
				for (int i = 0; i <= network.Length - 1; ++i) {
					Fingers[i] = new KeyValuePair<long, Node>(network.GetFingerTableKey(Id, i), this);
				}
				Predecessor = this;
			} else {
				Fingers = network.CreateFingerTable(this, knownNode);
				Predecessor = Successor.Predecessor;
				Successor.Predecessor = this;

				for (int i = 0; i < network.Length; ++i) {
					var currentNode = network.FindPredecessorById(Id - (long)Math.Pow(2, i), this);
					network.UpdateFingerTableRow(currentNode, this, i);
				}
			}
		}
		
public IList<KeyValuePair<long, Node>> CreateFingerTable(Node node, Node initialNode) {
			var result = new List<KeyValuePair<long, Node>>();

			var firstFingerRowId = GetFingerTableKey(node.Id, 0);
			var successorFinger = new KeyValuePair<long,Node>(firstFingerRowId, FindSuccessorById(firstFingerRowId, initialNode));
			result[0] = successorFinger;

			for (int i = 1; i < Length; ++i) {
				var currentFingerRowId = GetFingerTableKey(node.Id, 1);
				Node currentFingerRowValue = IsInCircularInterval(node.Id, initialNode.Id, result[i-1].Value.Id, includeLeft: true)
												? result[i-1].Value
												: FindSuccessorById(currentFingerRowId, initialNode);
				result[i] = new KeyValuePair<long,Node>(currentFingerRowId, currentFingerRowValue);
			}

			return result;
		}

		public void UpdateFingerTableRow(Node ownerNode, Node newNode, int rowPosition) {
			if (IsInCircularInterval(newNode.Id, ownerNode.Id, ownerNode.Fingers[rowPosition].Value.Id, includeLeft: true)) {
				ownerNode.Fingers[rowPosition] = new KeyValuePair<long,Node>(ownerNode.Fingers[rowPosition].Key, newNode);
				UpdateFingerTableRow(ownerNode.Predecessor, newNode, rowPosition);
		}		
			}*/