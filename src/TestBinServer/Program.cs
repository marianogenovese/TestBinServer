using Channels;
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
			var x = uvThread.ChannelFactory.CreateChannel();
			var ip = IPAddress.Any;
			UvTcpListener listener = new UvTcpListener(uvThread, new IPEndPoint(ip, port));
			listener.OnConnection(async connection =>
			{
				Interlocked.Increment(ref connectionCounter);
				var input = connection.Input;
				var output = connection.Output;

				while (true)
				{

					try
					{
						if (input.Completion.IsCompleted || output.Completion.IsCompleted)
						{
							// We're done with this connection
							break;
						}

						var binDate = Encoding.UTF8.GetBytes(DateTime.Now.ToString());
						await output.WriteAsync(binDate, 0, binDate.Length);
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

		private static void RunServerForTSP()
		{
			UvThread uvThread = new UvThread();
			var x = uvThread.ChannelFactory.CreateChannel();
			var ip = IPAddress.Any;
			UvTcpListener listener = new UvTcpListener(uvThread, new IPEndPoint(ip, port));
			listener.OnConnection(async connection =>
			{
				Interlocked.Increment(ref connectionCounter);
				var input = connection.Input;

				while (true)
				{
					ReadableBuffer inputBuffer;

					try
					{
						// Wait for data
						inputBuffer = await input.ReadAsync();
					}
					catch (Exception ex)
					{
						var eee = ex;
						return;
					}


					if (inputBuffer.IsEmpty && input.Completion.IsCompleted)
					{
						// We're done with this connection
						break;
					}

					// Get the buffer
					var lenghtBuffer = inputBuffer.Slice(0, 4);
					var length = ReadLength(lenghtBuffer);

					if (length > 0)
					{
						Interlocked.Increment(ref receiveCounter);
						var messageBuffer = inputBuffer.Slice(4, length);
						// Copy from the file channel to the console channel
						var outputbuffer = connection.Output.Alloc();
						//outputbuffer.Append(ref lenghtBuffer);
						//outputbuffer.Append(ref messageBuffer);
						outputbuffer.Write(lenghtBuffer.FirstSpan.Array, lenghtBuffer.FirstSpan.Offset, lenghtBuffer.FirstSpan.Length);
						outputbuffer.Write(messageBuffer.FirstSpan.Array, messageBuffer.FirstSpan.Offset, messageBuffer.FirstSpan.Length);
						await outputbuffer.FlushAsync();
						Interlocked.Increment(ref sendCounter);
						//connection.Output.CompleteWriting();
					}

					inputBuffer.Consumed();
				}
			});

			listener.Start();

			var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
			Console.WriteLine($"Listening on {ip} on port {port} / PID {pid}");
			Console.ReadKey();

			listener.Stop();
			uvThread.Dispose();
		}

		private unsafe static int ReadLength(ReadableBuffer buffer)
		{
			return ((((byte*)buffer.FirstSpan.BufferPtr)[0] & 0xFF) << 8) | (((byte*)buffer.FirstSpan.BufferPtr)[1] & 0xFF);
		}
	}
}
