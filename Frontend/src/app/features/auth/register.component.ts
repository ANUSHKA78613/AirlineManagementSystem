import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4 py-12 relative overflow-hidden bg-cover bg-center bg-no-repeat" style="background-image: url('assets/images/custom-auth-bg.png');">
      <!-- Dark Overlay -->
      <div class="absolute inset-0 z-0 bg-[#0a0a0a]/70 backdrop-blur-[2px]"></div>

      <!-- Register Card -->
      <div class="relative z-10 w-full max-w-md animate-fadeInUp">
        <div class="bg-[#1c1b1b]/90 backdrop-blur-2xl border border-[rgba(242,202,80,0.12)] rounded-2xl p-10 shadow-[0_30px_80px_rgba(0,0,0,0.5)]">
          
          <!-- Header -->
          <div class="text-center mb-10">
            <a routerLink="/" class="inline-block mb-6">
              <span class="font-serif italic text-3xl text-[#f2ca50]">SkyHorizon</span>
            </a>
            <h1 class="text-2xl font-bold text-white mb-2" style="font-family:'Plus Jakarta Sans'">Create Account</h1>
            <p class="text-[#99907c] text-sm">Begin your celestial journey today</p>
          </div>

          <!-- Form -->
          <form (ngSubmit)="register()" class="space-y-4">
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Full Name</label>
              <div class="relative">
                <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">person</span>
                <input type="text" [(ngModel)]="name" name="name"
                       class="w-full pl-12 pr-4 py-3 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] transition-all outline-none text-[15px]"
                       placeholder="John Doe" required />
              </div>
            </div>

            <!-- Email with Action Button -->
            <div>
              <div class="flex justify-between items-center mb-2">
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c]">Email Address</label>
                @if (emailVerified) {
                  <span class="flex items-center gap-1 text-[10px] bg-emerald-500/10 text-emerald-400 px-2 py-0.5 rounded border border-emerald-500/20">
                    <span class="material-symbols-outlined text-[12px]">verified</span> VERIFIED
                  </span>
                }
              </div>
              <div class="relative">
                <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">mail</span>
                <input type="email" [(ngModel)]="email" name="email" [disabled]="emailVerified"
                       class="w-full pl-12 pr-28 py-3 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] transition-all outline-none text-[15px] disabled:opacity-60"
                       placeholder="you@example.com" required />
                
                @if (!emailVerified) {
                  <button type="button" (click)="sendOtp()" [disabled]="sendingOtp || !email"
                          class="absolute right-2 top-1/2 -translate-y-1/2 px-3 py-1.5 bg-[#f2ca50]/10 text-[#f2ca50] text-[11px] font-bold rounded border border-[#f2ca50]/20 hover:bg-[#f2ca50]/20 transition-all disabled:opacity-50">
                    {{ sendingOtp ? 'SENDING...' : 'SEND CODE' }}
                  </button>
                }
              </div>
            </div>

            <!-- Inline OTP Field -->
            @if (otpSent && !emailVerified) {
              <div class="animate-fadeIn">
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Enter Verification Code</label>
                <div class="flex gap-2">
                  <div class="relative flex-1">
                    <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">lock_open</span>
                    <input type="text" [(ngModel)]="otpCode" name="otp" maxlength="6"
                           class="w-full pl-12 pr-4 py-3 bg-[#1e1e1e] border border-[#f2ca50]/30 rounded-lg text-[#f2ca50] font-mono tracking-widest placeholder-[#6b6358]/40 focus:border-[#f2ca50] transition-all outline-none text-[15px]"
                           placeholder="000000" required />
                  </div>
                  <button type="button" (click)="verifyOtp()" [disabled]="verifyingOtp || otpCode.length < 6"
                          class="px-5 bg-emerald-600 text-white text-[12px] font-bold rounded hover:bg-emerald-500 transition-all disabled:opacity-50">
                    {{ verifyingOtp ? '...' : 'VERIFY' }}
                  </button>
                </div>
                <p class="mt-2 text-[10px] text-[#6b6358]">OTP sent to your email. Check inbox/spam.</p>
              </div>
            }

            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Phone Number</label>
              <div class="relative">
                <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">phone</span>
                <input type="tel" [(ngModel)]="phone" name="phone"
                       class="w-full pl-12 pr-4 py-3 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] transition-all outline-none text-[15px]"
                       placeholder="+91 9876543210" required />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Account Type</label>
                <div class="relative">
                  <select [(ngModel)]="role1" name="role"
                          class="w-full pl-4 pr-10 py-3 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] focus:border-[#f2ca50] transition-all outline-none text-[15px] appearance-none"
                          required>
                    <option value="">select role</option>
                    <option *ngFor="let r of roles1" [value]="r">{{ r }}</option>
                  </select>
                  <span class="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-[#6b6358] pointer-events-none text-lg">expand_more</span>
                </div>
              </div>

              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Password</label>
                <div class="relative">
                  <input [type]="showPassword ? 'text' : 'password'" [(ngModel)]="password" name="password"
                         class="w-full px-4 py-3 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] transition-all outline-none text-[15px]"
                         placeholder="••••••••" required />
                  <button type="button" (click)="showPassword = !showPassword"
                          class="absolute right-3 top-1/2 -translate-y-1/2 text-[#6b6358] hover:text-[#f2ca50]">
                    <span class="material-symbols-outlined text-[20px]">{{ showPassword ? 'visibility_off' : 'visibility' }}</span>
                  </button>
                </div>
              </div>
            </div>

            @if (errorMessage) {
              <div class="flex items-center gap-3 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-[13px]">
                <span class="material-symbols-outlined text-lg">error</span>
                {{ errorMessage }}
              </div>
            }

            @if (successMessage) {
              <div class="flex items-center gap-3 p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-lg text-emerald-400 text-[13px]">
                <span class="material-symbols-outlined text-lg">check_circle</span>
                {{ successMessage }}
              </div>
            }

            <button type="submit" [disabled]="loading || !emailVerified"
                    class="w-full py-3.5 mt-2 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-40 disabled:grayscale disabled:cursor-not-allowed shadow-glow"
                    style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;">
              @if (loading) {
                <div class="flex items-center justify-center gap-2">
                  <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                  CREATING ACCOUNT...
                </div>
              } @else {
                <div class="flex items-center justify-center gap-2 font-bold uppercase tracking-wider">
                  <span class="material-symbols-outlined text-lg">person_add</span>
                  Create Account
                </div>
              }
            </button>
          </form>

          <!-- Footer -->
          <div class="text-center mt-6 pt-5 border-t border-[rgba(242,202,80,0.12)]">
            <p class="text-[13px] text-[#99907c]">Already have an account? <a routerLink="/login" class="text-[#f2ca50] hover:underline font-medium">Sign In</a></p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class RegisterComponent {
  name = '';
  email = '';
  phone = '';
  role = 'Passenger';
  password = '';
  otpCode = '';
  
  showPassword = false;
  loading = false;
  sendingOtp = false;
  verifyingOtp = false;
  otpSent = false;
  emailVerified = false;
  
  errorMessage = '';
  successMessage = '';
   role1 = ""
   roles1 : any [] = ["Passenger","Dealer"];
   selectRole(role:string){
if(role === "Passenger"){
  this.role1 = "Passenger";
}
if(role === "Dealer"){
  this.role = "Dealer";
}
   }
  constructor(private auth: AuthService, private router: Router, private toast: ToastService) {}

  sendOtp() {
    if (!this.email) return;
    this.sendingOtp = true;
    this.errorMessage = '';
    
    this.auth.sendRegistrationOtp(this.email).subscribe({
      next: () => {
        this.sendingOtp = false;
        this.otpSent = true;
        this.toast.success('Verification code sent to your email.');
      },
      error: (err) => {
        this.sendingOtp = false;
        this.errorMessage = err.error?.message || err.error?.error || 'Failed to send OTP.';
      }
    });
  }

  verifyOtp() {
    if (!this.otpCode) return;
    this.verifyingOtp = true;
    this.errorMessage = '';
    
    this.auth.verifyRegistrationOtp(this.email, this.otpCode).subscribe({
      next: () => {
        this.verifyingOtp = false;
        this.emailVerified = true;
        this.toast.success('Email verified successfully!');
      },
      error: (err) => {
        this.verifyingOtp = false;
        this.errorMessage = err.error?.message || err.error?.error || 'Invalid verification code.';
      }
    });
  }

  register() {
    if (!this.emailVerified) {
      this.errorMessage = 'Please verify your email address first.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.auth.register({ name: this.name, email: this.email, phone: this.phone, password: this.password, role: this.role }).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Account created successfully! Redirecting...';
        this.toast.success('Registration successful!');
        setTimeout(() => this.router.navigate(['/login']), 2000);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || err.error?.error || 'Registration failed.';
      }
    });
  }
}
