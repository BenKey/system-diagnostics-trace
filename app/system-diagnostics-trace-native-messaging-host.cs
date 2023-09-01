/*
Copyright 2023 Ben Key

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

  https://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum LogLevel
{
    logError,
    logWarning,
    logInfo,
    logDebug,
    logVerbose
}

public sealed class StringToLogLevelConverter: JsonConverter<LogLevel>
{
    public override LogLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (Enum.TryParse(reader.GetString(), out LogLevel value))
        {
            return value;
        }
        return LogLevel.logWarning;
    }

    public override void Write(Utf8JsonWriter writer, LogLevel value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}

public sealed class TraceMessage
{
    public string? Command { get; set; }
    public string? Message { get; set; }
    public string? Source { get; set; }
    public string? Context { get; set; }
    [JsonConverter(typeof(StringToLogLevelConverter))]
    public LogLevel? Level { get; set; }
}

public sealed class TraceStatus
{
    public string? Status { get; set; }
}

public class TraceNativeMessagingHost
{
    private static readonly Stream stdin = Console.OpenStandardInput();
    private static readonly Stream stdout = Console.OpenStandardOutput();

    public static void Main(string[] args)
    {
        TraceMessage? message;
        while ((message = Read()) != null)
        {
            string results = ProcessMessage(message);
            if (string.IsNullOrEmpty(results))
            {
                continue;
            }
            Write(results);
            if (results == "exit")
            {
                return;
            }
        }
    }

    private static TraceMessage? Read()
    {
        var messageLengthBuffer = new byte[4];
        int bytesRead = stdin.Read(messageLengthBuffer, 0, 4);
        if (bytesRead == 0)
        {
            return null;
        }
        int messageLength = BitConverter.ToInt32(messageLengthBuffer, 0);
        var messageBuffer = new byte[messageLength];
        stdin.Read(messageBuffer, 0, messageLength);
        TraceMessage? message = null;
        try
        {
            message = JsonSerializer.Deserialize<TraceMessage>(messageBuffer);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"JsonSerializer.Deserialize failed with error '{ex.Message}'.");
            Trace.WriteLine(GetString(messageBuffer));
        }
        return message;
    }

    private static string ProcessMessage(TraceMessage? message)
    {
        if (message == null || string.IsNullOrEmpty(message.Command))
        {
            return "exit";
        }
        switch (message.Command)
        {
        case "exit":
            return "exit";
        case "trace":
        {
            if (string.IsNullOrEmpty(message.Message))
            {
                return "exit";
            }
            if (!ShouldProcessMessage(message))
            {
                return "filtered";
            }
            if (!string.IsNullOrEmpty(message.Context))
            {
                Trace.WriteLine(message.Context, message.Message);
                return "processed";
            }
            Trace.WriteLine(message.Message);
            return "processed";
        }
        default:
            return "exit";
        }
    }

    private static void Write(string statusMessage)
    {
        TraceStatus statusObject = new()
        {
            Status = statusMessage
        };
        string statusJSON = JsonSerializer.Serialize(statusObject);
        var bytes = Encoding.UTF8.GetBytes(statusJSON);
        stdout.WriteByte((byte)((bytes.Length >> 0) & 0xFF));
        stdout.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
        stdout.WriteByte((byte)((bytes.Length >> 16) & 0xFF));
        stdout.WriteByte((byte)((bytes.Length >> 24) & 0xFF));
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();
    }

    private static string GetString(byte[] arr)
    {
        return Encoding.UTF8.GetString(arr, 0, arr.Length);
    }

    private static bool ShouldProcessMessage(TraceMessage message)
    {
        if (message.Level == null)
        {
            return true;
        }
        /* TO DO: Implement filtering based on a settings file. For now all messages are processed. */
        return true;
    }
}
