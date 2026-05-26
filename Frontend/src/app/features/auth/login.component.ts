import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4 py-12 relative overflow-hidden bg-cover bg-center bg-no-repeat" style="background-image: url('assets/images/custom-auth-bg.png');">
      <!-- Dark Overlay -->
      <div class="absolute inset-0 z-0 bg-[#0a0a0a]/70 backdrop-blur-[2px]"></div>

      <!-- Admin Setup Card -->
      @if (setupMode) {
      <div class="relative z-10 w-full max-w-md animate-fadeInUp">
        <div class="bg-[#1c1b1b]/90 backdrop-blur-2xl border border-[rgba(242,202,80,0.12)] rounded-2xl p-10 shadow-[0_30px_80px_rgba(0,0,0,0.5)]">
          <div class="text-center mb-10">
            <span class="font-serif italic text-3xl text-[#f2ca50]">SkyHorizon</span>
            <h1 class="text-2xl font-bold text-white mb-2 mt-4" style="font-family:'Plus Jakarta Sans'">Admin Setup</h1>
            <p class="text-[#99907c] text-sm">No admin exists yet. Create the first admin account.</p>
          </div>
          <form (ngSubmit)="registerAdmin()" class="space-y-4">
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Full Name</label>
              <input type="text" [(ngModel)]="adminName" name="adminName"
                     class="w-full px-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                     placeholder="Admin Name" required />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Email Address</label>
              <input type="email" [(ngModel)]="adminEmail" name="adminEmail"
                     class="w-full px-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                     placeholder="admin@skyhorizon.com" required />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Phone</label>
              <input type="tel" [(ngModel)]="adminPhone" name="adminPhone"
                     class="w-full px-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                     placeholder="+91 9876543210" />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Password</label>
              <input type="password" [(ngModel)]="adminPassword" name="adminPassword"
                     class="w-full px-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                     placeholder="Strong password" required />
            </div>
            @if (errorMessage) {
              <div class="flex items-center gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
                <span class="material-symbols-outlined text-lg">error</span>
                {{ errorMessage }}
              </div>
            }
            @if (successMessage) {
              <div class="flex items-center gap-3 p-4 bg-green-500/10 border border-green-500/20 rounded-lg text-green-400 text-sm">
                <span class="material-symbols-outlined text-lg">check_circle</span>
                {{ successMessage }}
              </div>
            }
            <button type="submit" [disabled]="loading"
                    class="w-full py-4 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-50"
                    style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;">
              @if (loading) {
                <div class="flex items-center justify-center gap-2">
                  <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                  Creating Admin...
                </div>
              } @else {
                <div class="flex items-center justify-center gap-2">
                  <span class="material-symbols-outlined text-lg">shield_person</span>
                  Create Admin Account
                </div>
              }
            </button>
          </form>
        </div>
      </div>
      } @else {
      <!-- Login Card -->
      <div class="relative z-10 w-full max-w-md animate-fadeInUp">
        <div class="bg-[#1c1b1b]/90 backdrop-blur-2xl border border-[rgba(242,202,80,0.12)] rounded-2xl p-10 shadow-[0_30px_80px_rgba(0,0,0,0.5)]">
          
          <!-- Header -->
          <div class="text-center mb-10">
            <a routerLink="/" class="inline-block mb-6">
              <span class="font-serif italic text-3xl text-[#f2ca50]">SkyHorizon</span>
            </a>
            <h1 class="text-2xl font-bold text-white mb-2" style="font-family:'Plus Jakarta Sans'">Welcome Back</h1>
            <p class="text-[#99907c] text-sm">Sign in to continue your celestial journey</p>
          </div>

          <!-- Form -->
          <form (ngSubmit)="login()" class="space-y-5">
            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Email Address</label>
              <div class="relative">
                <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">mail</span>
                <input type="email" [(ngModel)]="email" name="email"
                       class="w-full pl-12 pr-4 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                       placeholder="you@example.com" required />
              </div>
            </div>

            <div>
              <label class="block text-xs font-semibold uppercase tracking-wider text-[#99907c] mb-2">Password</label>
              <div class="relative">
                <span class="material-symbols-outlined absolute left-4 top-1/2 -translate-y-1/2 text-[#6b6358] text-lg">lock</span>
                <input [type]="showPassword ? 'text' : 'password'" [(ngModel)]="password" name="password"
                       class="w-full pl-12 pr-12 py-3.5 bg-[#1e1e1e] border border-[rgba(242,202,80,0.12)] rounded-lg text-[#e5e2e1] placeholder-[#6b6358]/60 focus:border-[#f2ca50] focus:ring-2 focus:ring-[#f2ca50]/10 transition-all outline-none text-[15px]"
                       placeholder="••••••••" required />
                <button type="button" (click)="showPassword = !showPassword"
                        class="absolute right-4 top-1/2 -translate-y-1/2 text-[#6b6358] hover:text-[#f2ca50] transition-colors">
                  <span class="material-symbols-outlined text-lg">{{ showPassword ? 'visibility_off' : 'visibility' }}</span>
                </button>
              </div>
            </div>

            <div class="flex justify-between items-center text-sm">
              <label class="flex items-center gap-2 text-[#99907c] cursor-pointer">
                <input type="checkbox" [(ngModel)]="rememberMe" name="remember" class="accent-[#f2ca50] w-4 h-4" />
                Remember me
              </label>
              <a [routerLink]="['/forgot-password']" [queryParams]="{ email: email }" class="text-[#f2ca50] hover:text-[#d4af37] transition-colors text-sm font-medium">Forgot Password?</a>
            </div>

            @if (errorMessage) {
              <div class="flex items-center gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
                <span class="material-symbols-outlined text-lg">error</span>
                {{ errorMessage }}
              </div>
            }

            <button type="submit" [disabled]="loading"
                    class="w-full py-4 rounded-lg font-bold text-[15px] transition-all duration-300 disabled:opacity-50 disabled:cursor-not-allowed"
                    style="background: linear-gradient(135deg, #f2ca50 0%, #d4af37 100%); color: #1a1400;"
                    [class.shadow-glow]="!loading">
              @if (loading) {
                <div class="flex items-center justify-center gap-2">
                  <div class="w-4 h-4 border-2 border-[#1a1400]/30 border-t-[#1a1400] rounded-full animate-spin"></div>
                  Signing in...
                </div>
              } @else {
                <div class="flex items-center justify-center gap-2">
                  <span class="material-symbols-outlined text-lg">login</span>
                  Sign In
                </div>
              }
            </button>
          </form>

          <!-- SSO -->
          <div class="mt-8">
            <div class="relative flex items-center justify-center my-6">
              <div class="absolute inset-0 flex items-center"><div class="w-full border-t border-[rgba(242,202,80,0.12)]"></div></div>
              <span class="relative px-4 text-sm text-[#6b6358] bg-[#1c1b1b]">or continue with</span>
            </div>

            <button type="button" (click)="loginWithGoogle()"
                    class="w-full py-3.5 rounded-lg border border-[rgba(242,202,80,0.12)] bg-[#1e1e1e] text-[#e5e2e1] hover:border-[#f2ca50]/40 hover:bg-[#252525] transition-all flex items-center justify-center gap-3 text-sm font-medium">
              <img src="https://img.icons8.com/color/48/000000/google-logo.png" alt="Google" width="20" height="20" />
              Continue with Google
            </button>
          </div>

          <!-- Footer -->
          <div class="text-center mt-8 pt-6 border-t border-[rgba(242,202,80,0.12)]">
            <p class="text-sm text-[#99907c]">Don't have an account? <a routerLink="/register" class="text-[#f2ca50] hover:underline font-medium">Create one</a></p>
          </div>

        </div>
      </div>
      }
    </div>
  `
})
export class LoginComponent implements OnInit {
  email = '';
  password = '';
  showPassword = false;
  rememberMe = false;
  loading = false;
  errorMessage = '';
  successMessage = '';

  // Admin setup
  setupMode = false;
  adminName = '';
  adminEmail = '';
  adminPhone = '';
  adminPassword = '';

  constructor(private auth: AuthService, private router: Router, private toast: ToastService) { }

  ngOnInit() {
    this.auth.checkAdminExists().subscribe({
      next: (res) => { this.setupMode = !res.adminExists; },
      error: () => { this.setupMode = false; }
    });
  }

  registerAdmin() {
    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.auth.registerAdmin({
      name: this.adminName,
      email: this.adminEmail,
      phone: this.adminPhone,
      password: this.adminPassword,
      role: 'Admin'
    }).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Admin created! Redirecting to login...';
        this.toast.success('Admin account created successfully!');
        setTimeout(() => {
          this.setupMode = false;
          this.email = this.adminEmail;
        }, 1500);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Registration failed.';
      }
    });
  }

  loginWithGoogle() {
    this.auth.loginWithGoogle();
  }

  login() {
    this.loading = true;
    this.errorMessage = '';
    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: (res) => {
        this.loading = false;
        this.toast.success('Login successful!');
        const role = this.auth.userRole;
        switch (role) {
          case 'Admin': this.router.navigate(['/admin']); break;
          case 'Staff': this.router.navigate(['/staff']); break;
          case 'Dealer': this.router.navigate(['/dealer']); break;
          default: this.router.navigate(['/passenger/search']); break;
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || err.error?.error || 'Login failed. Check credentials.';
      }
    });
  }
}
