// CCCD Reader & Face Verification Service - Frontend Application Logic
document.addEventListener('DOMContentLoaded', () => {
  // DOM Elements
  const chipWsStatus = document.getElementById('chip-ws-status');
  const chipReaderStatus = document.getElementById('chip-reader-status');
  const chipCardStatus = document.getElementById('chip-card-status');
  const chipCameraStatus = document.getElementById('chip-camera-status');

  const sessionBadge = document.getElementById('session-badge');
  const cccdPhotoContainer = document.getElementById('cccd-photo-container');
  const cccdFaceImg = document.getElementById('cccd-face-img');
  const cccdValNumber = document.getElementById('cccd-val-number');
  const cccdValName = document.getElementById('cccd-val-name');
  const cccdValDob = document.getElementById('cccd-val-dob');
  const cccdValGender = document.getElementById('cccd-val-gender');
  const cccdValHometown = document.getElementById('cccd-val-hometown');
  const cccdValExpiry = document.getElementById('cccd-val-expiry');
  const cccdValAddress = document.getElementById('cccd-val-address');

  const btnReadCard = document.getElementById('btn-read-card');
  const btnCancelSession = document.getElementById('btn-cancel-session');
  const btnRefreshStatus = document.getElementById('btn-refresh-status');

  const cameraVideo = document.getElementById('camera-video');
  const cameraPreviewImg = document.getElementById('camera-preview-img');
  const btnVerifyFace = document.getElementById('btn-verify-face');
  const btnCaptureFrame = document.getElementById('btn-capture-frame');
  const btnSwitchCamMode = document.getElementById('btn-switch-cam-mode');

  const livenessBadge = document.getElementById('liveness-badge');
  const verificationConclusion = document.getElementById('verification-conclusion');
  const valSimilarity = document.getElementById('val-similarity');
  const barSimilarityFill = document.getElementById('bar-similarity-fill');

  const logConsole = document.getElementById('log-console');
  const btnClearLog = document.getElementById('btn-clear-log');

  let activeSessionId = null;
  let hasActiveCardFace = false;
  let useBrowserWebcam = true;
  let localVideoStream = null;

  // 1. Logger helper
  function logEvent(eventName, message, type = 'info') {
    const entry = document.createElement('div');
    entry.className = 'log-entry';

    const now = new Date();
    const timeStr = now.toTimeString().split(' ')[0] + '.' + String(now.getMilliseconds()).padStart(3, '0');

    entry.innerHTML = `
      <span class="log-time">[${timeStr}]</span>
      <span class="log-event">${eventName}:</span>
      <span class="log-msg ${type}">${message}</span>
    `;

    logConsole.appendChild(entry);
    logConsole.scrollTop = logConsole.scrollHeight;

    // Giới hạn số lượng dòng log
    while (logConsole.children.length > 200) {
      logConsole.removeChild(logConsole.firstChild);
    }
  }

  btnClearLog.addEventListener('click', () => {
    logConsole.innerHTML = '';
    logEvent('SYSTEM', 'Đã dọn sạch lịch sử nhật ký.');
  });

  // 2. SignalR WebSocket Client Setup
  let connection = null;
  function initSignalR() {
    if (typeof signalR === 'undefined') {
      logEvent('SIGNALR', 'Không thể nạp thư viện signalr.js', 'error');
      return;
    }

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/ws/device')
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    connection.on('CardInserted', (payload) => {
      logEvent('CARD_INSERTED', 'Phát hiện thẻ CCCD cắm vào khe đọc.', 'success');
      chipCardStatus.className = 'status-chip online';
      chipCardStatus.querySelector('.chip-text').textContent = 'Thẻ: Đã cắm';
      
      // Gợi ý tự động hoặc thông báo
      sessionBadge.textContent = 'Thẻ sẵn sàng';
      sessionBadge.className = 'result-badge badge-real';
    });

    connection.on('CardRemoved', (payload) => {
      logEvent('CARD_REMOVED', 'Thẻ CCCD đã được rút ra khỏi khe đọc.', 'warning');
      chipCardStatus.className = 'status-chip offline';
      chipCardStatus.querySelector('.chip-text').textContent = 'Thẻ: Chưa cắm';
    });

    connection.on('CardReadSuccess', (payload) => {
      logEvent('CARD_READ_SUCCESS', `Đọc thành công thẻ: ${payload?.cardData?.cardNumber || ''}`, 'success');
      if (payload && payload.cardData) {
        renderCardData(payload.cardData, payload.sessionId);
      }
    });

    connection.on('DeviceStatusChanged', (payload) => {
      logEvent('DEVICE_STATUS', `${payload.device}: ${payload.status}`);
      fetchDeviceStatus();
    });

    connection.onreconnecting(() => {
      chipWsStatus.className = 'status-chip busy';
      chipWsStatus.querySelector('.chip-text').textContent = 'SignalR: Đang kết nối lại...';
      logEvent('SIGNALR', 'Mất kết nối WebSocket, đang tự động kết nối lại...', 'warning');
    });

    connection.onreconnected(() => {
      chipWsStatus.className = 'status-chip online';
      chipWsStatus.querySelector('.chip-text').textContent = 'SignalR: Đã kết nối';
      logEvent('SIGNALR', 'WebSocket đã kết nối lại thành công.', 'success');
      fetchDeviceStatus();
    });

    connection.onclose(() => {
      chipWsStatus.className = 'status-chip offline';
      chipWsStatus.querySelector('.chip-text').textContent = 'SignalR: Đã ngắt';
      logEvent('SIGNALR', 'Kết nối WebSocket đã đóng.', 'error');
    });

    connection.start()
      .then(() => {
        chipWsStatus.className = 'status-chip online';
        chipWsStatus.querySelector('.chip-text').textContent = 'SignalR: Đã kết nối';
        logEvent('SIGNALR', 'Kết nối thành công kênh sự kiện /ws/device', 'success');
      })
      .catch(err => {
        chipWsStatus.className = 'status-chip offline';
        chipWsStatus.querySelector('.chip-text').textContent = 'SignalR: Lỗi kết nối';
        logEvent('SIGNALR', 'Không thể kết nối WebSocket: ' + err.message, 'error');
      });
  }

  // 3. REST API: Kiểm tra trạng thái thiết bị
  async function fetchDeviceStatus() {
    try {
      const res = await fetch('/api/device/status');
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const data = await res.json();

      // Đầu đọc
      if (data.cardReaderStatus === 'Ready') {
        chipReaderStatus.className = 'status-chip online';
        chipReaderStatus.querySelector('.chip-text').textContent = 'Đầu đọc: Sẵn sàng';
      } else if (data.cardReaderStatus === 'Busy') {
        chipReaderStatus.className = 'status-chip busy';
        chipReaderStatus.querySelector('.chip-text').textContent = 'Đầu đọc: Đang bận';
      } else {
        chipReaderStatus.className = 'status-chip offline';
        chipReaderStatus.querySelector('.chip-text').textContent = 'Đầu đọc: Mất kết nối';
      }

      // Camera
      if (data.cameraStatus === 'Ready') {
        chipCameraStatus.className = 'status-chip online';
        chipCameraStatus.querySelector('.chip-text').textContent = 'Camera: Sẵn sàng';
      } else if (data.cameraStatus === 'Busy') {
        chipCameraStatus.className = 'status-chip busy';
        chipCameraStatus.querySelector('.chip-text').textContent = 'Camera: Đang bận';
      } else {
        chipCameraStatus.className = 'status-chip offline';
        chipCameraStatus.querySelector('.chip-text').textContent = 'Camera: Mất kết nối';
      }

      // Phiên đang hoạt động
      if (data.activeSessionId) {
        activeSessionId = data.activeSessionId;
        btnCancelSession.disabled = false;
        sessionBadge.textContent = 'Phiên đang hoạt động';
        sessionBadge.className = 'result-badge badge-real';
      }

    } catch (err) {
      logEvent('API_STATUS', 'Lỗi kiểm tra trạng thái thiết bị: ' + err.message, 'error');
    }
  }

  // 4. Render thông tin thẻ
  function renderCardData(cardData, sessionId) {
    if (!cardData) return;

    activeSessionId = sessionId || activeSessionId;
    btnCancelSession.disabled = false;
    sessionBadge.textContent = 'Phiên: ' + (activeSessionId ? activeSessionId.substring(0, 8) + '...' : 'Đang mở');
    sessionBadge.className = 'result-badge badge-real';

    cccdValNumber.textContent = cardData.cardNumber || '---';
    cccdValName.textContent = cardData.fullName || '---';
    cccdValDob.textContent = cardData.dateOfBirth || '---';
    cccdValGender.textContent = cardData.gender || '---';
    cccdValHometown.textContent = cardData.hometown || '---';
    cccdValExpiry.textContent = cardData.expiryDate || '---';
    cccdValAddress.textContent = cardData.permanentAddress || '---';

    if (cardData.faceImageBase64) {
      hasActiveCardFace = true;
      let src = cardData.faceImageBase64;
      if (!src.startsWith('data:')) {
        src = 'data:image/jpeg;base64,' + src;
      }
      cccdFaceImg.src = src;
      cccdPhotoContainer.classList.add('has-photo');
    } else {
      hasActiveCardFace = false;
      cccdFaceImg.src = '';
      cccdPhotoContainer.classList.remove('has-photo');
    }
  }

  // 5. Nút bấm: Đọc thẻ CCCD
  btnReadCard.addEventListener('click', async () => {
    btnReadCard.disabled = true;
    btnReadCard.textContent = 'Đang đọc thẻ...';
    logEvent('CARD_READ', 'Gửi yêu cầu đọc thẻ chip vi mạch CCCD...');

    try {
      const res = await fetch('/api/card/read', { method: 'POST' });
      const data = await res.json();

      if (res.ok) {
        logEvent('CARD_READ', `Đọc thành công CCCD: ${data.cardData?.cardNumber} (${data.cardData?.fullName})`, 'success');
        renderCardData(data.cardData, data.sessionId);
      } else {
        logEvent('CARD_READ', `Lỗi đọc thẻ (${res.status}): ${data.message || 'Không rõ nguyên nhân'}`, 'error');
        alert('Lỗi: ' + (data.message || 'Không thể đọc thẻ CCCD.'));
      }
    } catch (err) {
      logEvent('CARD_READ', 'Lỗi mạng khi đọc thẻ: ' + err.message, 'error');
      alert('Không thể kết nối đến máy chủ đọc thẻ.');
    } finally {
      btnReadCard.disabled = false;
      btnReadCard.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
          <polyline points="22,6 12,13 2,6"/>
        </svg>
        Đọc thẻ CCCD
      `;
    }
  });

  // 6. Nút bấm: Hủy phiên
  btnCancelSession.addEventListener('click', async () => {
    if (!confirm('Bạn có chắc chắn muốn hủy phiên làm việc hiện tại không?')) return;

    try {
      const res = await fetch('/api/session/cancel', { method: 'POST' });
      if (res.ok) {
        logEvent('SESSION', 'Đã hủy phiên làm việc hiện tại.', 'warning');
        resetSessionUI();
      }
    } catch (err) {
      logEvent('SESSION', 'Lỗi khi hủy phiên: ' + err.message, 'error');
    }
  });

  function resetSessionUI() {
    activeSessionId = null;
    hasActiveCardFace = false;
    btnCancelSession.disabled = true;
    sessionBadge.textContent = 'Chưa có phiên';
    sessionBadge.className = 'result-badge badge-waiting';

    cccdValNumber.textContent = '---';
    cccdValName.textContent = '---';
    cccdValDob.textContent = '---';
    cccdValGender.textContent = '---';
    cccdValHometown.textContent = '---';
    cccdValExpiry.textContent = '---';
    cccdValAddress.textContent = '---';

    cccdFaceImg.src = '';
    cccdPhotoContainer.classList.remove('has-photo');

    livenessBadge.textContent = 'Chờ xác thực';
    livenessBadge.className = 'result-badge badge-waiting';
    verificationConclusion.textContent = 'Chưa thực hiện';
    valSimilarity.textContent = '0%';
    barSimilarityFill.style.width = '0%';
  }

  btnRefreshStatus.addEventListener('click', () => {
    logEvent('STATUS', 'Đang cập nhật trạng thái thiết bị...');
    fetchDeviceStatus();
  });

  // 7. Khởi động Camera Browser (Webcam)
  async function startBrowserCamera() {
    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
      try {
        localVideoStream = await navigator.mediaDevices.getUserMedia({
          video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' }
        });
        cameraVideo.srcObject = localVideoStream;
        cameraVideo.style.display = 'block';
        cameraPreviewImg.style.display = 'none';
        logEvent('CAMERA', 'Đã mở luồng video webcam trực tiếp trên trình duyệt.', 'success');
      } catch (err) {
        logEvent('CAMERA', 'Không thể mở webcam trình duyệt (' + err.name + '). Sẽ sử dụng camera dịch vụ.', 'warning');
        useBrowserWebcam = false;
        btnSwitchCamMode.textContent = 'Dùng Camera Service';
      }
    } else {
      useBrowserWebcam = false;
    }
  }

  btnSwitchCamMode.addEventListener('click', () => {
    useBrowserWebcam = !useBrowserWebcam;
    if (useBrowserWebcam) {
      btnSwitchCamMode.textContent = 'Dùng Camera Trực Tiếp';
      startBrowserCamera();
    } else {
      btnSwitchCamMode.textContent = 'Dùng Camera Service';
      if (localVideoStream) {
        localVideoStream.getTracks().forEach(t => t.stop());
        localVideoStream = null;
      }
      cameraVideo.style.display = 'none';
      cameraPreviewImg.style.display = 'block';
      logEvent('CAMERA', 'Chuyển sang chế độ gọi Camera phần cứng của Service.');
    }
  });

  // 8. Chụp khung hình từ Camera
  btnCaptureFrame.addEventListener('click', async () => {
    logEvent('CAMERA', 'Đang chụp khung hình...');
    if (useBrowserWebcam && localVideoStream) {
      const canvas = document.createElement('canvas');
      canvas.width = cameraVideo.videoWidth || 640;
      canvas.height = cameraVideo.videoHeight || 480;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(cameraVideo, 0, 0, canvas.width, canvas.height);
      const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
      cameraPreviewImg.src = dataUrl;
      cameraPreviewImg.style.display = 'block';
      cameraVideo.style.display = 'none';
      logEvent('CAMERA', 'Đã chụp khung hình từ webcam trình duyệt.', 'success');
    } else {
      try {
        const res = await fetch('/api/face/capture');
        const data = await res.json();
        if (res.ok && data.imageBase64) {
          cameraPreviewImg.src = 'data:image/jpeg;base64,' + data.imageBase64;
          cameraPreviewImg.style.display = 'block';
          cameraVideo.style.display = 'none';
          logEvent('CAMERA', 'Đã chụp khung hình thành công từ Camera Service.', 'success');
        } else {
          alert('Lỗi chụp camera: ' + (data.message || 'Không khả dụng.'));
        }
      } catch (err) {
        logEvent('CAMERA', 'Lỗi gọi API capture: ' + err.message, 'error');
      }
    }
  });

  // 9. Xác thực khuôn mặt (Face Verification & Anti-Spoofing)
  btnVerifyFace.addEventListener('click', async () => {
    if (!hasActiveCardFace) {
      alert('Vui lòng đọc thẻ CCCD trước để lấy ảnh chân dung chip đối chiếu!');
      return;
    }

    btnVerifyFace.disabled = true;
    btnVerifyFace.textContent = 'Đang nhận diện...';
    verificationConclusion.textContent = 'Đang phân tích AI sinh trắc học...';
    logEvent('FACE_VERIFY', 'Bắt đầu quá trình đối soát khuôn mặt và kiểm tra người thật (FAS)...');

    try {
      let requestBody = {};

      if (useBrowserWebcam && localVideoStream) {
        // Chụp frame từ video element
        const canvas = document.createElement('canvas');
        canvas.width = cameraVideo.videoWidth || 640;
        canvas.height = cameraVideo.videoHeight || 480;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(cameraVideo, 0, 0, canvas.width, canvas.height);
        const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
        requestBody.cameraImageBase64 = dataUrl;
      }

      const res = await fetch('/api/face/verify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestBody)
      });

      const data = await res.json();

      if (res.ok) {
        const pct = Math.round(data.similarity * 100);
        valSimilarity.textContent = pct + '%';
        barSimilarityFill.style.width = Math.min(pct, 100) + '%';

        if (data.isMatch && data.isLive) {
          livenessBadge.className = 'result-badge badge-real';
          livenessBadge.textContent = 'Người thật (Live)';
          verificationConclusion.textContent = `HỢP LỆ: Trùng khớp (${pct}%)`;
          verificationConclusion.style.color = 'var(--accent-green)';
          logEvent('FACE_VERIFY', `Xác thực THÀNH CÔNG: Độ tương đồng ${pct}%, Trạng thái: ${data.livenessStatus}`, 'success');
        } else if (data.isMatch && !data.isLive) {
          livenessBadge.className = 'result-badge badge-fake';
          livenessBadge.textContent = 'Giả mạo (Spoof)';
          verificationConclusion.textContent = `CẢNH BÁO: Giả mạo (${data.livenessStatus})`;
          verificationConclusion.style.color = 'var(--accent-rose)';
          logEvent('FACE_VERIFY', `CẢNH BÁO GIẢ MẠO: Điểm trùng ${pct}% nhưng không vượt qua liveness!`, 'warning');
        } else {
          livenessBadge.className = 'result-badge badge-fake';
          livenessBadge.textContent = 'Không khớp';
          verificationConclusion.textContent = `KHÔNG KHỚP: Chỉ đạt ${pct}%`;
          verificationConclusion.style.color = 'var(--accent-rose)';
          logEvent('FACE_VERIFY', `Xác thực KHÔNG TRÙNG KHỚP: Chỉ đạt ${pct}% tương đồng.`, 'warning');
        }

      } else {
        livenessBadge.className = 'result-badge badge-fake';
        livenessBadge.textContent = 'Lỗi nhận diện';
        verificationConclusion.textContent = data.message || 'Lỗi xác thực';
        verificationConclusion.style.color = 'var(--accent-rose)';
        logEvent('FACE_VERIFY', `Lỗi (${res.status}): ${data.message}`, 'error');
        alert('Lỗi xác thực: ' + (data.message || 'Không thể xác thực khuôn mặt.'));
      }

    } catch (err) {
      logEvent('FACE_VERIFY', 'Lỗi mạng khi gọi xác thực khuôn mặt: ' + err.message, 'error');
      alert('Không thể kết nối đến máy chủ xác thực.');
    } finally {
      btnVerifyFace.disabled = false;
      btnVerifyFace.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
          <polyline points="22 4 12 14.01 9 11.01" />
        </svg>
        Xác thực khuôn mặt
      `;
    }
  });

  // Khởi động
  logEvent('SYSTEM', 'Khởi chạy Web Dashboard. Đang kết nối dịch vụ ngầm...');
  initSignalR();
  fetchDeviceStatus();
  startBrowserCamera();
});
