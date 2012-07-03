using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NReco;
using NReco.Logging;

namespace Koneko.P2P.Chord {
	public class LocalInstanceMaintenanceService {
		private static ILog Log = LogManager.GetLogger(typeof(LocalInstanceMaintenanceService));

		public LocalInstance LocalInstance { get; set; }
		public TaskFactory TaskFactory { get; set; }
		public RemoteServicesCache<INodeService> NodeServices { get; set; }

		private CancellationTokenSource Cancellation { get; set; }
		private IList<Task> RunningTasks { get; set; }

		public int StabilizeIdleTimeMs { get; set; }
		public int StabilizeSuccessorCacheIdleTimeMs { get; set; }
		public int FixFingersIdleTimeMs { get; set; }

		public MaintenanceStatus Status { get; set; }

		private LocalNodeDescriptor LocalNode {
			get { return LocalInstance.LocalNode; }
		}

		public LocalInstanceMaintenanceService(LocalInstance l) {
			StabilizeIdleTimeMs = 3000;
			StabilizeSuccessorCacheIdleTimeMs = 3000;
			FixFingersIdleTimeMs = 3000;

			// initally stopped
			Status = MaintenanceStatus.Stopped;

			RunningTasks = new List<Task>();
			LocalInstance = l;
			TaskFactory = new TaskFactory(TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
		}

		public void Start() {
			// reset 
			Stop();
			Cancellation = new CancellationTokenSource();

			var stabilizeTask = TaskFactory.StartNew(
					() => {
						var fingerRowIdxToFix = 0;
						while (!Cancellation.IsCancellationRequested) {
							var argsStab = new BackgroundTaskArgs { IdleTimeMs = StabilizeIdleTimeMs };
							var argsStabSc = new BackgroundTaskArgs { IdleTimeMs = StabilizeSuccessorCacheIdleTimeMs };
							var argsFf = new BackgroundTaskArgs { IdleTimeMs = FixFingersIdleTimeMs };

							Stabilize(argsStab);
							StabilizeSuccessorsCache(argsStabSc);
							FixFingers(argsFf, fingerRowIdxToFix);

							fingerRowIdxToFix = fingerRowIdxToFix == LocalInstance.RingLength - 1 ? 0 : fingerRowIdxToFix + 1;
							Thread.Sleep(Math.Min(argsStab.IdleTimeMs, argsStabSc.IdleTimeMs));
						}
					},
					Cancellation.Token
				);
			RunningTasks.Add(stabilizeTask);

			Status = MaintenanceStatus.Started;
		}

		public void Stop() {
			if (Cancellation != null) {
				Cancellation.Cancel();
				foreach (Task t in RunningTasks) {
					t.Wait();
				}
				RunningTasks.Clear();
			}
		}

		private void Stabilize(BackgroundTaskArgs args) {
			Log.Write(LogEvent.Debug, "Calling stabilize for node {0}", LocalNode.Endpoint);
			var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);

			NodeDescriptor succPredecessor = null;
			try {
				succPredecessor = successorNodeSrv.Service.GetNodePredecessor();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilization because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					// part of stabilize routine: if node successor has failed, replace it with the next successor from the successor cache
					if (!SetSuccessorFromCache()) {
						// either all nodes are failed or only local nodes are available - something wrong, we need to try rejoining
						SetRejoinRequested();
					} else {
						// repeat stabilize immediately
						args.IdleTimeMs = 0;
						return;
					}
				} else {
					throw;
				}
			}

			if (succPredecessor != null) {
				// include right?
				if (TopologyHelper.IsInCircularInterval(succPredecessor.Id, LocalNode.Id, LocalNode.Successor.Id)) {
					Log.Write(LogEvent.Debug, "Successor for node {0} changed from {1} to {2} during stabilization", LocalNode.Endpoint, LocalNode.Successor, succPredecessor);
					
					var previousSuccessor = LocalNode.Successor;
					LocalNode.Successor = succPredecessor;

					if (!previousSuccessor.Equals(LocalNode.Successor)) {
						// get the service for changed successor
						successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
					}
				}
			}

			try {
				successorNodeSrv.Service.FixPredecessor(LocalNode.Endpoint);
				Log.Write(LogEvent.Debug, "Finished stabilizing for node {0}", LocalNode.Endpoint);
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilization fix predecessor because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					// part of stabilize routine: if node successor has failed, replace it with the next successor from the successor cache
					if (!SetSuccessorFromCache()) {
						// either all nodes are failed or only local nodes are available - something wrong, we need to try rejoining
						SetRejoinRequested();
					} else {
						// repeat stabilize immediately
						args.IdleTimeMs = 0;
						return;
					}
				} else {
					throw;
				}
			}
		}

		private void StabilizeSuccessorsCache(BackgroundTaskArgs args) {
			Log.Write(LogEvent.Debug, "Calling stabilize successor cache for node {0}", LocalNode.Endpoint);

			var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
			NodeDescriptor successorNodeSuccessor = null;
			NodeDescriptor[] successorNodeSuccessorCache = null;

			try {
				// first entry in my successor cache -> the successor of my successor
				successorNodeSuccessor = successorNodeSrv.Service.GetNodeSuccessor();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilize successor cache because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					// part of stabilize routine: if node successor has failed, replace it with the next successor from the successor cache
					if (!SetSuccessorFromCache()) {
						// either all nodes are failed or only local nodes are available - something wrong, we need to try rejoining
						SetRejoinRequested();
					} else {
						// repeat stabilize immediately
						args.IdleTimeMs = 0;
						return;
					}
				} else {
					throw;
				}
			}

			try {
				// 2 to n entries in my successor cache -> 1 to n-1 entries in my successor's successor cache
				successorNodeSuccessorCache = successorNodeSrv.Service.GetNodeSuccessorCache();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot get successor successor cache because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					// part of stabilize routine: if node successor has failed, replace it with the next successor from the successor cache
					if (!SetSuccessorFromCache()) {
						// either all nodes are failed or only local nodes are available - something wrong, we need to try rejoining
						SetRejoinRequested();
					} else {
						// repeat stabilize immediately
						args.IdleTimeMs = 0;
						return;
					}
				} else {
					throw;
				}
			}
			
			// everything is ok, so we can stabilize successors cache
			for (var i = 0; i < LocalNode.SuccessorCacheSize; ++i) {
				if (i == 0) {
					if (successorNodeSuccessor != null) {
						LocalNode.SuccessorCache[i] = successorNodeSuccessor;
					}
				} else {
					if (successorNodeSuccessorCache != null && successorNodeSuccessorCache[i-1] != null) {
						LocalNode.SuccessorCache[i] = successorNodeSuccessorCache[i-1];
					}
				}
			}
			Log.Write(LogEvent.Debug, "Finished stabilizing successor cache for node {0}", LocalNode.Endpoint);
		}

		private void FixFingers(BackgroundTaskArgs args, int? fingerRowIdx = null) {
			var actualFingerRowIdx = fingerRowIdx.HasValue 
										? (fingerRowIdx.Value > LocalInstance.RingLength - 1 
												? 0
												: fingerRowIdx.Value 
											)
										: LocalNode.GetRandomFingerTableIndex();
			Log.Write(LogEvent.Debug, "Calling fix fingers for node {0}, finger row position {1}", LocalNode.Endpoint, actualFingerRowIdx);
			
			var successorForFinger = LocalInstance.FindSuccessorForId(LocalNode.Fingers[actualFingerRowIdx].Key);
			LocalNode.Fingers[actualFingerRowIdx] = new KeyValuePair<ulong, NodeDescriptor>(TopologyHelper.GetFingerTableKey(LocalNode.Id, actualFingerRowIdx, LocalInstance.RingLength), successorForFinger);

			Log.Write(LogEvent.Debug, "Finished fixing fingers for node {0}, finger row position {1}", LocalNode.Endpoint, actualFingerRowIdx);
		}

		private bool SetSuccessorFromCache() {
			NodeDescriptor lastSuccessfulEntry = null;
			int checkedSuccCacheEntries = 0;

			// no one must access successor because we are trying to fix it
			lock (LocalNode.SuccessorLockObject) {
				for (var i = 0; i < LocalNode.SuccessorCache.Length; ++i, ++checkedSuccCacheEntries) {
					var currEntry = LocalNode.SuccessorCache[i];
					if (!currEntry.Equals(LocalNode.Endpoint)) {
						var currEntrySrv = NodeServices.GetRemoteNodeService(currEntry);
						try {
							currEntrySrv.Service.Ping();
						} catch (Exception ex) {
							// if remote service is no good -> proceed to the next successor
							if (currEntrySrv.IsUnavailable) {
								Log.Write(
									LogEvent.Debug, 
									"Trying to set successor {0} for node {1} from successor cache has failed, continuing to the next successor cache entry. Error details: \r\n {2}", 
									currEntry,
									LocalNode.Endpoint, 
									ex
								);
								continue;
							} else {
								throw;
							}
						}
					}
				}
				// if there were some good successor from the cache -> set it as successor
				if (lastSuccessfulEntry != null) {
					LocalNode.Successor = lastSuccessfulEntry;
					return true;
				}
			}

			return false;
		}

		private void SetRejoinRequested() {
			// cancel own threads
			Cancellation.Cancel();

			// set state
			Status = MaintenanceStatus.Stopped;

			LocalInstance.SignalEvent(LocalInstanceEvent.RejoinRequested);
		}

		private class BackgroundTaskArgs {
			public int IdleTimeMs { get; set; }
		}
	}

	public enum MaintenanceStatus {
		WaitingForStart, Stopped, Started
	}
}
