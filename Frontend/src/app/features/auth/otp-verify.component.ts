import { Component, OnInit, OnDestroy, ViewChildren, QueryList, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { interval, Subscription, takeWhile } from 'rxjs';

@Component({
  selector: 'app-otp-verify',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4 py-12 relative overflow-hidden">
      <!-- Background orbs -->
      <div class="absolute inset-0 z-0">
        <div class="absolute w-[600px] h-[600px] rounded-full opacity-20 -top-40 -right-20" style="background: radial-gradient(circle, #f2ca50 0%, transparent 70%); filter: blur(100px);"></div>
        <div class="absolute w-[500px] h-[500px] rounded-full opacity-15 -bottom-40 -left-20" style="background: radial-gradient(circle, #d4af37 0%, transparent 70%); filter: blur(120px);"></div>
      </div>

      <div class="relative z-10 w-full max-w-md animate-fadeInUp">
        <div class="bg-[#1c1b1b]/90 backdrop-blur-2xl border border-[rgba(242,202,80,0.12)] rounded-2xl p-10 shadow-[0_30px_80px_rgba(0,0,0,0.5)]">

          <!-- Header -->
          <div class="text-center mb-10">
            <a routerLink="/" class="inline-block mb-6">
              <span class="font-serif italic text-3xl text-[#f2ca50]">SkyHorizon</span>
            </a>
            <div class="w-16 h-16 rounded-full bg-[#f2ca50]/10 border border-[#f2ca50]/20 flex items-center justify-center mx-auto mb-6">
              <span class="material-symbols-outlined text-[#f2ca50] text-3xl">verified_user</span>
            </div>
            <h1 class="text-2xl font-bold text-white mb-2" style="font-family:'Plus Jakarta Sans'">Verify Your Identity</h1>
            <p class="text-[#99907c] text-sm">
              We sent a 6-digit code to<br/>
              <span class="text-[#f2ca50] font-medium">{{ maskedContact }}</span>
            </p>
          </div>

          <!-- OTP Input Boxes -->
          <div class="flex justify-center gap-3 mb-8">
            @for (digit of otpDigits; track $index; let i = $index) {
              <input
                #otpInput
                type="text"
                maxlength="1"
                [value]="otpDigits[i]"
                (input)="onDigitInput($event, i)"
                (keydown)="onKeyDown($event, i)"
                (paste)="onPaste($event)"
                class="w-12 h-14 text-center text-xl font-bold rounded-lg transition-all duration-300 outline-none
                       bg-[#1e1e1e] border-2 text-[#e5e2e1]
                       focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/20 focus:shadow-[0_0_20px_rgba(242,202,80,0.15)]"
                [class]="otpDigits[i] ? 'border-[#f2ca50]/40' : 'border-[rgba(242,202,80,0.12)]'"
              />
            }
          </div>

          <!-- Error/Success Messages -->
          @if (errorMessage) {
            <div class="flex items-center gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm mb-6">
              <span class="material-symbols-outlined text-lg">error</span>
              {{ errorMessage }}
            </div>
          }

          @if (successMessage) {
            <div class="flex items-center gap-3 p-4 bg-green-500/10 border border-green-500/20 rounded-lg text-green-400 text-sm mb-6">
              <span class="material-symbols-outlined text-lg">check_circle</span>
              {{ successMessage }}
            </div>
          }

          <!-- Verify Button -->
          <button (click)="verifyOtp()" [disabled]="loading || otpCode.length < 6"
                  class="w-full py-4 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-50 disabled:cursor-not-allowed mb-6"
                  style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;">
            @if (loading) {
              <div class="flex items-center justify-center gap-2">
                <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                Verifying...
              </div>
            } @else {
              <div class="flex items-center justify-center gap-2">
                <span class="material-symbols-outlined text-lg">shield</span>
                Verify Code
              </div>
            }
          </button>

          <!-- Resend Timer -->
          <div class="text-center">
            @if (resendTimer > 0) {
              <p class="text-[#99907c] text-sm">
                Resend code in
                <span class="text-[#f2ca50] font-bold">{{ resendTimer }}s</span>
              </p>
              <!-- Timer Progress Bar -->
              <div class="mt-3 h-1 bg-[#1e1e1e] rounded-full overflow-hidden">
                <div class="h-full bg-gradient-to-r from-[#f2ca50] to-[#d4af37] rounded-full transition-all duration-1000"
                     [style.width.%]="(resendTimer / 60) * 100"></div>
              </div>
            } @else {
              <button (click)="resendOtp()" [disabled]="resendLoading"
                      class="text-[#f2ca50] hover:text-[#d4af37] font-medium text-sm transition-colors">
                @if (resendLoading) {
                  Sending...
                } @else {
                  Didn't receive? Resend Code
                }
              </button>
            }
          </div>

          <!-- Back to Login -->
          <div class="text-center mt-8 pt-6 border-t border-[rgba(242,202,80,0.12)]">
            <p class="text-sm text-[#99907c]">
              <a routerLink="/login" class="text-[#f2ca50] hover:underline font-medium">← Back to Login</a>
            </p>
          </div>

        </div>
      </div>
    </div>
  `
})
export class OtpVerifyComponent implements OnInit, OnDestroy {
  @ViewChildren('otpInput') otpInputs!: QueryList<ElementRef>;

  otpDigits: string[] = ['', '', '', '', '', ''];
  contact = '';
  loading = false;
  resendLoading = false;
  resendTimer = 60;
  errorMessage = '';
  successMessage = '';

  private timerSub?: Subscription;

  constructor(
    private auth: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.contact = params['contact'] || '';
    });
    this.startResendTimer();
  }

  ngOnDestroy() {
    this.timerSub?.unsubscribe();
  }

  get maskedContact(): string {
    if (!this.contact) return '***';
    if (this.contact.includes('@')) {
      const [local, domain] = this.contact.split('@');
      return local.substring(0, 2) + '***@' + domain;
    }
    return this.contact.substring(0, 3) + '****' + this.contact.substring(this.contact.length - 3);
  }

  get otpCode(): string {
    return this.otpDigits.join('');
  }

  onDigitInput(event: Event, index: number) {
    const input = event.target as HTMLInputElement;
    const value = input.value.replace(/\D/g, '');
    this.otpDigits[index] = value ? value[0] : '';

    if (value && index < 5) {
      const inputs = this.otpInputs.toArray();
      inputs[index + 1]?.nativeElement.focus();
    }

    // Auto-submit when all 6 digits are entered
    if (this.otpCode.length === 6) {
      this.verifyOtp();
    }
  }

  onKeyDown(event: KeyboardEvent, index: number) {
    if (event.key === 'Backspace' && !this.otpDigits[index] && index > 0) {
      const inputs = this.otpInputs.toArray();
      inputs[index - 1]?.nativeElement.focus();
    }
  }

  onPaste(event: ClipboardEvent) {
    event.preventDefault();
    const pastedData = event.clipboardData?.getData('text')?.replace(/\D/g, '').substring(0, 6);
    if (!pastedData) return;

    for (let i = 0; i < 6; i++) {
      this.otpDigits[i] = pastedData[i] || '';
    }

    const inputs = this.otpInputs.toArray();
    const focusIndex = Math.min(pastedData.length, 5);
    inputs[focusIndex]?.nativeElement.focus();

    if (pastedData.length === 6) {
      this.verifyOtp();
    }
  }

  startResendTimer() {
    this.resendTimer = 60;
    this.timerSub?.unsubscribe();
    this.timerSub = interval(1000)
      .pipe(takeWhile(() => this.resendTimer > 0))
      .subscribe(() => this.resendTimer--);
  }

  resendOtp() {
    if (!this.contact) {
      this.errorMessage = 'No contact information available.';
      return;
    }
    this.resendLoading = true;
    this.errorMessage = '';
    this.auth.sendOtp({ emailOrPhone: this.contact }).subscribe({
      next: (res: any) => {
        this.resendLoading = false;
        this.toast.success('OTP resent successfully!');
        this.startResendTimer();
      },
      error: (err) => {
        this.resendLoading = false;
        this.errorMessage = err.error?.error || 'Failed to resend OTP.';
      }
    });
  }

  verifyOtp() {
    if (this.otpCode.length < 6) return;
    this.loading = true;
    this.errorMessage = '';
    this.auth.verifyOtp({ emailOrPhone: this.contact, otpCode: this.otpCode }).subscribe({
      next: (res: any) => {
        this.loading = false;
        if (res.verified) {
          this.successMessage = 'Verified! Redirecting...';
          this.toast.success('OTP verified successfully!');
          if (res.token) {
            this.auth.handleSsoCallback(res.token);
          }
          setTimeout(() => {
            const role = this.auth.userRole;
            switch (role) {
              case 'Admin': this.router.navigate(['/admin']); break;
              case 'Staff': this.router.navigate(['/staff']); break;
              case 'Dealer': this.router.navigate(['/dealer']); break;
              default: this.router.navigate(['/passenger/search']); break;
            }
          }, 1500);
        } else {
          this.errorMessage = 'Invalid OTP. Please try again.';
        }
      },
      error: (err) => {
        this.loading = false;
        const msg = err.error?.error || 'OTP verification failed.';
        if (msg.toLowerCase().includes('expired')) {
          this.errorMessage = 'OTP has expired. Please request a new one.';
        } else {
          this.errorMessage = msg;
        }
      }
    });
  }
}
