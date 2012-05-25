using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Koneko.Common {
	public static class NetworkHelper {
		public static string GetLocalIpAddress() {
			var host = Dns.GetHostEntry(Dns.GetHostName());
			var result = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
			return result != null ? result.ToString() : "127.0.0.1";
		}
	}
}
