import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-sso-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="callback-page">
      <div class="loader">
        <div class="spinner"></div>
        <p>Authenticating...</p>
      </div>
    </div>
  `,
  styles: [`
    .callback-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg-color);
    }
    .loader {
      text-align: center;
    }
    .spinner {
      width: 40px; height: 40px;
      border: 4px solid rgba(255,255,255,0.1);
      border-left-color: var(--accent-primary);
      border-radius: 50%;
      animation: spin 1s linear infinite;
      margin: 0 auto 16px;
    }
    @keyframes spin { 100% { transform: rotate(360deg); } }
  `]
})
export class SsoCallbackComponent implements OnInit {
  constructor(
    private route: ActivatedRoute,
    private authService: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const token = params['sso_token'];
      if (token) {
        this.authService.handleSsoCallback(token);
        this.toast.success('Successfully logged in with Google!');
        
        const role = this.authService.userRole;
        switch (role) {
          case 'Admin': this.router.navigate(['/admin']); break;
          case 'Staff': this.router.navigate(['/staff']); break;
          case 'Dealer': this.router.navigate(['/dealer']); break;
          default: this.router.navigate(['/passenger/search']); break;
        }
      } else {
        this.toast.error('Authentication failed.');
        this.router.navigate(['/login']);
      }
    });
  }
}
