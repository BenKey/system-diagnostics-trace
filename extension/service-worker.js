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

/* First connect to the "benilda.key.system.diagnostics.trace.native.messaging.host" app. */
let nativePort = chrome.runtime.connectNative("benilda.key.system.diagnostics.trace.native.messaging.host");

function IsInvalid(variable) {
  return (typeof (variable) === 'undefined' || variable == null);
}

function IsValid(variable) {
  return !IsInvalid(variable);
}

function NativePortOnMessageListener(message) {
  if (IsInvalid(message)) {
    return false;
  }
  if (!('Status' in message)) {
    return false;
  }
  console.log(`Received from Native Messaging Host: ${message.Status}.`);
}

function NativePortOnDisconnectListener() {
  console.log(`Native Messaging Host disconnected.`);
  nativePort = null;
}

function RuntimeOnMessageExternalListener(message, sender, sendResponse) {
  const failedResponse = {
    Success: false,
    Status: 'Could not post message to Native Messaging Host.'
  };
  const succeededResponse = {
    Success: true,
    Status: 'Message posted to Native Messaging Host.'
  };
  if (IsInvalid(nativePort)) {
    sendResponse(failedResponse);
    return false;
  }
  nativePort.postMessage(message);
  sendResponse(succeededResponse);
}

function PortOnMessageListener(message) {
  if (IsInvalid(nativePort)) {
    return false;
  }
  nativePort.postMessage(message);
}

function RuntimeOnConnectExternalListener(port) {
  port.onMessage.addListener(PortOnMessageListener);
}

chrome.runtime.onMessageExternal.addListener(RuntimeOnMessageExternalListener);
chrome.runtime.onConnectExternal.addListener(RuntimeOnConnectExternalListener);

if (IsValid(nativePort)) {
  console.log('Successfully connected to the benilda.key.system.diagnostics.trace.native.messaging.host.')
  nativePort.onMessage.addListener(NativePortOnMessageListener);
  nativePort.onDisconnect.addListener(NativePortOnDisconnectListener);
  const traceMessage = {
    Command: "trace",
    Message: "Hello world! It is truly a wonderful day to be alive!",
    Source: "system-diagnostics-trace",
    Context: "service-worker",
    Level: "2"
  };
  nativePort.postMessage(traceMessage);
}
