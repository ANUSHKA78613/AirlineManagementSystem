import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { AuthResponse, LoginRequest, OtpRequest, OtpVerifyRequest, RegisterRequest, User } from '../models';
import { frontendUrl, gatewayUrl } from '../config/api.config';

function browserStorage(): Storage | null {
  return typeof window === 'undefined' ? null : window.localStorage;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly API = gatewayUrl('/api/Auth');
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    this.loadUser();
  }

  private loadUser(): void {
    const token = this.getToken();
    const user = browserStorage()?.getItem('sl_user');
    if (token && user) {
      try { this.currentUserSubject.next(JSON.parse(user)); }
      catch { this.logout(); }
    }
  }

  private persistSession(user: User, token: string): void {
    const storage = browserStorage();
    if (!storage) return;
    storage.setItem('sl_token', token);
    storage.setItem('sl_user', JSON.stringify(user));
    this.currentUserSubject.next(user);
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API}/login`, req).pipe(
      tap((res) => {
        if (res.token) {
          this.persistSession(res.user || this.parseToken(res.token), res.token);
        }
      })
    );
  }

  getSsoProviders(): Observable<any[]> {
    return this.http.get<any[]>(gatewayUrl('/api/Sso/providers'));
  }

  loginWithGoogle(): void {
    window.location.href = `${gatewayUrl('/api/Sso/google')}?returnUrl=${encodeURIComponent(frontendUrl('/sso/callback'))}`;
  }

  handleSsoCallback(token: string): void {
    const user = this.parseToken(token);
    this.persistSession(user, token);
  }

  register(req: RegisterRequest): Observable<any> {
    return this.http.post(`${this.API}/register`, req);
  }

  sendRegistrationOtp(email: string): Observable<any> {
    return this.http.post(`${this.API}/registration-otp/send`, { email });
  }

  verifyRegistrationOtp(email: string, otpCode: string): Observable<any> {
    return this.http.post(`${this.API}/registration-otp/verify`, { email, otpCode });
  }

  sendOtp(req: OtpRequest): Observable<any> {
    return this.http.post(`${this.API}/send-otp`, req);
  }

  verifyOtp(req: OtpVerifyRequest): Observable<any> {
    return this.http.post<any>(`${this.API}/verify-otp`, req).pipe(
      tap((res) => {
        if (res.token) {
          const user = this.parseToken(res.token);
          this.persistSession(user, res.token);
        }
      })
    );
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${this.API}/forgot-password`, { email });
  }

  resetPassword(data: { email: string; otpCode: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.API}/reset-password`, data);
  }

  changePassword(data: { currentPassword: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.API}/change-password`, data);
  }

  getProfile(): Observable<any> {
    return this.http.get<User>(`${this.API}/profile`).pipe(
      tap((profile) => {
        if (profile && this.currentUser) {
          const merged = { ...this.currentUser, ...profile };
          this.persistSession(merged, this.getToken()!);
        }
      })
    );
  }

  updateProfile(data: { name: string; phone: string }): Observable<any> {
    return this.http.put(`${this.API}/profile`, data).pipe(
      tap(() => {
        if (this.currentUser) {
          const updated = { ...this.currentUser, ...data };
          this.persistSession(updated, this.getToken()!);
        }
      })
    );
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.API}/users`);
  }

  checkAdminExists(): Observable<{ adminExists: boolean }> {
    return this.http.get<{ adminExists: boolean }>(`${this.API}/admin-exists`);
  }

  registerAdmin(req: RegisterRequest): Observable<any> {
    return this.http.post(`${this.API}/register-admin`, req);
  }

  uploadProfileImage(file: File): Observable<{ message: string; imageUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ message: string; imageUrl: string }>(`${this.API}/profile/image`, formData);
  }

  blockUser(userId: number): Observable<any> {
    return this.http.put(`${this.API}/users/${userId}/block`, {});
  }

  unblockUser(userId: number): Observable<any> {
    return this.http.put(`${this.API}/users/${userId}/unblock`, {});
  }

  getSavedPassengers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.API}/saved-passengers`);
  }

  addSavedPassenger(data: any): Observable<any> {
    return this.http.post(`${this.API}/saved-passengers`, data);
  }

  deleteSavedPassenger(id: number): Observable<any> {
    return this.http.delete(`${this.API}/saved-passengers/${id}`);
  }

  logout(): void {
    const storage = browserStorage();
    storage?.removeItem('sl_token');
    storage?.removeItem('sl_user');
    this.currentUserSubject.next(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return browserStorage()?.getItem('sl_token') ?? null;
  }

  get isLoggedIn(): boolean {
    return !!this.getToken();
  }

  get currentUser(): User | null {
    return this.currentUserSubject.value;
  }

  get userRole(): string {
    return this.currentUser?.role || '';
  }

  private parseToken(token: string): User {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        userId: payload.sub || payload.nameid || 0,
        name: payload.name || payload.unique_name || '',
        email: payload.email || '',
        phone: '',
        role: payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Passenger',
        isBlocked: false,
        createdAt: '',
      };
    } catch {
      return { userId: 0, name: '', email: '', phone: '', role: 'Passenger', isBlocked: false, createdAt: '' };
    }
  }
}
