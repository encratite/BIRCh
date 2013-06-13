using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BIRCh
{
	public class Client
	{
		const int BufferSize = 0x1000;
		const string EndOfLine = "\r\n";

		IPEndPoint ServerEndpoint;
		TcpClient IRCClient;

		public void Run()
		{
			while (true)
			{
				Connect();
				ReadLines();
			}
		}

		protected Client(IPEndPoint serverEndpoint)
		{
			ServerEndpoint = serverEndpoint;
		}

		void Connect()
		{
			IRCClient = new TcpClient();
			IRCClient.Connect(ServerEndpoint);
		}

		void Send(string line)
		{
			byte[] buffer = Encoding.UTF8.GetBytes(line + EndOfLine);
			IRCClient.GetStream().Write(buffer, 0, buffer.Length);
		}

		void ReadLines()
		{
			MemoryStream stream = new MemoryStream();
			while (IRCClient.Connected)
			{
				byte[] buffer = new byte[BufferSize];
				int bytesRead = IRCClient.GetStream().Read(buffer, 0, buffer.Length);
				stream.Write(buffer, 0, bytesRead);
				string asciiString = Encoding.ASCII.GetString(stream.GetBuffer());
				int offset = asciiString.IndexOf(EndOfLine);
				if (offset != -1)
				{
					byte[] streamBuffer = stream.GetBuffer();
					string unicodeString = Encoding.UTF8.GetString(streamBuffer, 0, offset);
					ParseLine(unicodeString);
					stream = new MemoryStream();
					stream.Write(streamBuffer, EndOfLine.Length, streamBuffer.Length - EndOfLine.Length);
				}
			}
		}

		void ParseLine(string line)
		{
			string[] tokens = line.Split(' ');
			if (tokens.Length < 2)
				throw new Exception("Malformed line");
			string command = tokens[0];
			switch (command)
			{
				case "PING":
					string reply = "PONG " + line.Substring(5);
					Send(reply);
					break;

				case "376":
				case "422":
					OnEndOfMotd();
					break;

				case "433":
				case "437":
					OnNickInUse();
					break;

				case "NOTICE":
					OnNotice();
					break;
			}
		}

		void OnEndOfMotd()
		{
		}

		void OnNickInUse()
		{
		}

		void OnNotice()
		{
		}
	}
}
