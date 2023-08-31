# System Diagnostics Trace Extension

## Introduction

The System Diagnostics Trace extension makes it possible to use the [System.Diagnostics.Trace.WriteLine method][6] from a browser extension or a website. This extension uses [Cross-extension messaging][2] to receive messages from other extensions, [Native messaging][3] to communicate with a [native messaging host][4], and supports [Sending messages from web pages][5].

This extension is composed of two parts.

* The browser extension.
* The system-diagnostics-trace-native-messaging-host application, which is written in C#.

## License

This extension licensed under the [Apache License Version 2.0][1].

[1]: <https://www.apache.org/licenses/LICENSE-2.0>
[2]: <https://developer.chrome.com/docs/extensions/mv3/messaging/#external>
[3]: <https://developer.chrome.com/docs/extensions/mv3/nativeMessaging/>
[4]: <https://developer.chrome.com/docs/extensions/mv3/nativeMessaging/#native-messaging-host>
[5]: <https://developer.chrome.com/docs/extensions/mv3/messaging/#external-webpage>
[6]: <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.trace.writeline>
