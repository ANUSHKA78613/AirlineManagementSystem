// ═══════════════ Auth Models ═══════════════
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  name: string;
  email: string;
  phone: string;
  password: string;
  role?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
  message?: string;
}

export interface User {
  userId: number;
  name: string;
  email: string;
  phone: string;
  role: string;
  isBlocked: boolean;
  createdAt: string;
  profileImageUrl?: string;
  ssoProvider?: string;
}

export interface OtpRequest {
  emailOrPhone: string;
}

export interface OtpVerifyRequest {
  emailOrPhone: string;
  otpCode: string;
}

// ═══════════════ Flight Models ═══════════════
export interface Flight {
  flightId: number;
  flightNumber: string;
  airline: string;
  source: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  sourceTimeZone?: string;       // IANA e.g. "Asia/Kolkata"
  destinationTimeZone?: string;  // IANA e.g. "Asia/Dubai"
  totalSeats: number;
  availableSeats: number;
  price?: number;
  basePrice?: number;
  status: string;
  aircraftType?: string;
  gateNumber?: string;
}

export interface Airport {
  airportId: number;
  code: string;
  name: string;
  city: string;
  country: string;
}

export interface FlightRoute {
  routeId: number;
  source?: string;
  destination?: string;
  sourceAirportCode?: string;
  destinationAirportCode?: string;
  distanceKm: number;
  estimatedDuration?: string;
}

export interface FlightSearchRequest {
  source: string;
  destination: string;
  date?: string;
  passengers?: number;
  minPrice?: number;
  maxPrice?: number;
  sortBy?: string;
}

// ═══════════════ Booking Models ═══════════════
export interface Booking {
  bookingId: number;
  pnr: string;
  userId: number;
  flightId: number;
  totalAmount: number;
  status: string;
  bookingDate: string;
  passengers?: Passenger[];
  source?: string;
  destination?: string;
  departureTime?: string;
  arrivalTime?: string;
  flightNumber?: string;
  airline?: string;
}

export interface Passenger {
  passengerId: number;
  pnr: string;
  name: string;
  age: number;
  gender: string;
  seatNo: string;
  passportNumber?: string;
}

export interface CreateBookingRequest {
  userId: number;
  flightId: number;
  totalAmount: number;
  passengers: PassengerInput[];
  email?: string;
  userName?: string;
  source?: string;
  destination?: string;
  departureTime?: string;
  arrivalTime?: string;
}

export interface PassengerInput {
  name: string;
  age: number;
  gender: string;
  seatNo?: string;
  seatClass?: string;
  passportNumber?: string;
  baggageWeight?: number;
}

// ═══════════════ Payment Models ═══════════════
export interface Payment {
  paymentId: number;
  pnr: string;
  amount: number;
  paymentMethod: string;
  transactionId: string;
  status: string;
  createdAt: string;
}

// ═══════════════ Operations Models ═══════════════
export interface Employee {
  employeeId: number;
  name: string;
  employeeCode: string;
  role: string;
  department: string;
}

export interface BoardingPass {
  boardingPassId: number;
  pnr: string;
  passengerId: number;
  seatNo: string;
  gate: string;
  boardingTime: string;
  status: string;
}

export interface Baggage {
  baggageId: number;
  pnr: string;
  tagNumber: string;
  weight: number;
  status: string;
  createdAt: string;
}

export interface Issue {
  issueId: number;
  pnr: string;
  category: string;
  description: string;
  status: string;
  priority: string;
  createdAt: string;
}

// ═══════════════ Dealer Models ═══════════════
export interface DealerAgent {
  agentId: number;
  agentName: string;
  agencyName: string;
  agentCode: string;
  email: string;
  phone: string;
  commissionRate: number;
  isActive: boolean;
  createdAt: string;
}

export interface DealerWallet {
  walletId: number;
  agentId: number;
  balance: number;
  lastUpdated: string;
}

export interface WalletTransaction {
  transactionId: number;
  walletId: number;
  amount: number;
  type: string;
  description: string;
  createdAt: string;
}

export interface Commission {
  commissionId: number;
  agentId: number;
  agentCode: string;
  bookingAmount: number;
  commissionRate: number;
  commissionAmount: number;
  status: string;
  pnr: string;
  earnedDate: string;
}

// ═══════════════ Notification Models ═══════════════
export interface NotificationRecord {
  notificationId: number;
  userId: number;
  email: string;
  subject: string;
  message: string;
  status: string;
  createdAt: string;
}

// ═══════════════ UI Models ═══════════════
export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  duration?: number;
}

export interface ApiResponse<T = any> {
  data?: T;
  message?: string;
  error?: string;
}
