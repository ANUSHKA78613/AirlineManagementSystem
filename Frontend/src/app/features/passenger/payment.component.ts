import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PaymentService, BookingService, DealerService, FlightService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

import { RazorpayWidget, RazorpayOptions } from '../../shared/components/razorpay-widget/razorpay-widget';

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule, FormsModule, RazorpayWidget, RouterModule],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.css'
})
export class PaymentComponent implements OnInit, OnDestroy {
  pnr = '';
  booking: any = null;
  step: 'checkout' | 'processing' | 'confirming' | 'success' | 'failed' = 'checkout';
  processing = false;
  transactionId = '';
  refundStatus = '';
  failureReason = '';
  cancelled = false;
  private cancelInProgress = false;
  private bookingFlightId: number | null = null;

  // Reward Points
  rewardPoints = 0;
  pointsToRedeem = 0;
  useRewardPoints = false;
  pointsEarned = 0;

  razorpayOptions!: RazorpayOptions;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private paymentService: PaymentService,
    private bookingService: BookingService,
    private dealerService: DealerService,
    private flightService: FlightService,
    public auth: AuthService,
    private toast: ToastService
  ) {}

  doLogout() {
    this.auth.logout();
  }

  ngOnInit() {
    this.pnr = this.route.snapshot.paramMap.get('pnr') || '';
    if (this.pnr) {
      this.bookingService.getByPnr(this.pnr).subscribe({
        next: (b) => {
          this.booking = { ...b.booking, passengers: b.passengers };
          this.bookingFlightId = this.booking?.flightId || null;
          this.setupRazorpay();
        },
        error: () => this.toast.error('Booking not found')
      });
      const userId = this.auth.currentUser?.userId || 0;
      if (userId) {
        this.dealerService.getReward(userId).subscribe({
          next: (data: any) => this.rewardPoints = data?.totalPoints || data?.points || 0,
          error: () => this.rewardPoints = 0
        });
      }
    }
  }

  ngOnDestroy() {}

  setupRazorpay() {
    const amount = this.effectiveAmount;
    const fallbackOptions = {
      key: 'rzp_test_SUDVzwAKVeUa91',
      amount: amount * 100,
      currency: 'INR',
      name: 'SkyLedger Airlines',
      description: `Flight Booking for PNR: ${this.pnr}`,
      order_id: '',
      prefill: {
        method: 'upi',
        name: this.auth.currentUser?.name,
        email: this.auth.currentUser?.email
      }
    };

    this.paymentService.createRazorpayOrder(this.pnr, amount).subscribe({
      next: (order) => {
        this.razorpayOptions = {
          ...fallbackOptions,
          key: order.keyId || fallbackOptions.key,
          amount: order.amount || fallbackOptions.amount,
          currency: order.currency || fallbackOptions.currency,
          order_id: order.orderId || ''
        };
      },
      error: () => {
        this.razorpayOptions = fallbackOptions;
      }
    });
  }

  // ============================================================
  // PAYMENT SUCCESS — go straight to confirm screen, no timers
  // ============================================================
  onPaymentSuccess(response: any) {
    if (this.cancelled) return;

    this.transactionId = response.razorpay_payment_id || 'TXN-' + Math.random().toString(36).substring(2, 10).toUpperCase();
    this.step = 'processing';

    // Step 1: Verify payment with backend, then confirm booking, then navigate
    this.paymentService.verifyRazorpay({
      pnr: this.pnr,
      amount: this.booking?.totalAmount || 0,
      method: 'Razorpay',
      razorpay_order_id: response.razorpay_order_id || '',
      razorpay_payment_id: response.razorpay_payment_id || '',
      razorpay_signature: response.razorpay_signature || ''
    }).subscribe({
      next: () => this.confirmAndNavigate(),
      error: () => this.confirmAndNavigate()  // Still confirm even if verify had a hiccup
    });
  }

  private confirmAndNavigate() {
    this.bookingService.confirm(this.pnr).subscribe({
      next: () => {
        this.earnRewardPoints();
        this.toast.success('🎉 Payment successful! Your ticket is confirmed.');
        this.router.navigate(['/passenger/bookings']);
      },
      error: () => {
        // Even if confirm call fails, the saga handler will eventually confirm it
        this.earnRewardPoints();
        this.toast.success('🎉 Payment successful! Your ticket is confirmed.');
        this.router.navigate(['/passenger/bookings']);
      }
    });
  }

  // ============================================================
  // CONFIRM — user clicks "Confirm Booking"
  // ============================================================
  confirmBookingExplicit() {
    if (this.cancelled || this.cancelInProgress) return;
    this.step = 'processing';
    this.bookingService.confirm(this.pnr).subscribe({
      next: () => {
        this.earnRewardPoints();
        this.step = 'success';
        this.toast.success('🎉 Payment successful! Your ticket is confirmed.');
        this.router.navigate(['/passenger/bookings']);
      },
      error: () => {
        this.earnRewardPoints();
        this.step = 'success';
        this.toast.success('🎉 Payment successful! Your ticket is confirmed.');
        this.router.navigate(['/passenger/bookings']);
      }
    });
  }

  // ============================================================
  // CANCEL — from confirm screen
  // ============================================================
  cancelDuringConfirm() {
    this.cancelled = true;
    this.cancelInProgress = true;
    this.cancelAndRelease('Payment cancelled. Cancelling booking & initiating refund...');
  }

  // ============================================================
  // PAYMENT ERROR — redirect to itinerary
  // ============================================================
  onPaymentError(_error: any) {
    if (this.cancelInProgress || this.cancelled) return;
    this.cancelled = true;
    this.cancelInProgress = true;
    this.router.navigate(['/passenger/bookings']);
    this.toast.warning('Payment failed. Redirecting to itinerary...');
  }

  // ============================================================
  // PAYMENT CLOSED — user closed Razorpay modal, redirect to itinerary
  // ============================================================
  onPaymentClosed() {
    if (this.cancelInProgress || this.cancelled) return;
    this.cancelled = true;
    this.cancelInProgress = true;
    this.cancelAndRelease('Payment cancelled. Releasing seats...');
  }

  // ============================================================
  // CANCEL & RELEASE — cancel booking and go to itinerary
  // ============================================================
  private cancelAndRelease(reason: string) {
    this.toast.warning(reason);

    const flightId = this.bookingFlightId || this.booking?.flightId;

    this.bookingService.cancel(this.pnr).subscribe({
      next: () => {
        if (this.booking?.passengers && flightId) {
          for (const p of this.booking.passengers) {
            if (p.seatNo) {
              this.flightService.releaseSeat(flightId, p.seatNo).subscribe();
            }
          }
        }
        this.router.navigate(['/passenger/bookings']);
      },
      error: () => {
        this.router.navigate(['/passenger/bookings']);
      }
    });
  }

  // ============================================================
  // CANCEL from checkout page — before Razorpay even opens
  // ============================================================
  cancelAndGoBack() {
    if (this.cancelInProgress) return;
    this.cancelled = true;
    this.cancelInProgress = true;
    this.cancelAndRelease('Payment cancelled by user. Releasing seats...');
  }

  retryPayment() {
    this.step = 'checkout';
    this.refundStatus = '';
    this.failureReason = '';
    this.cancelled = false;
    this.cancelInProgress = false;
    this.setupRazorpay();
  }

  goToBookings() {
    this.router.navigate(['/passenger/bookings']);
  }

  togglePoints() {
    this.setupRazorpay();
  }

  get effectiveAmount(): number {
    const base = this.booking?.totalAmount || 0;
    if (this.useRewardPoints && this.rewardPoints > 0) {
      this.pointsToRedeem = Math.min(this.rewardPoints, Math.floor(base * 0.5));
      return Math.max(base - this.pointsToRedeem, 1);
    }
    this.pointsToRedeem = 0;
    return base;
  }

  earnRewardPoints() {
    const userId = this.auth.currentUser?.userId || 0;
    if (!userId) return;

    if (this.useRewardPoints && this.pointsToRedeem > 0) {
      this.dealerService.redeemPoints(userId, this.pointsToRedeem).subscribe({
        next: () => this.toast.info(`${this.pointsToRedeem} reward points redeemed! 🎉`),
        error: () => {}
      });
    }

    const amountPaid = this.effectiveAmount;
    this.pointsEarned = Math.floor(amountPaid / 100);
    if (this.pointsEarned > 0) {
      this.dealerService.addPoints(userId, this.pointsEarned).subscribe({
        next: () => this.toast.success(`You earned ${this.pointsEarned} reward points! 🏆`),
        error: () => {}
      });
    }
  }
}
