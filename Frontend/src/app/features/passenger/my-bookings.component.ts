import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { BookingService, FlightService, OperationsService, PaymentService } from '../../core/services/api.service';

import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';
import { QRCodeComponent } from 'angularx-qrcode';

declare var Razorpay: any;

@Component({
  selector: 'app-my-bookings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, QRCodeComponent],
  template: `
    <!-- Dark Gold Nav -->
    <nav class="fixed top-0 w-full z-50 bg-[#131313]/80 backdrop-blur-xl flex justify-between items-center px-8 py-4 shadow-[0_4px_30px_rgba(0,0,0,0.5)]">
        <div class="font-serif italic text-2xl text-[#f2ca50] cursor-pointer" routerLink="/">SkyHorizon</div>
        <div class="hidden md:flex items-center space-x-8">
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/passenger/search">Fleet</a>
            <a class="font-serif tracking-tight text-lg text-[#f2ca50] border-b border-[#f2ca50]/50 pb-1 cursor-pointer" routerLink="/passenger/bookings">Itinerary</a>
        </div>
        <div class="flex items-center gap-4 relative">
            <button (click)="toggleNotifications()" class="relative text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300">
                <span class="material-symbols-outlined">notifications</span>
                <span *ngIf="unreadCount > 0" class="absolute -top-1 -right-1 bg-error text-white text-[10px] w-4 h-4 rounded-full flex items-center justify-center font-bold">{{ unreadCount }}</span>
            </button>
            <button class="text-[#d6c692]/70 hover:text-[#f2ca50] transition-all" routerLink="/passenger/profile"><span class="material-symbols-outlined">account_circle</span></button>
            <button (click)="doLogout()" class="text-error/70 hover:text-error transition-all duration-300">
                <span class="material-symbols-outlined">logout</span>
            </button>
            
            <!-- Notifications Dropdown -->
            <div *ngIf="showNotifications" class="absolute top-[120%] right-10 w-80 bg-surface-container-high border border-outline-variant/20 rounded-xl shadow-2xl z-50 overflow-hidden backdrop-blur-xl animate-fadeIn">
              <div class="bg-surface-container-low p-4 border-b border-outline-variant/10 flex justify-between items-center">
                 <h4 class="font-bold text-on-surface text-sm uppercase tracking-widest">Notifications</h4>
                 <button (click)="toggleNotifications()" class="text-secondary/60 hover:text-secondary"><span class="material-symbols-outlined text-sm">close</span></button>
              </div>
              <div class="max-h-80 overflow-y-auto p-2">
                 <div *ngIf="notifications.length === 0" class="p-4 text-center text-secondary/50 text-xs">No recent notifications</div>
                 <div *ngFor="let n of notifications" class="p-3 mb-1 bg-surface-container-low rounded-lg border border-outline-variant/5 transition-all" [class.opacity-60]="n.read">
                    <div class="flex justify-between items-start mb-1">
                       <div class="flex items-center gap-2">
                         <span class="material-symbols-outlined text-sm" [style.color]="n.color">{{ n.icon }}</span>
                         <span class="text-xs font-bold text-on-surface">{{ n.title }}</span>
                       </div>
                       <span class="text-[9px] text-secondary/40">{{ n.timestamp | date:'shortTime' }}</span>
                    </div>
                    <p class="text-[11px] text-secondary/70 line-clamp-2 leading-relaxed ml-6">{{ n.message }}</p>
                 </div>
              </div>
            </div>
        </div>
    </nav>
    <div class="bookings-page">
      <div class="page-header animate-fadeInUp">
        <h1>My <span class="text-gradient">Bookings</span></h1>
        <p>View, manage, reschedule and check-in for your flights</p>
      </div>

      @if (loading) {
        <div class="loading-overlay"><div class="spinner"></div><p>Loading bookings...</p></div>
      } @else if (bookings.length === 0) {
        <div class="empty-state animate-fadeIn">
          <div class="text-6xl mb-4 text-text-muted opacity-20"><span class="material-symbols-outlined text-8xl">confirmation_number</span></div>
          <h3>No bookings yet</h3>
          <p style="color:var(--text-secondary)">Book your first flight to see it here</p>
          <a routerLink="/passenger/search" class="btn btn-primary" style="margin-top:16px">Search Flights</a>
        </div>
      } @else {
        @for (b of bookings; track b.pnr; let i = $index) {
          <div class="booking-card card animate-fadeInUp" [style.animation-delay]="(i * 0.05) + 's'">
            <div class="booking-header">
              <div class="pnr">
                <span class="pnr-label">PNR</span>
                <span class="pnr-code">{{ b.pnr }}</span>
              </div>
              <span class="badge"
                [class.badge-success]="b.status === 'Confirmed'"
                [class.badge-warning]="b.status === 'Pending'"
                [class.badge-error]="b.status === 'Cancelled'">
                {{ b.status }}
              </span>
            </div>

            <div class="booking-body">
              <div class="booking-detail">
                <span class="label">Route</span>
                <span class="value">{{ b.source || '—' }} → {{ b.destination || '—' }}</span>
              </div>
              <div class="booking-detail">
                <span class="label">Flight</span>
                <span class="value">{{ b.flightNumber || ('AG-' + b.flightId) }}</span>
              </div>
              <div class="booking-detail">
                <span class="label">Amount</span>
                <span class="value price">₹{{ b.totalAmount | number:'1.0-0' }}</span>
              </div>
              <div class="booking-detail">
                <span class="label">Passengers</span>
                <span class="value">{{ b.passengers?.length || 1 }}</span>
              </div>
              <div class="booking-detail">
                <span class="label">Departure</span>
                <span class="value">{{ b.departureTime | date:'dd MMM yyyy, HH:mm' }}</span>
                <span class="value" style="font-size:10px;color:#f2ca50;opacity:0.7;font-family:monospace;">{{ b.departureTime | date:'HH:mm':'UTC' }} UTC</span>
              </div>
              <div class="booking-detail">
                <span class="label">Booked On</span>
                <span class="value">{{ b.bookingDate | date:'dd MMM yyyy, HH:mm' }}</span>
              </div>
            </div>

            <!-- Passenger list with partial cancellation -->
            @if (b.passengers?.length) {
              <div class="passengers-section">
                <div class="section-title">Passengers</div>
                @for (p of b.passengers; track p.passengerId) {
                  <div class="passenger-chip" [class.cancelled]="p.status === 'Cancelled'">
                    <span class="passenger-name">{{ p.name }}</span>
                    <span class="passenger-seat">{{ p.seatNo || 'No seat' }}</span>
                    @if (b.status !== 'Cancelled' && b.passengers.length > 1 && p.status !== 'Cancelled') {
                      <button class="btn-cancel-passenger" title="Cancel this passenger" (click)="cancelPassenger(b, p)">✕</button>
                    }
                    @if (p.status === 'Cancelled') {
                      <span class="badge badge-error" style="font-size:10px">Cancelled</span>
                    }
                  </div>
                }
              </div>
            }

            <!-- Baggage section -->
            @if (b.status !== 'Cancelled' && expandedBaggage[b.pnr]) {
              <div class="baggage-section">
                <div class="section-title flex items-center gap-2"><span class="material-symbols-outlined text-[18px]">luggage</span> Baggage</div>
                @if (baggageByPnr[b.pnr]?.length) {
                  @for (bag of baggageByPnr[b.pnr]; track bag.baggageId) {
                    <div class="baggage-item">
                      <span class="baggage-tag">{{ bag.tagNumber }}</span>
                      <span class="baggage-weight">{{ bag.weight }} kg</span>
                      <span class="badge badge-info">{{ bag.status }}</span>
                      @if (bag.weight > 15) {
                        <span class="badge badge-warning" style="font-size:10px">Extra: ₹{{ (bag.weight - 15) * 500 }}</span>
                      }
                    </div>
                  }
                } @else {
                  <p style="color:var(--text-secondary);font-size:13px;">No baggage added yet.</p>
                }
                <div class="add-baggage-form">
                  <input type="number" class="form-control" [(ngModel)]="newBaggageWeight" placeholder="Weight (kg)" style="width:120px" min="1" />
                  <button class="btn btn-secondary btn-sm" (click)="addBaggage(b.pnr, b.passengers?.[0]?.passengerId || 1)" [disabled]="!newBaggageWeight"><span class="material-symbols-outlined text-[16px] mr-1">add</span> Add Bag</button>
                  @if (newBaggageWeight && newBaggageWeight > 15) {
                    <span style="font-size:12px;color:#f2ca50;font-weight:600;margin-left:8px;">Fee: ₹{{ (newBaggageWeight - 15) * 500 }}</span>
                  } @else if (newBaggageWeight && newBaggageWeight > 0) {
                    <span style="font-size:12px;color:var(--success);margin-left:8px;">Free ✅</span>
                  }
                </div>
                <p style="font-size:11px;color:var(--text-muted);margin-top:4px;">15 kg free • ₹500/kg extra</p>
              </div>
            }

            @if (b.status === 'Cancelled') {
              <div class="refund-status-card" style="margin-top:12px;padding:16px;background:linear-gradient(135deg,rgba(16,185,129,0.08),rgba(16,185,129,0.02));border:1px solid rgba(16,185,129,0.2);border-radius:12px;">
                <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px;">
                  <span class="material-symbols-outlined text-success">payments</span>
                  <strong style="color:#10b981;font-size:14px;">Refund Status</strong>
                </div>
                @if (refundStatuses[b.pnr]) {
                  <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;font-size:13px;">
                    <div><span style="color:var(--text-secondary)">Status:</span> 
                      <span [style.color]="refundStatuses[b.pnr].paymentStatus === 'Refunded' ? '#10b981' : refundStatuses[b.pnr].paymentStatus === 'RefundPending' ? '#f59e0b' : refundStatuses[b.pnr].paymentStatus === 'RefundFailed' ? '#ef4444' : '#9ca3af'"
                            style="font-weight:700">{{ refundStatuses[b.pnr].paymentStatus }}</span>
                    </div>
                    <div><span style="color:var(--text-secondary)">Refund Amount:</span> <strong style="color:#10b981">₹{{ refundStatuses[b.pnr].refundAmount | number:'1.0-0' }}</strong></div>
                    @if (refundStatuses[b.pnr].countdown > 0) {
                      <div style="grid-column:1/-1;margin-top:4px;">
                        <div style="display:flex;align-items:center;gap:8px;">
                          <span class="material-symbols-outlined animate-spin" style="color:#f59e0b;font-size:18px;">progress_activity</span>
                          <span style="color:#f59e0b;font-weight:700;font-size:14px;">Refund processing in {{ refundStatuses[b.pnr].countdown }}s...</span>
                        </div>
                        <div style="margin-top:8px;height:6px;background:rgba(245,158,11,0.15);border-radius:4px;overflow:hidden;">
                          <div style="height:100%;background:linear-gradient(90deg,#f59e0b,#f2ca50);border-radius:4px;transition:width 1s linear;" [style.width]="((40 - refundStatuses[b.pnr].countdown) / 40 * 100) + '%'"></div>
                        </div>
                      </div>
                    }
                    @if (refundStatuses[b.pnr].refundId) {
                      <div><span style="color:var(--text-secondary)">Refund ID:</span> <code style="font-size:11px;color:#f2ca50;">{{ refundStatuses[b.pnr].refundId }}</code></div>
                    }
                    @if (refundStatuses[b.pnr].refundedAt) {
                      <div><span style="color:var(--text-secondary)">Processed:</span> {{ refundStatuses[b.pnr].refundedAt | date:'dd MMM yyyy, HH:mm' }}</div>
                    }
                  </div>
                  <p style="margin-top:8px;font-size:11px;color:var(--text-muted);font-style:italic;">{{ refundStatuses[b.pnr].testModeNote }}</p>
                } @else {
                  <div style="display:flex;align-items:center;gap:8px;">
                    <button class="btn btn-sm btn-secondary" (click)="checkRefundStatus(b.pnr)"><span class="material-symbols-outlined text-[16px] mr-1">search</span> Check Refund Status</button>
                    <span style="font-size:12px;color:var(--text-muted)">Click to check if your refund was processed</span>
                  </div>
                }
              </div>
            }

            @if (b.status !== 'Cancelled') {
              <div class="booking-actions">
                <button class="btn btn-secondary btn-sm" (click)="webCheckIn(b)"><span class="material-symbols-outlined text-[16px] mr-1">flight_takeoff</span> Web Check-in</button>
                <button class="btn btn-secondary btn-sm" (click)="toggleBaggage(b.pnr)"><span class="material-symbols-outlined text-[16px] mr-1">luggage</span> Baggage</button>
                <button class="btn btn-warning btn-sm" (click)="openReschedule(b)">🔄 Reschedule</button>
                <button class="btn btn-danger btn-sm" (click)="cancelBooking(b.pnr)">Cancel All</button>
              </div>
            }
          </div>
        }
      }
    </div>

    <!-- RESCHEDULE MODAL -->
    @if (rescheduleBooking) {
      <div class="modal-backdrop" (click)="rescheduleBooking = null">
        <div class="modal-content animate-fadeInUp" (click)="$event.stopPropagation()">
          <h2>🔄 Reschedule Booking</h2>
          <p style="color:var(--text-secondary);margin-bottom:12px;">
            PNR: <strong>{{ rescheduleBooking.pnr }}</strong> — Select a new flight
          </p>

          <div style="margin-bottom: 16px;">
            <label style="font-size: 12px; color: var(--text-muted); text-transform: uppercase;">Filter by Date</label>
            <input type="date" class="form-control" [(ngModel)]="rescheduleDateFilter" (change)="filterFlights()" style="margin-top: 4px;" [min]="minDate" />
          </div>

          @if (availableFlights.length === 0 && !rescheduleDateFilter) {
            <div class="loading-overlay" style="padding:40px 0"><div class="spinner"></div><p>Loading flights...</p></div>
          } @else if (filteredFlights.length === 0) {
            <p style="text-align: center; color: var(--text-muted); padding: 20px 0;">No flights available on selected date.</p>
          } @else {
            <div class="flights-list">
              @for (f of filteredFlights; track f.flightId) {
                <div class="flight-option" [class.selected]="selectedNewFlightId === f.flightId"
                     (click)="selectedNewFlightId = f.flightId">
                  <div class="fo-route">{{ f.source }} → {{ f.destination }}</div>
                  <div class="fo-details">
                    <span>{{ f.flightNumber }}</span>
                    <span>{{ f.departureTime | date:'dd MMM HH:mm' }}</span>
                    <span class="fo-price">₹{{ (f.price || f.basePrice) | number:'1.0-0' }}</span>
                  </div>
                  @if (selectedNewFlightId === f.flightId) {
                    <div style="margin-top: 8px; font-size: 13px; font-weight: 700; display: flex; justify-content: space-between; align-items: center; background: rgba(255,255,255,0.05); padding: 8px; border-radius: 8px;">
                      <span>Fare Difference</span>
                      <span [style.color]="getFareDiffAmount(f) > 0 ? '#ef4444' : (getFareDiffAmount(f) < 0 ? '#10b981' : '#9ca3af')">
                        {{ getFareDiffAmount(f) > 0 ? '+' : '' }}{{ getFareDiffAmount(f) | number:'1.0-0' }} ₹
                      </span>
                    </div>
                  }
                </div>
              }
            </div>
            
            @if (selectedNewFlightId) {
               <div style="margin-top: 16px; padding: 12px; border-radius: 8px; background: rgba(0,0,0,0.2); border: 1px solid var(--border-color); font-size: 13px;">
                  @if (getFareDiffAmount(getSelectedFlight()) > 0) {
                     <p style="color: #ef4444; margin: 0; display: flex; align-items: center; gap: 8px;"><span class="material-symbols-outlined text-[18px]">payment</span> You will be charged ₹{{ getFareDiffAmount(getSelectedFlight()) | number:'1.0-0' }} securely via Razorpay to complete the upgrade.</p>
                  } @else if (getFareDiffAmount(getSelectedFlight()) < 0) {
                     <p style="color: #10b981; margin: 0; display: flex; align-items: center; gap: 8px;"><span class="material-symbols-outlined text-[18px]">account_balance</span> A refund of ₹{{ Math.abs(getFareDiffAmount(getSelectedFlight())) | number:'1.0-0' }} will be initiated securely via Razorpay to your source account.</p>
                  } @else {
                     <p style="color: var(--text-muted); margin: 0; display: flex; align-items: center; gap: 8px;"><span class="material-symbols-outlined text-[18px]">info</span> No fare difference. Your reschedule will be processed instantly.</p>
                  }
               </div>
            }

            <div style="display:flex;gap:12px;margin-top:20px">
              <button class="btn btn-secondary" style="flex:1" (click)="closeReschedule()">Cancel</button>
              <button class="btn btn-primary" style="flex:1" [disabled]="!selectedNewFlightId || rescheduling"
                      (click)="confirmReschedule()">
                {{ rescheduling ? 'Processing...' : (getFareDiffAmount(getSelectedFlight()) > 0 ? 'Pay & Reschedule' : 'Confirm Reschedule') }}
              </button>
            </div>
          }
        </div>
      </div>
    }

    <!-- WEB CHECK-IN MODAL -->
    @if (checkinBooking) {
      <div class="modal-backdrop" (click)="checkinBooking = null">
        <div class="modal-content animate-fadeInUp" (click)="$event.stopPropagation()" style="max-width:580px">
          <!-- Header -->
          <div style="display:flex;align-items:center;gap:12px;margin-bottom:20px">
            <div style="width:44px;height:44px;border-radius:50%;background:linear-gradient(135deg,#f2ca50,#d1a030);display:flex;align-items:center;justify-content:center">
              <span class="material-symbols-outlined" style="color:#131118">flight_takeoff</span>
            </div>
            <div>
              <h2 style="margin:0;font-size:20px">Web Check-in</h2>
              <p style="margin:0;color:var(--text-secondary);font-size:13px">PNR: <strong style="color:#f2ca50;font-family:monospace">{{ checkinBooking.pnr }}</strong></p>
            </div>
          </div>

          @if (!viewingBoardingPass) {
            @if (checkinBooking.passengers?.length) {
              @for (p of checkinBooking.passengers; track p.passengerId) {
                <div class="checkin-passenger" [style.borderColor]="p.checkedIn ? 'rgba(16,185,129,0.4)' : 'var(--border-color)'" [style.background]="p.checkedIn ? 'rgba(16,185,129,0.06)' : 'var(--bg-glass)'">
                  <div style="display:flex;align-items:center;gap:10px;flex:1">
                    <div style="width:38px;height:38px;border-radius:50%;display:flex;align-items:center;justify-content:center" [style.background]="p.checkedIn ? 'rgba(16,185,129,0.2)' : 'rgba(255,255,255,0.05)'">
                      <span class="material-symbols-outlined" style="font-size:20px" [style.color]="p.checkedIn ? '#10b981' : 'var(--text-muted)'">{{ p.checkedIn ? 'how_to_reg' : 'person' }}</span>
                    </div>
                    <div>
                      <div style="font-weight:700;font-size:15px">{{ p.name }}</div>
                      <div style="font-size:11px;color:var(--text-muted);text-transform:uppercase;letter-spacing:1px">{{ p.gender }} · {{ p.age }} yrs</div>
                      @if (p.checkedIn && p._gate) {
                        <div style="font-size:12px;color:#f2ca50;margin-top:2px">Seat <strong>{{ p.seatNo }}</strong> · Gate <strong>{{ p._gate }}</strong></div>
                      }
                    </div>
                  </div>
                  @if (p.checkedIn) {
                    <span class="badge badge-success">✓ Done</span>
                  } @else {
                    <div style="display:flex;align-items:center;gap:8px">
                      <div class="cp-seat">
                        <label>Seat</label>
                        <input type="text" class="form-control" [(ngModel)]="checkinSeats[p.passengerId]" [placeholder]="p.seatNo || '14A'" style="width:70px;text-align:center" />
                      </div>
                      <button class="btn btn-primary btn-sm" (click)="executeCheckIn(p)" [disabled]="checkingIn">{{ checkingIn ? '...' : 'Check In' }}</button>
                    </div>
                  }
                </div>
              }
            }
            <div style="display:flex;gap:10px;margin-top:16px;flex-wrap:wrap">
              @if (allCheckedIn()) {
                <button (click)="viewBoardingPass()" style="flex:1;padding:12px;background:transparent;color:#f2ca50;border:2px solid rgba(242,202,80,0.4);border-radius:10px;font-weight:700;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;min-width:160px;transition:all 0.2s" onmouseover="this.style.background='rgba(242,202,80,0.1)'" onmouseout="this.style.background='transparent'">
                  <span class="material-symbols-outlined">visibility</span> View Boarding Pass
                </button>
                <button (click)="printAllBoardingPasses()" style="flex:1;padding:12px;background:#f2ca50;color:#131118;border:none;border-radius:10px;font-weight:700;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;min-width:160px">
                  <span class="material-symbols-outlined">print</span> Print Boarding Pass(es)
                </button>
              }
              <button class="btn btn-secondary" style="flex:1;margin:0;min-width:100px" (click)="checkinBooking = null">Close</button>
            </div>
          } @else {
            <!-- INLINE BOARDING PASS VIEW — Single consolidated ticket -->
            <div style="margin-bottom:16px">
              <div class="bp-card">
                <div class="bp-card-top">
                  <div class="bp-card-airline"><span style="font-size:20px">✈</span> <span style="font-weight:700;font-family:Outfit,sans-serif">SkyHorizon Airlines</span></div>
                  <span style="font-size:10px;font-weight:700;letter-spacing:3px;color:#d6c692">BOARDING PASS</span>
                </div>
                <div class="bp-card-route">
                  <div style="text-align:center"><span style="font-size:32px;font-weight:900;font-family:Outfit,sans-serif">{{ checkinBooking.source || 'DEL' }}</span><br><span style="font-size:12px;color:var(--text-muted)">{{ checkinBooking.sourceName || checkinBooking.source || 'Origin' }}</span></div>
                  <div style="flex:1;display:flex;align-items:center;gap:0"><div style="flex:1;height:2px;background:repeating-linear-gradient(90deg,#f2ca50 0,#f2ca50 4px,transparent 4px,transparent 8px)"></div><span style="color:#d6c692;font-size:18px;padding:0 6px">✈</span><div style="flex:1;height:2px;background:repeating-linear-gradient(90deg,#f2ca50 0,#f2ca50 4px,transparent 4px,transparent 8px)"></div></div>
                  <div style="text-align:center"><span style="font-size:32px;font-weight:900;font-family:Outfit,sans-serif">{{ checkinBooking.destination || 'BOM' }}</span><br><span style="font-size:12px;color:var(--text-muted)">{{ checkinBooking.destinationName || checkinBooking.destination || 'Destination' }}</span></div>
                </div>
                <div class="bp-card-details" style="grid-template-columns:1fr 1fr 1fr;">
                  <div class="bp-card-cell"><span class="bp-card-label">FLIGHT</span><span class="bp-card-val">AG-{{ checkinBooking.flightId }}</span></div>
                  <div class="bp-card-cell"><span class="bp-card-label">DATE</span><span class="bp-card-val">{{ checkinBooking.bookingDate | date:'dd MMM yyyy' }}</span></div>
                  <div class="bp-card-cell"><span class="bp-card-label">PASSENGERS</span><span class="bp-card-val">{{ getCheckedInPassengers().length }}</span></div>
                </div>

                <!-- Per-passenger rows within the single card -->
                @for (p of getCheckedInPassengers(); track p.passengerId; let pi = $index) {
                  <div style="display:grid;grid-template-columns:2fr 1fr 1fr 1fr;border-top:1px solid rgba(255,255,255,0.06);padding:10px 18px;align-items:center" [style.background]="pi % 2 === 0 ? 'rgba(255,255,255,0.02)' : 'transparent'">
                    <div><span class="bp-card-label">PASSENGER {{ pi + 1 }}</span><span class="bp-card-val">{{ p.name }}</span></div>
                    <div><span class="bp-card-label">SEAT</span><span class="bp-card-val" style="font-size:18px;background:linear-gradient(135deg,#f2ca50,#d6c692);-webkit-background-clip:text;-webkit-text-fill-color:transparent">{{ p.seatNo || '—' }}</span></div>
                    <div><span class="bp-card-label">GATE</span><span class="bp-card-val">{{ p._gate || 'TBD' }}</span></div>
                    <div><span class="bp-card-label">BOARDING</span><span class="bp-card-val">{{ p._boardingTime || 'See display' }}</span></div>
                  </div>
                }

                <div class="bp-card-pnr">
                  <div><span class="bp-card-label">PNR</span><span style="font-size:24px;font-weight:900;font-family:'Courier New',monospace;letter-spacing:4px;color:#f2ca50;display:block">{{ checkinBooking.pnr }}</span></div>
                  <div style="background:white;padding:6px;border-radius:6px;display:flex;align-items:center;justify-content:center"><qrcode [qrdata]="'https://skyhorizon.com/verify/' + checkinBooking.pnr" [width]="70" [errorCorrectionLevel]="'M'"></qrcode></div>
                </div>
                <div class="bp-card-tear"><div class="bp-card-tear-c left"></div><div style="flex:1;margin:0 14px;border-top:2px dashed rgba(255,255,255,0.1)"></div><div class="bp-card-tear-c right"></div></div>
                <div class="bp-card-stub">
                  <span>{{ checkinBooking.pnr }}</span><span>AG-{{ checkinBooking.flightId }}</span><span>{{ getCheckedInPassengers().length }} PAX</span><span>{{ checkinBooking.bookingDate | date:'dd MMM' }}</span>
                </div>
              </div>
            </div>
            <div style="display:flex;gap:10px;margin-top:4px">
              <button (click)="viewingBoardingPass = false" style="flex:1;padding:12px;background:transparent;color:var(--text-secondary);border:1px solid var(--border-color);border-radius:10px;font-weight:600;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px">
                <span class="material-symbols-outlined" style="font-size:18px">arrow_back</span> Back
              </button>
            </div>
          }
        </div>
      </div>
    }

  `,
  styles: [`
    .bookings-page { max-width: 850px; margin: 0 auto; padding: 40px 20px; }
    .booking-card { margin-bottom: 16px; }
    .booking-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .pnr-label { font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; display: block; }
    .pnr-code { font-size: 22px; font-weight: 800; letter-spacing: 3px; font-family: 'Courier New', monospace; color: var(--accent-primary); }
    .booking-body { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 16px; }
    .booking-detail .label { display: block; font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; }
    .booking-detail .value { font-size: 15px; font-weight: 600; }
    .booking-detail .price { color: var(--success); }
    .booking-actions { display: flex; gap: 8px; padding-top: 12px; border-top: 1px solid var(--border-color); flex-wrap: wrap; }
    .empty-state { text-align: center; padding: 80px 20px; }

    .passengers-section { margin-bottom: 12px; }
    .section-title { font-size: 12px; font-weight: 700; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; margin-bottom: 8px; }
    .passenger-chip { display: inline-flex; gap: 8px; align-items: center; background: var(--bg-glass); border: 1px solid var(--border-color); border-radius: 8px; padding: 6px 12px; margin-right: 8px; margin-bottom: 4px; font-size: 13px; }
    .passenger-name { font-weight: 600; }
    .passenger-seat { color: var(--accent-primary); font-family: monospace; font-weight: 700; }
    .passenger-chip.cancelled { opacity: 0.5; text-decoration: line-through; }
    .btn-cancel-passenger { background: none; border: 1px solid rgba(255,80,80,0.3); color: #ff5050; border-radius: 50%; width: 22px; height: 22px; font-size: 11px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; margin-left: auto; }
    .btn-cancel-passenger:hover { background: rgba(255,80,80,0.15); border-color: #ff5050; }

    .baggage-section { padding: 16px; background: var(--bg-glass); border-radius: var(--radius-md); margin-bottom: 12px; border: 1px solid var(--border-color); }
    .baggage-item { display: flex; gap: 12px; align-items: center; padding: 8px 0; border-bottom: 1px solid var(--border-color); font-size: 14px; }
    .baggage-tag { font-family: monospace; font-weight: 700; color: var(--accent-secondary); }
    .baggage-weight { font-weight: 600; }
    .add-baggage-form { display: flex; gap: 8px; align-items: center; margin-top: 12px; }

    .btn-warning { background: var(--warning); color: #000; border: none; }
    .btn-warning:hover { filter: brightness(1.1); }

    /* Modal */
    .modal-backdrop { position: fixed; inset: 0; z-index: 1000; background: rgba(0,0,0,0.7); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; padding: 20px; }
    .modal-content { background: var(--bg-card); border: 1px solid var(--border-color); border-radius: var(--radius-xl); padding: 32px; max-width: 540px; width: 100%; max-height: 80vh; overflow-y: auto; }
    .modal-content h2 { font-size: 22px; margin-bottom: 8px; }

    .flights-list { display: flex; flex-direction: column; gap: 8px; max-height: 300px; overflow-y: auto; }
    .flight-option { padding: 14px 16px; background: var(--bg-glass); border: 2px solid var(--border-color); border-radius: var(--radius-md); cursor: pointer; transition: all 0.2s; }
    .flight-option:hover { border-color: var(--accent-primary); }
    .flight-option.selected { border-color: var(--accent-primary); background: rgba(0,97,255,0.1); box-shadow: 0 0 15px rgba(0,97,255,0.2); }
    .fo-route { font-size: 16px; font-weight: 700; margin-bottom: 4px; }
    .fo-details { display: flex; gap: 16px; font-size: 13px; color: var(--text-secondary); }
    .fo-price { color: var(--success); font-weight: 700; }

    .checkin-passenger { display: flex; align-items: center; gap: 12px; padding: 12px; background: var(--bg-glass); border: 1px solid var(--border-color); border-radius: var(--radius-md); margin-bottom: 8px; }
    .cp-info { flex: 1; }
    .cp-name { display: block; font-weight: 700; font-size: 15px; }
    .cp-meta { font-size: 12px; color: var(--text-secondary); }
    .cp-seat { display: flex; flex-direction: column; gap: 2px; }
    .cp-seat label { font-size: 10px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 1px; }

    /* Inline Boarding Pass Card (dark theme) */
    .bp-card { background: linear-gradient(145deg, rgba(17,21,56,0.95), rgba(10,14,39,0.98)); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; overflow: hidden; margin-bottom: 16px; box-shadow: 0 12px 40px rgba(0,0,0,0.4); }
    .bp-card-top { display: flex; justify-content: space-between; align-items: center; padding: 20px 24px 14px; }
    .bp-card-airline { display: flex; align-items: center; gap: 10px; color: #fff; }
    .bp-card-route { display: flex; align-items: center; gap: 12px; padding: 14px 24px 24px; }
    .bp-card-details { display: grid; grid-template-columns: repeat(3,1fr); border-top: 1px solid rgba(255,255,255,0.08); }
    .bp-card-cell { padding: 14px 18px; border-bottom: 1px solid rgba(255,255,255,0.08); border-right: 1px solid rgba(255,255,255,0.08); }
    .bp-card-cell:nth-child(3n) { border-right: none; }
    .bp-card-label { display: block; font-size: 9px; font-weight: 700; color: rgba(255,255,255,0.35); text-transform: uppercase; letter-spacing: 1.5px; margin-bottom: 4px; }
    .bp-card-val { font-size: 14px; font-weight: 700; color: #fff; }
    .bp-card-pnr { padding: 18px 24px; display: flex; justify-content: space-between; align-items: center; }
    .bp-card-tear { position: relative; height: 20px; display: flex; align-items: center; }
    .bp-card-tear-c { width: 20px; height: 20px; border-radius: 50%; background: var(--bg-card); position: absolute; }
    .bp-card-tear-c.left { left: -10px; }
    .bp-card-tear-c.right { right: -10px; }
    .bp-card-stub { padding: 12px 24px 16px; display: flex; justify-content: space-between; font-size: 11px; color: rgba(255,255,255,0.3); text-transform: uppercase; letter-spacing: 1px; }

    @media (max-width: 768px) {
      .booking-body { grid-template-columns: 1fr 1fr; }
      .booking-actions { flex-direction: column; }
    }
  `]
})
export class MyBookingsComponent implements OnInit {
  bookings: any[] = [];
  loading = true;

  // Reschedule
  rescheduleBooking: any = null;
  availableFlights: any[] = [];
  filteredFlights: any[] = [];
  selectedNewFlightId: number | null = null;
  rescheduling = false;
  rescheduleDateFilter: string = '';
  minDate: string = new Date().toISOString().split('T')[0];
  Math = Math; // for template usage

  // Web Check-in
  checkinBooking: any = null;
  checkinSeats: Record<number, string> = {};
  checkingIn = false;

  // Baggage
  expandedBaggage: Record<string, boolean> = {};
  baggageByPnr: Record<string, any[]> = {};
  newBaggageWeight: number | null = null;

  // Notifications
  showNotifications = false;
  viewingBoardingPass = false;
  private flightMap: Record<number, any> = {};

  get notifications() { return this.notifService.getNotifications(); }
  get unreadCount() { return this.notifService.getUnreadCount(); }

  constructor(
    private bookingService: BookingService,
    private flightService: FlightService,
    private opsService: OperationsService,
    private paymentService: PaymentService,
    public auth: AuthService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService
  ) {}

  toggleNotifications() {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) {
      this.notifService.markAllRead();
    }
  }

  doLogout() {
    this.auth.logout();
  }

  ngOnInit() {
    this.loadRazorpayScript();
    this.notifService.loadServerNotifications();
    const userId = this.auth.currentUser?.userId || 0;

    // Load bookings and flights in PARALLEL for speed
    forkJoin({
      bookings: this.bookingService.getMyBookings(userId),
      flights: this.flightService.getAll()
    }).subscribe({
      next: ({ bookings, flights }) => {
        this.flightMap = {};
        (flights || []).forEach((f: any) => this.flightMap[f.flightId] = f);
        this.bookings = (bookings || []).map((b: any) => {
          const f = this.flightMap[b.flightId];
          if (f) {
            b.source = f.source || f.origin || '';
            b.destination = f.destination || '';
            b.flightNumber = f.flightNumber || '';
            b.departureTime = b.departureTime || f.departureTime;
          }
          return b;
        });
        this.loading = false;
        this.cdr.detectChanges();
        this.loadCheckInStatuses();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadCheckInStatuses() {
    this.bookings.forEach((b: any) => {
      if (b.status === 'Cancelled' || !b.passengers?.length) return;
      this.opsService.getCheckInStatus(b.pnr).subscribe({
        next: (statuses: any[]) => {
          if (statuses && statuses.length > 0) {
            statuses.forEach((s: any) => {
              const p = b.passengers.find((px: any) => px.passengerId === s.passengerId);
              if (p && s.status === 'CheckedIn') {
                p.checkedIn = true;
                p.seatNo = s.seatNo || p.seatNo;
                p._gate = s.gate || '';
                p._boardingTime = s.boardingTime || '';
              }
            });
            this.cdr.detectChanges();
          }
        }
      });
    });
  }

  loadRazorpayScript() {
    if (document.querySelector(`script[src="https://checkout.razorpay.com/v1/checkout.js"]`)) return;
    const script = document.createElement('script');
    script.src = 'https://checkout.razorpay.com/v1/checkout.js';
    script.async = true;
    document.body.appendChild(script);
  }

  refundStatuses: any = {};
  refundCountdowns: Record<string, number> = {};

  // ── Cancel (post-payment) ───────────────────────────────
  cancelBooking(pnr: string) {
    const b = this.bookings.find(x => x.pnr === pnr);
    const isPending = b?.status === 'Pending';
    const refundAmount = isPending ? 0 : (b ? Math.round(b.totalAmount * 0.9) : 0);
    
    const confirmMsg = isPending 
      ? `Cancel pending booking ${pnr}? No charges were made.`
      : `Cancel booking ${pnr}?\n\nRefund of ₹${refundAmount} (10% penalty) will be processed after 40 seconds.`;

    if (!confirm(confirmMsg)) return;

    this.bookingService.cancel(pnr).subscribe({
      next: (res) => {
        this.toast.success('Booking cancelled — seats released');
        if (b) {
          b.status = 'Cancelled';
          // Release seats immediately
          (b.passengers || []).forEach((p: any) => {
            if (p.seatNo && p.seatNo !== 'TBD') {
              this.flightService.releaseSeat(b.flightId, p.seatNo).subscribe();
            }
          });
        }
        this.notifService.bookingCancelled(pnr);
        this.cdr.detectChanges();

        // If booking was Pending, no payment was made to refund
        if (isPending) {
          return;
        }

        // Start 40-second refund countdown for Confirmed bookings
        this.refundCountdowns[pnr] = 40;
        this.refundStatuses[pnr] = { paymentStatus: 'RefundPending', refundAmount, countdown: 40 };
        this.cdr.detectChanges();

        const interval = setInterval(() => {
          this.refundCountdowns[pnr]--;
          if (this.refundStatuses[pnr]) {
            this.refundStatuses[pnr].countdown = this.refundCountdowns[pnr];
          }
          this.cdr.detectChanges();

          if (this.refundCountdowns[pnr] <= 0) {
            clearInterval(interval);
            // The backend RefundProcessingWorker automatically processes the refund 
            // asynchronously within 40 seconds. However, because it polls every 10s, it might take up to 50s.
            // We will poll the true status every 3 seconds for up to 5 attempts.
            let attempts = 0;
            const checkInterval = setInterval(() => {
                attempts++;
                this.paymentService.getRefundStatus(pnr).subscribe({
                    next: (res) => {
                        this.refundStatuses[pnr] = res;
                        this.cdr.detectChanges();
                        if (res.paymentStatus === 'Refunded' || attempts >= 5) {
                            clearInterval(checkInterval);
                            if (res.paymentStatus === 'Refunded') {
                                this.toast.success(`✅ Refund of ₹${res.refundAmount} processed successfully!`);
                            }
                        }
                    },
                    error: () => {
                        if (attempts >= 5) clearInterval(checkInterval);
                    }
                });
            }, 3000);
            
            // Show a toast that the time has elapsed and status is refreshed
            this.toast.info(`Refund processing window complete. Verifying settlement...`);
          }
        }, 1000);
      },
      error: (err) => this.toast.error(err.error?.error || 'Cancel failed')
    });
  }

  checkRefundStatus(pnr: string) {
    this.paymentService.getRefundStatus(pnr).subscribe({
      next: (res) => {
        this.refundStatuses[pnr] = res;
        this.cdr.detectChanges();
      },
      error: () => {
        this.refundStatuses[pnr] = { paymentStatus: 'No Payment Found', refundAmount: 0, testModeNote: 'No payment record exists for this PNR.' };
        this.cdr.detectChanges();
      }
    });
  }

  // ── Partial Cancellation (single passenger) ─────────────
  cancelPassenger(booking: any, passenger: any) {
    const activePassengers = booking.passengers.filter((p: any) => p.status !== 'Cancelled');
    if (activePassengers.length <= 1) {
      this.toast.warning('Cannot cancel the last passenger. Use full cancellation instead.');
      return;
    }
    if (!confirm(`Cancel passenger "${passenger.name}" from PNR ${booking.pnr}? A proportional refund will be processed.`)) return;

    this.bookingService.cancel(booking.pnr, [passenger.passengerId]).subscribe({
      next: () => {
        passenger.status = 'Cancelled';
        const refundPerPassenger = (booking.totalAmount / booking.passengers.length) * 0.9;
        this.toast.success(`Passenger "${passenger.name}" cancelled. Estimated refund: ₹${Math.round(refundPerPassenger)}`);
        this.cdr.detectChanges();
        // Automatically release seat and trigger refund for partial
        if (passenger.seatNo && passenger.seatNo !== 'TBD') {
          this.flightService.releaseSeat(booking.flightId, passenger.seatNo).subscribe();
        }
        this.paymentService.refund(0, `Partial Cancellation refund for passenger ${passenger.name}`, booking.pnr, refundPerPassenger).subscribe({
           next: () => this.checkRefundStatus(booking.pnr),
           error: () => {}
        });
      },
      error: (err) => this.toast.error(err.error?.error || 'Failed to cancel passenger')
    });
  }

  // ── Check-in Window Enforcement ──────────────────────────
  getCheckinStatus(booking: any): 'open' | 'early' | 'closed' {
    if (!booking.departureTime) return 'open'; // fallback
    const departure = new Date(booking.departureTime).getTime();
    const now = Date.now();
    const hoursUntilDeparture = (departure - now) / (1000 * 60 * 60);

    if (hoursUntilDeparture > 24) return 'early';   // Too early
    if (hoursUntilDeparture < 2) return 'closed';    // Too late
    return 'open';
  }

  getCheckinOpensIn(booking: any): string {
    if (!booking.departureTime) return '';
    const departure = new Date(booking.departureTime).getTime();
    const checkinOpens = departure - (24 * 60 * 60 * 1000);
    const now = Date.now();
    const diff = checkinOpens - now;
    if (diff <= 0) return 'now';
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    return hours > 0 ? `in ${hours}h ${mins}m` : `in ${mins}m`;
  }

  // ── Reschedule ──────────────────────────────────────────
  openReschedule(booking: any) {
    this.rescheduleBooking = booking;
    this.selectedNewFlightId = null;
    this.availableFlights = [];
    this.filteredFlights = [];
    this.rescheduleDateFilter = '';
    
    // Use cached flight map if available for instant display
    if (Object.keys(this.flightMap).length > 0) {
      this.availableFlights = Object.values(this.flightMap).filter((f: any) =>
        f.flightId !== booking.flightId && f.availableSeats > 0 && f.status !== 'Deleted'
      );
      this.filteredFlights = [...this.availableFlights];
      this.cdr.detectChanges();
    }
    // Also refresh from server
    this.flightService.getAll().subscribe({
      next: (flights) => {
        flights.forEach((f: any) => this.flightMap[f.flightId] = f);
        this.availableFlights = flights.filter((f: any) =>
          f.flightId !== booking.flightId && f.availableSeats > 0 && f.status !== 'Deleted'
        );
        this.filteredFlights = this.rescheduleDateFilter
          ? this.availableFlights.filter(f => new Date(f.departureTime).toISOString().split('T')[0] === this.rescheduleDateFilter)
          : [...this.availableFlights];
        this.cdr.detectChanges();
      }
    });
  }

  filterFlights() {
    if (this.rescheduleDateFilter) {
      this.filteredFlights = this.availableFlights.filter(f => 
        new Date(f.departureTime).toISOString().split('T')[0] === this.rescheduleDateFilter
      );
    } else {
      this.filteredFlights = this.availableFlights;
    }
  }

  closeReschedule() {
    this.rescheduleBooking = null;
    this.selectedNewFlightId = null;
  }

  getSelectedFlight(): any {
    return this.filteredFlights.find(f => f.flightId === this.selectedNewFlightId);
  }

  getFareDiffAmount(flight: any): number {
    if (!flight || !this.rescheduleBooking) return 0;
    const oldFare = this.rescheduleBooking.totalAmount;
    const passengersCount = this.rescheduleBooking.passengers?.length || 1;
    const newFare = flight.price * passengersCount;
    return newFare - oldFare;
  }

  confirmReschedule() {
    const flight = this.getSelectedFlight();
    if (!flight || !this.rescheduleBooking) return;
    
    const diff = Math.round(this.getFareDiffAmount(flight));
    
    if (diff > 0) {
      // Prompt payment to upgrade
      if (typeof Razorpay === 'undefined') {
        this.toast.error('Payment gateway not loaded. Please wait and try again.');
        return;
      }
      this.rescheduling = true;
      this.toast.info(`Initiating upgrade payment of ₹${diff}...`);
      this.paymentService.createRazorpayOrder(this.rescheduleBooking.pnr + '_RS', diff).subscribe({
        next: (order) => {
          this.processRescheduleUpgradePayment(diff, order, flight);
        },
        error: () => {
          this.rescheduling = false;
          this.toast.error('Failed to initiate upgrade payment');
        }
      });
    } else if (diff < 0) {
      // Refund difference
      this.rescheduling = true;
      this.toast.info(`Issuing automatic refund of ₹${Math.abs(diff)}...`);
      this.paymentService.refund(0, `Partial refund for rescheduling fare difference`, this.rescheduleBooking.pnr, Math.abs(diff)).subscribe({
        next: () => {
          this.executeRescheduleCall(flight.flightId, flight.price * (this.rescheduleBooking.passengers?.length || 1));
        },
        error: () => {
          // Warning but proceed with reschedule
          this.toast.warning('Refund encountered an issue but rescheduling will continue.');
          this.executeRescheduleCall(flight.flightId, flight.price * (this.rescheduleBooking.passengers?.length || 1));
        }
      });
    } else {
      this.rescheduling = true;
      this.executeRescheduleCall(flight.flightId, this.rescheduleBooking.totalAmount);
    }
  }

  processRescheduleUpgradePayment(diff: number, order: any, flight: any) {
    const options = {
      key: order.keyId,
      amount: order.amount,
      currency: order.currency,
      name: 'SkyLedger Airlines',
      description: `Flight Upgrade Fee (PNR: ${this.rescheduleBooking.pnr})`,
      order_id: order.orderId,
      prefill: {
        name: this.auth.currentUser?.name,
        email: this.auth.currentUser?.email
      },
      handler: (response: any) => {
        this.toast.info('Verifying upgrade payment...');
        this.paymentService.verifyRazorpay({
          pnr: this.rescheduleBooking.pnr + '_RS',
          amount: diff,
          method: 'Razorpay',
          razorpay_order_id: response.razorpay_order_id,
          razorpay_payment_id: response.razorpay_payment_id,
          razorpay_signature: response.razorpay_signature
        }).subscribe({
          next: () => {
            this.toast.success('Upgrade payment successful!');
            this.executeRescheduleCall(flight.flightId, flight.price * (this.rescheduleBooking.passengers?.length || 1));
          },
          error: () => {
            this.rescheduling = false;
            this.toast.error('Payment verification failed.');
          }
        });
      },
      modal: {
        ondismiss: () => {
          this.rescheduling = false;
          this.toast.warning('Upgrade payment cancelled.');
        }
      }
    };
    new Razorpay(options).open();
  }

  executeRescheduleCall(newFlightId: number, newTotalAmount: number) {
    const oldBooking = this.rescheduleBooking;
    // Release old seats directly for immediate sync
    (oldBooking.passengers || []).forEach((p: any) => {
      if (p.seatNo && p.seatNo !== 'TBD') {
        this.flightService.releaseSeat(oldBooking.flightId, p.seatNo).subscribe();
      }
    });
    this.bookingService.reschedule(oldBooking.pnr, newFlightId, newTotalAmount).subscribe({
      next: (res) => {
        const newFlight = this.flightMap[newFlightId];
        this.toast.success('Booking rescheduled successfully!');
        this.notifService.bookingRescheduled(oldBooking.pnr, newFlight?.flightNumber || `AG-${newFlightId}`);
        this.rescheduling = false;
        this.closeReschedule();
        this.ngOnInit();
      },
      error: (err) => {
        this.rescheduling = false;
        this.toast.error(err.error?.error || 'Reschedule failed');
      }
    });
  }

  // ── Web Check-in ────────────────────────────────────────
  webCheckIn(booking: any) {
    this.checkinBooking = { ...booking };
    this.checkinSeats = {};
    this.viewingBoardingPass = false;
    booking.passengers?.forEach((p: any) => {
      this.checkinSeats[p.passengerId] = p.seatNo || '';
    });
  }

  executeCheckIn(passenger: any) {
    const seat = this.checkinSeats[passenger.passengerId];
    if (!seat) {
      this.toast.warning('Please assign a seat first');
      return;
    }
    this.checkingIn = true;
    this.opsService.checkIn({
      PNR: this.checkinBooking.pnr,
      PassengerId: passenger.passengerId,
      SeatNo: seat,
      PassengerName: passenger.name || 'Passenger',
      Email: passenger.email || '',
      FlightDepartureTime: this.checkinBooking?.departureTime || null
    }).subscribe({
      next: (res) => {
        passenger.checkedIn = true;
        passenger.seatNo = seat;
        passenger._gate = res.gate;
        // Update parent booking state
        const b = this.bookings.find(x => x.pnr === this.checkinBooking.pnr);
        if (b) {
          const p = b.passengers?.find((x: any) => x.passengerId === passenger.passengerId);
          if (p) { p.seatNo = seat; p.checkedIn = true; }
        }
        this.checkingIn = false;
        this.toast.success(`✅ Checked in! Seat ${seat} · Gate ${res.gate || '—'}`);
        this.notifService.passengerCheckedIn(this.checkinBooking.pnr, passenger.name, seat, res.gate || '');
        this.cdr.detectChanges();
      },
      error: (err) => {
        const msg = err?.error?.error || 'Check-in failed';
        this.checkingIn = false;
        if (msg.toLowerCase().includes('already')) {
          passenger.checkedIn = true;
        } else {
          this.toast.error(msg);
        }
      }
    });
  }

  printAllBoardingPasses() {
    this.toast.info('Generating boarding passes...');
    this.opsService.downloadAllBoardingPasses(this.checkinBooking.pnr).subscribe({
      next: (html: string) => {
        // Open a new window with the boarding pass HTML and auto-trigger print
        const printWindow = window.open('', '_blank', 'width=800,height=600');
        if (printWindow) {
          printWindow.document.open();
          printWindow.document.write(html);
          printWindow.document.close();
          // window.print() is already called via <script> in the HTML
        } else {
          this.toast.error('Pop-up blocked — please allow pop-ups for this site.');
        }
      },
      error: () => {
        this.toast.error('Failed to load boarding passes. Please try again.');
      }
    });
  }

  allCheckedIn(): boolean {
    return this.checkinBooking?.passengers?.every((p: any) => p.checkedIn) ?? false;
  }

  viewBoardingPass() {
    this.viewingBoardingPass = true;
  }

  getCheckedInPassengers(): any[] {
    return this.checkinBooking?.passengers?.filter((p: any) => p.checkedIn) || [];
  }

  // ── Baggage ─────────────────────────────────────────────
  toggleBaggage(pnr: string) {
    this.expandedBaggage[pnr] = !this.expandedBaggage[pnr];
    if (this.expandedBaggage[pnr] && !this.baggageByPnr[pnr]) {
      this.opsService.getBaggage(pnr).subscribe({
        next: (data: any[]) => this.baggageByPnr[pnr] = data,
        error: () => this.baggageByPnr[pnr] = []
      });
    }
  }

  addBaggage(pnr: string, passengerId: number) {
    if (!this.newBaggageWeight || this.newBaggageWeight <= 0) return;
    const weight = this.newBaggageWeight;
    const freeAllowance = 15; // kg
    const ratePerKg = 500; // ₹ per kg over allowance
    const extraWeight = Math.max(0, weight - freeAllowance);
    const baggageFee = extraWeight * ratePerKg;

    if (baggageFee > 0) {
      // Need to pay for extra baggage
      this.toast.info(`Initiating payment of ₹${baggageFee} for extra baggage...`);
      this.paymentService.createRazorpayOrder(pnr, baggageFee).subscribe({
        next: (order) => {
          this.processBaggagePayment(pnr, passengerId, weight, extraWeight, baggageFee, order);
        },
        error: () => this.toast.error('Failed to initiate payment for extra baggage')
      });
    } else {
      this.executeAddBaggage(pnr, passengerId, weight, 0, 0);
    }
  }

  processBaggagePayment(pnr: string, passengerId: number, weight: number, extraWeight: number, fee: number, order: any) {
    if (typeof Razorpay === 'undefined') {
      this.toast.error('Payment gateway not loaded. Please try again.');
      return;
    }

    const options = {
      key: order.keyId,
      amount: order.amount,
      currency: order.currency,
      name: 'SkyLedger Airlines',
      description: `Baggage Fee for ${extraWeight}kg extra (PNR: ${pnr})`,
      order_id: order.orderId,
      prefill: {
        name: this.auth.currentUser?.name,
        email: this.auth.currentUser?.email
      },
      handler: (response: any) => {
        this.toast.info('Verifying payment...');
        this.paymentService.verifyRazorpay({
          pnr: pnr,
          amount: fee,
          method: 'Razorpay',
          razorpay_order_id: response.razorpay_order_id,
          razorpay_payment_id: response.razorpay_payment_id,
          razorpay_signature: response.razorpay_signature
        }).subscribe({
          next: () => {
            this.toast.success('Payment successful! Adding baggage...');
            this.executeAddBaggage(pnr, passengerId, weight, extraWeight, fee);
          },
          error: () => {
            // Fallback since webhooks might handle it, but for UI we execute anyway or show error.
            this.toast.warning('Payment verification delayed, but proceeding with baggage tag.');
            this.executeAddBaggage(pnr, passengerId, weight, extraWeight, fee);
          }
        });
      },
      modal: {
        ondismiss: () => {
          this.toast.info('Payment cancelled');
        }
      }
    };

    const rzp = new Razorpay(options);
    rzp.on('payment.failed', (response: any) => {
      this.toast.error(response.error?.description || 'Baggage payment failed');
    });
    rzp.open();
  }

  executeAddBaggage(pnr: string, passengerId: number, weight: number, extraWeight: number, fee: number) {
    this.opsService.addBaggage({
      pnr,
      passengerId,
      weight,
      tagNumber: `BAG-${Date.now()}`
    }).subscribe({
      next: (res: any) => {
        const bag = res.baggage || res;
        const feeMsg = fee > 0 ? ` — Paid ₹${fee} (${extraWeight}kg extra)` : ' — Within free allowance ✅';
        this.toast.success(`Baggage tagged: ${bag.tagNumber || 'OK'}${feeMsg}`);
        this.notifService.baggageAdded(pnr, bag.tagNumber || 'BAG', weight);
        if (!this.baggageByPnr[pnr]) this.baggageByPnr[pnr] = [];
        this.baggageByPnr[pnr].unshift(bag);
        this.newBaggageWeight = null;
        this.cdr.detectChanges();
      },
      error: (err) => this.toast.error(err.error?.error || 'Failed to add baggage')
    });
  }
}
