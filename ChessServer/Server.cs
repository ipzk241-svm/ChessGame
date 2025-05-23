﻿using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ChessServer
{
	public class Server
	{
		private readonly Logger logger;
		private TcpListener listener;
		private ConcurrentQueue<TcpClient> waitingClients = new();
		private bool isRunning = true;

		public Server(Logger logger)
		{
			this.logger = logger;
		}

		public void Start(int port)
		{
			listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			logger.Info($"Server started on port {port}.");

			Task.Run(() => MonitorWaitingClients());

			try
			{
				while (isRunning)
				{
					TcpClient client = listener.AcceptTcpClient();
					logger.Info("New client connected.");
					waitingClients.Enqueue(client);
					TryStartGame();
				}
			}
			catch (SocketException) when (!isRunning)
			{
				logger.Info("Server stopped.");
			}
		}

		public void Stop()
		{
			isRunning = false;
			listener.Stop();
			logger.Info("Server stopped manually.");
		}

		private void TryStartGame()
		{
			while (waitingClients.Count >= 2)
			{
				if (waitingClients.TryDequeue(out var client1) &&
					waitingClients.TryDequeue(out var client2))
				{
					if (IsClientConnected(client1) && IsClientConnected(client2))
					{
						logger.Info("Starting new game session.");
						var session = new GameSession(client1, client2, logger);
						Task.Run(() => session.RunAsync());
					}
					else
					{
						logger.Warning("Discarded disconnected clients.");
						CloseClientIfNotNull(client1);
						CloseClientIfNotNull(client2);
					}
				}
			}
		}

		private async Task MonitorWaitingClients()
		{
			while (isRunning)
			{
				await Task.Delay(5000); 

				var newQueue = new ConcurrentQueue<TcpClient>();

				while (waitingClients.TryDequeue(out var client))
				{
					if (IsClientConnected(client))
						newQueue.Enqueue(client);
					else
						CloseClientIfNotNull(client);
				}

				waitingClients = newQueue;
			}
		}

		private bool IsClientConnected(TcpClient client)
		{
			try
			{
				return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
			}
			catch
			{
				return false;
			}
		}

		private void CloseClientIfNotNull(TcpClient client)
		{
			try
			{
				client?.Close();
			}
			catch { }
		}
	}
}
