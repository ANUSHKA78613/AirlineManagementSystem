import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Airport, Flight, FlightRoute, FlightSearchRequest } from '../models';
import { gatewayUrl } from '../config/api.config';

@Injectable({ providedIn: 'root' })
export class FlightService {
  private readonly api = gatewayUrl('/flight/api/Flight');
  constructor(private http: HttpClient) {}

  getAll(): Observable<Flight[]> {
    return this.http.get<Flight[]>(this.api);
  }

  getById(id: number): Observable<Flight> {
    return this.http.get<Flight>(`${this.api}/${id}`);
  }

  search(req: FlightSearchRequest): Observable<Flight[]> {
    const params = new URLSearchParams();
    
    // UI placeholder is like "Delhi (DEL)". Extract the exact 3-letter code if present, otherwise fallback to typed string
    const extractCode = (str?: string) => {
      if (!str) return '';
      const match = str.match(/\(([A-Za-z]{3})\)/);
      if (match) return match[1].toUpperCase();
      // Otherwise just use First 3 characters uppercase if they type exactly "DEL" or "BOM"
      return str.trim().substring(0, 3).toUpperCase();
    };

    if (req.source) params.set('source', extractCode(req.source));
    if (req.destination) params.set('destination', extractCode(req.destination));
    if (req.date) params.set('departureDate', req.date);
    if (req.minPrice) params.set('minPrice', String(req.minPrice));
    if (req.maxPrice) params.set('maxPrice', String(req.maxPrice));
    if (req.sortBy) params.set('sortBy', req.sortBy);
    const url = params.toString() ? `${this.api}/search?${params}` : `${this.api}/search`;
    return this.http.get<Flight[]>(url);
  }

  create(flight: any): Observable<any> {
    const payload = {
      ...flight,
      source: flight.source?.toUpperCase(),
      destination: flight.destination?.toUpperCase(),
      price: flight.price || flight.basePrice || flight.economyPrice || 0,
      aircraftId: flight.aircraftId || 1,
      windowSeatCharge: flight.windowSeatPrice || flight.windowSeatCharge || 0
    }; 
    return this.http.post(this.api, payload);
  }

  update(id: number, flight: Partial<Flight> & Record<string, any>): Observable<any> {
    const payload = {
      ...flight,
      windowSeatCharge: flight['windowSeatPrice'] || flight['windowSeatCharge'] || 0
    };
    return this.http.put(`${this.api}/${id}`, payload);
  }

  delete(id: number): Observable<any> {
    return this.http.delete(`${this.api}/${id}`);
  }

  updateStatus(id: number, status: string): Observable<any> {
    return this.http.put(`${this.api}/${id}/status?status=${encodeURIComponent(status)}`, {});
  }

  getAirports(): Observable<Airport[]> {
    return this.http.get<Airport[]>(`${this.api}/airports`);
  }

  createAirport(airport: Partial<Airport>): Observable<any> {
    return this.http.post(`${this.api}/airports`, {
      code: airport.code?.toUpperCase(),
      name: airport.name,
      city: airport.city,
      country: airport.country,
    });
  }

  deleteAirport(code: string): Observable<any> {
    return this.http.delete(`${this.api}/airports/${code}`);
  }

  updateAirport(code: string, data: Partial<Airport>): Observable<any> {
    return this.http.put(`${this.api}/airports/${code}`, data);
  }

  getRoutes(): Observable<FlightRoute[]> {
    return this.http.get<FlightRoute[]>(`${this.api}/routes`);
  }

  createRoute(route: Partial<FlightRoute> & { distanceKm?: number; estimatedDuration?: string }): Observable<any> {
    return this.http.post(`${this.api}/routes`, {
      source: (route.source || route.sourceAirportCode || '').toUpperCase(),
      destination: (route.destination || route.destinationAirportCode || '').toUpperCase(),
      distanceKm: route.distanceKm || 0,
      estimatedDuration: route.estimatedDuration,
    });
  }

  deleteRoute(id: number): Observable<any> {
    return this.http.delete(`${this.api}/routes/${id}`);
  }

  updateRoute(id: number, data: Partial<FlightRoute>): Observable<any> {
    return this.http.put(`${this.api}/routes/${id}`, data);
  }

  getSeatMap(flightId: number, seatClass?: string): Observable<any[]> {
    const classParam = seatClass ? `?seatClass=${seatClass}` : '';
    return this.http.get<any[]>(gatewayUrl(`/inventory/api/Inventory/${flightId}/seats${classParam}`));
  }

  getSeatLayout(flightId: number): Observable<any[]> {
    return this.http.get<any[]>(gatewayUrl(`/inventory/api/Inventory/${flightId}/seat-layout`));
  }

  reserveSeat(flightId: number, seatNo: string): Observable<any> {
    return this.http.post(gatewayUrl(`/inventory/api/Inventory/${flightId}/reserve`), { seatNo });
  }

  releaseSeat(flightId: number, seatNo: string): Observable<any> {
    return this.http.post(gatewayUrl(`/inventory/api/Inventory/${flightId}/release`), { seatNo });
  }
}

@Injectable({ providedIn: 'root' })
export class PricingService {
  private readonly api = gatewayUrl('/pricing/api/Pricing');
  constructor(private http: HttpClient) {}

  getRules(flightId?: number): Observable<any[]> {
    const url = flightId ? `${this.api}/rules?flightId=${flightId}` : `${this.api}/rules`;
    return this.http.get<any[]>(url);
  }

  addRule(rule: any): Observable<any> {
    return this.http.post(`${this.api}/rules`, rule);
  }

  deleteRule(ruleId: number): Observable<any> {
    return this.http.delete(`${this.api}/rules/${ruleId}`);
  }

  updateRule(id: number, rule: any): Observable<any> {
    return this.http.put(`${this.api}/rules/${id}`, rule);
  }

  getCoupons(): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/coupons`);
  }

  getActiveCoupons(): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/active-coupons`);
  }

  addCoupon(coupon: any): Observable<any> {
    return this.http.post(`${this.api}/coupons`, coupon);
  }

  deleteCoupon(id: number): Observable<any> {
    return this.http.delete(`${this.api}/coupons/${id}`);
  }

  updateCoupon(id: number, coupon: any): Observable<any> {
    return this.http.put(`${this.api}/coupons/${id}`, coupon);
  }

  toggleCoupon(id: number): Observable<any> {
    return this.http.put(`${this.api}/coupons/${id}/toggle`, {});
  }

  validateCoupon(code: string, amount: number, userId?: number): Observable<any> {
    return this.http.post(`${this.api}/coupons/validate`, { code, amount, userId: userId || 0 });
  }

  calculate(flightId: number, seatClass = 'Economy'): Observable<any> {
    return this.http.get(`${this.api}/calculate?flightId=${flightId}&seatClass=${seatClass}`);
  }

  // Seat Configurations
  getSeatConfigs(flightId?: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/seat-configs${flightId ? `?flightId=${flightId}` : ''}`);
  }

  addSeatConfig(config: any): Observable<any> {
    return this.http.post(`${this.api}/seat-configs`, config);
  }

  updateSeatConfig(id: number, config: any): Observable<any> {
    return this.http.put(`${this.api}/seat-configs/${id}`, config);
  }

  deleteSeatConfig(id: number): Observable<any> {
    return this.http.delete(`${this.api}/seat-configs/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly api = gatewayUrl('/booking/api/Booking');
  constructor(private http: HttpClient) {}

  create(booking: any): Observable<any> {
    return this.http.post(this.api, booking);
  }

  getByPnr(pnr: string): Observable<any> {
    return this.http.get<any>(`${this.api}/${pnr.trim()}`);
  }

  getMyBookings(userId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/my?userId=${userId}`);
  }

  getAll(): Observable<any[]> {
    return this.http.get<any[]>(this.api);
  }

  cancel(pnr: string, passengerIds?: number[]): Observable<any> {
    return this.http.post(`${this.api}/${pnr.trim()}/cancel`, passengerIds ? { passengerIds } : {});
  }

  reschedule(pnr: string, newFlightId: number, newTotalAmount?: number, passengers?: any[]): Observable<any> {
    return this.http.post(`${this.api}/${pnr.trim()}/reschedule`, { newFlightId, newTotalAmount, passengers });
  }

  getBookedSeats(flightId: number): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/flight/${flightId}/booked-seats`);
  }

  confirm(pnr: string): Observable<any> {
    return this.http.post(`${this.api}/${pnr.trim()}/confirm`, {});
  }

  bulkBook(bookings: any[]): Observable<any> {
    return this.http.post(`${this.api}/bulk`, bookings);
  }
}

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly api = gatewayUrl('/payment/api/Payment');
  constructor(private http: HttpClient) {}

  create(payment: any): Observable<any> {
    return this.http.post(this.api, {
      pnr: payment.pnr,
      amount: payment.amount,
      paymentMethod: payment.paymentMethod || payment.method || 'Card',
    });
  }

  getByPnr(pnr: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/${pnr.trim()}`);
  }

  getAll(): Observable<any[]> {
    return this.http.get<any[]>(this.api);
  }

  getById(id: number): Observable<any> {
    return this.http.get(`${this.api}/details/${id}`);
  }

  createRazorpayOrder(pnr: string, amount: number): Observable<any> {
    return this.http.post(`${this.api}/razorpay/order`, { pnr, amount, currency: 'INR' });
  }

  verifyRazorpay(payment: {
    pnr: string; amount: number; method?: string;
    razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string;
  }): Observable<any> {
    return this.http.post(`${this.api}/razorpay/verify`, payment);
  }

  refund(paymentId: number, reason: string, pnr?: string, amount?: number): Observable<any> {
    const payload: any = pnr ? { pnr, reason } : { pnr: String(paymentId), reason };
    if (amount) payload.amount = amount;
    return this.http.post(`${this.api}/razorpay/refund`, payload);
  }

  getRefundStatus(pnr: string): Observable<any> {
    return this.http.get(`${this.api}/refund-status/${pnr.trim()}`);
  }
}

@Injectable({ providedIn: 'root' })
export class OperationsService {
  private readonly api = gatewayUrl('/operations/api/Operations');
  private readonly notificationApi = gatewayUrl('/notification/api/Notification');
  private readonly analyticsApi = gatewayUrl('/analytics/api/Analytics');
  constructor(private http: HttpClient) {}

  checkIn(data: any): Observable<any> {
    return this.http.post(`${this.api}/checkin`, data);
  }

  getCheckInStatus(pnr: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/checkin-status/${pnr}`);
  }

  getBoardingPass(pnr: string, passengerId: number): Observable<any> {
    return this.http.get(`${this.api}/boardingpass/${pnr}/${passengerId}`);
  }

  downloadBoardingPass(pnr: string, passengerId: number): Observable<string> {
    return this.http.get(`${this.api}/boardingpass/${pnr}/${passengerId}/download`, { responseType: 'text' });
  }

  downloadAllBoardingPasses(pnr: string): Observable<string> {
    return this.http.get(`${this.api}/boardingpass/${pnr}/download`, { responseType: 'text' });
  }

  scanBoarding(pnr: string, passengerId: number, qrCode: string): Observable<any> {
    return this.http.post(`${this.api}/boarding/scan`, { pnr, passengerId, qrCode });
  }

  verifyIdentity(pnr: string, passengerId: number): Observable<any> {
    return this.http.post(`${this.api}/verify-identity`, { pnr, passengerId });
  }

  getEmployees(): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/employees`);
  }

  addEmployee(data: any): Observable<any> {
    return this.http.post(`${this.api}/employees`, data);
  }

  getBaggage(pnr?: string): Observable<any[]> {
    const url = pnr ? `${this.api}/baggage?pnr=${pnr}` : `${this.api}/baggage`;
    return this.http.get<any[]>(url);
  }

  addBaggage(data: any): Observable<any> {
    return this.http.post(`${this.api}/baggage`, data);
  }



  updateBaggageStatus(id: number, status: string): Observable<any> {
    return this.http.put(`${this.api}/baggage/${id}/status`, { status });
  }

  getIssues(status?: string): Observable<any[]> {
    const url = status ? `${this.api}/issues?status=${status}` : `${this.api}/issues`;
    return this.http.get<any[]>(url);
  }

  getPassengerIssues(email: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.api}/issues/passenger/${email}`);
  }

  addPassengerIssue(data: any): Observable<any> {
    return this.http.post(`${this.api}/issues/passenger`, data);
  }

  addIssue(data: any): Observable<any> {
    return this.http.post(`${this.api}/issues`, data);
  }

  resolveIssue(id: number): Observable<any> {
    return this.http.put(`${this.api}/issues/${id}/resolve`, {});
  }

  replyIssue(id: number, reply: string): Observable<any> {
    return this.http.put(`${this.api}/issues/${id}/reply`, { reply });
  }

  sendNotification(data: any): Observable<any> {
    return this.http.post(`${this.notificationApi}/send`, data);
  }

  getNotifications(email?: string, status?: string): Observable<any[]> {
    let url = this.notificationApi;
    const params = new URLSearchParams();
    if (email) params.append('email', email);
    if (status) params.append('status', status);
    params.append('t', new Date().getTime().toString());
    const qString = params.toString();
    return this.http.get<any[]>(`${url}?${qString}`);
  }

  getNotificationStats(): Observable<any> {
    return this.http.get(`${this.notificationApi}/stats`);
  }

  getDashboardMetrics(): Observable<any> {
    return this.http.get(`${this.analyticsApi}/dashboard`);
  }
}

@Injectable({ providedIn: 'root' })
export class DealerService {
  private readonly api = gatewayUrl('/dealer/api/Dealer');
  private readonly commissionApi = gatewayUrl('/commission/api/Commission');
  private readonly rewardApi = gatewayUrl('/reward/api/Reward');
  constructor(private http: HttpClient) {}

  getAll(activeOnly?: boolean): Observable<any[]> {
    const url = activeOnly !== undefined ? `${this.api}?activeOnly=${activeOnly}` : this.api;
    return this.http.get<any[]>(url);
  }

  getById(id: number): Observable<any> {
    return this.http.get(`${this.api}/${id}`);
  }

  register(data: any): Observable<any> {
    return this.http.post(`${this.api}/register`, data);
  }

  activate(id: number): Observable<any> {
    return this.http.put(`${this.api}/${id}/activate`, {});
  }

  deactivate(id: number): Observable<any> {
    return this.http.put(`${this.api}/${id}/deactivate`, {});
  }

  updateCommission(id: number, rate: number): Observable<any> {
    return this.http.put(`${this.api}/${id}/commission-rate?rate=${rate}`, {});
  }

  deleteDealer(id: number): Observable<any> {
    return this.http.delete(`${this.api}/${id}`);
  }

  updateDealer(id: number, data: any): Observable<any> {
    return this.http.put(`${this.api}/${id}`, data);
  }

  getWallet(agentId: number): Observable<any> {
    return this.http.get(`${this.api}/${agentId}/wallet`);
  }

  topUpWallet(agentId: number, amount: number, description?: string): Observable<any> {
    return this.http.post(`${this.api}/${agentId}/wallet/credit`, { amount, description });
  }

  debitWallet(agentId: number, amount: number, description?: string): Observable<any> {
    return this.http.post(`${this.api}/${agentId}/wallet/debit`, { amount, description });
  }

  getTransactions(agentId: number, page = 1): Observable<any> {
    return this.http.get(`${this.api}/${agentId}/wallet/history?page=${page}`);
  }

  assignCustomer(agentId: number, data: any): Observable<any> {
    return this.http.post(`${this.api}/${agentId}/customers`, data);
  }

  getCustomers(agentId: number): Observable<any> {
    return this.http.get(`${this.api}/${agentId}/customers`);
  }

  removeCustomer(agentId: number, customerId: number): Observable<any> {
    return this.http.delete(`${this.api}/${agentId}/customers/${customerId}`);
  }

  getCommissions(agentCode: string, status?: string): Observable<any[]> {
    const url = status
      ? `${this.commissionApi}/agent/${agentCode}?status=${status}`
      : `${this.commissionApi}/agent/${agentCode}`;
    return this.http.get<any[]>(url);
  }

  recordSelfCommission(data: { agentId: number; agentCode: string; bookingAmount: number; pnr: string }): Observable<any> {
    return this.http.post(`${this.commissionApi}/self`, data);
  }

  getCommissionSummary(agentCode: string): Observable<any> {
    return this.http.get(`${this.commissionApi}/agent/${agentCode}/summary`);
  }

  payCommission(id: number): Observable<any> {
    return this.http.put(`${this.commissionApi}/${id}/pay`, {});
  }

  payAllCommissions(agentCode: string): Observable<any> {
    return this.http.put(`${this.commissionApi}/agent/${agentCode}/pay-all`, {});
  }

  reverseCommission(id: number): Observable<any> {
    return this.http.post(`${this.commissionApi}/${id}/reverse`, {});
  }

  getCommissionReport(from?: string, to?: string, agentCode?: string): Observable<any> {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    if (agentCode) params.set('agentCode', agentCode);
    return this.http.get(`${this.commissionApi}/report?${params}`);
  }

  enrollReward(userId: number): Observable<any> {
    return this.http.post(`${this.rewardApi}/enroll?userId=${userId}`, {});
  }

  getReward(userId: number): Observable<any> {
    return this.http.get(`${this.rewardApi}/${userId}`);
  }

  addPoints(userId: number, points: number): Observable<any> {
    return this.http.put(`${this.rewardApi}/${userId}/add-points?points=${points}`, {});
  }

  redeemPoints(userId: number, points: number): Observable<any> {
    return this.http.put(`${this.rewardApi}/${userId}/redeem?points=${points}`, {});
  }

  getRewardTransactions(userId: number): Observable<any> {
    return this.http.get(`${this.rewardApi}/${userId}/transactions`);
  }

  getLeaderboard(top = 10): Observable<any[]> {
    return this.http.get<any[]>(`${this.rewardApi}/leaderboard?top=${top}`);
  }

  getAnalytics(agentId: number): Observable<any> {
    return this.http.get(`${gatewayUrl('/dealer/api/Analytics')}/summary`);
  }
}

