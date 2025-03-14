/*
Copyright 2023 - 2025 Ben Key

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
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace TraceNativeMessagingHost
{
    using ContextEntriesDictionary = Dictionary<string, TraceLevel>;
    public enum TraceLevel
    {
        error,
        warning,
        info,
        debug,
        verbose,
        extremelyVerbose
    }

    public sealed class StringToTraceLevelConverter: JsonConverter<TraceLevel>
    {
        public override TraceLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (Enum.TryParse(reader.GetString(), out TraceLevel value))
            {
                return value;
            }
            return TraceLevel.warning;
        }

        public override void Write(Utf8JsonWriter writer, TraceLevel value, JsonSerializerOptions options)
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
        [JsonConverter(typeof(StringToTraceLevelConverter))]
        public TraceLevel? Level { get; set; }
    }

    public sealed class TraceStatus
    {
        public string? Status { get; set; }
    }

    public sealed class Settings
    {
        public TraceLevel GlobalDefaultTraceLevel { get; set; } = TraceLevel.warning;
        public Dictionary<string, ContextEntriesDictionary>? SourceEntries { get; set; } = null;
    }

    public sealed class ManifestFileData
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string[]? allowed_origins { get; set; } = null;
    }

    public sealed class App
    {
        const string  createRegistryKeyArgument = "create-registry-key";
        const string generateManifestFileArgument = "generate-manifest-file";
        const string installArgument = "install";
        const string testArgument = "test";
        const string unitTestArgument = "unit-test";
        private static readonly Stream stdin = Console.OpenStandardInput();
        private static readonly Stream stdout = Console.OpenStandardOutput();
        private static Settings? settings = null;

        public static void Main(string[] args)
        {
            // Build a config object, using env vars and JSON providers.
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            // Get values from the config given their key and their target type.
            settings = config.GetSection("Settings").Get<Settings>();
            if (ShouldCreateRegistryKey(args))
            {
                CreateRegistryKey();
                return;
            }
            else if (ShouldGenerateManifestFile(args))
            {
                GenerateManifestFile();
                return;
            }
            else if (ShouldInstall(args))
            {
                Install();
                return;
            }
            else if (ShouldRunUnitTests(args))
            {
                RunUnitTests();
                return;
            }
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
                Trace.WriteLine($"JsonSerializer.Deserialize failed with error '{ex}'.", "exception");
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
                    Trace.WriteLine(message.Message, message.Context);
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
            TraceStatus statusObject = new() {
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

        private static TraceLevel GetTraceLevelFromSettings(string? source, string? context)
        {
            if (settings is null)
            {
                return TraceLevel.info;
            }
            if (context is null || source is null || settings.SourceEntries is null)
            {
                return settings.GlobalDefaultTraceLevel;
            }
            ContextEntriesDictionary? contextEntries;
            bool getValue = settings.SourceEntries.TryGetValue(source, out contextEntries);
            if (!getValue || contextEntries is null)
            {
                return settings.GlobalDefaultTraceLevel;
            }
            TraceLevel levelForContext;
            getValue = contextEntries.TryGetValue(context, out levelForContext);
            if (!getValue)
            {
                getValue = contextEntries.TryGetValue("DefaultTraceLevel", out levelForContext);
            }
            if (!getValue)
            {
                return settings.GlobalDefaultTraceLevel;
            }
            return levelForContext;
        }

        private static bool ShouldProcessMessage(TraceMessage message)
        {
            if (message.Level is null)
            {
                return true;
            }
            var levelForContext = GetTraceLevelFromSettings(message.Source, message.Context);
            return (message.Level <= levelForContext);
        }

        private static bool ShouldCreateRegistryKey(string[] args)
        {
            if (args.Length != 1)
            {
                return false;
            }
            string firstArg = args[0];
            return (firstArg.IndexOf(createRegistryKeyArgument, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ShouldGenerateManifestFile(string[] args)
        {
            if (args.Length != 1)
            {
                return false;
            }
            string firstArg = args[0];
            return (firstArg.IndexOf(generateManifestFileArgument, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ShouldInstall(string[] args)
        {
            if (args.Length != 1)
            {
                return false;
            }
            string firstArg = args[0];
            return (firstArg.IndexOf(installArgument, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ShouldRunUnitTests(string[] args)
        {
            if (args.Length != 1)
            {
                return false;
            }
            string firstArg = args[0];
            return (firstArg.IndexOf(testArgument, StringComparison.OrdinalIgnoreCase) >= 0
                || firstArg.IndexOf(unitTestArgument,StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetTestState(bool testResult)
        {
            return testResult ? "Passed" : "Failed";
        }

        private static void RunInspectEventTraceLevelUnitTest()
        {
            const string testName = "InspectEventTraceLevel";
            const TraceLevel expectedResults = TraceLevel.debug;
            TraceLevel level = GetTraceLevelFromSettings("JAWSInspect", "InspectEvent");
            Trace.WriteLine($"Test {testName} {GetTestState(level == expectedResults)}", "test");
        }

        private static void RunYekNebTraceLevelUnitTest()
        {
            const string testName = "YekNebTraceLevel";
            const TraceLevel expectedResults = TraceLevel.info;
            TraceLevel level = GetTraceLevelFromSettings("SullivanAndKey.com", "yekneb.js");
            Trace.WriteLine($"Test {testName} {GetTestState(level == expectedResults)}", "test");
        }

        private static void RunYekNebShouldProcessMessageDebugUnitTest()
        {
            const string testName = "YekNebShouldProcessMessageDebug";
            const bool expectedResults = false;
            TraceMessage message = new();
            message.Command = "test";
            message.Message = "In RunYekNebShouldProcessMessageDebugUnitTest";
            message.Source = "SullivanAndKey.com";
            message.Context = "yekneb.js";
            message.Level = TraceLevel.debug;
            bool shouldProcess = ShouldProcessMessage(message);
            Trace.WriteLine($"Test {testName} {GetTestState(shouldProcess == expectedResults)}", "test");
        }

        private static void RunYekNebShouldProcessMessageInfoUnitTest()
        {
            const string testName = "YekNebShouldProcessMessageInfo";
            const bool expectedResults = false;
            TraceMessage message = new();
            message.Command = "test";
            message.Message = "In RunYekNebShouldProcessMessageInfoUnitTest";
            message.Source = "SullivanAndKey.com";
            message.Context = "yekneb.js";
            message.Level = TraceLevel.info;
            bool shouldProcess = ShouldProcessMessage(message);
            Trace.WriteLine($"Test {testName} {GetTestState(shouldProcess == expectedResults)}", "test");
        }

        private static void RunUnitTests()
        {
            RunInspectEventTraceLevelUnitTest();
            RunYekNebTraceLevelUnitTest();
            RunYekNebShouldProcessMessageDebugUnitTest();
            RunYekNebShouldProcessMessageInfoUnitTest();
        }

        private static string? GetManifestFilePath()
        {
            return Path.ChangeExtension(Application.ExecutablePath, "json");
        }

        private static bool CreateRegistryKey()
        {
            string? manifestFilePath = GetManifestFilePath();
            if (string.IsNullOrEmpty(manifestFilePath) || !File.Exists(manifestFilePath))
            {
                return false;
            }
            string[] registryKeyParts = { "SOFTWARE", "Google", "Chrome", "NativeMessagingHosts", "benilda.key.system.diagnostics.trace.native.messaging.host" };
            string registryKeyPath = Path.Combine(registryKeyParts);
            using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(registryKeyPath))
            {
                if (registryKey is null)
                {
                    return false;
                }
                registryKey.SetValue(null, manifestFilePath, RegistryValueKind.String);
            }
            return true;
        }

        private static string[] GetAllowedOrigins()
        {
            string[] allowedOrigins = { "chrome-extension://gigdeohcjhmkmfnpfefcjfmajlinajcb/" };
            return allowedOrigins;
        }

        private static bool GenerateManifestFile()
        {
            var manifestFilePath = GetManifestFilePath();
            if (string.IsNullOrEmpty(manifestFilePath))
            {
                return false;
            }
            var manifestFileData = new ManifestFileData
            {
                name = "benilda.key.system.diagnostics.trace.native.messaging.host",
                description = "Native messaging host for the system-diagnostics-trace browser extension.",
                path = Application.ExecutablePath,
                type = "stdio",
                allowed_origins = GetAllowedOrigins()
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(manifestFileData, options);
            File.WriteAllText(manifestFilePath, jsonString);
            return true;
        }

        private static void Install()
        {
            GenerateManifestFile();
            CreateRegistryKey();
        }

    }

}
