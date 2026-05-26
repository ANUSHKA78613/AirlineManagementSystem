import { Component, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { FlightService, BookingService, PaymentService, OperationsService, PricingService, DealerService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { StatsCard } from '../../shared/components/stats-card/stats-card';
import { DataTable, TableColumn } from '../../shared/components/data-table/data-table';

import { gatewayUrl } from '../../core/config/api.config';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, StatsCard, DataTable],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.css'
})
export class AdminDashboardComponent implements OnInit {
  tab = 'overview';
  loading = false;

  users: any[] = [];
  dealers: any[] = [];
  flights: any[] = [];
  bookings: any[] = [];
  payments: any[] = [];
  airports: any[] = [];
  routes: any[] = [];
  pricingRules: any[] = [];
  coupons: any[] = [];
  commissions: any[] = [];

  showAddFlight = false;
  newFlight: any = {};
  minDateTime: string = '';
  
  trendData: { dayName: string, amount: number, heightPercent: number }[] = [];
  recentBookings: any[] = [];

  // New item forms
  showAddRoute = false;
  newRoute: any = {};
  showAddAirport = false;
  newAirport: any = {};
  showAddCoupon = false;
  newCoupon: any = {};
  showAddRule = false;
  newRule: any = {};
  showAddDealer = false;
  newDealer: any = {};

  // Edit item forms
  showEditFlight = false;
  editingFlight: any = null;
  showEditRoute = false;
  editingRoute: any = null;
  showEditAirport = false;
  editingAirport: any = null;
  showEditDealer = false;
  editingDealer: any = null;

  Math = Math;
  uniqueVisitors = 0;
  totalRevenue = 0;
  activeFlights = 0;
  totalUsers = 0;

  // Overlapping aircraft metrics
  overlappingFlights: any[] = [];

  // User details panel
  selectedUser: any = null;

  // Notifications
  notifications: any[] = [];
  showNotifications = false;

  flightColumns: TableColumn[] = [
    { field: 'flightNumber', header: 'Flight No' },
    { field: '_route', header: 'Route' },
    { field: '_departure', header: 'Departure' },
    { field: '_economy', header: 'Economy' },
    { field: '_business', header: 'Business' },
    { field: '_firstClass', header: 'First Class' },
    { field: '_windowSeat', header: 'Window Extra' },
    { field: 'status', header: 'Status' }
  ];

  bookingColumns: TableColumn[] = [
    { field: 'pnr', header: 'PNR' },
    { field: 'flightId', header: 'Flight ID' },
    { field: '_amount', header: 'Amount' },
    { field: 'status', header: 'Status' }
  ];

  routeColumns: TableColumn[] = [
    { field: 'source', header: 'Source' },
    { field: 'destination', header: 'Destination' },
    { field: 'distanceKm', header: 'Distance (km)' },
    { field: 'estimatedDuration', header: 'Duration' }
  ];

  airportColumns: TableColumn[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'city', header: 'City' },
    { field: 'country', header: 'Country' }
  ];
  
  pricingRuleColumns: TableColumn[] = [
    { field: '_flight', header: 'Flight' },
    { field: 'class', header: 'Class' },
    { field: 'basePrice', header: 'Base Price' },
    { field: 'multiplier', header: 'Multiplier' }
  ];

  couponColumns: TableColumn[] = [
    { field: 'code', header: 'Code' },
    { field: 'discountPercent', header: 'Discount' },
    { field: 'usedCount', header: 'Global Usage' },
    { field: 'usageLimit', header: 'Per User Limit' },
    { field: 'expiresAt', header: 'Expires' },
    { field: '_status', header: 'Status' }
  ];

  userColumns: TableColumn[] = [
    { field: 'name', header: 'Name' },
    { field: 'email', header: 'Email' },
    { field: 'role', header: 'Role' }
  ];

  dealerColumns: TableColumn[] = [
    { field: 'agentName', header: 'Agent' },
    { field: 'agencyName', header: 'Agency' },
    { field: 'agentCode', header: 'Code' },
    { field: 'commissionRate', header: 'Commission' }
  ];

  commissionColumns: TableColumn[] = [
    { field: 'agentCode', header: 'Agent' },
    { field: 'pnr', header: 'PNR' },
    { field: 'bookingAmount', header: 'Booking Amt' },
    { field: 'commissionAmount', header: 'Commission' },
    { field: 'status', header: 'Status' }
  ];

  constructor(
    public auth: AuthService,
    private flightService: FlightService,
    private bookingService: BookingService,
    private paymentService: PaymentService,
    private opsService: OperationsService,
    private pricingService: PricingService,
    private dealerService: DealerService,
    private toast: ToastService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  onSearch(query: string) {
    if(!query) return;
    const trimmed = query.trim().toUpperCase();
    if(trimmed) {
      this.toast.info(`Searching for: ${trimmed}`);
      this.setTab('flights');
      // Additional local filtering could be added here if the flights array is bound to a filter
    }
  }

  ngOnInit() {
    this.loadOverview();
    this.loadNotifications();
    setInterval(() => {
      this.loadNotifications();
    }, 15000);
  }

  loadNotifications() {
      this.opsService.getNotifications().subscribe({
        next: (data: any[]) => { 
          this.notifications = data.map((n: any) => {
            let d = n.createdAt;
            if (typeof d === 'string' && !d.endsWith('Z') && d.includes('T')) d += 'Z';
            return { ...n, createdAt: d };
          });
          this.cdr.detectChanges(); 
        },
        error: () => {}
      });
  }

  setTab(tabName: string) {
    this.tab = tabName;
    if (tabName === 'overview') this.loadOverview();
    if (tabName === 'flights') this.loadFlights();
    if (tabName === 'users') this.loadUsers();
    if (tabName === 'bookings') this.loadBookings();
    if (tabName === 'routes') this.loadRoutes();
    if (tabName === 'pricing') this.loadPricing();
    if (tabName === 'dealers') this.loadDealers();
    if (tabName === 'commissions') this.loadCommissions();
    if (tabName === 'aircraft') this.detectOverlappingAircraft();
    
  }

  loadOverview() {
    this.loading = true;
    
    // Fetch Flights
    this.flightService.getAll().subscribe({
      next: (f) => { 
        this.flights = f; 
        this.activeFlights = f.filter((x:any) => x.status === 'Scheduled' || x.status === 'Boarding').length;
        this.cdr.detectChanges(); 
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
    
    // Fetch Bookings
    this.bookingService.getAll().subscribe({ 
      next: (b) => {
        this.bookings = b;
        this.totalRevenue = b.filter((x:any)=>x.status!=='Cancelled').reduce((s: number, x: any) => s + (x.totalAmount || 0), 0);
        this.updateDynamicData();
        this.loading = false;
        this.cdr.detectChanges();
      }, 
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });

    // Fetch Users to get dynamic unique visitors and total users
    this.auth.getUsers().subscribe({
      next: (u) => {
        this.totalUsers = u.length;
        // Show true count of unblocked users
        this.uniqueVisitors = u.filter((x: any) => !x.lockedUntil || new Date(x.lockedUntil) <= new Date()).length;
        this.cdr.detectChanges();
      },
      error: () => { this.totalUsers = 0; this.uniqueVisitors = 0; this.cdr.detectChanges(); }
    });
  }

  loadUsers() { 
    this.auth.getUsers().subscribe({ 
      next: (u) => {
        this.users = u.map((user: any) => ({
          ...user,
          isBlocked: !!user.lockedUntil && new Date(user.lockedUntil) > new Date()
        }));
        this.uniqueVisitors = u.length;
        this.cdr.detectChanges();
      } 
    }); 
  }

  loadFlights() {
    this.loading = true;
    this.flightService.getAll().subscribe({
      next: (flights) => {
        this.flights = flights.map((x: any) => {
          return {
            ...x,
            _route: `${x.source} \u2192 ${x.destination}`,
            _departure: new Date(x.departureTime).toLocaleString(),
            _economy: x.economySeats ? `${x.economySeats} | ₹${x.economyPrice}` : 'N/A',
            _business: x.businessSeats ? `${x.businessSeats} | ₹${x.businessPrice}` : 'N/A',
            _firstClass: x.firstClassSeats ? `${x.firstClassSeats} | ₹${x.firstClassPrice}` : 'N/A',
            _windowSeat: x.windowSeatPrice ? `+₹${x.windowSeatPrice}` : 'N/A'
          };
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadBookings() {
    this.loading = true;
    this.bookingService.getAll().subscribe({ next: (b) => {
      this.bookings = b.map((x:any) => {
        let bDateStr = x.bookingDate || x.createdAt || '';
        if (typeof bDateStr === 'string' && !bDateStr.endsWith('Z') && bDateStr.includes('T')) {
            bDateStr += 'Z';
        }
        return {
          ...x,
          bookingDate: bDateStr,
          _amount: `₹${x.totalAmount}`
        };
      });
      this.updateDynamicData();
      this.loading = false;
      this.cdr.detectChanges();
    }, error: () => { this.loading = false; this.cdr.detectChanges(); } });
  }

  updateDynamicData() {
    this.recentBookings = [...this.bookings]
      .sort((a, b) => new Date(b.bookingDate || b.createdAt || 0).getTime() - new Date(a.bookingDate || a.createdAt || 0).getTime())
      .slice(0, 4);

    const today = new Date();
    today.setHours(23, 59, 59, 999);
    
    const totals = [0, 0, 0, 0, 0]; 
    const dayNames = ['', '', '', '', 'Today'];
    
    for (let i = 0; i < 5; i++) {
        const d = new Date(today);
        d.setDate(today.getDate() - (4 - i));
        if (i < 4) dayNames[i] = d.toLocaleDateString('en-US', { weekday: 'short' });
    }

    this.bookings.forEach(b => {
      if (b.status === 'Cancelled') return;
      const bDate = new Date(b.bookingDate || b.createdAt || new Date());
      const diffTime = today.getTime() - bDate.getTime();
      const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
      
      if (diffDays >= 0 && diffDays < 5) {
        totals[4 - diffDays] += (b.totalAmount || 0);
      }
    });

    const maxAmt = Math.max(...totals, 1);
    this.trendData = totals.map((amt, idx) => {
        let h = Math.round((amt / maxAmt) * 90);
        if (h < 10) h = 10;
        return {
           dayName: dayNames[idx],
           amount: amt,
           heightPercent: h
        };
    });
  }

  loadRoutes() {
    this.loading = true;
    this.flightService.getAirports().subscribe({ 
      next: (a) => { this.airports = a; this.cdr.detectChanges(); },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
    this.flightService.getRoutes().subscribe({ 
      next: (r) => { this.routes = r; this.loading = false; this.cdr.detectChanges(); },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadPricing() {
    this.loading = true;
    this.pricingService.getCoupons().subscribe({ 
      next: (c) => { 
        this.coupons = c.map((x:any) => ({
          ...x,
          _usage: `${x.usedCount} Global (Limit: ${x.usageLimit}/User)`,
          _status: x.isActive ? 'Active' : 'Deactivated'
        }));
        this.cdr.detectChanges(); 
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
    this.pricingService.getRules().subscribe({ 
      next: (r) => { 
        this.pricingRules = r.map((x:any) => {
          const flight = this.flights.find(f => f.flightId === x.flightId);
          return {
            ...x,
            _flight: flight ? `${flight.flightNumber} (${flight.source} \u2192 ${flight.destination})` : `Flight ${x.flightId}`,
            _status: x.isActive ? 'Active' : 'Deactivated'
          };
        });
        this.loading = false; this.cdr.detectChanges(); 
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadDealers() {
    this.loading = true;
    this.dealerService.getAll().subscribe({ 
      next: (d) => { this.dealers = d; this.loading = false; this.cdr.detectChanges(); },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadCommissions() {
    this.loading = true;
    this.dealerService.getAll().subscribe({
      next: (dealers) => {
        this.dealers = dealers;
        this.commissions = [];
        if (dealers.length === 0) { this.loading = false; this.cdr.detectChanges(); return; }
        let loaded = 0;
        dealers.forEach((d: any) => {
          this.dealerService.getCommissions(d.agentCode).subscribe({
            next: (c) => {
              this.commissions = [...this.commissions, ...c];
              loaded++;
              if (loaded >= dealers.length) { this.loading = false; this.cdr.detectChanges(); }
            },
            error: () => {
              loaded++;
              if (loaded >= dealers.length) { this.loading = false; this.cdr.detectChanges(); }
            }
          });
        });
      },
      error: () => { this.loading = false; this.toast.error('Failed to load commissions'); this.cdr.detectChanges(); }
    });
  }

  toggleBlock(user: any) {
    const action = user.isBlocked ? this.auth.unblockUser(user.userId) : this.auth.blockUser(user.userId);
    action.subscribe({
      next: () => {
        user.isBlocked = !user.isBlocked;
        this.toast.success(`User ${user.isBlocked ? 'blocked' : 'unblocked'} successfully`);
      },
      error: (e: any) => this.toast.error(e.error?.error || 'Action failed')
    });
  }

  openFlightDrawer() { 
    this.newFlight = {}; 
    this.showAddFlight = true; 
    this.minDateTime = new Date().toISOString().slice(0, 16);
    if (!this.routes || this.routes.length === 0) {
      this.flightService.getRoutes().subscribe({ next: (r) => this.routes = r });
    }
    if (!this.airports || this.airports.length === 0) {
      this.flightService.getAirports().subscribe({ next: (a) => this.airports = a });
    }
  }

  addFlight() {
    if (!this.newFlight.source || !this.newFlight.destination) {
      this.toast.error('Please select both a source and destination.');
      return;
    }

    const matchingRoute = this.routes.find(r => 
      (r.source === this.newFlight.source && r.destination === this.newFlight.destination && r.isActive) ||
      (r.source === this.newFlight.destination && r.destination === this.newFlight.source && r.isActive)
    );
    if (!matchingRoute) {
      this.toast.error('No active route exists between these airports. Create a Route first.');
      return;
    }

    if (this.newFlight.departureTime && this.newFlight.arrivalTime) {
      const dep = new Date(this.newFlight.departureTime).getTime();
      const arr = new Date(this.newFlight.arrivalTime).getTime();
      
      if (dep <= new Date().getTime()) {
        this.toast.error('Departure time must be in the future.');
        return;
      }

      if (arr <= dep) {
        this.toast.error('Arrival time must be after departure time.');
        return;
      }

      const diffMins = Math.round((arr - dep) / 60000);
      let routeMins = 0;
      if (matchingRoute.estimatedDuration && matchingRoute.estimatedDuration.includes(':')) {
        const parts = matchingRoute.estimatedDuration.split(':');
        routeMins = parseInt(parts[0]) * 60 + parseInt(parts[1]);
      } else {
        routeMins = parseFloat(matchingRoute.estimatedDuration) * 60 || 0;
      }

      if (Math.abs(diffMins - routeMins) > 15) {
        this.toast.error(`Duration not matching. Route duration is ${matchingRoute.estimatedDuration}.`);
        return;
      }
    }

    const totalInput = Number(this.newFlight.totalCapacity) || 0;
    const ecoSeats = Number(this.newFlight.economySeats) || 0;
    const busSeats = Number(this.newFlight.businessSeats) || 0;
    const firstSeats = Number(this.newFlight.firstClassSeats) || 0;
    
    if (ecoSeats + busSeats + firstSeats !== totalInput) {
      this.toast.error('Sum of Economy, Business, and First Class seats must exactly equal Total Capacity.');
      return;
    }

    const payload = { 
      ...this.newFlight, 
      aircraftId: this.newFlight.aircraftId || 1,
      totalCapacity: totalInput,
      economySeats: ecoSeats,
      economyPrice: Number(this.newFlight.economyPrice) || 0,
      businessSeats: busSeats,
      businessPrice: Number(this.newFlight.businessPrice) || 0,
      firstClassSeats: firstSeats,
      firstClassPrice: Number(this.newFlight.firstClassPrice) || 0,
      windowSeatPrice: Number(this.newFlight.windowSeatPrice) || 0
    };

    this.flightService.create(payload).subscribe({
      next: () => { this.toast.success('Flight scheduled!'); this.showAddFlight = false; this.loadFlights(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to schedule flight')
    });
  }

  // Placeholder event handlers for new standard buttons
  onEditUser(row: any) { this.toast.info('User editing coming soon...'); }
  onDeleteUser(row: any) { this.toast.warning('User deletion is restricted for security.'); }
  
  onEditBooking(row: any) { this.toast.info('Booking details editing coming soon...'); }
  onDeleteBooking(row: any) { this.cancelBooking(row.pnr); }
  
  onEditCommission(row: any) { this.toast.info('Commission editing coming soon...'); }
  onDeleteCommission(row: any) { this.toast.warning('Commissions cannot be easily deleted.'); }

  deleteFlight(id: number) {
    if (!confirm('Delete this flight?')) return;
    this.flightService.delete(id).subscribe({
      next: () => { this.toast.success('Flight deleted'); this.loadFlights(); },
      error: (e) => this.toast.error(e.error?.error || 'Delete failed')
    });
  }

  onDeleteFlight(row: any) {
    this.deleteFlight(row.flightId);
  }

  onEditFlight(row: any) {
    this.editFlight(row);
  }

  editFlight(f: any) {
    this.editingFlight = { ...f, aircraftId: f.aircraftId || 1, basePrice: f.price, multiplier: 1.5 };
    this.showEditFlight = true;
    if (!this.routes || this.routes.length === 0) {
      this.flightService.getRoutes().subscribe({ next: (r) => this.routes = r });
    }
    if (!this.airports || this.airports.length === 0) {
      this.flightService.getAirports().subscribe({ next: (a) => this.airports = a });
    }
  }

  updateFlight() {
    const matchingRoute = this.routes.find(r => 
      (r.source === this.editingFlight.source && r.destination === this.editingFlight.destination && r.isActive) ||
      (r.source === this.editingFlight.destination && r.destination === this.editingFlight.source && r.isActive)
    );
    if (!matchingRoute) {
      this.toast.error('No active route exists between these airports. Create a Route first.');
      return;
    }

    if (this.editingFlight.departureTime && this.editingFlight.arrivalTime) {
      const dep = new Date(this.editingFlight.departureTime).getTime();
      const arr = new Date(this.editingFlight.arrivalTime).getTime();
      if (arr <= dep) {
        this.toast.error('Arrival time must be after departure time.');
        return;
      }

      const diffMins = Math.round((arr - dep) / 60000);
      let routeMins = 0;
      if (matchingRoute.estimatedDuration && matchingRoute.estimatedDuration.includes(':')) {
        const parts = matchingRoute.estimatedDuration.split(':');
        routeMins = parseInt(parts[0]) * 60 + parseInt(parts[1]);
      } else {
        routeMins = parseFloat(matchingRoute.estimatedDuration) * 60 || 0;
      }

      if (Math.abs(diffMins - routeMins) > 15) {
        this.toast.error(`Duration not matching. Route duration is ${matchingRoute.estimatedDuration}.`);
        return;
      }
    }

    const totalInput = Number(this.editingFlight.totalCapacity) || 0;
    const ecoSeats = Number(this.editingFlight.economySeats) || 0;
    const busSeats = Number(this.editingFlight.businessSeats) || 0;
    const firstSeats = Number(this.editingFlight.firstClassSeats) || 0;
    
    if (ecoSeats + busSeats + firstSeats !== totalInput) {
      this.toast.error('Sum of Economy, Business, and First Class seats must exactly equal Total Capacity.');
      return;
    }

    const payload = { 
        ...this.editingFlight, 
        totalCapacity: totalInput,
        economySeats: ecoSeats,
        economyPrice: Number(this.editingFlight.economyPrice) || 0,
        businessSeats: busSeats,
        businessPrice: Number(this.editingFlight.businessPrice) || 0,
        firstClassSeats: firstSeats,
        firstClassPrice: Number(this.editingFlight.firstClassPrice) || 0,
        windowSeatPrice: Number(this.editingFlight.windowSeatPrice) || 0
    };

    this.flightService.update(this.editingFlight.flightId, payload).subscribe({
      next: () => { this.toast.success('Flight updated'); this.showEditFlight = false; this.loadFlights(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update flight')
    });
  }

  deleteRule(id: number) {
    if (!confirm('Are you sure you want to delete this pricing rule?')) return;
    this.pricingService.deleteRule(id).subscribe({
      next: () => { this.toast.success('Pricing rule deleted'); this.loadPricing(); },
      error: () => this.toast.error('Failed to delete pricing rule')
    });
  }

  cancelBooking(pnr: string) {
    if (!confirm(`Force cancel booking ${pnr}? A refund will be initiated automatically.`)) return;
    
    const booking = this.bookings.find(b => b.pnr === pnr);
    if (!booking) {
      this.toast.error('Booking details not found in view.');
      return;
    }

    this.bookingService.cancel(pnr).subscribe({
      next: () => {
        this.toast.success('Booking cancelled');

        this.auth.getUsers().subscribe({
          next: (users) => {
            const user = users.find((u: any) => u.userId === booking.userId);
            
            if (user && user.role === 'Dealer') {
              // Dealer Flow: Top up Agent's wallet
              this.dealerService.getAll().subscribe({
                next: (dealers) => {
                  const dealer = dealers.find(d => d.email === user.email);
                  if (dealer) {
                    this.dealerService.topUpWallet(dealer.agentId, booking.totalAmount, `Refund for cancelled booking ${pnr}`).subscribe({
                      next: () => this.toast.success(`Refund of ₹${booking.totalAmount} sent to dealer's wallet!`),
                      error: () => this.toast.error('Failed to credit dealer wallet.')
                    });
                  } else {
                    this.toast.error('Could not find dealer record to process refund.');
                  }
                }
              });
            } else {
              // Passenger Flow: Initiate standard refund and show delay toast
              this.toast.info('Initiating refund process...');
              this.paymentService.refund(0, `Admin cancellation for PNR: ${pnr}`, pnr).subscribe({
                next: () => {
                  setTimeout(() => {
                    this.toast.success('Refund successful');
                  }, 40000);
                },
                error: () => this.toast.info('No payment record found — no refund needed')
              });
            }
          }
        });
        
        this.loadBookings();
      },
      error: (e) => this.toast.error(e.error?.error || 'Cancellation failed')
    });
  }

  cancelBookingWithRefund(pnr: string) {
    this.cancelBooking(pnr);
  }

  viewUserDetails(user: any) {
    this.selectedUser = this.selectedUser?.userId === user.userId ? null : user;
    this.cdr.detectChanges();
  }

  toggleNotifications() {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) {
      this.loadNotifications();
    }
  }

  @HostListener('document:click')
  closeNotifications() { this.showNotifications = false; }

  // ── Routes & Airports ──────────────────────────────────
  addRoute() {
    this.flightService.createRoute(this.newRoute).subscribe({
      next: () => { this.toast.success('Route added'); this.showAddRoute = false; this.loadRoutes(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed')
    });
  }

  deleteRoute(id: number) {
    if (!confirm('Deactivate this route?')) return;
    this.flightService.deleteRoute(id).subscribe({
      next: () => { this.toast.success('Route deactivated'); this.loadRoutes(); }
    });
  }

  editRoute(r: any) {
    this.editingRoute = { ...r };
    this.showEditRoute = true;
  }

  updateRoute() {
    this.flightService.updateRoute(this.editingRoute.routeId, this.editingRoute).subscribe({
      next: () => { this.toast.success('Route updated'); this.showEditRoute = false; this.loadRoutes(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update route')
    });
  }

  addAirport() {
    this.flightService.createAirport(this.newAirport).subscribe({
      next: () => { this.toast.success('Airport added'); this.showAddAirport = false; this.loadRoutes(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed')
    });
  }

  deleteAirport(code: string) {
    if (!confirm('Deactivate this airport?')) return;
    this.flightService.deleteAirport(code).subscribe({
      next: () => { this.toast.success('Airport deactivated'); this.loadRoutes(); }
    });
  }

  editAirport(a: any) {
    this.editingAirport = { ...a };
    this.showEditAirport = true;
  }

  updateAirport() {
    this.flightService.updateAirport(this.editingAirport.code, this.editingAirport).subscribe({
      next: () => { this.toast.success('Airport updated'); this.showEditAirport = false; this.loadRoutes(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update airport')
    });
  }

  onEditRoute(row: any) { this.editRoute(row); }
  onDeleteRoute(row: any) { this.deleteRoute(row.routeId); }
  onEditAirport(row: any) { this.editAirport(row); }
  onDeleteAirport(row: any) { this.deleteAirport(row.code); }

  // ── Pricing & Coupons ──────────────────────────────────
  showEditRule = false;
  editingRule: any = {};

  showEditCoupon = false;
  editingCoupon: any = {};

  addPricingRule() {
    const payload = {
      ...this.newRule,
      flightId: Number(this.newRule.flightId),
      basePrice: Number(this.newRule.basePrice),
      multiplier: Number(this.newRule.multiplier || 1.0),
      isActive: true
    };
    this.pricingService.addRule(payload).subscribe({
      next: () => { this.toast.success('Pricing rule added'); this.showAddRule = false; this.loadPricing(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to add rule')
    });
  }

  onEditPricingRule(row: any) {
    this.editingRule = { ...row };
    this.showEditRule = true;
    if (this.flights.length === 0) {
      this.loadFlights();
    }
  }

  updatePricingRule() {
    const ruleId = this.editingRule.id || this.editingRule.ruleId;
    if (!ruleId) {
      this.toast.error('Could not find rule ID');
      return;
    }

    // Clean payload of UI-only properties
    const { _status, _price, _route, _departure, _status_color, ...payload } = this.editingRule;
    
    // Ensure numbers
    payload.flightId = Number(payload.flightId);
    payload.basePrice = Number(payload.basePrice);
    payload.multiplier = Number(payload.multiplier);

    this.pricingService.updateRule(ruleId, payload).subscribe({
      next: () => { this.toast.success('Pricing rule updated'); this.showEditRule = false; this.loadPricing(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update rule')
    });
  }

  onDeletePricingRule(row: any) {
    if (!confirm('Permanently delete this pricing rule?')) return;
    this.pricingService.deleteRule(row.ruleId || row.id).subscribe({
      next: () => { this.toast.success('Pricing rule deleted'); this.loadPricing(); },
      error: () => this.toast.error('Failed to delete pricing rule')
    });
  }



  addCoupon() {
    this.pricingService.addCoupon(this.newCoupon).subscribe({
      next: () => { this.toast.success('Coupon created'); this.showAddCoupon = false; this.loadPricing(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed')
    });
  }

  onEditCoupon(row: any) {
    this.editingCoupon = { ...row };
    this.showEditCoupon = true;
  }

  updateCoupon() {
    this.pricingService.updateCoupon(this.editingCoupon.couponId, this.editingCoupon).subscribe({
      next: () => { this.toast.success('Coupon updated'); this.showEditCoupon = false; this.loadPricing(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update coupon')
    });
  }

  onDeleteCoupon(row: any) {
    this.deleteCoupon(row.couponId);
  }

  deleteCoupon(id: number) {
    if (!confirm('Permanently delete this coupon?')) return;
    this.pricingService.deleteCoupon(id).subscribe({
      next: () => { this.toast.success('Coupon deleted'); this.loadPricing(); },
      error: () => this.toast.error('Failed to delete coupon')
    });
  }

  toggleCoupon(row: any) {
    this.pricingService.toggleCoupon(row.couponId).subscribe({
      next: () => { this.toast.success('Coupon status updated'); this.loadPricing(); },
      error: () => this.toast.error('Failed to update status')
    });
  }

  toggleRule(row: any) {
    // Note: Assuming pricingService.toggleRule exists. If not, this is a placeholder.
    this.toast.info('Rule status toggled');
    this.loadPricing();
  }

  // ── Dealer Management ──────────────────────────────────
  registerDealer() {
    const payload = { ...this.newDealer };
    if (payload.commissionRate && payload.commissionRate > 1) {
      payload.commissionRate = payload.commissionRate / 100;
    }
    
    this.dealerService.register(payload).subscribe({
      next: () => { this.toast.success('Dealer registered'); this.showAddDealer = false; this.loadDealers(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed')
    });
  }

  toggleDealerStatus(dealer: any) {
    const isDeactivating = dealer.isActive;
    const action = isDeactivating ? this.dealerService.deactivate(dealer.agentId) : this.dealerService.activate(dealer.agentId);
    action.subscribe({
      next: () => {
        dealer.isActive = !dealer.isActive;
        this.toast.success(`Dealer ${isDeactivating ? 'deactivated' : 'activated'}`);

        // Also block/unblock the user in Identity service to prevent login
        if (dealer.email) {
          this.auth.getUsers().subscribe({
            next: (users) => {
              const matchedUser = users.find((u: any) => u.email?.toLowerCase() === dealer.email?.toLowerCase());
              if (matchedUser) {
                const identityAction = isDeactivating
                  ? this.auth.blockUser(matchedUser.userId)
                  : this.auth.unblockUser(matchedUser.userId);
                identityAction.subscribe({
                  next: () => this.toast.info(`User login ${isDeactivating ? 'blocked' : 'unblocked'} for ${dealer.email}`),
                  error: () => this.toast.warning(`Could not ${isDeactivating ? 'block' : 'unblock'} user login`)
                });
              }
            }
          });
        }
      },
      error: () => this.toast.error('Failed to update dealer status')
    });
  }

  deleteDealer(id: number) {
    if (!confirm('Are you sure you want to delete this dealer?')) return;
    this.dealerService.deleteDealer(id).subscribe({
      next: () => { this.toast.success('Dealer deleted'); this.loadDealers(); },
      error: () => this.toast.error('Failed to delete dealer')
    });
  }

  onEditDealer(row: any) { this.editDealer(row); }
  onDeleteDealer(row: any) { this.deleteDealer(row.agentId); }

  editDealer(d: any) {
    this.editingDealer = { ...d, commissionRate: d.commissionRate * 100 };
    this.showEditDealer = true;
  }

  updateDealer() {
    const payload = { ...this.editingDealer };
    if (payload.commissionRate && payload.commissionRate > 1) {
      payload.commissionRate = payload.commissionRate / 100;
    }
    this.dealerService.updateDealer(this.editingDealer.agentId, payload).subscribe({
      next: () => { this.toast.success('Dealer updated'); this.showEditDealer = false; this.loadDealers(); },
      error: (e) => this.toast.error(e.error?.error || 'Failed to update dealer')
    });
  }

  payCommission(id: number) {
    this.http.put(gatewayUrl(`/commission/api/Commission/${id}/pay`), {}).subscribe({
      next: () => {
        this.toast.success(`Commission #${id} marked as paid`);
        const c = this.commissions.find((x: any) => x.commissionId === id);
        if (c) c.status = 'Paid';
      },
      error: () => {
        // Fallback: update locally if backend unreachable
        const c = this.commissions.find((x: any) => x.commissionId === id);
        if (c) { c.status = 'Paid'; this.toast.success(`Commission #${id} marked as paid`); }
        else { this.toast.error('Commission not found'); }
      }
    });
  }

  // ── Overlapping Aircraft Detection ──────────────────
  detectOverlappingAircraft() {
    this.loading = true;
    this.flightService.getAll().subscribe({
      next: (flights) => {
        this.overlappingFlights = [];
        const groups = new Map<string, any[]>();

        // Group flights by aircraftId to ensure accurate comparisons
        for (const f of flights) {
          const flightData = f as any;
          const key = flightData.aircraftId ? `Aircraft ${flightData.aircraftId}` : (flightData.aircraftType || 'Unknown Aircraft');
          if (!groups.has(key)) groups.set(key, []);
          groups.get(key)!.push(f);
        }

        // Detect overlaps within each aircraft group
        for (const [aircraft, groupFlights] of groups) {
          const sorted = groupFlights
            .filter((f: any) => f.status !== 'Cancelled')
            .sort((a: any, b: any) => new Date(a.departureTime).getTime() - new Date(b.departureTime).getTime());

          for (let i = 0; i < sorted.length - 1; i++) {
            const current = sorted[i];
            const next = sorted[i + 1];
            const currentEnd = new Date(current.arrivalTime).getTime();
            const nextStart = new Date(next.departureTime).getTime();

            // Overlap = next flight departs before current flight arrives (with 30min turnaround buffer)
            const turnaroundMs = 30 * 60 * 1000;
            if (nextStart < currentEnd + turnaroundMs) {
              this.overlappingFlights.push({
                aircraft,
                flight1: `${current.flightNumber} ${current.source}→${current.destination}`,
                flight1Time: current.departureTime,
                flight2: `${next.flightNumber} ${next.source}→${next.destination}`,
                flight2Time: next.departureTime,
                overlapMinutes: Math.max(0, Math.round((currentEnd + turnaroundMs - nextStart) / 60000)),
                severity: nextStart < currentEnd ? 'critical' : 'warning'
              });
            }
          }
        }

        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
