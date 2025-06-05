using System.Net.Http;
using System.Net.Http.Headers;

using OpenAI.Chat;
using OpenAI.Audio;
using NAudio.Wave;

namespace BlazorVoice.Services
{
    public class OpenAIService
    {
        private readonly ChatClient _client;
        private readonly HttpClient _httpClient;

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


        }

        /// <summary>
        /// 주어진 메시지에 대한 ChatCompletion을 생성합니다.
        /// </summary>
        /// <param name="message">보낼 메시지</param>
        /// <returns>ChatCompletion 결과</returns>
        public string GetChatCompletion(string message)
        {
            ChatCompletion completion = _client.CompleteChat(message);
            return completion.Content[0].Text;
        }

        public async Task<float[]> ConvertTextToVoiceAsync(string text)
        {
            var requestBody = new
            {
                model = "gpt-4o-mini-tts", // TTS 모델 이름
                input = text,             // 변환할 텍스트
                voice = "alloy"           // 음성 스타일
            };

            var response = await _httpClient.PostAsJsonAsync("audio/speech", requestBody);

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
