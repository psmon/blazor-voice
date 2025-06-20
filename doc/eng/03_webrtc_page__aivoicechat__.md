# Chapter 3: WebRTC Page (AIVoiceChat)

In the [previous chapter](02_mudblazor_layout__mainlayout___mynavmenu__.md), we learned how to give our BlazorVoice application a consistent structure and navigation with MudBlazor layouts. Now, it’s time to explore how we can deliver real-time voice chat and AI-driven interactions on our stage — introducing the “WebRTC Page (AIVoiceChat).”

---

## Why Do We Need a WebRTC Page?

Imagine you want a live conversation with an AI or remote participants:
• You want to capture your microphone audio.  
• You want to stream it to others or send it to an AI for voice processing.  
• You want to see chat logs, possibly video, and also have a big “mic” button to turn things on/off.  

This page, “WebRTC.razor,” is like a **front-of-house control center**. You can record or play audio, see and send messages, and let the AI speak back in real time. It’s basically the “conference stage” where all the action happens.

---

## Key Concepts

1. **Real-time Voice & Video**  
   - The page uses WebRTC to capture audio (and video, if you want) from your browser, so you can communicate with others or feed it to an AI.

2. **User Controls**  
   - You’ll have buttons (like “Start WebRTC”) to toggle capturing, to change mic volume, and more.

3. **Chat & TTS Integration**  
   - You can type text into a chat, then call the AI to speak for you (Text-to-Speech).  
   - The page orchestrates sending your typed text to [VoiceChatActor](07_voicechatactor_.md), which calls the AI engine.

4. **Layout & Interaction**  
   - The left section might show video or a fun “AI face” animation.  
   - The right section shows a chat box with messages.  
   - A bottom bar handles mic volume and other controls.

---

## How to Use the WebRTC Page

Let’s pretend you want a simple scenario: “I open the page, I click ‘Start WebRTC’ to begin capturing my audio, and if I type a text message, the AI will read it out loud.”

Below is a cleaned-up look at parts of `WebRTC.razor`. We’ll break it down so everything is easy to follow.

### 1) Page Setup

```csharp
@page "/web-rtc"
@rendermode InteractiveServer

<PageTitle>AIVoiceChat</PageTitle>

<h1>AIVoiceChat</h1>
```
Explanation:  
• `@page "/web-rtc"` means users visit “/web-rtc” to open this page.  
• `@rendermode InteractiveServer` allows server interactivity (so real-time events can work).  
• `<PageTitle>` sets the browser tab’s text.

---

### 2) Video & Canvas

```csharp
<div>
    <canvas id="aiFaceCanvas" width="320" height="240"></canvas>
    <video id="localVideo" autoplay muted></video>
</div>
```
Explanation:  
• You can show an animated AI face on the canvas (just for fun!).  
• `video` is your live preview of the microphone/camera.

---

### 3) Chat Section

```csharp
<div>
    @foreach (var chat in ChatMessages)
    {
        <div>@chat</div>
    }
</div>

<input @bind="ChatInput" />
<button @onclick="SendChatMessage">Send</button>
```
Explanation:  
• A loop displays each chat message from `ChatMessages`.  
• A simple input field binds to `ChatInput`.  
• Clicking “Send” calls `SendChatMessage()`.

---

### 4) Audio Controls

```csharp
<div>
    <button @onclick="StartWebRTC">Start WebRTC</button>
    <div>Microphone Volume: @MicrophoneVolume</div>
    <input type="range" min="0" max="100" @bind="MicrophoneVolume" 
           @onchange="UpdateMicrophoneVolume" />
</div>
```
Explanation:  
• The “Start WebRTC” button begins microphone capture through JavaScript.  
• A slider sets `MicrophoneVolume`, which you can update in your JS code to control mic gain.

---

### 5) Basic Code-Behind

Below is an example snippet from the “code-behind” region (inside `@code { ... }`). It shows how we handle user input and pass messages along:

```csharp
private string ChatInput { get; set; } = "";
private List<string> ChatMessages { get; set; } = new();

private async Task SendChatMessage()
{
    if (!string.IsNullOrWhiteSpace(ChatInput))
    {
        ChatMessages.Add($"You: {ChatInput}");
        // Send TTS command to AI
        MyVoiceActor.Tell(new TTSCommand { Text = ChatInput, Voice = "coral" });
        ChatInput = "";
    }
}
```
Explanation:  
• We add the typed message to our internal list, then call `TTSCommand` to have the AI speak.  
• Finally, we clear `ChatInput`.

---

## Under the Hood (How It Actually Works)

Imagine a small flow:

```mermaid
sequenceDiagram
    participant User
    participant WebRTCPage as WebRTC.razor
    participant JS as webrtc.js
    participant Actor as VoiceChatActor

    User->>WebRTCPage: Click "Start WebRTC"
    WebRTCPage->>JS: Invoke startWebRTC()
    JS-->>WebRTCPage: Start audio capturing
    User->>WebRTCPage: Type text & press "Send"
    WebRTCPage->>Actor: Forward TTSCommand
    Actor-->>WebRTCPage: AI speech response
```

1. The **User** clicks “Start WebRTC.”  
2. **WebRTC.razor** calls the JavaScript function to capture audio.  
3. **JS** returns real-time audio data to Blazor.  
4. The **User** types a message and clicks “Send.”  
5. The Blazor page sends a **TTSCommand** to the [VoiceChatActor](07_voicechatactor_.md).  
6. The actor triggers the AI’s TTS, which ultimately plays back on the user’s page.

---

## Internal Implementation Details

• “WebRTC.razor” sits in `Components\Pages\WebRTC.razor`.  
• It injects dependencies like `[OpenAIService](08_openaiservice_.md)` and `[AkkaService](06_akka_service_.md)`.  
• It sets up or refers to a JavaScript file ([webrtc.js](04_webrtc_js_.md)) for capturing and sending real-time audio.  

Below is an example of how you might invoke the JS function:

```csharp
private async Task StartWebRTC()
{
    var dotNetRef = DotNetObjectReference.Create(this);
    await JSRuntime.InvokeVoidAsync("startWebRTC", "localVideo", dotNetRef);
}
```
Explanation:  
• We create a .NET reference (`dotNetRef`) to let JS call back into Blazor for events (like sending audio samples).  
• We call `startWebRTC` in our JavaScript file, passing the video element ID and the reference.

---

### Handling Incoming Audio

```csharp
[JSInvokable]
public async Task SendAudioData(string audioDataJson)
{
    // Parse audio data from JS
    // Possibly detect volume, do noise filtering, etc.
    // Forward to actor or do something else...
}
```
Explanation:  
• `[JSInvokable]` means JS can call this C# method.  
• We decode the audio data, measure the volume, or pass it to an AI for speech-to-text or analysis.

---

## Conclusion

Our “WebRTC Page (AIVoiceChat)” is the interactive stage where real-time voices, AI text-to-speech, and user chat come together. We can record from the mic, show chat messages, and let the AI speak back — all within one Blazor page.

Next, we’ll see how to implement the supporting JavaScript functionality for WebRTC in detail. Join us in the following chapter: [webrtc.js](04_webrtc_js_.md).

---

Generated by [AI Codebase Knowledge Builder](https://github.com/The-Pocket/Tutorial-Codebase-Knowledge)