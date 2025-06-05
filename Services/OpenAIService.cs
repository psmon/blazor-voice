using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using NAudio.Wave;

using OpenAI.Audio;
using OpenAI.Chat;

namespace BlazorVoice.Services
{
    public class OpenAIService
    {
        private readonly ChatClient _client;
        private readonly HttpClient _httpClient;
        private readonly ClientWebSocket _webSocket;

        private List<String> _conversationHistory = new();


        private string lastAiMessage = string.Empty;


        public OpenAIService()
        {
            // API 키를 환경 변수에서 가져옵니다.
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OPENAI_API_KEY 환경 변수가 설정되지 않았습니다.");
            }

            // ChatClient 초기화
            _client = new ChatClient(model: "gpt-4o", apiKey: apiKey);

            // HttpClient 초기화
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // WebSocket 초기화
            _webSocket = new ClientWebSocket();

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
            var recentHistory = _conversationHistory.Skip(Math.Max(0, _conversationHistory.Count - 20)).ToList();

            // 수정된 코드: ChatMessage 생성 시 올바른 정적 메서드 사용
            var completionResult = await _client.CompleteChatAsync(new ChatMessage[]
            {
                ChatMessage.CreateUserMessage($"요청메시지는 : {message} 이며 첨부메시지는 현재 대화내용의 히스토리이며 이 맥락을 유지하면서 답변, 답변은 50자미만으로 줄여서 답변을 항상해~ AI는 너가답변한것이니 언급없이 너인것처럼하면됨"),   // User 메시지 생성
                ChatMessage.CreateAssistantMessage(string.Join("\n", recentHistory))                                        // Assistant 메시지 생성
            });

            string aiResponse = completionResult.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

            _conversationHistory.Add($"AI:{aiResponse}");
            lastAiMessage = aiResponse;

            return aiResponse;
        }

        public string GetLastAiMessage()
        {
            return lastAiMessage;
        }

        /// <summary>
        /// voice alloy, ash, ballad, coral, echo, fable, onyx, nova, sage, , shimme
        /// </summary>
        /// <param name="text"></param>
        /// <param name="voice"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<float[]> ConvertTextToVoiceAsync(string text, string voice = "alloy")
        {
            var requestBody = new
            {
                model = "gpt-4o-mini-tts",  // TTS 모델 이름
                input = text,               // 변환할 텍스트
                voice                       // 음성 스타일
            };
            
            var ttsTask = _httpClient.PostAsJsonAsync("audio/speech", requestBody);
            
            await Task.WhenAll(ttsTask); // 두 작업이 완료될 때까지 기다림

            var response = await ttsTask;

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"TTS API 호출 실패: {response.ReasonPhrase}");
            }

            // MP3 데이터를 byte 배열로 변환하여 반환
            var audioBytes = await response.Content.ReadAsByteArrayAsync();

            // MP3 데이터를 PCM 데이터로 변환
            return ConvertMp3ToFloatArray(audioBytes);
        }

        /// <summary>
        /// voice alloy, ash, ballad, coral, echo, fable, onyx, nova, sage, , shimme
        /// </summary>
        /// <param name="text"></param>
        /// <param name="voice"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<float[]> ConvertTextToVoiceWithLLMAsync(string text, string voice = "coral")
        {
            var requestBody = new
            {
                model = "gpt-4o-mini-tts",  // TTS 모델 이름
                input = text,               // 변환할 텍스트
                voice                       // 음성 스타일
            };

            // TTS API 호출과 GetChatCompletion을 동시에 실행
            var ttsTask = _httpClient.PostAsJsonAsync("audio/speech", requestBody);

            // GetChatCompletion을 백그라운드에서 실행
            _ = Task.Run(() => GetChatCompletion(text));

            var response = await ttsTask;            

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"TTS API 호출 실패: {response.ReasonPhrase}");
            }

            // MP3 데이터를 byte 배열로 변환하여 반환
            var audioBytes = await response.Content.ReadAsByteArrayAsync();

            // sample.mp3 파일로 저장
            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "audio");
            Directory.CreateDirectory(directoryPath); // 디렉터리가 없으면 생성
            var filePath = Path.Combine(directoryPath, "sample.mp3");
            await File.WriteAllBytesAsync(filePath, audioBytes);

            // MP3 데이터를 PCM 데이터로 변환
            return ConvertMp3ToFloatArray(audioBytes);
        }


        private float[] ConvertMp3ToFloatArray(byte[] mp3Data)
        {
            using var mp3Stream = new MemoryStream(mp3Data);
            using var mp3Reader = new Mp3FileReader(mp3Stream);

            // PCM 데이터를 float 배열로 변환
            var sampleProvider = mp3Reader.ToSampleProvider();
            var totalSamples = (int)(mp3Reader.TotalTime.TotalSeconds * mp3Reader.WaveFormat.SampleRate * mp3Reader.WaveFormat.Channels);
            var floatBuffer = new float[totalSamples];
            int samplesRead = sampleProvider.Read(floatBuffer, 0, floatBuffer.Length);

            return floatBuffer.Take(samplesRead).ToArray();
        }


    }
}
