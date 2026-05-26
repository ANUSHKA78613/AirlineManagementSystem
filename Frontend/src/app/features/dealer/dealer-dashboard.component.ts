import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DealerService, FlightService, BookingService, PricingService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';
import { forkJoin, catchError, of } from 'rxjs';

import { DataTable, TableColumn } from '../../shared/components/data-table/data-table';
import { RazorpayWidget, RazorpayOptions } from '../../shared/components/razorpay-widget/razorpay-widget';
import { gatewayUrl } from '../../core/config/api.config';

@Component({
  selector: 'app-dealer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, RazorpayWidget],
  templateUrl: './dealer-dashboard.component.html',
  styleUrl: './dealer-dashboard.component.css'
})
export class DealerDashboardComponent implements OnInit {
  tab = 'wallet';
  loading = false;

  // Data
  dealer: any = null;
  wallet: any = null;
  flights: any[] = [];
  bookings: any[] = [];
  commissions: any[] = [];
  commissionSummary: any = null;
  reportData: any = { walletBalance: 0, totalRevenue: 0, totalCommission: 0, totalBookings: 0, activeBookings: 0, cancelledBookings: 0, avgBookingValue: 0, maxRevenue: 0, bookingBreakdown: [] };

  showTopUp = false;
  topUpAmount: number | null = null;
  razorpayOptions: RazorpayOptions | null = null;
  processingTopUp = false;

  bulkPassengers: any[] = [{ name: '', age: '', gender: '', seatNo: '', seatClass: 'Economy', passportNumber: '' }];
  selectedFlightId: number | null = null;
  totalBulkCost = 0;
  bookingInProgress = false;
  classPrices: Record<string, number> = {};

  // Seat Map
  seatMap: any[] = [];
  seatRows: number[] = Array.from({ length: 18 }, (_, i) => i + 1);
  seatMapLoading = false;
  selectedDealerSeats: string[] = [];
  occupiedSeats: string[] = [];
  currentAssigningIndex = 0;
  seatLayouts: any[] = [];
  seatBlocks: any[] = [];
  seatsByClass: any = {};

  get allDealerSeatsSelected(): boolean {
    return this.bulkPassengers.every((p: any) => p.seatNo && p.seatNo.length > 0);
  }

  get canExecuteBulkBooking(): boolean {
    if (!this.selectedFlightId || !this.allDealerSeatsSelected) return false;
    return this.bulkPassengers.every((p: any) => p.name && p.age && p.gender);
  }

  flightColumns: TableColumn[] = [
    { field: 'flightNumber', header: 'Flight' },
    { field: '_route', header: 'Route' },
    { field: '_date', header: 'Date' },
    { field: 'price', header: 'Fare' },
  ];

  bookingColumns: TableColumn[] = [
    { field: 'pnr', header: 'PNR' },
    { field: 'flightId', header: 'Flight ID' },
    { field: 'totalAmount', header: 'Total Paid' },
    { field: 'status', header: 'Status' },
  ];

  constructor(
    private dealerService: DealerService,
    private flightService: FlightService,
    private bookingService: BookingService,
    private pricingService: PricingService,
    private http: HttpClient,
    private toast: ToastService,
    public auth: AuthService
  ) { }

  ngOnInit() {
    this.dealerService.getAll().subscribe({
      next: (dealers) => {
        const currentEmail = this.auth.currentUser?.email?.toLowerCase();
        this.dealer =
          dealers.find((dealer: any) => dealer.email?.toLowerCase() === currentEmail) ||
          dealers.find((dealer: any) => dealer.agentId === this.auth.currentUser?.userId) ||
          null;

        if (!this.dealer) {
          this.toast.error('Could not load Agent Data. You might not be registered as an active dealer.');
          return;
        }

        this.loadWallet();
      },
      error: () => this.toast.error('Could not load Agent Data')
    });
  }

  setTab(t: string) {
    this.tab = t;
    if (t === 'wallet') this.loadWallet();
    if (t === 'bulk') this.loadFlights();
    if (t === 'history') this.loadAgentBookings();
    if (t === 'commissions') this.loadCommissions();
    if (t === 'reports') this.loadReports();

  }

  loadWallet() {
    this.loading = true;
    this.dealerService.getWallet(this.dealer?.agentId || 1).subscribe({
      next: (w) => { this.wallet = w; this.loading = false; }
    });
  }

  loadFlights() {
    this.loading = true;
    this.flightService.getAll().subscribe({
      next: (f) => {
        this.flights = f
          .filter((x: any) => new Date(x.departureTime).getTime() > new Date().getTime())
          .map((x: any) => ({
          ...x,
          _route: `${x.source} → ${x.destination}`,
          _date: new Date(x.departureTime).toLocaleDateString()
        }));
        this.loading = false;
      }
    });
  }

  loadAgentBookings() {
    this.loading = true;
    this.bookingService.getMyBookings(this.auth.currentUser?.userId || 1).subscribe({
      next: (b) => { this.bookings = b; this.loading = false; }
    });
  }

  loadCommissions() {
    this.loading = true;
    const agentCode = this.dealer?.agentCode || 'AG-DEFAULT';
    this.dealerService.getCommissions(agentCode).subscribe({
      next: (c) => {
        this.commissions = c;
        this.commissionSummary = {
          total: c.length,
          totalEarned: c.filter((x: any) => !x.isReversed).reduce((s: number, x: any) => s + (x.commissionAmount || 0), 0),
          pending: c.filter((x: any) => x.status === 'Pending' && !x.isReversed).reduce((s: number, x: any) => s + (x.commissionAmount || 0), 0),
          paid: c.filter((x: any) => x.status === 'Paid' && !x.isReversed).reduce((s: number, x: any) => s + (x.commissionAmount || 0), 0)
        };
        this.loading = false;
      },
      error: () => { this.commissions = []; this.loading = false; }
    });
  }

  loadReports() {
    this.loading = true;
    const agentId = this.dealer?.agentId || 1;
    const userId = this.auth.currentUser?.userId || 1;
    const agentCode = this.dealer?.agentCode || 'AG-DEFAULT';

    let walletDone = false, bookingsDone = false, commissionsDone = false;
    let walletBal = 0;
    let agentBookings: any[] = [];
    let agentCommissions: any[] = [];

    const tryFinish = () => {
      if (!walletDone || !bookingsDone || !commissionsDone) return;
      const activeBookings = agentBookings.filter((b: any) => b.status !== 'Cancelled');
      const cancelledBookings = agentBookings.filter((b: any) => b.status === 'Cancelled');
      const totalRevenue = activeBookings.reduce((s: number, b: any) => s + (b.totalAmount || 0), 0);
      const totalCommission = agentCommissions.filter((c: any) => !c.isReversed).reduce((s: number, c: any) => s + (c.commissionAmount || 0), 0);
      const breakdown = agentBookings.map((b: any) => ({ pnr: b.pnr, amount: b.totalAmount || 0, status: b.status }));
      const maxRev = breakdown.length > 0 ? Math.max(...breakdown.map((b: any) => b.amount)) : 0;

      this.reportData = {
        walletBalance: walletBal,
        totalRevenue,
        totalCommission,
        totalBookings: agentBookings.length,
        activeBookings: activeBookings.length,
        cancelledBookings: cancelledBookings.length,
        avgBookingValue: agentBookings.length > 0 ? totalRevenue / activeBookings.length : 0,
        maxRevenue: maxRev,
        bookingBreakdown: breakdown
      };
      this.loading = false;
    };

    this.dealerService.getWallet(agentId).subscribe({
      next: (w) => { walletBal = w?.balance || 0; walletDone = true; tryFinish(); },
      error: () => { walletDone = true; tryFinish(); }
    });

    this.bookingService.getMyBookings(userId).subscribe({
      next: (b) => { agentBookings = b || []; bookingsDone = true; tryFinish(); },
      error: () => { bookingsDone = true; tryFinish(); }
    });

    this.dealerService.getCommissions(agentCode).subscribe({
      next: (c) => { agentCommissions = c || []; commissionsDone = true; tryFinish(); },
      error: () => { commissionsDone = true; tryFinish(); }
    });
  }

  processTopUp() {
    if (!this.topUpAmount || this.topUpAmount <= 0) return;
    this.processingTopUp = true;

    const txId = 'WT-' + Math.random().toString(36).substring(2, 10).toUpperCase();
    this.razorpayOptions = {
      key: 'rzp_test_SUDVzwAKVeUa91',
      amount: this.topUpAmount * 100,
      currency: 'INR',
      name: 'SkyLedger Agent Wallet',
      description: `Wallet TopUp Transaction`,
      prefill: {
        method: 'upi',
        name: this.dealer?.agentName || this.dealer?.agencyName,
        email: this.dealer?.email || this.auth.currentUser?.email
      }
    };
  }

  onTopUpSuccess(response: any) {
    this.dealerService.topUpWallet(this.dealer?.agentId || 1, this.topUpAmount!, `TopUp Via Razorpay (ID: ${response.razorpay_payment_id || 'Mock'})`).subscribe({
      next: () => {
        this.toast.success(`Wallet funded with ₹${this.topUpAmount}`);
        this.closeTopUp();
        this.loadWallet();
      },
      error: () => {
        this.toast.error('Top-up failed to process on ledger');
        this.processingTopUp = false;
        this.razorpayOptions = null;
      }
    });
  }

  onTopUpError() {
    this.toast.error('Payment cancelled or failed');
    this.processingTopUp = false;
    this.razorpayOptions = null;
  }

  closeTopUp() {
    this.showTopUp = false;
    this.processingTopUp = false;
    this.razorpayOptions = null;
    this.topUpAmount = 0;
  }

  // ─── Seat Map Logic ────────────────────────────────────────

  loadSeatMap(flightId: number) {
    this.seatMapLoading = true;
    this.seatMap = [];
    this.selectedDealerSeats = [];
    this.occupiedSeats = [];
    this.currentAssigningIndex = 0;
    // Clear any previously assigned seats
    this.bulkPassengers.forEach((p: any) => p.seatNo = '');

    // Fetch both the inventory seat map AND actual booked seats from the Booking service
    forkJoin({
      seats: this.flightService.getSeatMap(flightId),
      bookedSeats: this.bookingService.getBookedSeats(flightId).pipe(catchError(() => of([] as string[])))
    }).subscribe({
      next: ({ seats, bookedSeats }) => {
        const bookedSet = new Set((bookedSeats || []).map((s: string) => s.toUpperCase()));
        this.occupiedSeats = Array.from(bookedSet);

        // Merge booking data into seat map — mark booked seats as unavailable
        this.seatMap = (seats || []).map((s: any) => {
          const occ = !s.isAvailable || bookedSet.has((s.seatNo || '').toUpperCase());
          if (occ && !this.occupiedSeats.includes((s.seatNo || '').toUpperCase())) {
            this.occupiedSeats.push((s.seatNo || '').toUpperCase());
          }
          return {
            ...s,
            isAvailable: !occ
          };
        });

        // Group by class
        this.seatsByClass = {};
        this.seatMap.forEach((s: any) => {
          const className = s.seatClass || 'Economy';
          if (!this.seatsByClass[className]) this.seatsByClass[className] = [];
          this.seatsByClass[className].push(s);
        });

        this.seatMapLoading = false;
      },
      error: () => {
        this.toast.error('Could not load seat map');
        this.seatMapLoading = false;
      }
    });
  }

  parseLayoutColumnsRet(layoutStr: string) {
    const parts = (layoutStr || '3-3').split('-');
    let maxCols = parts.reduce((a, b) => a + parseInt(b), 0);
    const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('');
    const cols = alphabet.slice(0, maxCols);

    let left: string[] = [];
    let mid: string[] = [];
    let right: string[] = [];

    let currentIdx = 0;
    if (parts.length === 2) {
      const leftCount = parseInt(parts[0]);
      const rightCount = parseInt(parts[1]);
      left = cols.slice(currentIdx, currentIdx + leftCount);
      currentIdx += leftCount;
      right = cols.slice(currentIdx, currentIdx + rightCount);
    } else if (parts.length === 3) {
      const leftCount = parseInt(parts[0]);
      const midCount = parseInt(parts[1]);
      const rightCount = parseInt(parts[2]);

      left = cols.slice(currentIdx, currentIdx + leftCount);
      currentIdx += leftCount;

      mid = cols.slice(currentIdx, currentIdx + midCount);
      currentIdx += midCount;

      right = cols.slice(currentIdx, currentIdx + rightCount);
    } else {
      left = cols;
    }
    return { left, mid, right };
  }

  selectSeatClassTab() {
    this.seatBlocks = [];
    const classOrder = ['First', 'Business', 'Economy'];
    const classMap: Record<string, string> = { 'First': 'First', 'Business': 'Business', 'Economy': 'Economy' };
    let currentRow = 1;

    classOrder.forEach(cls => {
      const layout = this.seatLayouts.find((l: any) => l.seatClass === cls);
      const seatsForClass = this.seatsByClass[cls] || [];

      if (layout && seatsForClass.length > 0) {
        const { left, mid, right } = this.parseLayoutColumnsRet(layout.columnsLayout);
        
        // Extract ACTUAL row numbers from real seat data (e.g., 'E16A' -> 16)
        const rowSet = new Set<number>();
        seatsForClass.forEach((s: any) => {
          const match = (s.seatNo || '').match(/^[A-Z](\d+)/);
          if (match) rowSet.add(parseInt(match[1], 10));
        });
        const rows = Array.from(rowSet).sort((a, b) => a - b);

        this.seatBlocks.push({
          seatClass: cls,
          internalClass: classMap[cls],
          rows: rows,
          leftCols: left,
          midCols: mid,
          rightCols: right
        });
      } else if (layout) {
        // Seats not loaded yet — use layout row count as fallback
        const { left, mid, right } = this.parseLayoutColumnsRet(layout.columnsLayout);
        const rows = Array.from({ length: layout.totalRows }, (_, i) => currentRow + i);
        currentRow += layout.totalRows;
        this.seatBlocks.push({
          seatClass: cls,
          internalClass: classMap[cls],
          rows: rows,
          leftCols: left,
          midCols: mid,
          rightCols: right
        });
      }
    });

    if (this.seatBlocks.length === 0) {
      // Fallback matching passenger portal
      this.seatBlocks.push({
        seatClass: 'First',
        internalClass: 'First',
        rows: [1, 2, 3, 4],
        leftCols: ['A', 'B'], midCols: [], rightCols: ['C', 'D']
      });
      this.seatBlocks.push({
        seatClass: 'Business',
        internalClass: 'Business',
        rows: [5, 6, 7, 8],
        leftCols: ['A', 'B'], midCols: [], rightCols: ['E', 'F']
      });
      this.seatBlocks.push({
        seatClass: 'Economy',
        internalClass: 'Economy',
        rows: Array.from({length: 10}, (_, i) => 9 + i),
        leftCols: ['A', 'B', 'C'], midCols: [], rightCols: ['D', 'E', 'F']
      });
    }
  }

  getSeatInfo(seatNo: string): any {
    for (const cls of Object.keys(this.seatsByClass || {})) {
      const seat = this.seatsByClass[cls]?.find((s: any) => s.seatNo === seatNo);
      if (seat) return seat;
    }
    return null;
  }

  isWindowSeat(row: number, col: string, seatClass: string): boolean {
    const seatNo = this.getFullDealerSeatNo(row, col, seatClass);
    const info = this.getSeatInfo(seatNo);
    return info?.category === 'Window';
  }

  getFullDealerSeatNo(row: number, col: string, seatClass: string): string {
    const prefix = seatClass.charAt(0).toUpperCase();
    return `${prefix}${row}${col}`;
  }

  getDealerSeatStatus(seatNo: string): 'available' | 'selected' | 'occupied' {
    // Highly reliable override from Booking and Inventory populated list
    if (this.occupiedSeats.includes(seatNo.toUpperCase())) return 'occupied';

    // Check if selected by dealer
    if (this.selectedDealerSeats && this.selectedDealerSeats.includes(seatNo)) return 'selected';

    // Check from seat map fallback
    const seat = (this.seatMap || []).find(s => s.seatNo === seatNo);
    if (seat && !seat.isAvailable) return 'occupied';
    if (seat && seat.isBlocked) return 'occupied';
    return 'available';
  }

  getDealerSeatBorder(seatNo: string, row: number, seatClass: string = 'Economy'): string {
    const status = this.getDealerSeatStatus(seatNo);
    if (status === 'selected') return '#f2ca50';
    if (status === 'occupied') return 'rgba(255,77,79,0.3)';
    if (seatClass === 'First') return 'rgba(212,175,55,0.25)';
    if (seatClass === 'Business') return 'rgba(24,144,255,0.25)';
    return 'rgba(255,255,255,0.1)';
  }

  getDealerSeatBg(seatNo: string, row: number, seatClass: string = 'Economy'): string {
    const status = this.getDealerSeatStatus(seatNo);
    if (status === 'selected') return 'rgba(242,202,80,0.2)';
    if (status === 'occupied') return 'rgba(255,77,79,0.08)';
    if (seatClass === 'First') return 'rgba(212,175,55,0.08)';
    if (seatClass === 'Business') return 'rgba(24,144,255,0.08)';
    return 'rgba(255,255,255,0.04)';
  }

  getDealerSeatColor(seatNo: string, row: number, seatClass: string = 'Economy'): string {
    const status = this.getDealerSeatStatus(seatNo);
    if (status === 'selected') return '#f2ca50';
    if (status === 'occupied') return '#ff4d4f';
    if (seatClass === 'First') return '#d4af37';
    if (seatClass === 'Business') return '#1890ff';
    return '#888';
  }

  toggleDealerSeat(seatNo: string, seatClass: string) {
    const status = this.getDealerSeatStatus(seatNo);
    if (status === 'occupied') return;

    if (status === 'selected') {
      // Deselect: remove from passenger and array
      this.selectedDealerSeats = this.selectedDealerSeats.filter(s => s !== seatNo);
      const pIdx = this.bulkPassengers.findIndex((p: any) => p.seatNo === seatNo);
      if (pIdx !== -1) {
        this.bulkPassengers[pIdx].seatNo = '';
        this.bulkPassengers[pIdx].seatClass = 'Economy';
      }
      return;
    }

    // Find next unassigned passenger
    const unassignedIdx = this.bulkPassengers.findIndex((p: any) => !p.seatNo || p.seatNo === '');
    if (unassignedIdx === -1) {
      this.toast.warning('All passengers already have seats assigned');
      return;
    }

    // Check seat count limit  
    if (this.selectedDealerSeats.length >= this.bulkPassengers.length) {
      this.toast.warning('All passengers already have seats assigned');
      return;
    }

    this.selectedDealerSeats.push(seatNo);
    this.bulkPassengers[unassignedIdx].seatNo = seatNo;

    // Auto-set seat class based on blocks
    const block = this.seatBlocks.find(b => b.seatClass === seatClass);
    if (block) {
      this.bulkPassengers[unassignedIdx].seatClass = block.internalClass;
    } else {
      this.bulkPassengers[unassignedIdx].seatClass = 'Economy';
    }

    this.recalculateBulkCost();
  }

  // ─── Passenger Roster ──────────────────────────────────────

  addPassengerRow() {
    this.bulkPassengers.push({ name: '', age: '', gender: '', seatNo: '', seatClass: 'Economy', passportNumber: '' });
    this.recalculateBulkCost();
  }

  removePassengerRow(index: number) {
    const removed = this.bulkPassengers[index];
    if (removed.seatNo) {
      this.selectedDealerSeats = this.selectedDealerSeats.filter(s => s !== removed.seatNo);
    }
    this.bulkPassengers.splice(index, 1);
    this.recalculateBulkCost();
  }

  selectFlight(flightId: number) {
    this.selectedFlightId = flightId;
    this.loadSeatMap(flightId);

    // Fetch prices for classes and layouts
    forkJoin({
      eco: this.pricingService.calculate(flightId, 'Economy').pipe(catchError(() => of(null))),
      bus: this.pricingService.calculate(flightId, 'Business').pipe(catchError(() => of(null))),
      first: this.pricingService.calculate(flightId, 'First').pipe(catchError(() => of(null))),
      layouts: this.flightService.getSeatLayout(flightId).pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ eco, bus, first, layouts }) => {
        if (eco) this.classPrices['Economy'] = eco.finalPrice;
        if (bus) this.classPrices['Business'] = bus.finalPrice;
        if (first) this.classPrices['First'] = first.finalPrice;

        this.seatLayouts = layouts;
        this.selectSeatClassTab();
        this.recalculateBulkCost();
      }
    });
  }

  recalculateBulkCost() {
    if (!this.selectedFlightId) { this.totalBulkCost = 0; return; }
    const flight = this.flights.find(f => f.flightId === this.selectedFlightId);
    if (!flight) return;

    // Calculate cost per passenger based on seat class
    let totalRaw = 0;
    const fallbackBasePrice = flight.price || flight.basePrice || 0;
    for (const p of this.bulkPassengers) {
      const ticketPrice = this.classPrices[p.seatClass || 'Economy'] || fallbackBasePrice;
      totalRaw += ticketPrice;
    }

    const netCost = totalRaw - (totalRaw * (this.dealer?.commissionRate || 0.05));
    this.totalBulkCost = netCost;
  }

  executeBulkBooking() {
    if (this.bookingInProgress) return;
    if (!this.canExecuteBulkBooking) {
      this.toast.error('Please complete all passenger details including Gender and Age.');
      return;
    }

    if (!this.allDealerSeatsSelected) {
      this.toast.error('Please select seats for all passengers');
      return;
    }

    if (this.totalBulkCost > (this.wallet?.balance || 0)) {
      const shortfall = this.totalBulkCost - (this.wallet?.balance || 0);
      const wantsToPay = confirm(`Insufficient Wallet Balance. You are short by ₹${Math.ceil(shortfall)}. Do you want to top-up via Razorpay to proceed?`);
      if (wantsToPay) {
        this.toast.info('Redirecting to Payment Gateway...');
        this.topUpAmount = Math.ceil(shortfall);
        this.setTab('wallet');
        this.showTopUp = true;
        this.processTopUp();
      }
      return;
    }

    this.bookingInProgress = true;
    const bookingRequest = {
      userId: this.auth.currentUser?.userId,
      flightId: this.selectedFlightId,
      totalAmount: Math.round(this.totalBulkCost * 100) / 100,
      passengers: this.bulkPassengers.map((p: any) => ({
        name: (p.name || '').trim(),
        age: parseInt(p.age, 10) || 0,
        gender: p.gender,
        seatNo: p.seatNo || '',
        seatClass: p.seatClass || 'Economy'
      })),
      email: this.dealer?.email || this.auth.currentUser?.email,
      userName: this.dealer?.agentName || this.auth.currentUser?.name
    };

    this.bookingService.create(bookingRequest).subscribe({
      next: (res) => {
        // Immediately reserve seats via Inventory API (synchronous backup to event bus)
        this.bulkPassengers.forEach((p: any) => {
          if (p.seatNo && this.selectedFlightId) {
            this.flightService.reserveSeat(this.selectedFlightId, p.seatNo).subscribe();
          }
        });

        this.dealerService.debitWallet(this.dealer?.agentId || 1, this.totalBulkCost, `Bulk Booking PNR: ${res.pnr}`).subscribe({
          next: () => {
            // Confirm the booking now that wallet payment is complete
            this.bookingService.confirm(res.pnr).subscribe({
              next: () => {
                // Record commission for this bulk booking (wallet-debited, no Razorpay event)
                this.dealerService.recordSelfCommission({
                  agentId: this.dealer?.agentId || 0,
                  agentCode: this.dealer?.agentCode || '',
                  bookingAmount: this.totalBulkCost,
                  pnr: res.pnr
                }).subscribe({ error: () => { } }); // fire-and-forget, don't block UX

                this.toast.success(`Booking confirmed! PNR: ${res.pnr}`);
                if (this.wallet) this.wallet.balance -= this.totalBulkCost;
                this.bulkPassengers = [{ name: '', age: '', gender: '', seatNo: '', seatClass: 'Economy', passportNumber: '' }];
                this.selectedFlightId = null;
                this.totalBulkCost = 0;
                this.seatMap = [];
                this.selectedDealerSeats = [];
                this.bookingInProgress = false;
                this.setTab('history');
              },
              error: () => {
                // Wallet debited but confirm failed — still proceed but warn
                this.toast.warning(`Booking ${res.pnr} created & paid but confirmation sync delayed.`);
                if (this.wallet) this.wallet.balance -= this.totalBulkCost;
                this.bookingInProgress = false;
                this.setTab('history');
              }
            });
          },
          error: () => {
            this.bookingInProgress = false;
            this.toast.warning(`Booking ${res.pnr} created but wallet debit sync failed.`);
          }
        });
      },
      error: () => {
        this.bookingInProgress = false;
        this.toast.error('Booking failed');
      }
    });
  }

  cancelBooking(pnr: string) {
    if (!confirm(`Cancel booking ${pnr}? A refund will be processed.`)) return;
    this.bookingService.cancel(pnr).subscribe({
      next: () => {
        this.toast.success('Booking cancelled — seats released');
        const b = this.bookings.find(x => x.pnr === pnr);
        if (b) {
          b.status = 'Cancelled';
          if (this.wallet && b.totalAmount) {
            const refundAmount = b.totalAmount * 0.9;
            this.dealerService.topUpWallet(this.dealer?.agentId || 1, refundAmount, `Refund for PNR ${pnr}`).subscribe({
              next: () => {
                this.wallet!.balance += refundAmount;
                this.toast.info(`₹${Math.ceil(refundAmount)} credited back to dealer wallet`);
                this.loadWallet();
              }
            });
          }
        }
      },
      error: (err) => this.toast.error(err?.error?.error || 'Cancel failed')
    });
  }

}
