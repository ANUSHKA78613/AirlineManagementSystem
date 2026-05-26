import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      @for (toast of (toastService.toasts$ | async); track toast.id) {
        <div class="toast toast-{{ toast.type }}" (click)="toastService.dismiss(toast.id)">
          <span class="toast-icon">
            @switch (toast.type) {
              @case ('success') { ✅ }
              @case ('error') { ❌ }
              @case ('info') { ℹ️ }
              @case ('warning') { ⚠️ }
            }
          </span>
          <span class="toast-msg">{{ toast.message }}</span>
          <button class="toast-close">✕</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 24px;
      right: 24px;
      z-index: 99999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      pointer-events: none;
      max-width: 420px;
      width: 100%;
    }

    .toast {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 18px;
      border-radius: 12px;
      background: rgba(28, 27, 27, 0.92);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border: 1px solid rgba(242, 202, 80, 0.15);
      color: #e5e2e1;
      font-size: 14px;
      font-weight: 500;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255,255,255,0.03);
      pointer-events: all;
      cursor: pointer;
      animation: toastSlideIn 0.35s cubic-bezier(0.22, 1, 0.36, 1);
      transition: opacity 0.3s, transform 0.3s;
    }

    .toast:hover {
      transform: translateX(-4px);
      box-shadow: 0 12px 40px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255,255,255,0.05);
    }

    .toast-icon {
      font-size: 18px;
      flex-shrink: 0;
      line-height: 1;
    }

    .toast-msg {
      flex: 1;
      line-height: 1.4;
    }

    .toast-close {
      background: none;
      border: none;
      color: rgba(214, 198, 146, 0.4);
      font-size: 14px;
      cursor: pointer;
      padding: 0 2px;
      line-height: 1;
      transition: color 0.2s;
      flex-shrink: 0;
    }
    .toast-close:hover {
      color: rgba(214, 198, 146, 0.8);
    }

    /* Type-specific accent borders */
    .toast-success {
      border-left: 3px solid #4ade80;
    }
    .toast-error {
      border-left: 3px solid #f87171;
    }
    .toast-warning {
      border-left: 3px solid #fbbf24;
    }
    .toast-info {
      border-left: 3px solid #60a5fa;
    }

    @keyframes toastSlideIn {
      from {
        opacity: 0;
        transform: translateX(100px) scale(0.95);
      }
      to {
        opacity: 1;
        transform: translateX(0) scale(1);
      }
    }

    @media (max-width: 480px) {
      .toast-container {
        top: 12px;
        right: 12px;
        left: 12px;
        max-width: none;
      }
    }
  `]
})
export class ToastComponent {
  constructor(public toastService: ToastService) {}
}
