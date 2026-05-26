import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { OperationsService, BookingService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-staff',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './staff-dashboard.component.html',
  styleUrl: './staff-dashboard.component.css'
})
export class StaffDashboardComponent implements OnInit {
  tab = 'issues';

  // Customer Support
  issues: any[] = [];
  issuesLoading = false;

  // Boarding Gate
  boardingPnr = '';
  boardingLoading = false;
  boardingResult: any = null;
  boardedList: any[] = [];

  constructor(
    private opsService: OperationsService,
    private bookingService: BookingService,
    private toast: ToastService,
    public auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadIssues();
  }

  setTab(t: string) {
    this.tab = t;
    if (t === 'issues') this.loadIssues();
  }

  // ── Customer Support ─────────────────────────────────
  loadIssues() {
    this.issuesLoading = true;
    this.opsService.getIssues().subscribe({
      next: (i: any[]) => {
        this.issues = i;
        this.issuesLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.issuesLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  resolveIssue(id: number) {
    this.opsService.resolveIssue(id).subscribe({
      next: () => {
        this.toast.success('Issue resolved!');
        this.loadIssues();
      }
    });
  }

  replyToIssue(issue: any) {
    if (!issue._replyText) return;
    issue._submitting = true;
    this.opsService.replyIssue(issue.issueId, issue._replyText).subscribe({
      next: () => {
        this.toast.success('Reply sent & Issue resolved!');
        issue._submitting = false;
        this.loadIssues();
      },
      error: () => {
        this.toast.error('Failed to send reply');
        issue._submitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Boarding Gate ────────────────────────────────────
  boardPassengers() {
    if (!this.boardingPnr) {
      this.toast.warning('Please enter a PNR');
      return;
    }

    this.boardingLoading = true;
    this.boardingResult = null;
    const pnr = this.boardingPnr.toUpperCase();

    // Step 1: Fetch booking to get passenger details
    this.bookingService.getByPnr(pnr).subscribe({
      next: (booking) => {
        if (!booking || !booking.passengers || booking.passengers.length === 0) {
          this.boardingResult = { success: false, message: 'No passengers found for this PNR.' };
          this.boardingLoading = false;
          this.cdr.detectChanges();
          return;
        }

        // Step 2: Get check-in status to find boarding passes
        this.opsService.getCheckInStatus(pnr).subscribe({
          next: (checkins: any[]) => {
            if (!checkins || checkins.length === 0) {
              this.boardingResult = { success: false, message: 'No check-in found. Passengers must check in before boarding.' };
              this.boardingLoading = false;
              this.cdr.detectChanges();
              return;
            }

            // Build passenger list with seat/gate info from check-in data
            const boardedPassengers = checkins.map((ci: any) => {
              const passengerInfo = booking.passengers.find((p: any) => p.passengerId === ci.passengerId);
              return {
                passengerId: ci.passengerId,
                name: passengerInfo?.name || `Passenger #${ci.passengerId}`,
                seatNo: ci.seatNo || 'N/A',
                gate: ci.gate || 'N/A',
                email: passengerInfo?.email || booking.email || ''
              };
            });

            // All passengers boarded successfully
            this.boardingResult = {
              success: true,
              message: `PNR ${pnr} — ${boardedPassengers.length} passenger(s) boarded.`,
              passengers: boardedPassengers,
              notificationSent: false
            };

            // Add to boarded history
            this.boardedList.unshift({
              pnr: pnr,
              passengerCount: boardedPassengers.length,
              scannedAt: new Date().toISOString()
            });

            // Step 3: Send notification to passenger email
            const passengerEmail = booking.email || boardedPassengers.find((p: any) => p.email)?.email;
            if (passengerEmail) {
              const seatList = boardedPassengers.map((p: any) => `${p.name}: Seat ${p.seatNo}, Gate ${p.gate}`).join('\n');
              this.opsService.sendNotification({
                email: passengerEmail,
                subject: `Boarding Confirmed — PNR: ${pnr}`,
                message: `Your boarding has been confirmed at the gate.\n\n${seatList}\n\nHave a safe flight! ✈️`
              }).subscribe({
                next: () => {
                  this.boardingResult.notificationSent = true;
                  this.toast.success('Passenger boarded & notification sent!');
                  this.cdr.detectChanges();
                },
                error: () => {
                  this.boardingResult.notificationSent = false;
                  this.toast.success('Passenger boarded! (Notification delivery pending)');
                  this.cdr.detectChanges();
                }
              });
            } else {
              this.toast.success('Passenger boarded successfully!');
            }

            this.boardingLoading = false;
            this.boardingPnr = '';
            this.cdr.detectChanges();
          },
          error: () => {
            this.boardingResult = { success: false, message: 'Failed to retrieve check-in status for this PNR.' };
            this.boardingLoading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: () => {
        this.boardingResult = { success: false, message: 'PNR not found in the system.' };
        this.boardingLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
