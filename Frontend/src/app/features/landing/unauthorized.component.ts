import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterModule],
  template: `
    <div class="error-page">
      <div class="error-bg">
        <div class="hero-orb orb-1"></div>
        <div class="hero-orb orb-2"></div>
      </div>
      <div class="error-content animate-fadeInUp">
        <div class="error-icon">🔒</div>
        <h1 class="error-code">4<span class="text-gradient">0</span>3</h1>
        <h2>Access Denied</h2>
        <p>You don't have permission to access this area.</p>
        <div class="error-actions">
          <a routerLink="/" class="btn btn-primary btn-lg">🏠 Go Home</a>
          <a routerLink="/login" class="btn btn-secondary btn-lg">🔑 Login</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .error-page {
      min-height:100vh;display:flex;align-items:center;justify-content:center;
      padding:40px 20px;position:relative;overflow:hidden;text-align:center;
    }
    .error-bg{position:absolute;inset:0;z-index:0;}
    .hero-orb{position:absolute;border-radius:50%;filter:blur(140px);opacity:0.25;}
    .orb-1{width:500px;height:500px;background:#F59E0B;top:-150px;right:-100px;animation:float 8s ease-in-out infinite;}
    .orb-2{width:400px;height:400px;background:#EF4444;bottom:-100px;left:-100px;animation:float 10s ease-in-out infinite reverse;}
    .error-content{position:relative;z-index:1;max-width:500px;}
    .error-icon{font-size:60px;margin-bottom:16px;}
    .error-code{font-size:120px;font-weight:900;line-height:1;font-family:'Outfit',sans-serif;margin-bottom:8px;}
    .error-content h2{font-size:24px;margin-bottom:12px;}
    .error-content p{color:var(--text-secondary);font-size:16px;margin-bottom:36px;}
    .error-actions{display:flex;gap:16px;justify-content:center;}
    @media(max-width:768px){.error-code{font-size:80px;}.error-actions{flex-direction:column;}}
  `]
})
export class UnauthorizedComponent {}
