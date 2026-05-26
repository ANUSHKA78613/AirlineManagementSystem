import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterModule],
  template: `
    <div class="error-page">
      <div class="error-bg">
        <div class="hero-orb orb-1"></div>
        <div class="hero-orb orb-2"></div>
      </div>
      <div class="error-content animate-fadeInUp">
        <div class="error-plane">✈</div>
        <h1 class="error-code">4<span class="text-gradient">0</span>4</h1>
        <h2>Flight Not Found</h2>
        <p>Looks like this page took off without you.</p>
        <div class="error-actions">
          <a routerLink="/" class="btn btn-primary btn-lg">🏠 Go Home</a>
          <a routerLink="/passenger/search" class="btn btn-secondary btn-lg">🔍 Search Flights</a>
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
    .orb-1{width:500px;height:500px;background:#EF4444;top:-150px;left:-100px;animation:float 8s ease-in-out infinite;}
    .orb-2{width:400px;height:400px;background:#f2c14e;bottom:-100px;right:-100px;animation:float 10s ease-in-out infinite reverse;}
    .error-content{position:relative;z-index:1;max-width:500px;}
    .error-plane{font-size:60px;animation:float 4s ease-in-out infinite;margin-bottom:16px;display:inline-block;transform:rotate(-20deg);}
    .error-code{font-size:120px;font-weight:900;line-height:1;font-family:'Outfit',sans-serif;margin-bottom:8px;}
    .error-content h2{font-size:24px;margin-bottom:12px;}
    .error-content p{color:var(--text-secondary);font-size:16px;margin-bottom:36px;}
    .error-actions{display:flex;gap:16px;justify-content:center;}
    @media(max-width:768px){.error-code{font-size:80px;}.error-actions{flex-direction:column;}}
  `]
})
export class NotFoundComponent {}
