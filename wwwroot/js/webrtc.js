﻿let audioContext;
let analyser;
let dataArray;
let gainNode;

// === AI 얼굴 애니메이션 ===
let aiFaceAnimationActive = false;
let aiFaceAnimationFrame = null;

// Live2D 관련 전역 변수
let live2dModel = null;
let live2dApp = null;
let live2dIdleMotion = "Idle";
let live2dTalkMotion = "Talk";
let live2D;

// Live2D 모델 초기화 함수 (한 번만 호출)
async function initLive2D() {

}

// 모션 실행 함수
function playMotionByName(name) {
    try {
        const coreModel = L2Dwidget.live2DModel; // Live2DModel instance
        const motions = coreModel.modelSetting.getMotionGroup(name); // 보통 여기서 모션 목록 확인 가능
        const motionIndex = motions.findIndex(m => m.file.includes(name));
        if (motionIndex >= 0) {
            coreModel.startMotion(name, motionIndex, 0);
        }
    } catch (e) {
        console.warn("모션 실행 실패", e);
    }
}


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

        if (type === 2) {
            startAIFaceAnimation();
            //playMotionByName("tap_body")
        }

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
                stopAIFaceAnimation();
                //playMotionByName("idle")
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

async function getOrCreateUniqueId() {
    let uniqueId = localStorage.getItem("uniqueUserId");
    if (!uniqueId) {
        uniqueId = crypto.randomUUID(); // 유니크한 ID 생성
        localStorage.setItem("uniqueUserId", uniqueId);
    }
    return uniqueId;
}

function startAIFaceAnimation() {
    aiFaceAnimationActive = true;
    drawAIFace();
}

function stopAIFaceAnimation() {
    aiFaceAnimationActive = false;
    if (aiFaceAnimationFrame) {
        cancelAnimationFrame(aiFaceAnimationFrame);
        aiFaceAnimationFrame = null;
    }
    clearAIFaceCanvas();
}

function clearAIFaceCanvas() {
    const canvas = document.getElementById('aiFaceCanvas');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        drawAIFaceStatic(ctx, canvas);
    }
}

function drawAIFaceStatic(ctx, canvas) {
    const w = canvas.width;
    const h = canvas.height;
    const cx = w / 2;
    const cy = h / 2 + 20;
    const faceRadius = 70;

    // 머리카락 (윗부분)
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(cx, cy - 60, faceRadius * 0.9, 30, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#a3c9f7';
    ctx.globalAlpha = 0.7;
    ctx.fill();
    ctx.restore();

    // 얼굴 원
    ctx.save();
    ctx.globalAlpha = 0.85;
    ctx.beginPath();
    ctx.arc(cx, cy, faceRadius, 0, Math.PI * 2);
    ctx.fillStyle = '#fffbe7';
    ctx.shadowColor = '#e0e0e0';
    ctx.shadowBlur = 10;
    ctx.fill();
    ctx.restore();

    // 볼터치
    ctx.save();
    ctx.globalAlpha = 0.25;
    ctx.beginPath();
    ctx.ellipse(cx - 32, cy + 10, 14, 7, 0, 0, Math.PI * 2);
    ctx.ellipse(cx + 32, cy + 10, 14, 7, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#ffb6b6';
    ctx.fill();
    ctx.restore();

    // 눈썹
    ctx.save();
    ctx.strokeStyle = '#444';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(cx - 25, cy - 32, 13, Math.PI * 1.1, Math.PI * 1.9, false);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(cx + 25, cy - 32, 13, Math.PI * 1.1, Math.PI * 1.9, false);
    ctx.stroke();
    ctx.restore();

    // 눈
    ctx.beginPath();
    ctx.arc(cx - 25, cy - 20, 11, 0, Math.PI * 2);
    ctx.arc(cx + 25, cy - 20, 11, 0, Math.PI * 2);
    ctx.fillStyle = '#222';
    ctx.fill();

    // 눈동자(살짝 움직임)
    const t = Date.now() / 500;
    const eyeOffsetX = Math.sin(t) * 2;
    ctx.beginPath();
    ctx.arc(cx - 25 + eyeOffsetX, cy - 20, 5, 0, Math.PI * 2);
    ctx.arc(cx + 25 + eyeOffsetX, cy - 20, 5, 0, Math.PI * 2);
    ctx.fillStyle = '#fff';
    ctx.fill();

    // 눈 하이라이트
    ctx.save();
    ctx.globalAlpha = 0.7;
    ctx.beginPath();
    ctx.arc(cx - 28 + eyeOffsetX, cy - 23, 2, 0, Math.PI * 2);
    ctx.arc(cx + 22 + eyeOffsetX, cy - 23, 2, 0, Math.PI * 2);
    ctx.fillStyle = '#a3c9f7';
    ctx.fill();
    ctx.restore();

    // 입 (닫힌 상태, 살짝 미소)
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(cx - 15, cy + 32);
    ctx.quadraticCurveTo(cx, cy + 40, cx + 15, cy + 32);
    ctx.quadraticCurveTo(cx, cy + 38, cx - 15, cy + 32);
    ctx.closePath();
    ctx.fillStyle = '#e57373';
    ctx.globalAlpha = 0.85;
    ctx.fill();
    ctx.restore();

    // 눈동자만 반복 애니
    if (!aiFaceAnimationActive) {
        requestAnimationFrame(() => drawAIFaceStatic(ctx, canvas));
    }
}

function drawAIFace() {
    const canvas = document.getElementById('aiFaceCanvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const w = canvas.width;
    const h = canvas.height;
    const cx = w / 2;
    const cy = h / 2 + 20;
    const faceRadius = 70;

    // 머리카락 (윗부분)
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(cx, cy - 60, faceRadius * 0.9, 30, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#a3c9f7';
    ctx.globalAlpha = 0.7;
    ctx.fill();
    ctx.restore();

    // 얼굴 원
    ctx.save();
    ctx.globalAlpha = 0.85;
    ctx.beginPath();
    ctx.arc(cx, cy, faceRadius, 0, Math.PI * 2);
    ctx.fillStyle = '#fffbe7';
    ctx.shadowColor = '#e0e0e0';
    ctx.shadowBlur = 10;
    ctx.fill();
    ctx.restore();

    // 볼터치
    ctx.save();
    ctx.globalAlpha = 0.25;
    ctx.beginPath();
    ctx.ellipse(cx - 32, cy + 10, 14, 7, 0, 0, Math.PI * 2);
    ctx.ellipse(cx + 32, cy + 10, 14, 7, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#ffb6b6';
    ctx.fill();
    ctx.restore();

    // 눈썹
    ctx.save();
    ctx.strokeStyle = '#444';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(cx - 25, cy - 32, 13, Math.PI * 1.1, Math.PI * 1.9, false);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(cx + 25, cy - 32, 13, Math.PI * 1.1, Math.PI * 1.9, false);
    ctx.stroke();
    ctx.restore();

    // 눈
    ctx.beginPath();
    ctx.arc(cx - 25, cy - 20, 11, 0, Math.PI * 2);
    ctx.arc(cx + 25, cy - 20, 11, 0, Math.PI * 2);
    ctx.fillStyle = '#222';
    ctx.fill();

    // 눈동자(간단한 애니)
    const t = Date.now() / 300;
    const eyeOffsetX = Math.sin(t) * 3;
    ctx.beginPath();
    ctx.arc(cx - 25 + eyeOffsetX, cy - 20, 5, 0, Math.PI * 2);
    ctx.arc(cx + 25 + eyeOffsetX, cy - 20, 5, 0, Math.PI * 2);
    ctx.fillStyle = '#fff';
    ctx.fill();

    // 눈 하이라이트
    ctx.save();
    ctx.globalAlpha = 0.7;
    ctx.beginPath();
    ctx.arc(cx - 28 + eyeOffsetX, cy - 23, 2, 0, Math.PI * 2);
    ctx.arc(cx + 22 + eyeOffsetX, cy - 23, 2, 0, Math.PI * 2);
    ctx.fillStyle = '#a3c9f7';
    ctx.fill();
    ctx.restore();

    // 입 (말하는 애니, 자연스러운 입모양)
    const mouthOpen = aiFaceAnimationActive ? (Math.abs(Math.sin(t * 2)) * 18 + 8) : 8;
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(cx - 15, cy + 32);
    ctx.quadraticCurveTo(cx, cy + 32 + mouthOpen, cx + 15, cy + 32);
    ctx.quadraticCurveTo(cx, cy + 38, cx - 15, cy + 32);
    ctx.closePath();
    ctx.fillStyle = '#e57373';
    ctx.globalAlpha = 0.85;
    ctx.fill();
    ctx.restore();

    // 다음 프레임
    if (aiFaceAnimationActive) {
        aiFaceAnimationFrame = requestAnimationFrame(drawAIFace);
    }
}

console.log("webrtc.js loaded successfully");