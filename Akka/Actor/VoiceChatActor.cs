using System;

using Akka.Actor;
using Akka.Event;

using BlazorVoice.Services;

using OpenAI.Chat;

namespace BlazorVoice.Akka.Actor
{
    public class VoiceChatActorCommand { }

    public class ContentAutoUpdateCommand : VoiceChatActorCommand
    {
    }

    public class TTSCommand : VoiceChatActorCommand
    {
        public string From { get; set; } = "Your"; // 기본 발신자 설정
        public string Text { get; set; }
        public string Voice { get; set; } = "alloy"; // 기본 음성 설정
    }


    public class VoiceChatActor : ReceiveActor, IWithTimers
    {
        private readonly ILoggingAdapter logger = Context.GetLogger();

        private readonly IServiceProvider _serviceProvider;

        private List<String> _conversationHistory = new();

        private string lastAiMessage = string.Empty;

        private Action<string, object[]> _blazorCallback;

        private OpenAIService _openAIService;

        private int MaxAIWordCount = 150; // AI 응답 최대 단어 수 설정

        private sealed class TimerKey
        {
            public static readonly TimerKey Instance = new();
            private TimerKey() { }
        }

        public int RefreshTimeSecForContentAutoUpdate { get; set; } = 30;

        public ITimerScheduler Timers { get; set; } = null!;

        public VoiceChatActor(IServiceProvider serviceProvider)
        {
            logger.Info($"VoiceChatActor : Constructor - {Self.Path}");

            _openAIService = new OpenAIService();

            // 액터별 반복스케줄러 기능을 가져~ 응답이 아닌 능동형기능에 이용될수 있습니다.
            Timers.StartPeriodicTimer(
                key: TimerKey.Instance,
                msg: new ContentAutoUpdateCommand(),
                initialDelay: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromSeconds(RefreshTimeSecForContentAutoUpdate));

            Receive<ContentAutoUpdateCommand>( command =>
            {
                logger.Info("VoiceChatActor : ContentAutoUpdateCommand");                
            });

            Receive<TTSCommand>( command =>
            {
                logger.Info($"VoiceChatActor : Received Command - {command.GetType().Name}");
                switch (command.From)
                {
                    case "Your":
                    {
                        int playType = 1;
                        _ = Task.Run(() => GetChatCompletion(command.Text));
                        var recVoice = _openAIService.ConvertTextToVoiceAsync(command.Text, command.Voice).Result;
                        _blazorCallback?.Invoke("AddMessage", new object[] { command.From, command.Text });
                        _blazorCallback?.Invoke("PlayAudioBytes", new object[] { recVoice, 0.5f, playType });
                    }                        
                    break;
                    case "AI":
                    {
                        var msg = lastAiMessage;
                        int playType = 2;
                        var recVoice = _openAIService.ConvertTextToVoiceAsync(msg, command.Voice).Result;
                        _blazorCallback?.Invoke("AddMessage", new object[] { command.From, msg });
                        _blazorCallback?.Invoke("PlayAudioBytes", new object[] { recVoice, 0.5f, playType });
                    }
                    break;
                    default:
                        logger.Warning($"Unknown command received: {command.From}");
                    break;
                }
            });


            Receive<Action<string, object[]>>( command =>
            {
                _blazorCallback = command;
                int playType = 2; //2: AI
                var msg = "웹컴 TTS AI서비스입니다.";                
                var recVoice = _openAIService.ConvertTextToVoiceAsync(msg, "alloy").Result;
                _blazorCallback?.Invoke("AddMessage", new object[] { "AI", msg });
                _blazorCallback?.Invoke("PlayAudioBytes", new object[] { recVoice, 0.5f, playType });

            });

            _serviceProvider = serviceProvider;

        }

        /// <summary>
        /// 주어진 메시지에 대한 ChatCompletion을 생성합니다.
        /// </summary>
        /// <param name="message">보낼 메시지</param>
        /// <returns>ChatCompletion 결과</returns>
        public async Task<string> GetChatCompletion(string message)
        {
            _conversationHistory.Add($"User:{message}");

            // 최근 20개의 대화 기록을 가져옵니다.
            var recentHistory = _conversationHistory.Skip(Math.Max(0, _conversationHistory.Count - 100)).ToList();

            // 수정된 코드: ChatMessage 생성 시 올바른 정적 메서드 사용
            var aiResponse = await _openAIService.GetChatCompletion(

                $"요청메시지는 : {message} 이며 첨부메시지는 현재 대화내용의 히스토리이며 이 맥락을 유지하면서 답변, 답변은 {MaxAIWordCount}자미만으로 줄여서 답변을 항상해~ AI는 너가답변한것이니 언급없이 너인것처럼하면됨",
                recentHistory
            );            

            _conversationHistory.Add($"AI:{aiResponse}");
            lastAiMessage = aiResponse;

            return aiResponse;
        }
    }
}
