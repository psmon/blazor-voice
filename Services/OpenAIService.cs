using System.Net.Http;
using System.Net.Http.Headers;

using OpenAI.Chat;
using OpenAI.Audio;

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

        /// <summary>
        /// 텍스트를 음성으로 변환하여 byte 스트림으로 반환합니다.
        /// </summary>
        /// <param name="text">변환할 텍스트</param>
        /// <returns>음성 데이터의 byte 배열</returns>
        public async Task<byte[]> ConvertTextToVoiceAsync(string text)
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
            return await response.Content.ReadAsByteArrayAsync();
        }

    }
}
