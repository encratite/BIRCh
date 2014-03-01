using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BIRCh
{
	public class Client
	{
		private const int BufferSize = 0x1000;
		private const string EndOfLine = "\r\n";
		private const int ConnectionTimeout = 60 * 60 * 1000;

		private delegate void MatchHandler(Match match);

		public string Nick;
		public string User;
		public string Localhost;
		public string Host;
		public string RealName;

		protected string ActualNick;

		private TcpClient IRCClient;

		private Dictionary<string, MatchHandler> MatchHandlers;

		public void Run(IPEndPoint serverEndpoint)
		{
			while (true)
			{
				try
				{
					Connect(serverEndpoint);
					ReadLines();
				}
				catch (Exception exception)
				{
					OnDisconnect(exception);
				}
			}
		}

		protected Client()
		{
			Nick = "BIRCh";
			User = "BIRCh";
			Localhost = "0";
			Host = "*";
			RealName = "BIRCh";
			SetMatchHandlers();
		}

		#region Default event handlers

		protected virtual void OnConnect()
		{
		}

		protected virtual void OnDisconnect(Exception exception)
		{
		}

		protected virtual void OnReceive(string line)
		{
		}

		protected virtual void OnSend(string line)
		{
		}

		protected virtual void OnEntry()
		{
		}

		protected virtual void OnNotice(User user, string target, string message)
		{
		}

		protected virtual void OnInvite(User user, string channel)
		{
		}

		protected virtual void OnJoin(User user, string channel)
		{
		}

		protected virtual void OnMessage(User user, string target, string message)
		{
		}

		protected virtual void OnMode(User user, string target, string modes)
		{
		}

		protected virtual void OnQuit(User user, string message)
		{
		}

		#endregion

		protected void SendMessage(string target, string message)
		{
			Send(string.Format("PRIVMSG {0} :{1}", target, message));
		}

		protected void SendNotice(string target, string message)
		{
			Send(string.Format("NOTICE {0} :{1}", target, message));
		}

		protected void JoinChannel(string channel)
		{
			Send(string.Format("JOIN {0}", channel));
		}

		protected void ChangeNick(string nick)
		{
			Send(string.Format("NICK {0}", nick));
		}

		protected void SetUser(string user, string localhost, string host, string realName)
		{
			Send(string.Format("USER {0} {1} {2} :{3}", user, localhost, host, realName));
		}

		private void SetMatchHandlers()
		{
			MatchHandlers = new Dictionary<string, MatchHandler>();
			MatchHandlers["^PING (.+?)$"] = OnPingMatch;
			MatchHandlers["^([^ ]+?) 376 ([^ ]+?) :?(.+?)$"] = OnEndOfMotdMatch;
			MatchHandlers["^([^ ]+?) 422 ([^ ]+?) :?(.+?)$"] = OnEndOfMotdMatch;
			MatchHandlers["^([^ ]+?) 433 (.+?)$"] = OnNickInUseMatch;
			MatchHandlers["^([^ ]+?) 437 (.+?)$"] = OnNickInUseMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) NOTICE ([^ ]+?) :?(.+?)$"] = OnNoticeMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) INVITE ([^ ]+?) :?(.+?)$"] = OnInviteMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) JOIN ([^ ]+?)$"] = OnJoinMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) PRIVMSG ([^ ]+?) :?(.+?)$"] = OnMessageMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) MODE ([^ ]+?) :?(.+?)$"] = OnModeMatch;
			MatchHandlers["^:?([^ ]+?)!([^ ]+?)@([^ ]+?) QUIT :?(.+?)$"] = OnQuitMatch;
		}

		private void Connect(IPEndPoint serverEndpoint)
		{
			IRCClient = new TcpClient();
			IRCClient.Connect(serverEndpoint);
			OnConnect();
			// Send("CAP LS");
			ChangeNick(Nick);
			SetUser(User, Localhost, Host, RealName);
		}

		private void Send(string line)
		{
			OnSend(line);
			byte[] buffer = Encoding.UTF8.GetBytes(line + EndOfLine);
			IRCClient.GetStream().Write(buffer, 0, buffer.Length);
		}

		void ReadLines()
		{
			MemoryStream stream = new MemoryStream();
			while (IRCClient.Connected)
			{
				byte[] buffer = new byte[BufferSize];
				var tokenSource = new CancellationTokenSource();
				int? bytesRead = null;
				var task = Task.Factory.StartNew
				(
					(x) => bytesRead = IRCClient.GetStream().Read(buffer, 0, buffer.Length),
					tokenSource
				);
				if (!task.Wait(ConnectionTimeout, tokenSource.Token))
					throw new ApplicationException("Connection timeout");
				if (bytesRead == 0)
				{
					OnDisconnect(null);
					return;
				}
				stream.Write(buffer, 0, bytesRead.Value);
				while(true)
				{
					byte[] streamBuffer = stream.GetBuffer();
					string asciiString = Encoding.ASCII.GetString(streamBuffer);
					int offset = asciiString.IndexOf(EndOfLine);
					if (offset == -1)
						break;
					string unicodeString = Encoding.UTF8.GetString(streamBuffer, 0, offset);
					ParseLine(unicodeString);
					int oldStreamLength = (int)stream.Length;
					stream = new MemoryStream();
					int nextLineOffset = offset + EndOfLine.Length;
					string restString = Encoding.UTF8.GetString(streamBuffer, nextLineOffset, oldStreamLength - nextLineOffset);
					stream.Write(streamBuffer, nextLineOffset, oldStreamLength - nextLineOffset);
				}
			}
		}

		private void ParseLine(string line)
		{
			OnReceive(line);
			foreach (var pair in MatchHandlers)
			{
				Match match = Regex.Match(line, pair.Key);
				if (match.Success)
				{
					pair.Value(match);
					break;
				}
			}
		}

		private User GetUser(Match match)
		{
			return new User(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
		}

		#region Match handlers

		private void OnPingMatch(Match match)
		{
			string reply = "PONG " + match.Groups[1].Value;
			Send(reply);
			// Send("CAP END");
		}

		private void OnEndOfMotdMatch(Match match)
		{
			ActualNick = match.Groups[2].Value;
			OnEntry();
		}

		private void OnNickInUseMatch(Match match)
		{
			throw new Exception("Nick in use");
		}

		private void OnNoticeMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string message = match.Groups[5].Value;
			OnNotice(user, target, message);
		}

		private void OnInviteMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string channel = match.Groups[5].Value;
			OnInvite(user, channel);
		}

		private void OnJoinMatch(Match match)
		{
			User user = GetUser(match);
			string channel = match.Groups[4].Value;
			OnJoin(user, channel);
		}

		private void OnMessageMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string message = match.Groups[5].Value;
			OnMessage(user, target, message);
		}

		private void OnModeMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string modes = match.Groups[5].Value;
			OnMode(user, target, modes);
		}

		private void OnQuitMatch(Match match)
		{
			User user = GetUser(match);
			string message = match.Groups[4].Value;
			OnQuit(user, message);
		}

		#endregion
	}
}
