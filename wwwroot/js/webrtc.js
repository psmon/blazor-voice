let audioContext;
let analyser;
let dataArray;
let gainNode;

async function playAudioBytes2(audioBytes) {
    try {
        // 오디오 컨텍스트 초기화 (전역에서 관리하거나 외부에서 주입 가능)
        if (!audioContext || audioContext.state === 'closed') {
            audioContext = new AudioContext();
        }

        // Uint8Array → Float32Array 로 변환
        const float32Array = convertUint8ToFloat32(audioBytes);

        // AudioBuffer 생성
        const audioBuffer = audioContext.createBuffer(1, float32Array.length, audioContext.sampleRate);
        audioBuffer.copyToChannel(float32Array, 0);

        // 재생
        const bufferSource = audioContext.createBufferSource();
        bufferSource.buffer = audioBuffer;
        bufferSource.connect(audioContext.destination);
        bufferSource.start();
    } catch (err) {
        console.error("오디오 재생 중 오류 발생:", err);
    }
}


async function playAudioBytes(audioBytes, playbackRate, type, dotNetRef) {
    try {
        if (!audioContext || audioContext.state === 'closed') {
            audioContext = new AudioContext();
        }

        // Float32Array로 변환된 PCM 데이터 사용
        const float32Array = new Float32Array(audioBytes);

        // AudioBuffer 생성
        const audioBuffer = audioContext.createBuffer(1, float32Array.length, audioContext.sampleRate);
        audioBuffer.copyToChannel(float32Array, 0);

        // 재생
        const bufferSource = audioContext.createBufferSource();
        bufferSource.buffer = audioBuffer;
        bufferSource.playbackRate.value = playbackRate; // 재생 속도 설정
        bufferSource.connect(audioContext.destination);                

        // 재생 완료 이벤트 핸들러 추가
        bufferSource.onended = () => {            
            // 타입에 따라 추가 작업 수행
            console.log(`오디오 재생 완료 재생 타입: ${type}`);
            if (type === 1) {
                console.log("Type 1: 휴먼 재생완료~ LLM응답요청");
                if (dotNetRef && typeof dotNetRef.invokeMethodAsync === "function") {
                    dotNetRef.invokeMethodAsync("OnAudioPlaybackCompleted", 1)
                        .catch(err => console.error("Blazor 메서드 호출 OnAudioPlaybackCompleted 중 오류 발생:", err));
                }                
            } else if (type === 2) {
                console.log("Type 2: AI재생완료");
            } else if (type === 3) {
                console.log("Type 3: 사용자 정의 작업");
            }
        };

        bufferSource.start();
    } catch (err) {
        console.error("오디오 재생 중 오류 발생:", err);
    }
}

// 바이트 데이터를 Float32 배열로 변환
function convertUint8ToFloat32(uint8Array) {
    const dataView = new DataView(uint8Array.buffer);
    const float32Array = new Float32Array(uint8Array.byteLength / 4);

    for (let i = 0; i < float32Array.length; i++) {
        float32Array[i] = dataView.getFloat32(i * 4, true); // Little Endian
    }

    return float32Array;
}

async function startWebRTC(localVideoId, remoteVideoId, dotNetRef) {
    const localVideo = document.getElementById(localVideoId);
    const remoteVideo = document.getElementById(remoteVideoId);
    dotNetRef = dotNetRef || null; // dotNetRef가 제공되지 않으면 null로 설정

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


async function setMicrophoneVolume(volume) {
    if (gainNode) {
        gainNode.gain.value = volume / 100; // 볼륨을 0~1로 변환
    }
}

console.log("webrtc.js loaded successfully");