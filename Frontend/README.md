# ✈ SkyHorizon — Frontend

> **Angular 21 · Standalone Components · TailwindCSS v3 · Razorpay SDK · QR Scanner**

The SkyHorizon frontend is a **Single-Page Application** built with Angular 21 that provides role-based dashboards for **Passengers, Admins, Staff, and Dealers**. It communicates with 6 backend microservices through a single Ocelot API Gateway (`http://localhost:9118`).

---

## Table of Contents

- [Quick Start](#quick-start)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [File Reference (Every File)](#file-reference-every-file)
  - [Root Configuration](#1-root-configuration-files)
  - [Source Entry Point](#2-source-entry-point)
  - [Core Layer](#3-core-layer)
  - [Feature Modules](#4-feature-modules)
  - [Shared UI Components](#5-shared-ui-components)
- [Routing Map](#routing-map)
- [Service Layer API Reference](#service-layer-api-reference)
- [Data Models](#data-models)
- [Authentication Flow](#authentication-flow)
- [Role-Based Access](#role-based-access)
- [Environment Configuration](#environment-configuration)
- [Build & Deployment](#build--deployment)
- [Coding Conventions](#coding-conventions)

---

## Quick Start

```bash
# Prerequisites: Node.js 20+, npm 10+

# 1. Install dependencies
cd Frontend
npm install

# 2. Start dev server (hot-reload on port 4200)
npm start

# 3. Production build
npm run build
```

> **Note:** The backend must be running (`docker compose up` from repository root) on `localhost:9118` before the frontend can make API calls.

---

## Tech Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| Angular | 21.2 | Component framework (standalone components, signals) |
| TypeScript | 5.9 | Type-safe JavaScript |
| TailwindCSS | 3.4 | Utility-first CSS styling |
| RxJS | 7.8 | Reactive HTTP & state management |
| angularx-qrcode | 21.0 | QR code generation for boarding passes |
| html5-qrcode | 2.3 | QR code scanner for staff boarding gate |
| Razorpay SDK | (CDN) | Payment gateway integration |

---

## Project Structure

```
Frontend/
├── src/
│   ├── index.html                 # App shell with Razorpay SDK script
│   ├── main.ts                    # Bootstrap entry point
│   ├── styles.css                 # Global styles + Tailwind directives
│   │
│   └── app/
│       ├── app.ts                 # Root component
│       ├── app.html               # Root template (<router-outlet>)
│       ├── app.css                # Root component styles
│       ├── app.config.ts          # Application providers (router, HTTP)
│       ├── app.routes.ts          # All route definitions
│       │
│       ├── core/                  # Singleton services & infrastructure
│       │   ├── config/
│       │   │   └── api.config.ts
│       │   ├── guards/
│       │   │   └── auth.guard.ts
│       │   ├── interceptors/
│       │   │   └── jwt.interceptor.ts
│       │   ├── models/
│       │   │   └── index.ts
│       │   ├── services/
│       │   │   ├── api.service.ts
│       │   │   ├── auth.service.ts
│       │   │   ├── notification.service.ts
│       │   │   └── toast.service.ts
│       │   └── data/              # (empty — reserved for static data)
│       │
│       ├── features/              # Role-based feature pages
│       │   ├── auth/              # Login, Register, OTP, SSO, Forgot Password
│       │   ├── landing/           # Home, 404, Unauthorized
│       │   ├── passenger/         # Search, Book, Bookings, Payment, Profile, Boarding Pass
│       │   ├── admin/             # Admin Dashboard (single-page with tab sidebar)
│       │   ├── staff/             # Staff Dashboard
│       │   └── dealer/            # Dealer Dashboard
│       │
│       └── shared/                # Reusable UI components & pipes
│           ├── components/
│           │   ├── navbar/
│           │   ├── footer/
│           │   ├── sidebar/
│           │   ├── data-table/
│           │   ├── stats-card/
│           │   ├── qr-scanner/
│           │   ├── razorpay-widget/
│           │   ├── navbar.component.ts    (standalone inline)
│           │   ├── image-upload.component.ts
│           │   └── toast.component.ts
│           └── pipes/
│               └── rupee-currency/
│                   └── rupee-currency-pipe.ts
```

---

## File Reference (Every File)

### 1. Root Configuration Files

| File | Purpose |
|------|---------|
| `package.json` | NPM dependencies, scripts (`start`, `build`, `test`, `watch`) |
| `angular.json` | Angular CLI workspace config — build/serve targets, assets, styles |
| `tsconfig.json` | Base TypeScript config — strict mode, path aliases, target ES2022 |
| `tsconfig.app.json` | App-specific TS config — extends base, includes `src/**/*.ts` |
| `tsconfig.spec.json` | Test TS config — Jasmine types |
| `tailwind.config.js` | Tailwind CSS config — custom colors, fonts, breakpoints |
| `.editorconfig` | Editor rules — 2-space indent, UTF-8, trim trailing whitespace |
| `.prettierrc` | Prettier formatting — single quotes, trailing commas, 120 print width |
| `.gitignore` | Git ignores — `node_modules/`, `dist/`, `.angular/` cache |

---

### 2. Source Entry Point

| File | Purpose |
|------|---------|
| `src/index.html` | HTML shell — imports Google Fonts (Inter, Outfit), Razorpay Checkout SDK via `<script>`, sets viewport meta |
| `src/main.ts` | Angular bootstrap — calls `bootstrapApplication(AppComponent, appConfig)` |
| `src/styles.css` | Global stylesheet — Tailwind `@tailwind` directives, custom CSS variables, dark-mode support, scrollbar styling, animation keyframes |

---

### 3. Core Layer

The `core/` directory contains **singleton services** (providedIn: `'root'`), guards, interceptors, and type definitions. These are never imported by lazy-loaded feature modules directly — Angular's DI tree handles it.

#### `core/config/api.config.ts`

| Export | Type | Purpose |
|--------|------|---------|
| `apiConfig` | `const object` | Holds `gatewayBaseUrl` (`http://localhost:9118`) and `defaultFrontendUrl` (`http://localhost:4200`) |
| `gatewayUrl(path)` | `function` | Prepends gateway base URL to any path → `http://localhost:9118/path` |
| `frontendUrl(path)` | `function` | Returns frontend origin URL (uses `window.location.origin` in browser) |

> **To change the backend URL**, edit `gatewayBaseUrl` in this file only.

---

#### `core/guards/auth.guard.ts`

| Export | Type | Purpose |
|--------|------|---------|
| `authGuard` | `CanActivateFn` | Checks `AuthService.isLoggedIn`. Redirects to `/login` if not authenticated. |
| `roleGuard(...roles)` | `(...roles: string[]) => CanActivateFn` | Higher-order guard. Checks login + verifies `AuthService.userRole` is in allowed roles. Redirects to `/unauthorized` on mismatch. |

**Usage in routes:**
```ts
{ path: 'admin', canActivate: [roleGuard('Admin')], ... }
{ path: 'passenger/book/:id', canActivate: [authGuard], ... }
```

---

#### `core/interceptors/jwt.interceptor.ts`

| Export | Type | Purpose |
|--------|------|---------|
| `jwtInterceptor` | `HttpInterceptorFn` | Reads JWT from `localStorage('sl_token')`, attaches `Authorization: Bearer <token>` header to every outgoing HTTP request. |

Registered in `app.config.ts`:
```ts
provideHttpClient(withInterceptors([jwtInterceptor]))
```

---

#### `core/models/index.ts`

Central barrel file exporting **all TypeScript interfaces** used across the app. Contains **15 interfaces + 2 utility types**:

| Model | Fields | Used By |
|-------|--------|---------|
| `LoginRequest` | email, password | Auth |
| `RegisterRequest` | name, email, phone, password, role? | Auth |
| `AuthResponse` | token, user, message? | Auth |
| `User` | userId, name, email, phone, role, isBlocked, createdAt, profileImageUrl?, ssoProvider? | Auth, Profile, Admin |
| `OtpRequest` | emailOrPhone | Auth |
| `OtpVerifyRequest` | emailOrPhone, otpCode | Auth |
| `Flight` | flightId, flightNumber, airline, source, destination, departureTime, arrivalTime, totalSeats, availableSeats, price?, status, gateNumber? | Search, Book, Admin |
| `Airport` | airportId, code, name, city, country | Admin, Search autocomplete |
| `FlightRoute` | routeId, source?, destination?, distanceKm, estimatedDuration? | Admin |
| `FlightSearchRequest` | source, destination, date?, passengers?, minPrice?, maxPrice?, sortBy? | Search |
| `Booking` | bookingId, pnr, userId, flightId, totalAmount, status, bookingDate, passengers?, source?, destination?, ... | My Bookings, Admin |
| `Passenger` | passengerId, pnr, name, age, gender, seatNo, passportNumber? | Booking form |
| `CreateBookingRequest` | userId, flightId, totalAmount, passengers[], email?, userName?, source?, destination? | Book |
| `PassengerInput` | name, age, gender, seatNo?, seatClass?, passportNumber?, baggageWeight? | Book form |
| `Payment` | paymentId, pnr, amount, paymentMethod, transactionId, status, createdAt | Payment, Admin |
| `Employee` | employeeId, name, employeeCode, role, department | Admin, Staff |
| `BoardingPass` | boardingPassId, pnr, passengerId, seatNo, gate, boardingTime, status | Boarding Pass |
| `Baggage` | baggageId, pnr, tagNumber, weight, status, createdAt | Baggage |
| `Issue` | issueId, pnr, category, description, status, priority, createdAt | Issues |
| `DealerAgent` | agentId, agentName, agencyName, agentCode, email, phone, commissionRate, isActive, createdAt | Dealer, Admin |
| `DealerWallet` | walletId, agentId, balance, lastUpdated | Dealer |
| `WalletTransaction` | transactionId, walletId, amount, type, description, createdAt | Dealer |
| `Commission` | commissionId, agentId, agentCode, bookingAmount, commissionRate, commissionAmount, status, pnr, earnedDate | Dealer, Admin |
| `NotificationRecord` | notificationId, userId, email, subject, message, status, createdAt | Notifications |
| `ToastMessage` | id, type ('success'\|'error'\|'info'\|'warning'), message, duration? | UI |
| `ApiResponse<T>` | data?, message?, error? | General |

---

#### `core/services/auth.service.ts`

**Class:** `AuthService` — Handles all authentication, session management, and user profile operations.

| Method | Returns | API Endpoint | Purpose |
|--------|---------|-------------|---------|
| `login(req)` | `Observable<AuthResponse>` | `POST /api/Auth/login` | Email + password login, stores JWT + user in localStorage |
| `register(req)` | `Observable<any>` | `POST /api/Auth/register` | Create new user account |
| `sendRegistrationOtp(email)` | `Observable<any>` | `POST /api/Auth/registration-otp/send` | Send email OTP for registration verification |
| `verifyRegistrationOtp(email, otp)` | `Observable<any>` | `POST /api/Auth/registration-otp/verify` | Validate registration OTP |
| `loginWithGoogle()` | `void` | Redirect to `/api/Sso/google` | Initiates Google OAuth flow |
| `handleSsoCallback(token)` | `void` | — | Parses SSO token and persists session |
| `sendOtp(req)` | `Observable<any>` | `POST /api/Auth/send-otp` | Send login OTP |
| `verifyOtp(req)` | `Observable<any>` | `POST /api/Auth/verify-otp` | Verify login OTP, auto-persists session |
| `forgotPassword(email)` | `Observable<any>` | `POST /api/Auth/forgot-password` | Send password reset OTP |
| `resetPassword(data)` | `Observable<any>` | `POST /api/Auth/reset-password` | Reset password with OTP token |
| `changePassword(data)` | `Observable<any>` | `POST /api/Auth/change-password` | Change password (requires current) |
| `getProfile()` | `Observable<any>` | `GET /api/Auth/profile` | Fetch user profile, merges into cached user |
| `updateProfile(data)` | `Observable<any>` | `PUT /api/Auth/profile` | Update name + phone |
| `uploadProfileImage(file)` | `Observable<{message, imageUrl}>` | `POST /api/Auth/profile/image` | Upload profile photo (multipart/form-data) |
| `getUsers()` | `Observable<User[]>` | `GET /api/Auth/users` | Admin: list all users |
| `blockUser(userId)` | `Observable<any>` | `PUT /api/Auth/users/{id}/block` | Admin: block user |
| `unblockUser(userId)` | `Observable<any>` | `PUT /api/Auth/users/{id}/unblock` | Admin: unblock user |
| `getSavedPassengers()` | `Observable<any[]>` | `GET /api/Auth/saved-passengers` | Get user's saved passenger profiles |
| `addSavedPassenger(data)` | `Observable<any>` | `POST /api/Auth/saved-passengers` | Save a passenger profile |
| `deleteSavedPassenger(id)` | `Observable<any>` | `DELETE /api/Auth/saved-passengers/{id}` | Delete saved passenger |
| `logout()` | `void` | — | Clears localStorage, navigates to `/login` |
| `isLoggedIn` | `boolean` (getter) | — | Checks if `sl_token` exists in localStorage |
| `currentUser` | `User \| null` (getter) | — | Returns cached user from BehaviorSubject |
| `userRole` | `string` (getter) | — | Returns `currentUser.role` or empty string |

**Session Storage Keys:**
- `sl_token` — JWT access token
- `sl_user` — Serialized `User` JSON

---

#### `core/services/api.service.ts`

Contains **6 injectable service classes** in a single file, each targeting a different backend microservice through the Gateway:

| Service Class | Gateway Prefix | Backend Service | Methods |
|---------------|----------------|-----------------|---------|
| `FlightService` | `/flight/api/Flight` | Flight.API | `getAll`, `getById`, `search`, `create`, `update`, `delete`, `updateStatus`, `getAirports`, `createAirport`, `deleteAirport`, `updateAirport`, `getRoutes`, `createRoute`, `deleteRoute`, `updateRoute`, `getSeatMap`, `getSeatLayout`, `reserveSeat`, `releaseSeat` |
| `PricingService` | `/pricing/api/Pricing` | Flight.API (PricingController) | `getRules`, `addRule`, `deleteRule`, `updateRule`, `getCoupons`, `getActiveCoupons`, `addCoupon`, `deleteCoupon`, `updateCoupon`, `toggleCoupon`, `validateCoupon`, `calculate`, `getSeatConfigs`, `addSeatConfig`, `updateSeatConfig`, `deleteSeatConfig` |
| `BookingService` | `/booking/api/Booking` | Booking.API | `create`, `getByPnr`, `getMyBookings`, `getAll`, `cancel`, `reschedule`, `getBookedSeats`, `confirm`, `bulkBook` |
| `PaymentService` | `/payment/api/Payment` | Payment.API | `create`, `getByPnr`, `getAll`, `getById`, `createRazorpayOrder`, `verifyRazorpay`, `refund`, `getRefundStatus` |
| `OperationsService` | `/operations/api/Operations` | Operations.API | `checkIn`, `getCheckInStatus`, `getBoardingPass`, `downloadBoardingPass`, `downloadAllBoardingPasses`, `scanBoarding`, `verifyIdentity`, `getEmployees`, `addEmployee`, `getBaggage`, `addBaggage`, `updateBaggageStatus`, `getIssues`, `getPassengerIssues`, `addPassengerIssue`, `addIssue`, `resolveIssue`, `replyIssue`, `sendNotification`, `getNotifications`, `getNotificationStats`, `getDashboardMetrics` |
| `DealerService` | `/dealer/api/Dealer` | Dealer.API | `getAll`, `getById`, `register`, `activate`, `deactivate`, `updateCommission`, `deleteDealer`, `updateDealer`, `getWallet`, `topUpWallet`, `debitWallet`, `getTransactions`, `assignCustomer`, `getCustomers`, `removeCustomer`, `getCommissions`, `recordSelfCommission`, `getCommissionSummary`, `payCommission`, `payAllCommissions`, `reverseCommission`, `getCommissionReport`, `enrollReward`, `getReward`, `addPoints`, `redeemPoints`, `getRewardTransactions`, `getLeaderboard`, `getAnalytics` |

---

#### `core/services/notification.service.ts`

**Class:** `NotificationService` — Manages in-app notification bell state, polling, and display. Uses `BehaviorSubject` for reactive notification count updates.

---

#### `core/services/toast.service.ts`

**Class:** `ToastService` — Emits `ToastMessage` events for success/error/info/warning toast notifications. Used by all feature components for user feedback.

---

### 4. Feature Modules

All components are **standalone** (no NgModules). Each uses `loadComponent` lazy-loading in the router.

---

#### `features/auth/` — Authentication Pages

| File | Component | Route | Purpose |
|------|-----------|-------|---------|
| `login.component.ts` | `LoginComponent` | `/login` | Email+password form, "Sign in with Google" button, OTP login option, remember-me, lockout handling |
| `register.component.ts` | `RegisterComponent` | `/register` | Multi-step registration — sends OTP first, then creates account. Role selector (Passenger/Dealer) |
| `otp-verify.component.ts` | `OtpVerifyComponent` | `/verify-otp` | 6-digit OTP input with auto-focus, countdown timer, resend link. Handles both registration and login OTP flows |
| `forgot-password.component.ts` | `ForgotPasswordComponent` | `/forgot-password` | Two-step: enter email → receive OTP → enter new password |
| `sso-callback.component.ts` | `SsoCallbackComponent` | `/sso/callback` | Receives Google SSO redirect, extracts token from URL params, calls `AuthService.handleSsoCallback()`, redirects to dashboard |

---

#### `features/landing/` — Public Pages

| File | Component | Route | Purpose |
|------|-----------|-------|---------|
| `landing.component.ts` | `LandingComponent` | `/` | Hero section with animated background, feature cards, testimonials, CTA buttons. The marketing landing page |
| `not-found.component.ts` | `NotFoundComponent` | `/**` (wildcard) | Styled 404 page with "Go Home" button |
| `unauthorized.component.ts` | `UnauthorizedComponent` | `/unauthorized` | Styled 403 page shown when role guard blocks access |

---

#### `features/passenger/` — Passenger Role Pages

| File | Component | Route | Size | Purpose |
|------|-----------|-------|------|---------|
| `search.component.ts` | `SearchComponent` | `/passenger/search` | 38 KB | Flight search with autocomplete airports, date picker, price filter, sort by price/duration/departure. Inline results with class-wise pricing cards |
| `book.component.ts` | `BookComponent` | `/passenger/book/:id` | 23 KB | Multi-step booking: seat map visualization, passenger forms, coupon apply, total calculation. Uses `FlightService.getSeatLayout()` for interactive seat grid |
| `book.component.html` | — | — | 33 KB | Template for book component — seat map layout, passenger detail forms, price breakdown |
| `book.component.css` | — | — | 1.5 KB | Seat map grid styles, seat type color coding |
| `my-bookings.component.ts` | `MyBookingsComponent` | `/passenger/bookings` | 58 KB | Full itinerary management: view bookings, cancel (full/partial), reschedule with fare diff, web check-in modal, baggage CRUD, boarding pass viewer, refund polling, support issue submission |
| `payment.component.ts` | `PaymentComponent` | `/passenger/payment/:pnr` | 9.5 KB | Razorpay checkout integration — creates order, opens Razorpay modal, handles success/failure callbacks, verifies payment, confirms booking, then navigates to itinerary |
| `payment.component.html` | — | — | 15 KB | Payment summary card, price breakdown, processing spinner |
| `payment.component.css` | — | — | 31 B | Minimal overrides |
| `profile.component.ts` | `ProfileComponent` | `/passenger/profile` | 26 KB | View/edit profile (name, phone), change password, upload profile photo, saved passengers CRUD, loyalty rewards (points, tier, history), support issues list |
| `boarding-pass.component.ts` | `BoardingPassComponent` | `/passenger/boarding-pass/:pnr` | 14 KB | Displays boarding pass cards with QR codes (via `angularx-qrcode`), print button, download link |

---

#### `features/admin/` — Admin Dashboard

| File | Component | Route | Size | Purpose |
|------|-----------|-------|------|---------|
| `admin-dashboard.component.ts` | `AdminDashboardComponent` | `/admin` | 36 KB | Single-page dashboard with sidebar tabs. TypeScript logic for all admin operations: fleet CRUD, bookings, payments, users, pricing, dealers, coupons, employees, issues, baggage, notifications, analytics |
| `admin-dashboard.component.html` | — | — | 56 KB | Complete template with tab-switched views: Fleet Manager, Booking Manager, Payment Records, User Manager, Pricing Rules, Coupon Manager, Seat Configs, Dealer Manager, Employee Manager, Issue Tracker, Baggage Tracker, Notification Center, Analytics Dashboard |
| `admin-dashboard.component.css` | — | — | 55 B | Minimal style overrides |

**Admin Tab Sections (inside the single component):**

| Tab | Operations |
|-----|------------|
| Fleet | Add/edit/delete flights, update status, set pricing + seat capacity inline |
| Airports | Add/edit/delete airports (code, name, city, country) |
| Routes | Add/edit/delete routes between airports |
| Bookings | View all bookings, cancel any booking (triggers refund) |
| Payments | View all payment records, check refund status |
| Users | View/block/unblock users, filter by role |
| Pricing | View/add/edit/delete per-flight pricing rules |
| Seat Configs | View/add/edit/delete seat configurations per flight/class |
| Coupons | Create/edit/delete/toggle coupons |
| Dealers | Register/activate/deactivate/delete dealers, set commission rate, top up wallet |
| Employees | View/add employees |
| Issues | View/resolve/reply to operational and customer issues |
| Baggage | View all baggage, update status (Checked/Lost/Found/Delivered) |
| Notifications | Send email notifications, view sent/failed history, stats |
| Analytics | Revenue, bookings, flight utilization dashboard metrics |

---

#### `features/staff/` — Staff Dashboard

| File | Component | Route | Size | Purpose |
|------|-----------|-------|------|---------|
| `staff-dashboard.component.ts` | `StaffDashboardComponent` | `/staff` | 6.5 KB | Operations-focused dashboard: view flights (status updates), boarding gate scanner, baggage management, issue tracker, employee list |
| `staff-dashboard.component.html` | — | — | 11.5 KB | Tab-based template for staff operations |
| `staff-dashboard.component.css` | — | — | 82 B | Minimal overrides |

---

#### `features/dealer/` — Dealer Dashboard

| File | Component | Route | Size | Purpose |
|------|-----------|-------|------|---------|
| `dealer-dashboard.component.ts` | `DealerDashboardComponent` | `/dealer` | 24 KB | Bulk booking (search → add up to 20 bookings → wallet debit), wallet management (credit/debit/history), commission tracking + summary, customer management (assign/remove), analytics |
| `dealer-dashboard.component.html` | — | — | 33 KB | Multi-tab dashboard: Bookings, Wallet, Commissions, Customers, Analytics |
| `dealer-dashboard.component.css` | — | — | 15.5 KB | Custom styles for dealer cards, wallet UI, booking grid |

---

### 5. Shared UI Components

Reusable components and pipes used across multiple feature pages.

#### `shared/components/navbar/`

| File | Purpose |
|------|---------|
| `navbar.ts` | Navigation bar component — role-aware menu items, notification bell with unread count, profile dropdown, logout |
| `navbar.html` | Template — responsive navbar with mobile hamburger |
| `navbar.css` | Glassmorphism styling, dropdown animations |

#### `shared/components/navbar.component.ts`
Standalone inline navbar variant (legacy, used by some pages).

#### `shared/components/footer/`

| File | Purpose |
|------|---------|
| `footer.ts` | Footer component — copyright, quick links, social icons |
| `footer.html` | Footer template |
| `footer.css` | Footer styling |

#### `shared/components/sidebar/`

| File | Purpose |
|------|---------|
| `sidebar.ts` | Collapsible sidebar — used by Admin/Staff/Dealer dashboards. Takes menu items as input, emits selection events |
| `sidebar.html` | Sidebar template with icon + label items |
| `sidebar.css` | Sidebar collapse animation, active item highlight |

#### `shared/components/data-table/`

| File | Purpose |
|------|---------|
| `data-table.ts` | Generic data table — accepts columns config + data array via `@Input`. Supports sorting, pagination, search filter |
| `data-table.html` | Table template with thead/tbody, pagination controls |
| `data-table.css` | Table striping, hover effects, responsive overflow |

#### `shared/components/stats-card/`

| File | Purpose |
|------|---------|
| `stats-card.ts` | Dashboard stat card — shows icon, label, value, trend indicator. Used in Admin/Staff/Dealer analytics |
| `stats-card.html` | Card template |
| `stats-card.css` | Gradient backgrounds, number animation |

#### `shared/components/qr-scanner/`

| File | Purpose |
|------|---------|
| `qr-scanner.component.ts` | Camera QR scanner — wraps `html5-qrcode` library. Used by Staff dashboard for boarding gate scanning. Emits decoded QR data |

#### `shared/components/razorpay-widget/`

| File | Purpose |
|------|---------|
| `razorpay-widget.ts` | Razorpay checkout wrapper — configures and opens Razorpay popup, handles success/failure/dismiss callbacks |
| `razorpay-widget.html` | "Pay Now" button template |
| `razorpay-widget.css` | Button styling |

#### `shared/components/image-upload.component.ts`
Standalone profile image upload component — drag & drop, preview, file size validation (max 2 MB), emits `File` on selection.

#### `shared/components/toast.component.ts`
Toast notification overlay — subscribes to `ToastService`, displays stacked toasts with auto-dismiss (default 4s), color-coded by type (success=green, error=red, info=blue, warning=yellow).

#### `shared/pipes/rupee-currency/rupee-currency-pipe.ts`
Custom pipe `rupeeCurrency` — formats numbers as Indian Rupee: `₹1,23,456.00`. Uses Indian grouping (lakhs/crores).

---

## Routing Map

```
/                           → LandingComponent        [public]
/login                      → LoginComponent           [public]
/register                   → RegisterComponent        [public]
/verify-otp                 → OtpVerifyComponent       [public]
/forgot-password            → ForgotPasswordComponent  [public]
/sso/callback               → SsoCallbackComponent     [public]

/passenger/search           → SearchComponent          [public]
/passenger/book/:id         → BookComponent            [authGuard]
/passenger/bookings         → MyBookingsComponent      [authGuard]
/passenger/profile          → ProfileComponent         [authGuard]
/passenger/payment/:pnr     → PaymentComponent         [authGuard]
/passenger/boarding-pass/:pnr → BoardingPassComponent  [authGuard]
/search                     → redirects to /passenger/search
/profile                    → ProfileComponent

/admin                      → AdminDashboardComponent  [roleGuard('Admin')]
/staff                      → StaffDashboardComponent  [roleGuard('Staff')]
/dealer                     → DealerDashboardComponent [roleGuard('Dealer')]

/unauthorized               → UnauthorizedComponent    [public]
/**                         → NotFoundComponent        [wildcard]
```

---

## Service Layer API Reference

All services route through the Ocelot Gateway at `http://localhost:9118`.

| Gateway Path Prefix | Backend Service | Port (Docker) |
|---------------------|-----------------|---------------|
| `/identity/`, `/api/Auth/`, `/api/Sso/`, `/api/Admin/` | Identity.API | 8080 |
| `/flight/` | Flight.API | 8080 |
| `/inventory/` | Flight.API (InventoryController) | 8080 |
| `/pricing/` | Flight.API (PricingController) | 8080 |
| `/booking/` | Booking.API | 8080 |
| `/payment/` | Payment.API | 8080 |
| `/operations/` | Operations.API | 8080 |
| `/notification/` | Operations.API (NotificationController) | 8080 |
| `/analytics/` | Dealer.API (AnalyticsController) | 8080 |
| `/dealer/` | Dealer.API | 8080 |
| `/commission/` | Dealer.API (CommissionController) | 8080 |
| `/reward/` | Dealer.API (RewardController) | 8080 |

---

## Authentication Flow

```
1. User enters credentials on /login
2. POST /api/Auth/login → returns { token, user }
3. jwtInterceptor reads sl_token from localStorage
4. Every HTTP request gets Authorization: Bearer <token>
5. Route guards check AuthService.isLoggedIn & AuthService.userRole
6. On 401 → redirect to /login
7. On logout → clear localStorage, navigate to /login
```

**Token format:** JWT with claims — `sub` (userId), `name`, `email`, `role`

---

## Role-Based Access

| Role | Landing Route | Guard | Accessible Pages |
|------|--------------|-------|------------------|
| **Passenger** | `/passenger/search` | `authGuard` | search, book, bookings, payment, boarding-pass, profile |
| **Admin** | `/admin` | `roleGuard('Admin')` | Admin dashboard (all tabs) |
| **Staff** | `/staff` | `roleGuard('Staff')` | Staff dashboard |
| **Dealer** | `/dealer` | `roleGuard('Dealer')` | Dealer dashboard |
| **Public** | `/` | none | landing, login, register, forgot-password, search |

---

## Environment Configuration

There is only **one configuration point** for API URLs:

```typescript
// src/app/core/config/api.config.ts
export const apiConfig = {
  gatewayBaseUrl: 'http://localhost:9118',     // ← Change for production
  defaultFrontendUrl: 'http://localhost:4200',
} as const;
```

For production, update `gatewayBaseUrl` to the deployed gateway URL.

---

## Build & Deployment

```bash
# Development (hot-reload)
npm start                    # → http://localhost:4200

# Development build
npm run build                # → dist/frontend/

# Production build
npx ng build --configuration=production   # → optimized dist/

# Watch mode
npm run watch
```

**Build output:** `dist/frontend/browser/` — deploy this folder to any static file server (Nginx, Apache, S3, Azure Static Web Apps).

**Nginx example:**
```nginx
server {
    listen 80;
    root /var/www/skyhorizon/browser;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;   # SPA fallback
    }
}
```

---

## Coding Conventions

| Convention | Rule |
|------------|------|
| **Components** | All standalone (`standalone: true`), no NgModules |
| **Naming** | `kebab-case` files, `PascalCase` classes, `camelCase` methods/properties |
| **Templates** | Inline for small components, separate `.html` for large ones (>50 lines) |
| **Styles** | TailwindCSS utilities in templates, custom CSS in separate files only when needed |
| **Services** | All `providedIn: 'root'` singleton services in `core/services/` |
| **Models** | All interfaces in `core/models/index.ts` barrel file |
| **API calls** | Always through service classes, never direct `HttpClient` in components |
| **State** | Component-local state with class properties; `BehaviorSubject` for shared state in services |
| **Error handling** | Services return `Observable`, components subscribe and handle errors with `ToastService` |
| **Lazy loading** | All feature routes use `loadComponent` dynamic imports |
| **Formatting** | Prettier with single quotes, trailing commas, 120 char width |
