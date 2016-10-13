using Channels;
using Channels.Text;
using Channels.Text.Primitives;
using Channels.Networking.Libuv;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestBinServer
{
	public class Program
	{
		static Random r = new Random();
		static int port = 0;
		static int connectionCounter = 0;
		static long receiveCounter = 0;
		static long sendCounter = 0;

		public static void Main(string[] args)
		{
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			var builder = new ConfigurationBuilder();
			builder.SetBasePath(System.IO.Directory.GetCurrentDirectory());
			builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			IConfiguration config = builder.Build();
			port = int.Parse(config["port"]);
			Task.Factory.StartNew(Stats);
			RunServerForNode();
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Console.WriteLine(e.Exception);
		}

		private async static void Stats()
		{
			while (true)
			{
				Console.Title = $"CONNCOUNTER {connectionCounter} RCVCOUNTER {Interlocked.Exchange(ref receiveCounter, 0)} SNDCOUNTER {Interlocked.Exchange(ref sendCounter, 0)}";
				await Task.Delay(1000);
			}
		}

		private static void RunServerForNode()
		{
			UvThread uvThread = new UvThread();
			var ip = IPAddress.Any;
			UvTcpListener listener = new UvTcpListener(uvThread, new IPEndPoint(ip, port));
			listener.OnConnection(async connection =>
			{
				Interlocked.Increment(ref connectionCounter);
				var input = connection.Input;
				var output = connection.Output;
				var flag = false;

				//Used for stop sending info to connected client.
				await Task.Factory.StartNew(async () =>
				{
					//Wait for client disconnection.
					var result = await input.ReadAsync();
					flag = true;
				});

				while (!flag)
				{
					try
					{
						WritableBuffer oBuffer = output.Alloc();
						oBuffer.WriteUtf8String(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:ms"));
						await oBuffer.FlushAsync();
						Interlocked.Increment(ref sendCounter);
						await Task.Delay(r.Next(0, 500));
					}
					catch (Exception e)
					{
						break;
					}
				}
			});

			listener.Start();

			var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
			Console.WriteLine($"Listening on {ip} on port {port} / PID {pid}");
			Console.ReadKey();

			listener.Stop();
			uvThread.Dispose();
		}
	}
}
