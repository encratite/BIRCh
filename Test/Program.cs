using System;
using System.Net;

using BIRCh;

namespace Test
{
	class Program
	{
		class TestClient : Client
		{
			protected override void OnConnect()
			{
				Console.WriteLine("Connected");
			}

			protected override void OnDisconnect(Exception exception)
			{
				Console.WriteLine("Disconnected: {0}", exception.Message);
			}

			protected override void OnReceive(string line)
			{
				Console.WriteLine("< {0}", line);
			}

			protected override void OnSend(string line)
			{
				Console.WriteLine("> {0}", line);
			}

			protected override void OnMessage(User user, string target, string message)
			{
				Console.WriteLine("[{0}] <{1}@{2}> {3}", target, user.Nick, user.Host, message);
			}
		}

		static void Main(string[] arguments)
		{
			if (arguments.Length != 5)
				return;
			string server = arguments[0];
			int port = Convert.ToInt32(arguments[1]);
			string nick = arguments[2];
			string user = arguments[3];
			string realName = arguments[4];
			IPAddress[] addresses = Dns.GetHostAddresses(server);
			IPEndPoint endPoint = new IPEndPoint(addresses[0], port);
			TestClient client = new TestClient();
			client.Nick = nick;
			client.User = user;
			client.RealName = realName;
			client.Run(endPoint);
		}
	}
}
