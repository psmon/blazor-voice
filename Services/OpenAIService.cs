using System.IO;
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

        public async Task<string> GetChatCompletion(string message, List<string> assist)
        {
            var completionResult = await _client.CompleteChatAsync(new ChatMessage[]
            {
                ChatMessage.CreateUserMessage(message),
                ChatMessage.CreateAssistantMessage(string.Join("\n", assist))
            });
            string aiResponse = completionResult.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

            return aiResponse;
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

        public async Task<string> ConvertVoiceToTextAsync(byte[] audioBytes, string fileName = "audio.wav", string language = "ko", bool stream = false)
        {
            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", fileName);
            content.Add(new StringContent("whisper-1"), "model");
            if (!string.IsNullOrEmpty(language))
                content.Add(new StringContent(language), "language");
            if (stream)
                content.Add(new StringContent("true"), "stream");

            var response = await _httpClient.PostAsync("audio/transcriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"STT API 호출 실패: {response.ReasonPhrase}\n{error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
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
