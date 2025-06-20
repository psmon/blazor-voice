# Chapter 4: webrtc.js

In the [previous chapter](03_webrtc_page__aivoicechat__.md), we discovered how the WebRTC Page (AIVoiceChat) uses the browser’s ability to capture audio and communicate in real time. Now, let’s focus on the backstage helper: “webrtc.js.” Think of it like the audio-visual technician who manages microphone input, processes audio levels, and animates an AI face canvas—all before handing the data off to the rest of the BlazorVoice application.

---

## Why Do We Need webrtc.js?

Imagine you have a microphone on stage. You need someone behind the scenes to:
• Set up the audio channels (turn on the mic, process sound levels).  
• Stream the audio to the server or AI.  
• Animate an “AI face” whenever it starts talking.

That’s exactly what `webrtc.js` does. It works directly with the browser’s WebRTC (Web Real-Time Communication) APIs, measuring volume levels and sending them back into .NET land via JavaScript interop. It also animates a friendly face while the AI speaks.

---

## Key Concepts

1. **Capturing Microphone & Video**  
   - Uses `getUserMedia` to grab your mic (and camera, if needed).  
   - Displays a local video preview so you can see yourself (or test the mic).

2. **Audio Analysis**  
   - Creates an `AudioContext` and `AnalyserNode` to inspect volume levels.  
   - Regularly sends that volume data (and snippets of audio) back to Blazor for further processing.

3. **AI Face Animation**  
   - Shows a moving mouth on a canvas to represent AI speaking.  
   - Starts or stops the animation based on events (like when audio plays).

4. **Interop with Blazor**  
   - .NET calls JavaScript to start capturing.  
   - JavaScript calls .NET (via `[JSInvokable]`) whenever it has audio data.  
   - This two-way bridge keeps everything in sync.

---

## How to Use webrtc.js

Let’s say you want to detect your microphone volume in real time and also let the AI “talk” with a fun face animation. You can:

1. Import `webrtc.js` in your `index.html` or `_Layout.cshtml`.  
2. From your Blazor Page (like [WebRTC Page (AIVoiceChat)](03_webrtc_page__aivoicechat__.md)), call `startWebRTC(...)` with the relevant IDs or references.  
3. When you receive audio data, do something in C# (log volume, store it, etc.).  
4. Whenever the AI needs to speak, you pass an audio buffer to `playAudioBytes(...)`, which triggers the animated face.

Below is a step-by-step flow:

```mermaid
sequenceDiagram
    participant Blazor as AIVoiceChat.razor
    participant JS as webrtc.js
    participant Actor as VoiceChatActor
    participant AI as AI Engine

    Blazor->>JS: startWebRTC(localVideo, dotNetRef)
    JS-->>Blazor: Volume & audio callback (JSInvokable)
    Blazor->>Actor: Sends volume or commands
    Actor->>AI: Request speech
    AI-->>Actor: Speech audio
    Actor-->>Blazor: Audio buffer for AI voice
    Blazor->>JS: playAudioBytes(buffer, type=2)
    JS-->>JS: Animate AI face while playing
```

---

## Code Snippets (Simplified)

Here are small pieces of the actual `webrtc.js`. Each snippet is below 10 lines to keep it friendly for beginners.

### 1) Starting the Microphone & Video

```js
async function startWebRTC(localVideoId, dotNetRef) {
  const localVideo = document.getElementById(localVideoId);
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: true });
  localVideo.srcObject = stream;
  localVideo.play();
  setupAudioAnalysis(stream, dotNetRef);
}
```

Explanation:  
• We grab audio/video from your device with `getUserMedia`.  
• We put the video stream into an HTML `<video>` so you see it live.  
• We call `setupAudioAnalysis` to measure audio volume.

---

### 2) Analyzing Audio Levels

```js
function setupAudioAnalysis(stream, dotNetRef) {
  const audioContext = new AudioContext();
  const analyser = audioContext.createAnalyser();
  const source = audioContext.createMediaStreamSource(stream);
  source.connect(analyser);

  analyzeLoop(analyser, dotNetRef);
}
```

Explanation:  
• Create an `AudioContext` and `AnalyserNode`.  
• `createMediaStreamSource` links our mic stream to the analyser.  
• Then we call `analyzeLoop`, which continuously checks the volume.

---

### 3) The Audio Analysis Loop

```js
function analyzeLoop(analyser, dotNetRef) {
  const dataArray = new Uint8Array(analyser.frequencyBinCount);

  function loop() {
    analyser.getByteFrequencyData(dataArray);
    const volume = dataArray.reduce((a, b) => a + b, 0) / dataArray.length;
    dotNetRef.invokeMethodAsync("SendAudioData", volume);
    requestAnimationFrame(loop);
  }
  loop();
}
```

Explanation:  
• `analyser.getByteFrequencyData(dataArray)` fills `dataArray` with volume data.  
• `volume` is calculated as an average.  
• We call a C# method called `SendAudioData(double volume)` (exposed with `[JSInvokable]`), letting Blazor know about the current volume.  
• `requestAnimationFrame(loop)` repeats, capturing volume over time.

---

### 4) Playing AI Speech & Animating the Face

```js
async function playAudioBytes(audioBytes) {
  const audioContext = new AudioContext();
  const float32Array = new Float32Array(audioBytes);
  const audioBuffer = audioContext.createBuffer(1, float32Array.length, audioContext.sampleRate);
  audioBuffer.copyToChannel(float32Array, 0);
  
  const source = audioContext.createBufferSource();
  source.buffer = audioBuffer;
  startAIFaceAnimation(); // Start mouth animation
  source.onended = () => stopAIFaceAnimation(); 
  source.connect(audioContext.destination);
  source.start();
}
```

Explanation:  
• Convert `audioBytes` to a Float32 array so we can create an `AudioBuffer`.  
• We connect it to the audio output (`audioContext.destination`).  
• We start the AI face animation and stop it when the audio ends.

---

### 5) The AI Face Animation (High-Level)

```js
function startAIFaceAnimation() {
  aiFaceAnimationActive = true;
  drawAIFace();
}

function stopAIFaceAnimation() {
  aiFaceAnimationActive = false;
}
```

Explanation:  
• We set a flag `aiFaceAnimationActive = true`.  
• Then `drawAIFace()` is repeatedly called to animate a mouth opening and closing while the AI “talks.”  
• When the audio stops, we turn that flag off.

---

## Under the Hood (Simplified Walkthrough)

When your Blazor page calls `startWebRTC`, here’s what happens step-by-step:

1. `navigator.mediaDevices.getUserMedia` asks for mic/camera permission.  
2. The local video element shows the live camera preview.  
3. `setupAudioAnalysis` starts measuring the audio data every frame.  
4. `analyzeLoop` calculates volume, calls `dotNetRef.invokeMethodAsync("SendAudioData", volume)`.  
5. On the Blazor side, you get the volume in `SendAudioData`.  
6. When you need the AI to speak, Blazor calls `playAudioBytes(...)` with an audio buffer.  
7. JavaScript plays the audio and animates the AI mouth.  

---

## Internal Implementation Details

Inside `wwwroot/js/webrtc.js`, you’ll find more functions solving advanced tasks (like connecting to a remote peer or storing user IDs). But the main idea remains:

• Microphone + WebRTC ↔ AudioContext (analysis, playback) ↔ Blazor interop.  
• Optional canvas animations for an AI face.  
• Clean handoff of data to .NET, so the rest of the system can do speech recognition or other features.

If you want to expand it, you could add more error handling, remote participant logic, or advanced audio filtering. But for core usage, these snippets suffice to get you capturing audio and animating your AI face.

---

## Conclusion

You’ve now learned how `webrtc.js` is the audio-visual technician backstage, capturing your mic, measuring volume, sending data to .NET, and animating a friendly AI face. This script is essential for bridging your browser’s real-time media capabilities with your Blazor app.

In the next chapter, we’ll see how [AudioStreamHub](05_audiostreamhub_.md) ties these audio streams into SignalR for live communication—making the show truly interactive!

[Next Chapter: AudioStreamHub](05_audiostreamhub_.md)

---

Generated by [AI Codebase Knowledge Builder](https://github.com/The-Pocket/Tutorial-Codebase-Knowledge)