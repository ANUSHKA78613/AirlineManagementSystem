import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { PricingService, OperationsService } from '../../core/services/api.service';

import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';

import { ImageUploadComponent } from '../../shared/components/image-upload.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, ImageUploadComponent],
  template: `
    <!-- Dark Gold Nav -->
    <nav class="fixed top-0 w-full z-50 bg-[#131313]/80 backdrop-blur-xl flex justify-between items-center px-8 py-4 shadow-[0_4px_30px_rgba(0,0,0,0.5)]">
        <div class="font-serif italic text-2xl text-[#f2ca50] cursor-pointer" routerLink="/">SkyHorizon</div>
        <div class="hidden md:flex items-center space-x-8">
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/passenger/search">Fleet</a>
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/passenger/bookings">Itinerary</a>
        </div>
        <div class="hidden md:flex items-center space-x-8" *ngIf="user?.role !== 'Passenger' && user?.role">
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] cursor-pointer" 
               [routerLink]="user?.role === 'Dealer' ? '/dealer' : (user?.role === 'Admin' ? '/admin' : '/staff')">
               <span class="material-symbols-outlined" style="vertical-align: middle; margin-right: 4px;">dashboard</span>
               Return to Dashboard
            </a>
        </div>
        <div class="flex items-center gap-6 relative">
            <button (click)="toggleNotifications()" class="relative text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300">
                <span class="material-symbols-outlined">notifications</span>
                <span *ngIf="unreadCount > 0" class="absolute -top-1 -right-1 bg-error text-white text-[10px] w-4 h-4 rounded-full flex items-center justify-center font-bold">{{ unreadCount }}</span>
            </button>
            <button class="text-[#f2ca50] transition-all duration-300" routerLink="/profile"><span class="material-symbols-outlined">account_circle</span></button>
            <button (click)="doLogout()" class="text-error/70 hover:text-error transition-all duration-300">
                <span class="material-symbols-outlined">logout</span>
            </button>

            <!-- Notifications Dropdown -->
            <div *ngIf="showNotifications" class="absolute top-[120%] right-10 w-80 bg-[#131118]/95 border border-[#f2ca50]/20 rounded-xl shadow-[0_8px_32px_rgba(0,0,0,0.8)] z-50 overflow-hidden backdrop-blur-xl animate-fadeIn">
              <div class="bg-[#1c1c1e] p-4 border-b border-[#f2ca50]/10 flex justify-between items-center">
                 <h4 class="font-bold text-[#f2ca50] text-sm uppercase tracking-widest">Notifications</h4>
                 <button (click)="toggleNotifications()" class="text-[#d6c692]/60 hover:text-[#d6c692]"><span class="material-symbols-outlined text-sm">close</span></button>
              </div>
              <div class="max-h-80 overflow-y-auto p-2">
                 <div *ngIf="notifications.length === 0" class="p-4 text-center text-[#d6c692]/50 text-xs">No recent notifications</div>
                 <div *ngFor="let n of notifications" class="p-3 mb-1 bg-[#1a1a1d] rounded-lg border border-[#f2ca50]/5 transition-all" [class.opacity-60]="n.read">
                    <div class="flex justify-between items-start mb-1">
                       <div class="flex items-center gap-2">
                         <span class="material-symbols-outlined text-sm" [style.color]="n.color">{{ n.icon }}</span>
                         <span class="text-xs font-bold text-[#e2e8f0]">{{ n.title }}</span>
                       </div>
                       <span class="text-[9px] text-[#94a3b8]">{{ n.timestamp | date:'shortTime' }}</span>
                    </div>
                    <p class="text-[11px] text-[#cbd5e1] line-clamp-2 leading-relaxed ml-6">{{ n.message }}</p>
                 </div>
              </div>
            </div>
        </div>
    </nav>
    <div class="profile-page" style="padding-top:60px;">
      <div class="profile-bg">
        <div class="hero-orb orb-1"></div>
        <div class="hero-orb orb-2"></div>
      </div>

      <div class="profile-content">
        <!-- Profile Header -->
        <div class="profile-header animate-fadeInUp">
          <app-image-upload
            [existingImageUrl]="user?.profileImageUrl || null"
            [autoUpload]="true"
            (fileUploaded)="onProfileImageUpload($event)">
          </app-image-upload>
          <div style="margin-top: 16px;"></div>
          <h1>{{ user?.name || 'Traveler' }}</h1>
          <p class="email">{{ user?.email }}</p>
          <span class="badge badge-primary badge-lg">{{ user?.role || 'Passenger' }}</span>
        </div>

        <div class="profile-grid">
          <!-- Personal Info Card -->
          <div class="card profile-card animate-fadeInUp delay-1">
            <div class="card-header">
              <h2>👤 Personal Information</h2>
              @if (!editingProfile) {
                <button class="btn btn-secondary btn-sm" (click)="startEditing()">Edit</button>
              }
            </div>

            @if (editingProfile) {
              <div class="form-group">
                <label>Full Name</label>
                <input class="form-control" [(ngModel)]="editName" placeholder="Your name" />
              </div>
              <div class="form-group">
                <label>Phone Number</label>
                <input class="form-control" [(ngModel)]="editPhone" placeholder="+91 9876543210" />
              </div>
              <div class="form-actions">
                <button class="btn btn-primary" (click)="saveProfile()" [disabled]="savingProfile">
                  @if (savingProfile) { <div class="spinner spinner-sm"></div> Saving... } @else { Save Changes }
                </button>
                <button class="btn btn-secondary" (click)="editingProfile = false">Cancel</button>
              </div>
            } @else {
              <div class="info-grid">
                <div class="info-item">
                  <span class="info-label">Name</span>
                  <span class="info-value">{{ user?.name }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">Email</span>
                  <span class="info-value">{{ user?.email }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">Phone</span>
                  <span class="info-value">{{ user?.phone || 'Not set' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">Role</span>
                  <span class="info-value">{{ user?.role }}</span>
                </div>
              </div>
            }
          </div>

          <!-- Change Password Card -->
          <div class="card profile-card animate-fadeInUp delay-2">
            <div class="card-header">
              <h2>🔒 Change Password</h2>
            </div>
            <div class="form-group">
              <label>Current Password</label>
              <input type="password" class="form-control" [(ngModel)]="currentPassword" placeholder="••••••••" />
            </div>
            <div class="form-group">
              <label>New Password</label>
              <input type="password" class="form-control" [(ngModel)]="newPassword" placeholder="Min 6 characters" />
            </div>
            <div class="form-group">
              <label>Confirm New Password</label>
              <input type="password" class="form-control" [(ngModel)]="confirmPassword" placeholder="••••••••" />
            </div>
            @if (passwordError) {
              <div class="form-error-box">❌ {{ passwordError }}</div>
            }
            <button class="btn btn-primary btn-full" (click)="changePassword()" [disabled]="changingPassword">
              @if (changingPassword) { <div class="spinner spinner-sm"></div> Updating... } @else { Update Password }
            </button>
          </div>

          <!-- Coupons Card -->
          <div class="card profile-card animate-fadeInUp delay-3">
            <div class="card-header">
              <h2>🎫 Available Coupons</h2>
            </div>
            @if (availableCoupons && availableCoupons.length > 0) {
              <div class="coupons-list" style="display:flex;flex-direction:column;gap:12px;">
                @for (c of availableCoupons; track c.couponId) {
                  <div style="background:var(--bg-glass);border:1px dashed var(--accent-primary);border-radius:12px;padding:16px;display:flex;justify-content:space-between;align-items:center;">
                    <div>
                      <h4 style="margin:0;font-size:16px;color:var(--text-primary);">{{ c.code }}</h4>
                      <p style="margin:4px 0 0;font-size:12px;color:var(--text-secondary);">
                        {{ c.discountType === 'Percentage' ? c.discountValue + '% OFF' : '₹' + c.discountValue + ' OFF' }}
                        @if (c.minimumAmount) { <span>(Min spend ₹{{ c.minimumAmount }})</span> }
                      </p>
                    </div>
                    <button class="btn btn-sm btn-secondary" (click)="copyCoupon(c.code)" style="background:transparent;border:1px solid var(--accent-primary);color:var(--accent-primary);">Copy</button>
                  </div>
                }
              </div>
            } @else {
              <div style="text-align:center;padding:24px;background:rgba(255,255,255,0.02);border-radius:12px;">
                <span class="material-symbols-outlined" style="font-size:48px;color:var(--text-muted);opacity:0.5;margin-bottom:8px;display:block;">sell</span>
                <p style="color:var(--text-secondary);font-size:14px;margin:0;">No active coupons right now.</p>
              </div>
            }
          </div>

          <!-- Quick Actions Card -->
          <div class="card profile-card animate-fadeInUp delay-3">
            <div class="card-header">
              <h2>⚡ Quick Actions</h2>
            </div>
            <div class="actions-list">
              <ng-container *ngIf="user?.role === 'Passenger' || !user?.role">
                <a routerLink="/passenger/search" class="action-item">
                  <span class="action-icon">🔍</span>
                  <div>
                    <h4>Search Flights</h4>
                    <p>Find and book your next flight</p>
                  </div>
                  <span class="action-arrow">→</span>
                </a>
                <a routerLink="/passenger/bookings" class="action-item">
                  <span class="action-icon">🎫</span>
                  <div>
                    <h4>My Bookings</h4>
                    <p>View all your flight bookings</p>
                  </div>
                  <span class="action-arrow">→</span>
                </a>
              </ng-container>
              <button class="action-item danger" (click)="auth.logout()">
                <span class="action-icon">🚪</span>
                <div>
                  <h4>Sign Out</h4>
                  <p>Log out from your account</p>
                </div>
                <span class="action-arrow">→</span>
              </button>
            </div>
          </div>

          <!-- Customer Care Card -->
          <div class="card profile-card animate-fadeInUp delay-4" *ngIf="user?.role === 'Passenger' || !user?.role">
            <div class="card-header">
              <h2>🎧 Customer Care</h2>
              <button class="btn btn-secondary btn-sm" (click)="showIssueForm = !showIssueForm">
                {{ showIssueForm ? 'Cancel' : 'Raise Issue' }}
              </button>
            </div>
            
            @if (showIssueForm) {
              <div class="form-group">
                <label>Issue Subject</label>
                <input class="form-control" [(ngModel)]="newIssue.title" placeholder="Flight delay, bag lost, etc." />
              </div>
              <div class="form-group">
                <label>Description</label>
                <textarea class="form-control" [(ngModel)]="newIssue.description" rows="3" placeholder="Provide details..."></textarea>
              </div>
              <button class="btn btn-primary" (click)="submitIssue()" [disabled]="submittingIssue || !newIssue.title || !newIssue.description">
                @if (submittingIssue) { <div class="spinner spinner-sm"></div> } @else { Submit Ticket }
              </button>
            }
            
            <div class="issues-list" style="margin-top:20px;">
              @if (myIssues.length > 0) {
                @for (issue of myIssues; track issue.issueId) {
                  <div style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.1); border-radius:12px; padding:16px; margin-bottom:12px;">
                    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;">
                      <h4 style="margin:0; font-size:16px;">{{ issue.title }}</h4>
                      <span [ngClass]="{'text-success': issue.status==='Resolved', 'text-warning': issue.status==='Open'}" style="font-size:12px; font-weight:bold;">
                        {{ issue.status }}
                      </span>
                    </div>
                    <p style="margin:0; font-size:13px; color:var(--text-secondary); margin-bottom:8px;">{{ issue.description }}</p>
                    
                    @if (issue.status === 'Resolved' && issue.staffReply) {
                      <div style="background:rgba(0,0,0,0.2); border-left:3px solid var(--accent-primary); padding:10px; border-radius:4px; margin-top:10px;">
                        <div style="font-size:11px; color:var(--accent-primary); text-transform:uppercase; font-weight:bold; margin-bottom:4px;">Staff Reply</div>
                        <p style="margin:0; font-size:13px; color:#fff;">{{ issue.staffReply }}</p>
                      </div>
                    }
                  </div>
                }
              } @else {
                <div *ngIf="!showIssueForm" style="text-align:center; padding:16px; opacity:0.5;">
                  <span class="material-symbols-outlined" style="font-size:32px;">support_agent</span>
                  <p style="font-size:13px; margin:0;">No support tickets found.</p>
                </div>
              }
            </div>
          </div>

        </div>
      </div>
    </div>

  `,
  styles: [`
    .profile-page {
      min-height: 100vh;
      position: relative;
      overflow: hidden;
    }
    .profile-bg {
      position: absolute; inset: 0; z-index: 0;
    }
    .hero-orb {
      position: absolute; border-radius: 50%; filter: blur(140px); opacity: 0.25;
    }
    .orb-1 { width: 500px; height: 500px; background: #2b6fff; top: -100px; right: -100px; animation: float 8s ease-in-out infinite; }
    .orb-2 { width: 400px; height: 400px; background: #f2c14e; bottom: -100px; left: -100px; animation: float 10s ease-in-out infinite reverse; }

    .profile-content {
      position: relative;
      z-index: 1;
      max-width: 900px;
      margin: 0 auto;
      padding: 40px 20px;
    }

    .profile-header {
      text-align: center;
      margin-bottom: 48px;
    }
    .avatar-ring {
      width: 110px; height: 110px;
      border-radius: 50%;
      background: var(--accent-gradient);
      padding: 3px;
      margin: 0 auto 20px;
    }
    .avatar {
      width: 100%; height: 100%;
      border-radius: 50%;
      background: var(--bg-secondary);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 36px;
      font-weight: 800;
      font-family: 'Outfit', sans-serif;
      color: var(--accent-secondary);
    }
    .profile-header h1 { font-size: 32px; margin-bottom: 6px; }
    .profile-header .email { color: var(--text-secondary); font-size: 15px; margin-bottom: 12px; }
    .badge-lg { padding: 6px 16px; font-size: 13px; }

    .profile-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
    }
    .profile-grid > :last-child {
      grid-column: 1 / -1;
    }

    .reward-tier-display { display: flex; align-items: center; gap: 20px; margin-bottom: 16px; }
    .tier-badge { padding: 8px 20px; border-radius: 20px; font-weight: 800; font-size: 14px; text-transform: uppercase; letter-spacing: 2px; }
    .tier-silver { background: linear-gradient(135deg, #a8a8a8, #d4d4d4); color: #333; }
    .tier-gold { background: linear-gradient(135deg, #f4a01c, #fcd34d); color: #333; }
    .tier-platinum { background: linear-gradient(135deg, #6366f1, #a5b4fc); color: #fff; }
    .tier-diamond { background: linear-gradient(135deg, #0ea5e9, #a5f3fc); color: #333; }
    .tier-points { text-align: center; }
    .points-value { display: block; font-size: 28px; font-weight: 900; color: var(--accent-primary); font-family: 'Outfit', sans-serif; }
    .points-label { font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; }
    .reward-actions { display: flex; gap: 16px; }
    .reward-info { flex: 1; padding: 12px; background: var(--bg-glass); border: 1px solid var(--border-color); border-radius: var(--radius-md); }
    .reward-info-label { display: block; font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; margin-bottom: 4px; }
    .reward-info-value { font-size: 15px; font-weight: 700; }

    .profile-card { padding: 28px; }
    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .card-header h2 { font-size: 18px; }

    .info-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
    }
    .info-item {}
    .info-label {
      display: block;
      font-size: 11px;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 1px;
      margin-bottom: 4px;
    }
    .info-value {
      font-size: 15px;
      font-weight: 600;
    }

    .form-actions { display: flex; gap: 12px; margin-top: 8px; }
    .form-error-box {
      padding: 12px;
      background: rgba(239,68,68,0.1);
      border-radius: 8px;
      font-size: 14px;
      color: var(--error);
      margin-bottom: 16px;
    }

    .actions-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .action-item {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px 20px;
      background: var(--bg-glass);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-md);
      cursor: pointer;
      transition: all var(--transition-base);
      text-decoration: none;
      color: var(--text-primary);
      width: 100%;
      text-align: left;
      font-family: inherit;
      font-size: inherit;
    }
    .action-item:hover {
      background: var(--bg-glass-hover);
      border-color: var(--border-accent);
      transform: translateX(4px);
    }
    .action-item.danger:hover { border-color: rgba(239,68,68,0.3); }
    .action-icon { font-size: 24px; flex-shrink: 0; }
    .action-item h4 { font-size: 15px; font-weight: 600; margin-bottom: 2px; }
    .action-item p { font-size: 13px; color: var(--text-secondary); margin: 0; }
    .action-item div { flex: 1; }
    .action-arrow { color: var(--text-muted); font-size: 18px; }

    @media (max-width: 768px) {
      .profile-grid { grid-template-columns: 1fr; }
      .profile-grid > :last-child { grid-column: auto; }
      .info-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class ProfileComponent implements OnInit {
  user: any = null;
  editingProfile = false;
  editName = '';
  editPhone = '';
  savingProfile = false;

  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changingPassword = false;
  passwordError = '';

  availableCoupons: any[] = [];

  myIssues: any[] = [];
  showIssueForm = false;
  newIssue = { title: '', description: '' };
  submittingIssue = false;
  
  showNotifications = false;

  constructor(
    public auth: AuthService,
    private pricingService: PricingService,
    private opsService: OperationsService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService
  ) {}

  get notifications() { return this.notifService.getNotifications(); }
  get unreadCount() { return this.notifService.getUnreadCount(); }

  toggleNotifications() {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications && this.unreadCount > 0) {
       this.notifService.markAllRead();
    }
  }

  get initials(): string {
    const name = this.user?.name || '';
    return name.split(' ').map((w: string) => w[0]).join('').toUpperCase().substring(0, 2) || '?';
  }

  doLogout() {
    this.auth.logout();
  }

  startEditing() {
    this.editName = this.user?.name || '';
    this.editPhone = this.user?.phone || '';
    this.editingProfile = true;
  }

  ngOnInit() {
    this.user = this.auth.currentUser;
    this.editName = this.user?.name || '';
    this.editPhone = this.user?.phone || '';
    this.auth.getProfile().subscribe({
      next: (profile) => {
        if (profile) {
          this.user = { ...this.user, ...profile };
          this.editName = this.user?.name || '';
          this.editPhone = this.user?.phone || '';
          this.cdr.detectChanges();
        }
      }
    });
    this.loadCoupons();
    if (this.user?.email && (this.user.role === 'Passenger' || !this.user.role)) {
      this.loadIssues();
    }
  }

  loadIssues() {
    if (!this.user?.email) return;
    this.opsService.getPassengerIssues(this.user.email).subscribe({
      next: (issues) => {
        this.myIssues = issues;
        this.cdr.detectChanges();
      }
    });
  }

  submitIssue() {
    this.submittingIssue = true;
    this.opsService.addPassengerIssue(this.newIssue).subscribe({
      next: () => {
        this.toast.success('Support ticket submitted successfully!');
        this.submittingIssue = false;
        this.showIssueForm = false;
        this.newIssue = { title: '', description: '' };
        this.loadIssues();
      },
      error: (err) => {
        this.toast.error(err.error?.error || 'Failed to submit ticket');
        this.submittingIssue = false;
      }
    });
  }

  loadCoupons() {
    this.pricingService.getActiveCoupons().subscribe({
      next: (data: any[]) => {
        // Active checking happens in backend, just bind data
        this.availableCoupons = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.availableCoupons = [];
        this.cdr.detectChanges();
      }
    });
  }

  copyCoupon(code: string) {
    navigator.clipboard.writeText(code).then(() => {
      this.toast.success('Coupon code copied!');
    });
  }

  uploadingImage = false;

  onProfileImageUpload(file: File) {
    this.uploadingImage = true;
    this.auth.uploadProfileImage(file).subscribe({
      next: (res) => {
        this.toast.success('Profile image updated!');
        if (this.user) {
          this.user.profileImageUrl = res.imageUrl;
        }
        this.uploadingImage = false;
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Failed to upload image.');
        this.uploadingImage = false;
      }
    });
  }

  saveProfile() {
    this.savingProfile = true;
    this.auth.updateProfile({ name: this.editName, phone: this.editPhone }).subscribe({
      next: () => {
        this.savingProfile = false;
        this.editingProfile = false;
        this.toast.success('Profile updated!');
        if (this.user) {
          this.user.name = this.editName;
          this.user.phone = this.editPhone;
        }
        // Re-fetch full profile from server to ensure everything is in sync
        this.auth.getProfile().subscribe({
          next: (profile) => {
            if (profile) {
              this.user = { ...this.user, ...profile };
              this.cdr.detectChanges();
            }
          }
        });
      },
      error: (err) => {
        this.savingProfile = false;
        this.toast.error(err.error?.message || 'Update failed');
      }
    });
  }

  changePassword() {
    this.passwordError = '';
    if (this.newPassword !== this.confirmPassword) {
      this.passwordError = 'Passwords do not match';
      return;
    }
    if (this.newPassword.length < 6) {
      this.passwordError = 'Password must be at least 6 characters';
      return;
    }
    this.changingPassword = true;
    this.auth.changePassword({ currentPassword: this.currentPassword, newPassword: this.newPassword }).subscribe({
      next: () => {
        this.changingPassword = false;
        this.toast.success('Password updated!');
        this.currentPassword = this.newPassword = this.confirmPassword = '';
      },
      error: (err) => {
        this.changingPassword = false;
        this.passwordError = err.error?.message || 'Password change failed';
      }
    });
  }
}
