import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { OperationsService } from '../../core/services/api.service';

interface NavLink {
  label: string;
  route: string;
  exact?: boolean;
}

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <nav class="navbar">
      <a class="navbar-brand" routerLink="/" (click)="closeMenu()">
        <span class="brand-mark">SL</span>
        <span class="brand-copy">
          <strong>SkyLedger</strong>
          <small>Airline Control Grid</small>
        </span>
      </a>

      <div class="navbar-links" [class.is-open]="menuOpen">
        @for (link of navLinks; track link.route) {
          <a
            [routerLink]="link.route"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: !!link.exact }"
            (click)="closeMenu()">
            {{ link.label }}
          </a>
        }
      </div>

      <div class="navbar-actions">
        @if (auth.isLoggedIn) {
          <div class="notif-wrapper">
            <button class="notif-btn" (click)="toggleNotifications($event)">
              Alerts
              @if (unreadCount > 0) {
                <span class="notif-badge">{{ unreadCount > 9 ? '9+' : unreadCount }}</span>
              }
            </button>
            @if (showNotifications) {
              <div class="notif-panel" (click)="$event.stopPropagation()">
                <div class="notif-header">Latest updates</div>
                @if (notifications.length === 0) {
                  <div class="notif-empty">No notifications yet</div>
                }
                @for (n of notifications.slice(0, 5); track n.notificationId) {
                  <div class="notif-item">
                    <div class="notif-subject">{{ n.subject }}</div>
                    <div class="notif-time">{{ n.createdAt | date:'dd MMM, HH:mm' }}</div>
                  </div>
                }
              </div>
            }
          </div>
        }

        @if (!auth.isLoggedIn) {
          <a routerLink="/login" class="btn btn-secondary btn-sm" (click)="closeMenu()">Sign In</a>
          <a routerLink="/register" class="btn btn-primary btn-sm" (click)="closeMenu()">Create Account</a>
        } @else {
          <div class="identity-chip">
            <span class="identity-name">{{ auth.currentUser?.name }}</span>
            <span class="badge badge-primary">{{ auth.userRole }}</span>
          </div>
          <button class="btn btn-secondary btn-sm" (click)="logout()">Logout</button>
        }
      </div>
    </nav>
  `,
  styles: [`
    :host { display: block; }
    .navbar-brand { gap: 12px; color: var(--text-primary); }
    .brand-mark {
      width: 42px;
      height: 42px;
      border-radius: 14px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--accent-gradient);
      color: #051120;
      font-size: 14px;
      font-weight: 900;
      letter-spacing: 0.12em;
      box-shadow: 0 12px 28px rgba(36, 93, 255, 0.24);
    }
    .brand-copy {
      display: flex;
      flex-direction: column;
      line-height: 1.05;
    }
    .brand-copy strong {
      font-size: 18px;
      letter-spacing: 0.02em;
    }
    .brand-copy small {
      font-size: 11px;
      letter-spacing: 0.18em;
      text-transform: uppercase;
      color: var(--text-muted);
    }
    .navbar-links {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--border-color);
      padding: 6px;
      border-radius: 999px;
      gap: 8px;
    }
    .navbar-links a {
      padding: 8px 14px;
      border-radius: 999px;
    }
    .navbar-links a.active {
      background: var(--accent-soft);
    }
    .navbar-actions {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .identity-chip {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--border-color);
    }
    .identity-name {
      font-size: 13px;
      font-weight: 600;
      color: var(--text-primary);
    }
    .notif-wrapper { position: relative; }
    .notif-btn {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--border-color);
      color: var(--text-secondary);
      font-size: 13px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      cursor: pointer;
      position: relative;
      padding: 10px 14px;
      border-radius: 999px;
      transition: all 0.2s;
    }
    .notif-btn:hover { color: var(--accent-secondary); border-color: var(--border-accent); }
    .notif-badge {
      position: absolute;
      top: -2px;
      right: -2px;
      background: var(--error);
      color: #fff;
      font-size: 10px;
      font-weight: 700;
      min-width: 18px;
      height: 18px;
      border-radius: 99px;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 0 4px;
    }
    .notif-panel {
      position: absolute;
      top: calc(100% + 10px);
      right: 0;
      width: 320px;
      background: rgba(9, 29, 58, 0.96);
      border: 1px solid var(--border-color);
      border-radius: 18px;
      box-shadow: var(--shadow-lg);
      z-index: 100;
      overflow: hidden;
    }
    .notif-header {
      padding: 14px 18px;
      font-weight: 800;
      font-size: 13px;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--accent-secondary);
      border-bottom: 1px solid var(--border-color);
    }
    .notif-empty { padding: 24px; text-align: center; color: var(--text-muted); font-size: 13px; }
    .notif-item { padding: 12px 18px; border-bottom: 1px solid var(--border-color); cursor: pointer; transition: background 0.2s; }
    .notif-item:hover { background: rgba(255, 255, 255, 0.03); }
    .notif-subject { font-size: 13px; font-weight: 600; margin-bottom: 2px; }
    .notif-time { font-size: 11px; color: var(--text-muted); }
    @media (max-width: 960px) {
      .navbar-links { display: none; }
      .identity-chip { display: none; }
    }
  `],
})
export class NavbarComponent {
  menuOpen = false;
  showNotifications = false;
  notifications: any[] = [];
  unreadCount = 0;

  constructor(public auth: AuthService, private opsService: OperationsService) {
    if (this.auth.isLoggedIn) {
      this.loadNotifications();
    }
  }

  loadNotifications() {
    const email = this.auth.currentUser?.email;
    this.opsService.getNotifications(email).subscribe({
      next: (n) => {
        this.notifications = n;
        this.unreadCount = n.filter((x: any) => x.status !== 'Read').length;
      }
    });
  }

  toggleNotifications(e: Event) {
    e.stopPropagation();
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) this.unreadCount = 0;
  }

  get navLinks(): NavLink[] {
    if (!this.auth.isLoggedIn) {
      return [
        { label: 'Home', route: '/', exact: true },
        { label: 'Flight Search', route: '/search' },
        { label: 'Passenger Portal', route: '/passenger/search' },
        { label: 'Operations', route: '/staff' },
      ];
    }

    switch (this.auth.userRole) {
      case 'Admin':
        return [
          { label: 'Dashboard', route: '/admin' },
          { label: 'Flight Search', route: '/search' },
        ];
      case 'Staff':
        return [
          { label: 'Operations Desk', route: '/staff' },
          { label: 'Flight Search', route: '/search' },
        ];
      case 'Dealer':
        return [
          { label: 'Dealer Hub', route: '/dealer' },
          { label: 'Flight Search', route: '/search' },
        ];
      default:
        return [
          { label: 'Search Flights', route: '/passenger/search' },
          { label: 'My Bookings', route: '/passenger/bookings' },
          { label: 'Profile', route: '/passenger/profile' },
        ];
    }
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  logout(): void {
    this.closeMenu();
    this.auth.logout();
  }
}
