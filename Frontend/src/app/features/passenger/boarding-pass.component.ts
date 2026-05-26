import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { BookingService, OperationsService, FlightService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';
import { RouterModule } from '@angular/router';
import { QRCodeComponent } from 'angularx-qrcode';

@Component({
  selector: 'app-boarding-pass',
  standalone: true,
  imports: [CommonModule, RouterModule, QRCodeComponent],
  template: `
    <!-- Dark Gold Nav -->
    <nav class="fixed top-0 w-full z-50 bg-[#131313]/80 backdrop-blur-xl flex justify-between items-center px-8 py-4 shadow-[0_4px_30px_rgba(0,0,0,0.5)]">
        <div class="font-serif italic text-2xl text-[#f2ca50] cursor-pointer" routerLink="/">SkyHorizon</div>
        <div class="hidden md:flex items-center space-x-8">
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] cursor-pointer" routerLink="/passenger/bookings">Itinerary</a>
        </div>
        <div class="flex items-center gap-4">
            <button class="text-[#d6c692]/70 hover:text-[#f2ca50] transition-all" routerLink="/passenger/profile"><span class="material-symbols-outlined">account_circle</span></button>
            <button (click)="doLogout()" class="text-error/70 hover:text-error transition-all duration-300">
                <span class="material-symbols-outlined">logout</span>
            </button>
        </div>
    </nav>
    <div class="bp-page" style="padding-top:60px;">
      <div class="bp-bg">
        <div class="hero-orb orb-1"></div>
        <div class="hero-orb orb-2"></div>
      </div>

      <div class="bp-content">
        <h1 class="animate-fadeInUp">Boarding <span class="text-gradient">Pass</span></h1>

        @if (loading) {
          <div class="loading-overlay"><div class="spinner"></div><p>Loading boarding pass...</p></div>
        } @else if (booking) {
          <div class="boarding-card animate-fadeInUp delay-1">
            <!-- Top section -->
            <div class="bp-top">
              <div class="bp-airline">
                <span class="bp-logo">✈</span>
                <span class="bp-airline-name">SkyLedger Airlines</span>
              </div>
              <span class="bp-type">BOARDING PASS</span>
            </div>

            <!-- Route section -->
            <div class="bp-route">
              <div class="bp-city">
                <span class="bp-code">{{ booking.source || 'DEL' }}</span>
                <span class="bp-city-name">{{ booking.source || 'Delhi' }}</span>
              </div>
              <div class="bp-flight-line">
                <div class="bp-dots"></div>
                <span class="bp-plane">✈</span>
                <div class="bp-dots"></div>
              </div>
              <div class="bp-city">
                <span class="bp-code">{{ booking.destination || 'BOM' }}</span>
                <span class="bp-city-name">{{ booking.destination || 'Mumbai' }}</span>
              </div>
            </div>

            <!-- Details Grid -->
            <div class="bp-details">
              <div class="bp-detail">
                <span class="bp-label">PASSENGER</span>
                <span class="bp-value">{{ passengerName }}</span>
              </div>
              <div class="bp-detail">
                <span class="bp-label">FLIGHT</span>
                <span class="bp-value">AG-{{ booking.flightId }}</span>
              </div>
              <div class="bp-detail">
                <span class="bp-label">DATE</span>
                <span class="bp-value">{{ booking.bookingDate | date:'dd MMM yyyy' }}</span>
              </div>
              <div class="bp-detail">
                <span class="bp-label">SEAT</span>
                <span class="bp-value seat-value">{{ seatNo || '—' }}</span>
              </div>
              <div class="bp-detail">
                <span class="bp-label">GATE</span>
                <span class="bp-value">{{ gate || 'TBD' }}</span>
              </div>
              <div class="bp-detail">
                <span class="bp-label">BOARDING</span>
                <span class="bp-value">{{ boardingTime || '—' }}</span>
              </div>
            </div>

            <!-- PNR Bar -->
            <div class="bp-pnr-section">
              <div class="bp-pnr">
                <span class="bp-label">PNR</span>
                <span class="bp-pnr-code">{{ pnr }}</span>
              </div>
              <div class="bp-barcode" style="background: white; padding: 4px; border-radius: 4px;">
                <qrcode [qrdata]="'https://skyhorizon-air.com/verify/' + pnr" [width]="80" [errorCorrectionLevel]="'M'"></qrcode>
              </div>
            </div>

            <!-- Tear Line -->
            <div class="bp-tear">
              <div class="tear-circle left"></div>
              <div class="tear-line"></div>
              <div class="tear-circle right"></div>
            </div>

            <!-- Bottom stub -->
            <div class="bp-stub">
              <div class="bp-stub-info">
                <span><strong>{{ pnr }}</strong></span>
                <span>AG-{{ booking.flightId }}</span>
                <span>SEAT {{ seatNo || '—' }}</span>
                <span>{{ booking.bookingDate | date:'dd MMM' }}</span>
              </div>
            </div>
          </div>

          <div class="bp-actions animate-fadeInUp delay-2">
            <button class="btn btn-primary btn-lg" (click)="printPass()">🖨️ Print Boarding Pass</button>
          </div>
        } @else {
          <div class="empty-state animate-fadeIn">
            <div style="font-size:64px;margin-bottom:16px;">🎫</div>
            <h3>Boarding pass not available</h3>
            <p style="color:var(--text-secondary)">Check in first to get your boarding pass</p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .bp-page { min-height: 100vh; position: relative; overflow: hidden; }
    .bp-bg { position: absolute; inset: 0; z-index: 0; }
    .hero-orb { position: absolute; border-radius: 50%; filter: blur(140px); opacity: 0.2; }
    .orb-1 { width: 500px; height: 500px; background: #2b6fff; top: -100px; right: -100px; animation: float 8s ease-in-out infinite; }
    .orb-2 { width: 400px; height: 400px; background: #f2c14e; bottom: -100px; left: -100px; animation: float 10s ease-in-out infinite reverse; }

    .bp-content {
      position: relative; z-index: 1;
      max-width: 520px; margin: 0 auto; padding: 40px 20px;
    }
    .bp-content h1 { font-size: 32px; text-align: center; margin-bottom: 32px; }

    .boarding-card {
      background: linear-gradient(145deg, rgba(17,21,56,0.95), rgba(10,14,39,0.98));
      border: 1px solid var(--border-color);
      border-radius: var(--radius-xl);
      overflow: hidden;
      backdrop-filter: blur(30px);
      box-shadow: 0 20px 60px rgba(0,0,0,0.5);
    }

    .bp-top {
      display: flex; justify-content: space-between; align-items: center;
      padding: 24px 28px 16px;
    }
    .bp-airline { display: flex; align-items: center; gap: 10px; }
    .bp-logo {
      font-size: 24px;
      width: 40px; height: 40px;
      background: var(--accent-gradient);
      border-radius: 10px;
      display: flex; align-items: center; justify-content: center;
    }
    .bp-airline-name { font-weight: 700; font-size: 16px; font-family: 'Outfit', sans-serif; }
    .bp-type {
      font-size: 11px; font-weight: 700;
      letter-spacing: 3px; color: var(--accent-secondary);
      text-transform: uppercase;
    }

    .bp-route {
      display: flex; align-items: center; gap: 20px;
      padding: 20px 28px 28px;
    }
    .bp-city { text-align: center; flex: 1; }
    .bp-code { display: block; font-size: 36px; font-weight: 900; font-family: 'Outfit', sans-serif; }
    .bp-city-name { font-size: 13px; color: var(--text-secondary); }
    .bp-flight-line { display: flex; align-items: center; flex: 1.5; gap: 0; }
    .bp-dots {
      flex: 1; height: 2px;
      background: repeating-linear-gradient(90deg, var(--accent-primary) 0, var(--accent-primary) 4px, transparent 4px, transparent 8px);
    }
    .bp-plane { font-size: 20px; padding: 0 8px; color: var(--accent-secondary); }

    .bp-details {
      display: grid; grid-template-columns: repeat(3, 1fr);
      gap: 0; border-top: 1px solid var(--border-color);
    }
    .bp-detail {
      padding: 16px 20px;
      border-bottom: 1px solid var(--border-color);
      border-right: 1px solid var(--border-color);
    }
    .bp-detail:nth-child(3n) { border-right: none; }
    .bp-label {
      display: block; font-size: 10px; font-weight: 700;
      color: var(--text-muted); text-transform: uppercase;
      letter-spacing: 1.5px; margin-bottom: 4px;
    }
    .bp-value { font-size: 15px; font-weight: 700; }
    .seat-value {
      font-size: 22px; font-weight: 900;
      background: linear-gradient(135deg, var(--accent-primary), var(--accent-secondary));
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }

    .bp-pnr-section {
      padding: 20px 28px;
      display: flex; justify-content: space-between; align-items: center;
    }
    .bp-pnr-code {
      display: block; font-size: 28px; font-weight: 900;
      font-family: 'Courier New', monospace;
      letter-spacing: 4px; color: var(--accent-primary);
    }
    .bp-barcode {
      display: flex; gap: 2px; align-items: flex-end; height: 70px;
    }
    .bar {
      height: 100%; background: var(--text-primary); border-radius: 1px;
    }

    .bp-tear {
      position: relative; height: 24px;
      display: flex; align-items: center;
    }
    .tear-circle {
      width: 24px; height: 24px; border-radius: 50%;
      background: var(--bg-primary); position: absolute;
    }
    .tear-circle.left { left: -12px; }
    .tear-circle.right { right: -12px; }
    .tear-line {
      flex: 1; margin: 0 16px;
      border-top: 2px dashed var(--border-color);
    }

    .bp-stub { padding: 16px 28px 20px; }
    .bp-stub-info {
      display: flex; justify-content: space-between;
      font-size: 12px; color: var(--text-muted);
      text-transform: uppercase; letter-spacing: 1px;
    }

    .bp-actions { text-align: center; margin-top: 28px; }

    .empty-state { text-align: center; padding: 80px 20px; }

    @media print {
      .bp-page { background: white !important; }
      .bp-bg, nav, .bp-actions, .bp-content h1 { display: none !important; }
      .boarding-card { box-shadow: none; border: 2px solid #333; }
    }

    @media (max-width: 768px) {
      .bp-details { grid-template-columns: repeat(2, 1fr); }
      .bp-detail:nth-child(3n) { border-right: 1px solid var(--border-color); }
      .bp-detail:nth-child(2n) { border-right: none; }
      .bp-route { gap: 12px; }
      .bp-code { font-size: 28px; }
    }
  `]
})
export class BoardingPassComponent implements OnInit {
  pnr = '';
  booking: any = null;
  loading = true;
  passengerName = '';
  seatNo = '';
  gate = 'TBD';
  boardingTime = '';
  barcodeLines: number[] = [];

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService,
    private opsService: OperationsService,
    private flightService: FlightService,
    private toast: ToastService,
    public auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  doLogout() {
    this.auth.logout();
  }

  ngOnInit() {
    // Generate random barcode pattern
    this.barcodeLines = Array.from({ length: 30 }, () => Math.random() > 0.5 ? 3 : 1);

    this.pnr = this.route.snapshot.paramMap.get('pnr') || '';
    if (this.pnr) {
      this.bookingService.getByPnr(this.pnr).subscribe({
        next: (b) => {
          // Flatten the booking structure
          this.booking = { ...b.booking, passengers: b.passengers };
          this.loading = false;
          
          if (b.passengers && b.passengers.length > 0) {
            this.passengerName = b.passengers[0].name;
            this.seatNo = b.passengers[0].seatNo || '—';
          }

          // Enrich with flight route data
          if (this.booking.flightId) {
            this.flightService.getById(this.booking.flightId).subscribe({
              next: (f) => {
                this.booking.source = f.source || '';
                this.booking.destination = f.destination || '';
                this.cdr.detectChanges();
              }
            });
          }
          
          // Force UI refresh after initial load to clear spinner
          this.cdr.detectChanges();

          // Try to get boarding pass data
          this.opsService.getBoardingPass(this.pnr, b.passengers?.[0]?.passengerId || 0).subscribe({
            next: (bp) => {
              this.seatNo = bp.seatNo || this.seatNo;
              this.gate = bp.gate || 'G12';
              this.boardingTime = bp.boardingTime;
              this.cdr.detectChanges();
            },
            error: () => {
              this.gate = 'TBD';
              this.boardingTime = 'Check in required';
              this.cdr.detectChanges();
            }
          });
        },
        error: () => {
          this.loading = false;
          this.toast.error('Booking not found');
          this.cdr.detectChanges();
        }
      });
    } else {
      this.loading = false;
    }
  }

  printPass() {
    window.print();
  }
}
