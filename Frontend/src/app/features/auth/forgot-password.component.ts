import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4 py-12 relative overflow-hidden bg-cover bg-center bg-no-repeat" style="background-image: url('assets/images/custom-auth-bg.png');">
      <!-- Dark Overlay -->
      <div class="absolute inset-0 z-0 bg-[#0a0a0a]/70 backdrop-blur-[2px]"></div>

      <!-- Card -->
      <div class="relative z-10 w-full max-w-md animate-fadeInUp">
        <div class="bg-[#1c1b1b]/90 backdrop-blur-2xl border border-[rgba(242,202,80,0.12)] rounded-2xl p-10 shadow-[0_30px_80px_rgba(0,0,0,0.5)]">
          
          <!-- Header -->
          <div class="text-center mb-10">
            <a routerLink="/" class="inline-block mb-6">
              <span class="font-serif italic text-3xl text-[#f2ca50]">SkyHorizon</span>
            </a>

            @if (step === 'email') {
              <h1 class="text-2xl font-bold text-white mb-2">Login with OTP</h1>
              <p class="text-[#99907c] text-sm">Enter your email and we'll send you an OTP</p>
            } @else {
              <h1 class="text-2xl font-bold text-white mb-2">Verify OTP</h1>
              <p class="text-[#99907c] text-sm">Enter the code sent to {{ email }}</p>
            }
          </div>

          <!-- Step 1: Email -->
          @if (step === 'email') {
            <form (ngSubmit)="sendOtp()" class="space-y-5">
              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Email Address</label>
                <div class="relative">
                  <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">mail</span>
                  <input type="email" [(ngModel)]="email" name="email"
                         class="w-full pl-12 pr-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                         placeholder="you@example.com" required />
                </div>
              </div>

              @if (errorMessage) {
                <div class="flex items-center gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
                  <span class="material-symbols-outlined text-lg">error</span>
                  {{ errorMessage }}
                </div>
              }

              <button type="submit" [disabled]="loading"
                      class="w-full py-4 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-50"
                      style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;">
                @if (loading) {
                  <div class="flex items-center justify-center gap-2">
                    <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                    Sending OTP...
                  </div>
                } @else {
                  <div class="flex items-center justify-center gap-2">
                    <span class="material-symbols-outlined text-lg">send</span>
                    Send Login Code
                  </div>
                }
              </button>
            </form>
          }

          <!-- Step 2: OTP Login -->
          @if (step === 'otp') {
            <form (ngSubmit)="loginWithOtp()" class="space-y-5">
              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">OTP Code</label>
                <input type="text" [(ngModel)]="otpCode" name="otp"
                       class="w-full py-4 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#f2ca50] text-center text-2xl tracking-[8px] font-mono font-bold placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none"
                       placeholder="000000" maxlength="6" required />
              </div>

              @if (errorMessage) {
                <div class="flex items-center gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
                  <span class="material-symbols-outlined text-lg">error</span>
                  {{ errorMessage }}
                </div>
              }

              <button type="submit" [disabled]="loading"
                      class="w-full py-4 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-50"
                      style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;">
                @if (loading) {
                  <div class="flex items-center justify-center gap-2">
                    <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                    Logging in...
                  </div>
                } @else {
                  <div class="flex items-center justify-center gap-2">
                    <span class="material-symbols-outlined text-lg">login</span>
                    Log In
                  </div>
                }
              </button>

              <button type="button" (click)="sendOtp()" [disabled]="loading || resendCooldown > 0"
                      class="w-full py-3 rounded-lg border border-[rgba(242,202,80,0.12)] text-[#d6c692] hover:bg-[#f2ca50]/5 hover:border-[#f2ca50]/30 transition-all text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed">
                {{ resendCooldown > 0 ? 'Resend in ' + resendCooldown + 's' : (loading ? 'Sending...' : 'Resend OTP') }}
              </button>
            </form>
          }

          <!-- Footer -->
          <div class="text-center mt-8 pt-6 border-t border-[rgba(242,202,80,0.12)]">
            <p class="text-sm text-[#99907c]">Remember your password? <a routerLink="/login" class="text-[#f2ca50] hover:underline font-medium">Sign In</a></p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ForgotPasswordComponent implements OnInit {
  email = '';
  otpCode = '';
  step: 'email' | 'otp' = 'email';
  loading = false;
  errorMessage = '';
  resendCooldown = 0;
  private cooldownTimer: any = null;

  constructor(private auth: AuthService, private router: Router, private toast: ToastService, private route: ActivatedRoute) { }

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      if (params['email']) {
        this.email = params['email'];
      }
    });
  }

  sendOtp() {
    if (this.loading || this.resendCooldown > 0) return;
    this.loading = true;
    this.errorMessage = '';
    this.auth.forgotPassword(this.email).subscribe({
      next: (res: any) => {
        this.loading = false;
        this.step = 'otp';
        this.toast.success('OTP sent to your email!');
        this.startResendCooldown();
      },
      error: (err) => {
        this.loading = false;
        this.step = 'otp';
        this.toast.info('Enter the OTP sent to your email');
        this.startResendCooldown();
      }
    });
  }

  private startResendCooldown() {
    this.resendCooldown = 30;
    if (this.cooldownTimer) clearInterval(this.cooldownTimer);
    this.cooldownTimer = setInterval(() => {
      this.resendCooldown--;
      if (this.resendCooldown <= 0) {
        clearInterval(this.cooldownTimer);
        this.cooldownTimer = null;
      }
    }, 1000);
  }

  loginWithOtp() {
    this.errorMessage = '';
    this.loading = true;
    this.auth.verifyOtp({ emailOrPhone: this.email, otpCode: this.otpCode }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.token) {
          this.toast.success('Login successful!');
          const role = this.auth.userRole;
          switch (role) {
            case 'Admin': this.router.navigate(['/admin']); break;
            case 'Staff': this.router.navigate(['/staff']); break;
            case 'Dealer': this.router.navigate(['/dealer']); break;
            default: this.router.navigate(['/']); break;
          }
        } else {
          this.errorMessage = 'Login failed. No token received.';
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Login failed. Check your OTP.';
      }
    });
  }
}

