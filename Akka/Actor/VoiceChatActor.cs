using Akka.Actor;
using Akka.Event;

namespace BlazorVoice.Akka.Actor
{
    public class VoiceChatActorCommand { }

    public class ContentAutoUpdateCommand : VoiceChatActorCommand
    {
    }

    public class VoiceChatActor : ReceiveActor, IWithTimers
    {
        private readonly ILoggingAdapter logger = Context.GetLogger();

        private readonly IServiceProvider _serviceProvider;

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

            // 액터별 반복스케줄러 기능을 가져~ 응답이 아닌 능동형기능에 이용될수 있습니다.
            Timers.StartPeriodicTimer(
                key: TimerKey.Instance,
                msg: new ContentAutoUpdateCommand(),
                initialDelay: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromSeconds(RefreshTimeSecForContentAutoUpdate));

            Receive<ContentAutoUpdateCommand>(async command =>
            {
                logger.Info("VoiceChatActor : ContentAutoUpdateCommand");
            });

            _serviceProvider = serviceProvider;

        }
    }
}
