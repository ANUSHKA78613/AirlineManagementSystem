import { Component, EventEmitter, Output, OnDestroy, AfterViewInit, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Html5Qrcode, Html5QrcodeScannerState } from 'html5-qrcode';

@Component({
  selector: 'app-qr-scanner',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="qr-scanner-wrapper">
      <div class="scanner-header">
        <div class="scanner-status" [class.active]="scanning" [class.idle]="!scanning">
          <span class="status-dot"></span>
          {{ scanning ? 'Camera Active' : 'Camera Ready' }}
        </div>
      </div>

      <div id="qr-reader" class="qr-reader-container" [class.active]="scanning"></div>

      <div class="scanner-controls">
        @if (!scanning) {
          <button class="scan-btn start" (click)="startScanning()">
            <i class="fa-solid fa-camera"></i> Start Camera Scan
          </button>
        } @else {
          <button class="scan-btn stop" (click)="stopScanning()">
            <i class="fa-solid fa-stop"></i> Stop Scanner
          </button>
        }
      </div>

      @if (lastScannedData) {
        <div class="scan-result">
          <div class="result-icon"><i class="fa-solid fa-qrcode"></i></div>
          <div class="result-data">
            <span class="result-label">Last Scanned</span>
            <span class="result-value">{{ lastScannedData }}</span>
          </div>
        </div>
      }

      @if (errorMessage) {
        <div class="scan-error">
          <i class="fa-solid fa-triangle-exclamation"></i> {{ errorMessage }}
        </div>
      }
    </div>
  `,
  styles: [`
    .qr-scanner-wrapper {
      border: 1px solid var(--border-color);
      border-radius: var(--radius-lg, 12px);
      overflow: hidden;
      background: rgba(17, 21, 56, 0.6);
      backdrop-filter: blur(20px);
    }

    .scanner-header {
      padding: 12px 16px;
      border-bottom: 1px solid var(--border-color);
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .scanner-status {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 1px;
    }

    .status-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #666;
      transition: background 0.3s;
    }

    .scanner-status.active .status-dot {
      background: #10b981;
      box-shadow: 0 0 8px rgba(16, 185, 129, 0.6);
      animation: pulse 1.5s ease-in-out infinite;
    }

    .scanner-status.idle .status-dot {
      background: #f59e0b;
    }

    .qr-reader-container {
      width: 100%;
      min-height: 60px;
      background: #0a0e27;
      position: relative;
    }

    .qr-reader-container.active {
      min-height: 280px;
    }

    .scanner-controls {
      padding: 16px;
      display: flex;
      justify-content: center;
    }

    .scan-btn {
      padding: 12px 28px;
      border: none;
      border-radius: 10px;
      font-weight: 700;
      font-size: 14px;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 8px;
      transition: all 0.3s;
      font-family: 'Inter', sans-serif;
    }

    .scan-btn.start {
      background: linear-gradient(135deg, #2b6fff, #1a4fd0);
      color: white;
      box-shadow: 0 4px 16px rgba(43, 111, 255, 0.3);
    }

    .scan-btn.start:hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 24px rgba(43, 111, 255, 0.5);
    }

    .scan-btn.stop {
      background: linear-gradient(135deg, #ef4444, #b91c1c);
      color: white;
      box-shadow: 0 4px 16px rgba(239, 68, 68, 0.3);
    }

    .scan-btn.stop:hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 24px rgba(239, 68, 68, 0.5);
    }

    .scan-result {
      margin: 0 16px 16px;
      padding: 14px 16px;
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: 10px;
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .result-icon {
      font-size: 24px;
      color: #10b981;
    }

    .result-label {
      display: block;
      font-size: 10px;
      text-transform: uppercase;
      letter-spacing: 1.5px;
      color: var(--text-muted);
      margin-bottom: 2px;
    }

    .result-value {
      font-size: 16px;
      font-weight: 800;
      font-family: 'Courier New', monospace;
      letter-spacing: 2px;
      color: #10b981;
    }

    .scan-error {
      margin: 0 16px 16px;
      padding: 12px 16px;
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 10px;
      font-size: 13px;
      color: #ef4444;
      display: flex;
      align-items: center;
      gap: 8px;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.4; }
    }
  `]
})
export class QrScannerComponent implements AfterViewInit, OnDestroy {
  @Output() qrScanned = new EventEmitter<string>();
  @Input() autoStart = false;

  scanning = false;
  lastScannedData = '';
  errorMessage = '';

  private html5QrCode: Html5Qrcode | null = null;
  private readonly readerId = 'qr-reader';

  ngAfterViewInit() {
    // Small delay to ensure the DOM element is rendered
    setTimeout(() => {
      try {
        this.html5QrCode = new Html5Qrcode(this.readerId);
        if (this.autoStart) {
          this.startScanning();
        }
      } catch (err) {
        this.errorMessage = 'QR scanner initialization failed';
      }
    }, 200);
  }

  ngOnDestroy() {
    this.stopScanning();
  }

  async startScanning() {
    if (!this.html5QrCode) {
      this.errorMessage = 'Scanner not initialized';
      return;
    }

    this.errorMessage = '';

    try {
      await this.html5QrCode.start(
        { facingMode: 'environment' },
        {
          fps: 10,
          qrbox: { width: 220, height: 220 },
          aspectRatio: 1.0,
        },
        (decodedText: string) => {
          this.lastScannedData = decodedText;
          this.qrScanned.emit(decodedText);
          // Vibrate for haptic feedback (if supported)
          if (navigator.vibrate) {
            navigator.vibrate(100);
          }
        },
        () => { /* ignore scan errors (no QR found in frame) */ }
      );
      this.scanning = true;
    } catch (err: any) {
      this.errorMessage = err?.message || 'Could not access camera. Please grant camera permission.';
      this.scanning = false;
    }
  }

  async stopScanning() {
    if (this.html5QrCode) {
      try {
        const state = this.html5QrCode.getState();
        if (state === Html5QrcodeScannerState.SCANNING || state === Html5QrcodeScannerState.PAUSED) {
          await this.html5QrCode.stop();
        }
      } catch {
        // Ignore stop errors
      }
    }
    this.scanning = false;
  }
}
