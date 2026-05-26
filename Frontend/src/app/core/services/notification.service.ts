import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { OperationsService } from './api.service';
import { AuthService } from './auth.service';

export interface AppNotification {
  id: string;
  type: 'booking' | 'cancel' | 'reschedule' | 'checkin' | 'payment' | 'baggage' | 'system' | 'email';
  icon: string;
  title: string;
  message: string;
  timestamp: Date;
  read: boolean;
  color: string;
}

const ICON_MAP: Record<string, string> = {
  booking:    'flight_takeoff',
  cancel:     'event_busy',
  reschedule: 'sync',
  checkin:    'how_to_reg',
  payment:    'payments',
  baggage:    'luggage',
  system:     'notifications',
  email:      'mail',
};

const COLOR_MAP: Record<string, string> = {
  booking:    '#f2ca50',
  cancel:     '#ef4444',
  reschedule: '#3b82f6',
  checkin:    '#10b981',
  payment:    '#8b5cf6',
  baggage:    '#f59e0b',
  system:     '#6b7280',
  email:      '#0ea5e9',
};

const STORAGE_KEY = 'skyhorizon_notifications';
const MAX_NOTIFICATIONS = 50;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private notificationsSubject = new BehaviorSubject<AppNotification[]>([]);
  public notifications$: Observable<AppNotification[]> = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$: Observable<number> = this.unreadCountSubject.asObservable();

  private serverNotificationsLoaded = false;

  constructor(
    private opsService: OperationsService,
    private auth: AuthService
  ) {
    this.loadFromStorage();
    if (this.auth.isLoggedIn) {
      this.loadServerNotifications();
    }
  }

  /** Add a local activity notification */
  push(type: AppNotification['type'], title: string, message: string): void {
    const notification: AppNotification = {
      id: `local_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      type,
      icon: ICON_MAP[type] || 'notifications',
      title,
      message,
      timestamp: new Date(),
      read: false,
      color: COLOR_MAP[type] || '#6b7280',
    };

    const current = this.notificationsSubject.value;
    const updated = [notification, ...current].slice(0, MAX_NOTIFICATIONS);
    this.notificationsSubject.next(updated);
    this.updateUnreadCount(updated);
    this.saveToStorage(updated);
  }

  /** Convenience methods for common events */
  bookingCreated(pnr: string, route: string, amount: number): void {
    this.push('booking', 'Booking Confirmed', `PNR ${pnr} • ${route} • ₹${Math.round(amount).toLocaleString()}`);
  }

  bookingCancelled(pnr: string): void {
    this.push('cancel', 'Booking Cancelled', `PNR ${pnr} has been cancelled. Refund will be processed.`);
  }

  bookingRescheduled(pnr: string, newFlight: string): void {
    this.push('reschedule', 'Flight Rescheduled', `PNR ${pnr} moved to flight ${newFlight}.`);
  }

  passengerCheckedIn(pnr: string, name: string, seat: string, gate: string): void {
    this.push('checkin', 'Check-in Complete', `${name} checked in for PNR ${pnr}. Seat ${seat}, Gate ${gate || 'TBD'}.`);
  }

  paymentSuccess(pnr: string, amount: number): void {
    this.push('payment', 'Payment Successful', `₹${Math.round(amount).toLocaleString()} paid for PNR ${pnr}.`);
  }

  refundInitiated(pnr: string, amount: number): void {
    this.push('payment', 'Refund Initiated', `₹${Math.round(amount).toLocaleString()} refund for PNR ${pnr}.`);
  }

  baggageAdded(pnr: string, tag: string, weight: number): void {
    this.push('baggage', 'Baggage Tagged', `Tag ${tag} (${weight}kg) added to PNR ${pnr}.`);
  }

  /** Load server email notifications and merge with local ones */
  loadServerNotifications(): void {
    const email = this.auth.currentUser?.email;
    if (!email) return;

    this.opsService.getNotifications(email).subscribe({
      next: (data: any[]) => {
        const serverNotifs: AppNotification[] = (data || []).map((n: any) => ({
          id: `server_${n.id || n.notificationId || Math.random()}`,
          type: 'email' as const,
          icon: n.subject?.toLowerCase().includes('otp') ? 'lock' : 'mail',
          title: n.subject || 'Notification',
          message: n.message || '',
          timestamp: new Date(n.createdAt || Date.now()),
          read: n.status !== 'Sent',
          color: n.status === 'Failed' ? '#ef4444' : '#0ea5e9',
        }));

        // Merge: local + server, deduplicate by id, sort by timestamp
        const local = this.notificationsSubject.value.filter(n => n.id.startsWith('local_'));
        const merged = [...local, ...serverNotifs]
          .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
          .slice(0, MAX_NOTIFICATIONS);

        this.notificationsSubject.next(merged);
        this.updateUnreadCount(merged);
        this.serverNotificationsLoaded = true;
      }
    });
  }

  /** Mark all as read */
  markAllRead(): void {
    const updated = this.notificationsSubject.value.map(n => ({ ...n, read: true }));
    this.notificationsSubject.next(updated);
    this.unreadCountSubject.next(0);
    this.saveToStorage(updated.filter(n => n.id.startsWith('local_')));
  }

  /** Clear all notifications */
  clearAll(): void {
    this.notificationsSubject.next([]);
    this.unreadCountSubject.next(0);
    localStorage.removeItem(STORAGE_KEY);
  }

  /** Get current snapshot */
  getNotifications(): AppNotification[] {
    return this.notificationsSubject.value;
  }

  getUnreadCount(): number {
    return this.unreadCountSubject.value;
  }

  // ── Private helpers ──────────────────────────

  private updateUnreadCount(notifications: AppNotification[]): void {
    this.unreadCountSubject.next(notifications.filter(n => !n.read).length);
  }

  private saveToStorage(localOnly: AppNotification[]): void {
    try {
      const toSave = localOnly.filter(n => n.id.startsWith('local_')).slice(0, MAX_NOTIFICATIONS);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(toSave));
    } catch { /* quota exceeded — silent */ }
  }

  private loadFromStorage(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed: AppNotification[] = JSON.parse(raw).map((n: any) => ({
          ...n,
          timestamp: new Date(n.timestamp),
        }));
        this.notificationsSubject.next(parsed);
        this.updateUnreadCount(parsed);
      }
    } catch { /* corrupted storage — silent */ }
  }
}
