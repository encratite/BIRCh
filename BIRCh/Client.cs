using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace BIRCh
{
	public class Client
	{
		const int BufferSize = 0x1000;
		const string EndOfLine = "\r\n";

		delegate void MatchHandler(Match match);

		protected string User;
		protected string Localhost;
		protected string Host;
		protected string RealName;

		protected string ActualNick;

		IPEndPoint ServerEndpoint;
		TcpClient IRCClient;

		Dictionary<string, MatchHandler> MatchHandlers;

		public void Run()
		{
			while (true)
			{
				Connect();
				ReadLines();
			}
		}

		protected Client(IPEndPoint serverEndpoint, string user, string localhost, string host, string realName)
		{
			User = user;
			Localhost = localhost;
			Host = host;
			RealName = realName;
			ServerEndpoint = serverEndpoint;
			SetMatchHandlers();
		}

		#region Default event handlers

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
			Send(string.Format("USER {0} \"{1}\" \"{2}\" \"{3}\"", user, localhost, host, realName));
		}

		void SetMatchHandlers()
		{
			MatchHandlers = new Dictionary<string, MatchHandler>();
			MatchHandlers["^PING (.+?)$"] = OnPingMatch;
			MatchHandlers["^([^ ]+?) 376 ([^ ]+?) :?(.+?)$"] = OnEndOfMotdMatch;
			MatchHandlers["^([^ ]+?) 422 ([^ ]+?) :?(.+?)$"] = OnEndOfMotdMatch;
			MatchHandlers["^([^ ]+?) 433 (.+?)$"] = OnNickInUseMatch;
			MatchHandlers["^([^ ]+?) 437 (.+?)$"] = OnNickInUseMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) NOTICE ([^ ]+?) :?(.+?)$"] = OnNoticeMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) INVITE ([^ ]+?) :?(.+?)$"] = OnInviteMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) JOIN ([^ ]+?)$"] = OnJoinMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) PRIVMSG ([^ ]+?) :?(.+?)$"] = OnMessageMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) MODE ([^ ]+?) :?(.+?)$"] = OnModeMatch;
			MatchHandlers["^([^ ]+?)!([^ ]+?)@([^ ]+?) QUIT :?(.+?)$"] = OnQuitMatch;
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
			foreach (var pair in MatchHandlers)
			{
				Match match = Regex.Match(line, pair.Key);
				if (match != null)
				{
					pair.Value(match);
					break;
				}
			}
		}

		User GetUser(Match match)
		{
			return new User(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
		}

		#region Match handlers

		void OnPingMatch(Match match)
		{
			string reply = "PONG " + match.Groups[1].Value;
			Send(reply);
		}

		void OnEndOfMotdMatch(Match match)
		{
			ActualNick = match.Groups[2].Value;
			OnEntry();
		}

		void OnNickInUseMatch(Match match)
		{
			throw new Exception("Nick in use");
		}

		void OnNoticeMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string message = match.Groups[5].Value;
			OnNotice(user, target, message);
		}

		void OnInviteMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string channel = match.Groups[5].Value;
			OnInvite(user, channel);
		}

		void OnJoinMatch(Match match)
		{
			User user = GetUser(match);
			string channel = match.Groups[4].Value;
			OnJoin(user, channel);
		}

		void OnMessageMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string message = match.Groups[5].Value;
			OnMessage(user, target, message);
		}

		void OnModeMatch(Match match)
		{
			User user = GetUser(match);
			string target = match.Groups[4].Value;
			string modes = match.Groups[5].Value;
			OnMode(user, target, modes);
		}

		void OnQuitMatch(Match match)
		{
			User user = GetUser(match);
			string message = match.Groups[4].Value;
			OnQuit(user, message);
		}

		#endregion
	}
}
