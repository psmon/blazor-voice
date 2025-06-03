let audioContext;
let analyser;
let dataArray;
let gainNode;


async function startWebRTC(localVideoId, remoteVideoId, dotNetRef) {
    const localVideo = document.getElementById(localVideoId);
    const remoteVideo = document.getElementById(remoteVideoId);

    navigator.mediaDevices.getUserMedia({ audio: true, video: true })
        .then(stream => {
            localVideo.srcObject = stream;
            localVideo.play();

            // Initialize AudioContext for audio analysis
            audioContext = new AudioContext();
            gainNode = audioContext.createGain();
            
            const source = audioContext.createMediaStreamSource(stream);
            analyser = audioContext.createAnalyser();
            source.connect(analyser);

            analyser.fftSize = 256;
            dataArray = new Uint8Array(analyser.frequencyBinCount);

            // Start analyzing audio levels
            function analyzeAudio() {
                analyser.getByteFrequencyData(dataArray);
                const volume = dataArray.reduce((a, b) => a + b, 0) / dataArray.length; // Average volume

                // Ensure dotNetRef is callable
                if (dotNetRef && typeof dotNetRef.invokeMethodAsync === "function") {
                    // Combine UpdateAudioLevel and SendAudioData functionality
                    const audioBuffer = new Float32Array(analyser.frequencyBinCount);
                    analyser.getFloatTimeDomainData(audioBuffer); // Get time-domain audio data
                    const byteArray = new Uint8Array(audioBuffer.buffer); // Convert to Uint8Array

                    dotNetRef.invokeMethodAsync("SendAudioData", JSON.stringify({
                        volume: volume,
                        audioData: Array.from(byteArray) // Ensure audioData is an array
                    })).catch(err => console.error("Error invoking SendAudioData:", err));
                } else {
                    console.error("Error: dotNetRef is not callable.");
                }

                requestAnimationFrame(analyzeAudio);
            }
            analyzeAudio();

            const peerConnection = new RTCPeerConnection();
            stream.getTracks().forEach(track => peerConnection.addTrack(track, stream));

            peerConnection.ontrack = event => {
                remoteVideo.srcObject = event.streams[0];
                remoteVideo.play();
            };
        })
        .catch(error => console.error('Error accessing media devices.', error));
}

function setMicrophoneVolume(volume) {
    if (gainNode) {
        gainNode.gain.value = volume / 100; // 볼륨을 0~1로 변환
    }
}

console.log("webrtc.js loaded successfully");