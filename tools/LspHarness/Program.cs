using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

if (args.Length == 0)
{
	PrintUsage();
	return 1;
}

List<string> argList = new(args);
string? rootPath = null;
for (int i = 0; i < argList.Count; i++)
{
	if (string.Equals(argList[i], "--root", StringComparison.OrdinalIgnoreCase) && i + 1 < argList.Count)
	{
		rootPath = argList[i + 1];
		argList.RemoveAt(i + 1);
		argList.RemoveAt(i);
		i--;
	}
}

if (argList.Count == 0)
{
	PrintUsage();
	return 1;
}

string serverPath = argList[0];
string[] serverArgs = argList.Count > 1 ? argList.GetRange(1, argList.Count - 1).ToArray() : Array.Empty<string>();

Uri? rootUri = null;
if (!string.IsNullOrWhiteSpace(rootPath))
{
	rootUri = new Uri(Path.GetFullPath(rootPath));
}

using Process process = StartServer(serverPath, serverArgs);
using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

Task stderrPump = PumpStandardErrorAsync(process, cts.Token);

LspClient client = new(process.StandardInput.BaseStream, process.StandardOutput.BaseStream);

Console.WriteLine("Initializing LSP server...");
JsonDocument initializeResult = await client.SendRequestAsync(
	"initialize",
	new
	{
		processId = Environment.ProcessId,
		rootUri = rootUri?.AbsoluteUri,
		capabilities = new { },
		clientInfo = new { name = "XamlVisualEditor.LspHarness", version = "0.1" }
	},
	cts.Token);

Console.WriteLine("Initialize response received.");
await client.SendNotificationAsync("initialized", new { }, cts.Token);

Console.WriteLine("Requesting shutdown...");
await client.SendRequestAsync("shutdown", new { }, cts.Token);
await client.SendNotificationAsync("exit", new { }, cts.Token);

Console.WriteLine("LSP handshake completed.");
return 0;

static void PrintUsage()
{
	Console.WriteLine("Usage:");
	Console.WriteLine("  dotnet run --project tools/LspHarness -- <serverPath> [serverArgs...] [--root <path>]");
}

static Process StartServer(string serverPath, string[] serverArgs)
{
	ProcessStartInfo startInfo = new()
	{
		FileName = serverPath,
		RedirectStandardInput = true,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false
	};

	foreach (string arg in serverArgs)
	{
		startInfo.ArgumentList.Add(arg);
	}

	Process process = new()
	{
		StartInfo = startInfo
	};

	if (!process.Start())
	{
		throw new InvalidOperationException("Failed to start LSP server process.");
	}

	return process;
}

static async Task PumpStandardErrorAsync(Process process, CancellationToken ct)
{
	using StreamReader reader = process.StandardError;
	while (!ct.IsCancellationRequested)
	{
		string? line = await reader.ReadLineAsync(ct);
		if (line is null)
		{
			break;
		}

		Console.WriteLine($"[server] {line}");
	}
}

sealed class LspClient
{
	private readonly Stream _output;
	private readonly Stream _input;
	private int _nextId;

	public LspClient(Stream output, Stream input)
	{
		_output = output;
		_input = input;
	}

	public async Task<JsonDocument> SendRequestAsync(string method, object @params, CancellationToken ct)
	{
		int id = Interlocked.Increment(ref _nextId);
		await SendMessageAsync(new
		{
			jsonrpc = "2.0",
			id,
			method,
			@params
		}, ct);

		return await WaitForResponseAsync(id, ct);
	}

	public Task SendNotificationAsync(string method, object @params, CancellationToken ct)
	{
		return SendMessageAsync(new
		{
			jsonrpc = "2.0",
			method,
			@params
		}, ct);
	}

	private async Task<JsonDocument> WaitForResponseAsync(int id, CancellationToken ct)
	{
		string expectedId = id.ToString(CultureInfo.InvariantCulture);

		while (true)
		{
			JsonDocument message = await ReadMessageAsync(ct);
			if (!message.RootElement.TryGetProperty("id", out JsonElement idElement))
			{
				continue;
			}

			string? messageId = idElement.ValueKind switch
			{
				JsonValueKind.Number => idElement.GetInt32().ToString(CultureInfo.InvariantCulture),
				JsonValueKind.String => idElement.GetString(),
				_ => null
			};

			if (!string.Equals(messageId, expectedId, StringComparison.Ordinal))
			{
				continue;
			}

			if (message.RootElement.TryGetProperty("error", out JsonElement error))
			{
				Console.WriteLine($"LSP error: {error}");
			}

			return message;
		}
	}

	private async Task SendMessageAsync(object payload, CancellationToken ct)
	{
		byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		string header = $"Content-Length: {body.Length}\r\n\r\n";
		byte[] headerBytes = Encoding.ASCII.GetBytes(header);

		await _output.WriteAsync(headerBytes, ct);
		await _output.WriteAsync(body, ct);
		await _output.FlushAsync(ct);
	}

	private async Task<JsonDocument> ReadMessageAsync(CancellationToken ct)
	{
		int contentLength = await ReadContentLengthAsync(ct);
		byte[] body = new byte[contentLength];
		await ReadExactAsync(_input, body, ct);
		return JsonDocument.Parse(body);
	}

	private async Task<int> ReadContentLengthAsync(CancellationToken ct)
	{
		List<byte> headerBytes = new();
		int matchState = 0;
		byte[] buffer = new byte[1];

		while (matchState < 4)
		{
			int read = await _input.ReadAsync(buffer, 0, 1, ct);
			if (read == 0)
			{
				throw new EndOfStreamException("Unexpected end of stream while reading LSP header.");
			}

			byte value = buffer[0];
			headerBytes.Add(value);

			matchState = value switch
			{
				(byte)'\r' when matchState == 0 || matchState == 2 => matchState + 1,
				(byte)'\n' when matchState == 1 || matchState == 3 => matchState + 1,
				_ => 0
			};
		}

		string headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
		using StringReader reader = new(headerText);
		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
			{
				string value = line["Content-Length:".Length..].Trim();
				return int.Parse(value, CultureInfo.InvariantCulture);
			}
		}

		throw new InvalidOperationException("Content-Length header not found.");
	}

	private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
	{
		int offset = 0;
		while (offset < buffer.Length)
		{
			int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
			if (read == 0)
			{
				throw new EndOfStreamException("Unexpected end of stream while reading LSP body.");
			}

			offset += read;
		}
	}
}
