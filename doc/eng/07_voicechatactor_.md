# Chapter 7: VoiceChatActor

In the [previous chapter](06_akka_service_.md), we introduced the Akka Service as our “director” that manages different ActorSystems. Now it’s time to meet a special performer living inside those ActorSystems: the VoiceChatActor. Think of the VoiceChatActor as a friendly voice assistant on stage, ready to speak (via TTS), keep track of conversation context, and occasionally do housekeeping (like auto-updating content).

--------------------------------------------------------------------------------

## Why Do We Need a VoiceChatActor?

Let’s imagine a scenario:
• You have a chat interface where you type messages.  
• You want the system to respond with realistic voice output.  
• You want to maintain context across multiple user turns (so the AI can reference previous prompts).

The VoiceChatActor is perfectly suited for that. Whenever you send a “TTSCommand,” it:
1. Interacts with OpenAI (for voice generation or chat responses).  
2. Keeps a conversation history (so each new response feels connected).  
3. Periodically refreshes content or greets the user again (housekeeping).

In short, it’s your conversation “performer,” ensuring the dialogue flows smoothly and users hear each new line.

--------------------------------------------------------------------------------

## Key Concepts

1. **TTSCommand**  
   - A simple command telling the actor: “Please turn this text into speech.”  
   - Includes who sent the message, the text itself, and which voice to use.

2. **Conversation History**  
   - The actor keeps an internal list of messages (like “User: Hello” and “AI: Hi there!”).  
   - This list is used to maintain context when generating new responses.

3. **Housekeeping with Timers**  
   - The actor has a timer that triggers every so often (e.g., 30 seconds).  
   - This might refresh content, greet the user again, or do other background tasks.

4. **Callbacks to Blazor**  
   - Once the VoiceChatActor gets new speech audio, it calls back into the UI, playing the result for the user.  
   - This is like the actor on stage stepping forward and saying the next line.

--------------------------------------------------------------------------------

## How to Use VoiceChatActor

Below is a typical use case:

1. In your Blazor code, you send a TTSCommand to the VoiceChatActor.  
2. The actor receives it, updates conversation history, and calls the OpenAI engine for speech.  
3. The actor invokes a callback that plays audio in your browser and shows the text in a chat window.

--------------------------------------------------------------------------------

### Minimal Code Examples

The actual file VoiceChatActor.cs has more code, but let’s simplify it. We’ll break it into pieces under 10 lines each.

#### 1) Actor Setup

```csharp
public class VoiceChatActor : ReceiveActor, IWithTimers
{
    private Action<string, object[]>? _blazorCallback;
    private List<string> _conversationHistory = new();

    public ITimerScheduler Timers { get; set; } = null!;

    public VoiceChatActor()
    {
        // Setup incoming message types here...
    }
}
```

Explanation:  
• Inherit from “ReceiveActor” to handle messages in a straightforward manner.  
• “IWithTimers” so the actor can schedule housekeeping commands.  
• “_blazorCallback” is a function the actor calls to update the UI (e.g., add chat messages).  
• “_conversationHistory” stores all user and AI messages.

--------------------------------------------------------------------------------

#### 2) Timers for Housekeeping

```csharp
Timers.StartPeriodicTimer(
    "ContentUpdateKey",
    new ContentAutoUpdateCommand(),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30));
```

Explanation:  
• Schedules a timer to send “ContentAutoUpdateCommand” to this actor every 30 seconds (starting after 10 seconds).  
• You can do anything you like when this command arrives, such as refreshing conversation logic.

--------------------------------------------------------------------------------

#### 3) Handling ContentAutoUpdateCommand

```csharp
Receive<ContentAutoUpdateCommand>(cmd =>
{
    // For example, log a message or greet the user
    Console.WriteLine("VoiceChatActor: Performing housekeeping...");
});
```

Explanation:  
• Whenever the actor receives a ContentAutoUpdateCommand, it runs this block.  
• Currently, we just log to the console (but you could expand it).

--------------------------------------------------------------------------------

#### 4) Handling TTSCommand

```csharp
Receive<TTSCommand>(command =>
{
    // 1) Update conversation
    _conversationHistory.Add($"{command.From}:{command.Text}");
    
    // 2) Generate speech
    // (Pretend we call OpenAIService here and get audio bytes)

    // 3) Call back into Blazor
    _blazorCallback?.Invoke("AddMessage", new object[] { command.From, command.Text });
    _blazorCallback?.Invoke("PlayAudioBytes", new object[] { /* audio bytes */ });
});
```

Explanation:  
• Grabs the text from “command.Text” and adds it to the conversation history.  
• Uses some imaginary AI call to generate speech (not shown in this snippet).  
• Calls `_blazorCallback` twice: once to show the text in the chat, and once to play the generated audio.

--------------------------------------------------------------------------------

#### 5) Setting the Blazor Callback

```csharp
Receive<Action<string, object[]>>(callback =>
{
    _blazorCallback = callback;
    Console.WriteLine("VoiceChatActor: Blazor callback set!");
});
```

Explanation:  
• Another message type is an Action delegate that the actor stores.  
• This lets the actor call into Blazor at any time.

--------------------------------------------------------------------------------

### Putting It All Together

1. When your app starts, you create the VoiceChatActor inside the “default” ActorSystem (see [Akka Service](06_akka_service_.md)).  
2. You also provide a callback so the VoiceChatActor can “speak” back.  
3. On the user’s button click, you send a TTSCommand with text.  
4. The actor uses AI (via [OpenAIService](08_openaiservice_.md)) to generate speech.  
5. The actor calls your UI, which plays the audio for the user.

--------------------------------------------------------------------------------

## Under the Hood (Step-by-Step)

Let’s see a tiny sequence diagram of what happens when a user sends text they want read aloud:

```mermaid
sequenceDiagram
    participant User
    participant Blazor as Blazor Page
    participant Actor as VoiceChatActor
    participant AI as OpenAI Service

    User->>Blazor: Type text & click "Send"
    Blazor->>Actor: TTSCommand(Text="Hello")
    Actor->>AI: Convert text to speech
    AI-->>Actor: Return speech audio bytes
    Actor->>Blazor: AddMessage & PlayAudioBytes
```

1. **User** clicks the “Send” button with some text.  
2. The **Blazor** page sends a TTSCommand to the **VoiceChatActor**.  
3. The VoiceChatActor calls **OpenAI** (or another TTS engine) to get audio.  
4. It then calls back to **Blazor** to display the text in chat and play the audio bytes.

--------------------------------------------------------------------------------

## A Peek at the Actual Code

In the real file (VoiceChatActor.cs), the actor also:

• Stores the last AI message in “lastAiMessage.”  
• Has a method “GetChatCompletion” to do multi-turn chat with the AI.  
• Runs a timer to do housekeeping tasks like “ContentAutoUpdateCommand.”

Here’s a small snippet showing how the actor processes user text with AI (simplified):

```csharp
public async Task<string> GetChatCompletion(string message)
{
    _conversationHistory.Add($"User:{message}");
    
    var aiResponse = /* ask AI, e.g. OpenAI Chat Completion */;
    
    _conversationHistory.Add($"AI:{aiResponse}");
    lastAiMessage = aiResponse;
    return aiResponse;
}
```

Explanation:  
• We add the user’s message to the history.  
• We call into our AI (not shown here) to get a response.  
• We update “lastAiMessage” and store that reply.  

This ensures next time we ask for more text, the conversation has context.

--------------------------------------------------------------------------------

## Conclusion

The VoiceChatActor is your on-stage performer, listening for chat commands, managing the conversation’s spirit, and calling back to Blazor so your users can hear the next line. By keeping track of history, automatically refreshing content, and calling text-to-speech on demand, the VoiceChatActor keeps the show lively and interactive.

In the next chapter, we’ll reveal the behind-the-scenes power source for generating AI text and speech: the [OpenAIService](08_openaiservice_.md). Let’s see how the actor’s requests become AI-driven answers!

---

Generated by [AI Codebase Knowledge Builder](https://github.com/The-Pocket/Tutorial-Codebase-Knowledge)