import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { FlightService, BookingService, OperationsService, PricingService, DealerService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';
import { RouterModule } from '@angular/router';
import { PassengerInput } from '../../core/models';

@Component({
  selector: 'app-book',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './book.component.html',
  styleUrl: './book.component.css'
})
export class BookComponent implements OnInit {
  flight: any;
  loading = true;
  booking = false;
  step = 1;
  seatsLoading = false;

  passengers: PassengerInput[] = [{ name: '', age: null as any, gender: '', seatNo: '', seatClass: '' }];

  // Class Prices fetched from Pricing Rules calculate API
  classPrices: Record<string, number> = {};

  // Shared booking info
  contactEmail = '';

  // Seat Map Data — loaded from API
  seatBlocks: any[] = [];
  seatLayouts: any[] = [];
  seatsByClass: any = {};
  activeClass: string = 'Economy';
  
  rows: number[] = [];
  leftCols: string[] = [];
  midCols: string[] = [];
  rightCols: string[] = [];
  occupiedSeats: string[] = [];
  
  showNotifications = false;

  get notifications() { return this.notifService.getNotifications(); }
  get unreadCount() { return this.notifService.getUnreadCount(); }
  
  // Coupon
  availableCoupons: any[] = [];
  couponCode = '';
  appliedCoupon: any = null;
  validatingCoupon = false;
  couponError = '';

  // Reward Points
  rewardBalance = 0;
  pointsToRedeem: number | null = null;
  appliedPoints = 0;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private flightService: FlightService,
    private bookingService: BookingService,
    private opsService: OperationsService,
    private pricingService: PricingService,
    private dealerService: DealerService,
    public auth: AuthService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService
  ) {}

  doLogout() {
    this.auth.logout();
  }

  getPassengerSeatPrice(p: any): number {
    if (p.seatNo) {
      const info = this.getSeatInfo(p.seatNo);
      if (info?.price) return info.price;
    }
    return this.classPrices[p.seatClass || 'Economy'] || (this.flight?.price || this.flight?.basePrice || 0);
  }

  get totalAmountBeforeDiscount(): number {
    return this.passengers.reduce((sum, p) => {
      let fees = 0;
      if (p.baggageWeight && p.baggageWeight > 15) {
        fees += (p.baggageWeight - 15) * 500;
      }
      return sum + this.getPassengerSeatPrice(p) + fees;
    }, 0);
  }

  get totalAmount(): number {
    let total = this.totalAmountBeforeDiscount;
    if (this.appliedCoupon) {
      total = total - (total * (this.appliedCoupon.discountPercent / 100));
    }
    if (this.appliedPoints > 0) {
      total = total - this.appliedPoints; // 1 point = 1 Rupee
    }
    return Math.max(total, 0); // Ensure total does not go below 0
  }

  get couponDiscountAmount(): number {
    if (!this.appliedCoupon) return 0;
    return this.totalAmountBeforeDiscount * (this.appliedCoupon.discountPercent / 100);
  }

  getFlightDuration(): string {
    if (!this.flight?.departureTime || !this.flight?.arrivalTime) return '';
    const dep = new Date(this.flight.departureTime).getTime();
    const arr = new Date(this.flight.arrivalTime).getTime();
    const diff = arr - dep;
    if (diff <= 0) return '';
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }

  get canProceedToSeats(): boolean {
    return this.passengers.every(p => p.name && p.age && p.gender);
  }

  get canConfirm(): boolean {
    return this.passengers.every(p => p.seatNo);
  }

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.notifService.loadServerNotifications();
    this.loadRewardPoints();
    this.loadCoupons();
    
    // Default to the logged-in user's email if available
    this.contactEmail = this.auth.currentUser?.email || '';

    // Load flight, seats, layouts, booked seats, and pricing in PARALLEL
    forkJoin({
      flight: this.flightService.getById(id),
      seats: this.flightService.getSeatMap(id).pipe(catchError(() => of([]))),
      bookedSeats: this.bookingService.getBookedSeats(id).pipe(catchError(() => of([] as string[]))),
      layouts: this.flightService.getSeatLayout(id).pipe(catchError(() => of([]))),
      ecoPrice: this.pricingService.calculate(id, 'Economy').pipe(catchError(() => of(null))),
      busPrice: this.pricingService.calculate(id, 'Business').pipe(catchError(() => of(null))),
      firstPrice: this.pricingService.calculate(id, 'First').pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ flight, seats, bookedSeats, layouts, ecoPrice, busPrice, firstPrice }) => {
        this.flight = flight;
        this.seatLayouts = layouts || [];
        
        // Build a set of booked seats from the Booking service for reliable cross-check
        const bookedSet = new Set((bookedSeats || []).map((s: string) => s.toUpperCase()));

        // Group seats by class
        seats.forEach((s: any) => {
          const className = s.seatClass || 'Economy';
          if (!this.seatsByClass[className]) this.seatsByClass[className] = [];
          this.seatsByClass[className].push(s);
          // Mark as occupied if Inventory says unavailable OR Booking service has it locked
          if (!s.isAvailable || bookedSet.has((s.seatNo || '').toUpperCase())) {
            this.occupiedSeats.push(s.seatNo);
          }
        });

        if (ecoPrice) this.classPrices['Economy'] = ecoPrice.finalPrice;
        if (busPrice) this.classPrices['Business'] = busPrice.finalPrice;
        if (firstPrice) this.classPrices['First'] = firstPrice.finalPrice || firstPrice.basePrice;
        
        // Render All Seat Layouts Sequentially
        this.selectSeatClassTab();

        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.toast.error('Flight not found');
        this.cdr.detectChanges();
      }
    });
  }

  loadCoupons() {
    this.pricingService.getActiveCoupons().subscribe({
      next: (data) => {
        this.availableCoupons = data;
        this.cdr.detectChanges();
      }
    });
  }

  selectCoupon(code: string) {
    this.couponCode = code;
    this.applyCoupon();
  }

  loadRewardPoints() {
    const user = this.auth.currentUser;
    if (user?.userId) {
       this.dealerService.getReward(user.userId).subscribe({
          next: (res) => { this.rewardBalance = res.balance || 0; },
          error: () => { this.rewardBalance = 0; }
       });
    }
  }

  applyPoints() {
    if (!this.pointsToRedeem || this.pointsToRedeem <= 0) return;
    if (this.pointsToRedeem > this.rewardBalance) {
      this.toast.warning('You do not have enough points.');
      return;
    }
    
    // 1 point = 1 Rupee approx scale, we cap at totalAmountBeforeDiscount.
    // If they have coupons applied, totalAmountBeforeDiscount already doesn't reflect it, but it's simpler
    // to just check against the remaining totalAmount. 
    // We already do Math.max(total, 0) inside get totalAmount() so it's fine.
    
    // Actually limit to the remaining amount before points so they don't waste points
    let maxRedeemable = this.totalAmountBeforeDiscount;
    if (this.appliedCoupon) {
      maxRedeemable = maxRedeemable - (maxRedeemable * (this.appliedCoupon.discountPercent / 100));
    }

    if (this.pointsToRedeem > maxRedeemable) {
       this.toast.info(`Adjusted given points to match final ticket amount.`);
       this.pointsToRedeem = Math.ceil(maxRedeemable);
    }

    this.appliedPoints = this.pointsToRedeem;
    this.toast.success(`Applied ${this.appliedPoints} points!`);
  }

  removePoints() {
    this.appliedPoints = 0;
    this.pointsToRedeem = null;
  }

  toggleNotifications() {
     this.showNotifications = !this.showNotifications;
     if (this.showNotifications) {
        this.notifService.markAllRead();
     }
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
          rows: rows,
          leftCols: left,
          midCols: mid,
          rightCols: right
        });
      } else if (layout) {
        // Seats not loaded yet — use layout row count as fallback
        const { left, mid, right } = this.parseLayoutColumnsRet(layout.columnsLayout);
        // We don't know real row numbers yet, use sequential (will be refreshed)
        const rows = Array.from({length: layout.totalRows}, (_, i) => i + 1);
        this.seatBlocks.push({
          seatClass: cls,
          rows: rows,
          leftCols: left,
          midCols: mid,
          rightCols: right
        });
      }
    });

    if (this.seatBlocks.length === 0) {
      // Fallback
      this.seatBlocks.push({
        seatClass: 'First',
        rows: [1, 2, 3, 4],
        leftCols: ['A', 'B'], midCols: [], rightCols: ['C', 'D']
      });
      this.seatBlocks.push({
        seatClass: 'Business',
        rows: [5, 6, 7, 8],
        leftCols: ['A', 'B'], midCols: [], rightCols: ['E', 'F']
      });
      this.seatBlocks.push({
        seatClass: 'Economy',
        rows: Array.from({length: 10}, (_, i) => 9 + i),
        leftCols: ['A', 'B', 'C'], midCols: [], rightCols: ['D', 'E', 'F']
      });
    }
  }

  loadSeatMap(flightId: number) {
    this.seatsLoading = true;

    // Fetch both the inventory seat map AND actual booked seats from the Booking service
    forkJoin({
      seats: this.flightService.getSeatMap(flightId),
      bookedSeats: this.bookingService.getBookedSeats(flightId).pipe(catchError(() => of([] as string[])))
    }).subscribe({
      next: ({ seats, bookedSeats }) => {
        this.seatsByClass = {};
        this.occupiedSeats = [];
        const bookedSet = new Set((bookedSeats || []).map((s: string) => s.toUpperCase()));

        seats.forEach((s: any) => {
          const className = s.seatClass || 'Economy';
          if (!this.seatsByClass[className]) this.seatsByClass[className] = [];
          this.seatsByClass[className].push(s);
          // Mark as occupied if Inventory says unavailable OR Booking service has it locked
          if (!s.isAvailable || bookedSet.has((s.seatNo || '').toUpperCase())) {
            this.occupiedSeats.push(s.seatNo);
          }
        });
        this.seatsLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.occupiedSeats = [];
        this.seatsLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  addPassenger() { 
    if (this.passengers.length >= 6) {
      this.toast.error('Maximum 6 passengers allowed per booking');
      return;
    }
    this.passengers.push({ name: '', age: null as any, gender: '', seatNo: '', seatClass: '' }); 
  }
  
  removePassenger(i: number) { this.passengers.splice(i, 1); }

  goToStep(s: number) {
    if (s === 2 && !this.canProceedToSeats) {
      this.toast.warning('Please fill in all passenger details first.');
      return;
    }
    if (s === 2) {
      // Refresh seat availability before showing the seat map
      this.loadSeatMap(this.flight.flightId);
    }
    this.step = s;
  }

  getSeatStatus(seatNo: string): 'available' | 'selected' | 'occupied' | 'wrong-class' {
    if (this.occupiedSeats.includes(seatNo)) return 'occupied';
    
    // Seat class logic. Only assign seats for passenger choosing this class
    if (this.passengers.some(p => p.seatNo === seatNo)) return 'selected';

    // Highlight available but wrong class if passenger needs a different class? No, we filter earlier.
    return 'available';
  }

  getSeatBg(seatNo: string, row: number): string {
    const status = this.getSeatStatus(seatNo);
    if (status === 'selected') return 'rgba(242,202,80,0.2)';
    if (status === 'occupied') return 'rgba(255,60,60,0.15)';
    // Window seats get a subtle blue tint
    const info = this.getSeatInfo(seatNo);
    if (info?.category === 'Window') return 'rgba(59,130,246,0.08)';
    if (row <= 4) return 'rgba(212,175,55,0.08)';
    if (row <= 8) return 'rgba(24,144,255,0.08)';
    return 'rgba(255,255,255,0.04)';
  }

  getSeatBorder(seatNo: string, row: number): string {
    const status = this.getSeatStatus(seatNo);
    if (status === 'selected') return '#f2ca50';
    if (status === 'occupied') return 'rgba(255,60,60,0.5)';
    const info = this.getSeatInfo(seatNo);
    if (info?.category === 'Window') return 'rgba(59,130,246,0.3)';
    
    // Dynamic coloring based on actual class
    if (info?.seatClass === 'First') return 'rgba(212,175,55,0.25)';
    if (info?.seatClass === 'Business') return 'rgba(24,144,255,0.25)';
    return 'rgba(255,255,255,0.1)';
  }

  getSeatColor(seatNo: string, row: number): string {
    const status = this.getSeatStatus(seatNo);
    if (status === 'selected') return '#fff';
    if (status === 'occupied') return 'rgba(255,60,60,0.7)';
    const info = this.getSeatInfo(seatNo);
    if (info?.category === 'Window') return 'rgba(59,130,246,0.8)';
    
    // Dynamic coloring based on actual class
    if (info?.seatClass === 'First') return 'rgba(212,175,55,0.8)';
    if (info?.seatClass === 'Business') return 'rgba(24,144,255,0.8)';
    return 'rgba(255,255,255,0.5)';
  }

  getClassPrice(cls: string): number {
    return this.classPrices[cls] || (this.flight?.price || this.flight?.basePrice || 0);
  }

  getFullSeatNo(row: number, col: string, seatClass: string): string {
    // Matches backend: {ClassPrefix}{Row}{Col} e.g. F1A, B2C, E10A
    const prefix = seatClass.charAt(0).toUpperCase();
    return `${prefix}${row}${col}`;
  }

  scrollToClass(cls: string) {
    const element = document.getElementById(`class-section-${cls}`);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  toggleSeat(row: number, col: string, seatClass: string) {
    const seatNo = this.getFullSeatNo(row, col, seatClass);
    const status = this.getSeatStatus(seatNo);
    if (status === 'occupied') return;

    if (status === 'selected') {
      const px = this.passengers.find(p => p.seatNo === seatNo);
      if (px) {
        px.seatNo = '';
        px.seatClass = '';
      }
      return;
    }

    const anyPxNeedsSeat = this.passengers.find(p => !p.seatNo);
    if (anyPxNeedsSeat) {
      anyPxNeedsSeat.seatNo = seatNo;
      anyPxNeedsSeat.seatClass = seatClass;
    } else {
      this.toast.info('All passengers already have seats.');
    }
  }


  getSeatTooltip(row: number, col: string, seatClass: string): string {
    const seatNo = this.getFullSeatNo(row, col, seatClass);
    const status = this.getSeatStatus(seatNo);
    const info = this.getSeatInfo(seatNo);
    const price = info?.price ?? this.getClassPrice(seatClass);
    const cat = info?.category ? ` | ${info.category}` : '';
    
    if (status === 'occupied') return `Seat ${seatNo}: Booked${cat}`;
    return `Seat ${seatNo} (${seatClass}${cat}): ₹${price.toLocaleString()}`;
  }

  getSeatInfo(seatNo: string): any {
    for (const cls of Object.keys(this.seatsByClass)) {
      const seat = this.seatsByClass[cls]?.find((s: any) => s.seatNo === seatNo);
      if (seat) return seat;
    }
    return null;
  }

  getSeatPrice(row: number, col: string, seatClass: string): number {
    const seatNo = this.getFullSeatNo(row, col, seatClass);
    const info = this.getSeatInfo(seatNo);
    return info?.price ?? this.getClassPrice(seatClass);
  }

  isWindowSeat(row: number, col: string, seatClass: string): boolean {
    const seatNo = this.getFullSeatNo(row, col, seatClass);
    const info = this.getSeatInfo(seatNo);
    return info?.category === 'Window';
  }

  applyCoupon() {
    if (!this.couponCode) return;
    this.validatingCoupon = true;
    this.couponError = '';
    
    this.pricingService.validateCoupon(this.couponCode, this.totalAmountBeforeDiscount, this.auth.currentUser?.userId).subscribe({
      next: (res) => {
        this.validatingCoupon = false;
        if (res.valid) {
          this.appliedCoupon = res;
          this.toast.success(`Coupon applied! ${res.discountPercent}% off`);
        } else {
          this.couponError = res.error || 'Invalid coupon';
        }
      },
      error: () => {
        this.validatingCoupon = false;
        this.couponError = 'Failed to validate coupon';
      }
    });
  }

  removeCoupon() {
    this.appliedCoupon = null;
    this.couponCode = '';
  }

  confirmBooking() {
    if (!this.canConfirm) {
      this.toast.warning('Please select seats for all passengers');
      return;
    }

    // Edge Case: Duplicate passenger detection
    const names = this.passengers.map(p => (p.name || '').trim().toLowerCase());
    const duplicates = names.filter((n, i) => n !== '' && names.indexOf(n) !== i);
    if (duplicates.length > 0) {
      this.toast.error(`Duplicate passenger detected: "${duplicates[0]}". Each passenger must have a unique name.`);
      this.step = 1;
      return;
    }

    this.booking = true;
    const user = this.auth.currentUser;
    this.bookingService.create({
      userId: user?.userId || 0,
      flightId: this.flight.flightId,
      totalAmount: this.totalAmount,
      passengers: this.passengers,
      email: this.contactEmail || user?.email,
      userName: user?.name,
      source: this.flight.source,
      destination: this.flight.destination,
      departureTime: this.flight.departureTime,
      arrivalTime: this.flight.arrivalTime,
      couponCode: this.appliedCoupon ? this.appliedCoupon.code : null
    }).subscribe({
      next: (res) => {
        this.toast.success(`Booking created! PNR: ${res.pnr}`);
        this.notifService.bookingCreated(
          res.pnr,
          `${this.flight.source} → ${this.flight.destination}`,
          this.totalAmount
        );

        // Immediately reserve seats via Inventory API (synchronous backup to event bus)
        this.passengers.forEach(p => {
          if (p.seatNo) {
            this.flightService.reserveSeat(this.flight.flightId, p.seatNo).subscribe();
          }
        });
        
        // Deduct points if any
        if (this.appliedPoints > 0 && user?.userId) {
           this.dealerService.redeemPoints(user.userId, this.appliedPoints).subscribe();
        }

        // Handle Baggage if any passenger requested explicitly
        const passengersWithBaggage = this.passengers.filter(p => p.baggageWeight && p.baggageWeight > 0);
        if (passengersWithBaggage.length > 0) {
          this.toast.info('Syncing baggage details...');
          this.bookingService.getByPnr(res.pnr).subscribe({
            next: (bookingDetails: any) => {
               const savedPassengers = bookingDetails.passengers || [];
               passengersWithBaggage.forEach(pInput => {
                  // Find the matching saved passenger by seatNo
                  const match = savedPassengers.find((sp: any) => sp.seatNo === pInput.seatNo);
                  if (match) {
                     this.opsService.addBaggage({
                        pnr: res.pnr,
                        passengerId: match.passengerId || match.id,
                        weight: pInput.baggageWeight,
                        tagNumber: `BAG-${match.passengerId || match.id}-${Date.now()}`
                     }).subscribe();
                  }
               });
               this.booking = false;
               this.router.navigate(['/passenger/payment', res.pnr]);
            },
            error: () => {
               this.booking = false;
               this.router.navigate(['/passenger/payment', res.pnr]); 
            }
          });
        } else {
          this.booking = false;
          this.router.navigate(['/passenger/payment', res.pnr]);
        }
      },
      error: (err) => {
        this.booking = false;
        const errorMsg = err.error?.message || err.error?.error || 'Booking failed';
        // Edge Case: Seat sold during booking
        if (errorMsg.toLowerCase().includes('seat') || errorMsg.toLowerCase().includes('occupied') || errorMsg.toLowerCase().includes('unavailable')) {
          this.toast.error('One or more seats are no longer available. Refreshing seat map...');
          this.passengers.forEach(p => p.seatNo = '');
          this.loadSeatMap(this.flight.flightId);
          this.step = 2;
          return;
        }
        // Edge Case: Price change
        if (errorMsg.toLowerCase().includes('price')) {
          this.toast.warning('Price has changed. Please review and confirm again.');
          this.flightService.getById(this.flight.flightId).subscribe({
            next: (f) => this.flight = f
          });
          return;
        }
        this.toast.error(errorMsg);
      }
    });
  }
}
