using System;

using Akka.Actor;
using Akka.Event;

using BlazorVoice.Services;

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

        private Action<string, object[]> _blazorCallback;

        private OpenAIService _openAIService;

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
                        int playType = 1; //1: Human
                        var recVoice = _openAIService.ConvertTextToVoiceWithLLMAsync(command.Text, command.Voice).Result;
                        _blazorCallback?.Invoke("AddMessage", new object[] { command.From, command.Text });
                        _blazorCallback?.Invoke("PlayAudioBytes", new object[] { recVoice, 0.5f, playType });
                    }                        
                    break;
                    case "AI":
                    {
                        var msg = _openAIService.GetLastAiMessage();
                        int playType = 2; //2: AI
                        var recVoice = _openAIService.ConvertTextToVoiceAsync(msg, "alloy").Result;
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
    }
}
