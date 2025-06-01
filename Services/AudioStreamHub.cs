using Microsoft.AspNetCore.SignalR;

namespace BlazorVoice.Services
{
    public class AudioStreamHub : Hub
    {
        public async Task SendAudioData(byte[] audioData)
        {
            try
            {
                Console.WriteLine($"Received audio data of size: {audioData.Length} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendAudioData: {ex.Message}");
                throw;
            }
        }
    }
}
