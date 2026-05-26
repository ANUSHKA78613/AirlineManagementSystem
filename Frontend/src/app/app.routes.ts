import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Public
  
  { path: '', loadComponent: () => import('./features/landing/landing.component').then(m => m.LandingComponent) },
  { path: 'login', loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'sso/callback', loadComponent: () => import('./features/auth/sso-callback.component').then(m => m.SsoCallbackComponent) },
  { path: 'verify-otp', loadComponent: () => import('./features/auth/otp-verify.component').then(m => m.OtpVerifyComponent) },

  // Passenger
  { path: 'passenger/search', loadComponent: () => import('./features/passenger/search.component').then(m => m.SearchComponent) },
  { path: 'passenger/book/:id', canActivate: [authGuard], loadComponent: () => import('./features/passenger/book.component').then(m => m.BookComponent) },
  { path: 'passenger/bookings', canActivate: [authGuard], loadComponent: () => import('./features/passenger/my-bookings.component').then(m => m.MyBookingsComponent) },
  { path: 'passenger/profile', canActivate: [authGuard], loadComponent: () => import('./features/passenger/profile.component').then(m => m.ProfileComponent) },
  { path: 'passenger/reschedule/:pnr', redirectTo: 'passenger/bookings', pathMatch: 'full' },
  { path: 'passenger/payment/:pnr', canActivate: [authGuard], loadComponent: () => import('./features/passenger/payment.component').then(m => m.PaymentComponent) },
  { path: 'passenger/boarding-pass/:pnr', canActivate: [authGuard], loadComponent: () => import('./features/passenger/boarding-pass.component').then(m => m.BoardingPassComponent) },
  { path: 'search', redirectTo: 'passenger/search', pathMatch: 'full' },
  { path: 'profile', loadComponent: () => import('./features/passenger/profile.component').then(m => m.ProfileComponent) },

  // Admin
  { path: 'admin', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/admin/admin-dashboard.component').then(m => m.AdminDashboardComponent) },

  // Staff
  { path: 'staff', canActivate: [roleGuard('Staff')], loadComponent: () => import('./features/staff/staff-dashboard.component').then(m => m.StaffDashboardComponent) },

  // Dealer
  { path: 'dealer', canActivate: [roleGuard('Dealer')], loadComponent: () => import('./features/dealer/dealer-dashboard.component').then(m => m.DealerDashboardComponent) },

  // Error pages
  { path: 'unauthorized', loadComponent: () => import('./features/landing/unauthorized.component').then(m => m.UnauthorizedComponent) },
  { path: '**', loadComponent: () => import('./features/landing/not-found.component').then(m => m.NotFoundComponent) },
];
