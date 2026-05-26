import { Component, OnInit, ChangeDetectorRef, Pipe, PipeTransform } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { FlightService, OperationsService } from '../../core/services/api.service';

import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';
import { RupeeCurrencyPipe } from '../../shared/pipes/rupee-currency/rupee-currency-pipe';

@Pipe({
  name: 'tzDate',
  standalone: true
})
export class TzDatePipe implements PipeTransform {
  transform(value: string | Date, format: 'time' | 'date', timeZone: string): string {
    if (!value) return '';
    let isoStr = typeof value === 'string' ? value : (value as Date).toISOString();
    if (!isoStr.endsWith('Z')) {
      isoStr += 'Z';
    }
    const date = new Date(isoStr);
    try {
      if (format === 'time') {
        return new Intl.DateTimeFormat('en-GB', {
          timeZone,
          hour: '2-digit',
          minute: '2-digit',
          hour12: false
        }).format(date);
      } else {
        const parts = new Intl.DateTimeFormat('en-GB', {
          timeZone,
          day: '2-digit',
          month: 'short'
        }).formatToParts(date);
        const day = parts.find(p => p.type === 'day')?.value || '';
        const month = parts.find(p => p.type === 'month')?.value || '';
        return `${day} ${month}`;
      }
    } catch {
      return '';
    }
  }
}

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, RupeeCurrencyPipe, TzDatePipe],
  template: `
    <!-- Fullscreen Background Image -->
    <div class="image-bg">
      <img src="assets/images/fleet-map.png" alt="World Map Background" class="image-el" />
      <div class="image-overlay"></div>
    </div>

    <!-- Top Navigation Shell -->
    <nav class="fixed top-0 w-full z-50 bg-[#131313]/60 backdrop-blur-xl flex justify-between items-center px-8 py-4 shadow-[0_4px_30px_rgba(0,0,0,0.5)]">
        <div class="font-serif italic text-2xl text-[#f2ca50] cursor-pointer" routerLink="/">SkyHorizon</div>
        <div class="hidden md:flex items-center space-x-8">
            <a class="font-serif tracking-tight text-lg text-[#f2ca50] border-b border-[#f2ca50]/50 pb-1 cursor-pointer" routerLink="/passenger/search">Fleet</a>
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/passenger/bookings">Itinerary</a>
        </div>
        <div class="flex items-center gap-6 relative">
            <button (click)="toggleNotifications()" class="relative text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300">
                <span class="material-symbols-outlined">notifications</span>
                <span *ngIf="unreadCount > 0" class="absolute -top-1 -right-1 bg-error text-white text-[10px] w-4 h-4 rounded-full flex items-center justify-center font-bold">{{ unreadCount }}</span>
            </button>
            <ng-container *ngIf="auth.isLoggedIn; else loggedOutNav">
              <button class="text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300" routerLink="/passenger/profile">
                  <span class="material-symbols-outlined">account_circle</span>
              </button>
              <button (click)="doLogout()" class="text-error/70 hover:text-error transition-all duration-300">
                  <span class="material-symbols-outlined">logout</span>
              </button>
            </ng-container>
            <ng-template #loggedOutNav>
              <button class="px-4 py-2 text-xs font-bold text-primary border border-primary/30 rounded-full hover:bg-primary/10 transition-colors" routerLink="/login">LOGIN</button>
              <button class="px-4 py-2 text-xs font-bold text-on-primary bg-primary rounded-full hover:shadow-[0_0_15px_rgba(242,202,80,0.3)] transition-all" routerLink="/register">SIGN UP</button>
            </ng-template>

            <!-- Notifications Dropdown -->
            <div *ngIf="showNotifications" class="absolute top-[120%] right-10 w-80 bg-surface-container-high border border-outline-variant/20 rounded-xl shadow-2xl z-50 overflow-hidden backdrop-blur-xl animate-fadeIn text-left">
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

    <main class="pt-16 pb-12 px-4 md:px-8 max-w-7xl mx-auto min-h-screen">
      <!-- Header Section -->
      <header class="mb-4">
        <div class="flex items-center justify-between">
          <div class="flex items-baseline gap-3">
            <h1 class="font-headline text-3xl tracking-tight text-on-surface">
              SkyHorizon <span class="italic text-primary">Flights</span>
            </h1>
            <p class="text-secondary/60 text-sm hidden md:block">
              {{ flights.length > 0 ? flights.length + ' flights found' : 'Search all destinations' }}
            </p>
          </div>
          
          <!-- Timezone Switcher -->
          <div class="relative group" tabindex="0">
            <button (click)="showTimezones = !showTimezones" class="flex items-center gap-2 px-4 py-2 rounded-full border border-primary/20 bg-surface-container-low hover:bg-primary/10 transition-all text-primary shadow-[0_0_15px_rgba(242,202,80,0.1)]">
              🌍 <span class="font-bold text-xs uppercase tracking-widest">{{ getTimezoneLabel(selectedTimeZone) }} ▼</span>
            </button>
            <div *ngIf="showTimezones" class="absolute right-0 mt-2 w-48 bg-surface-container-high border border-outline-variant/20 rounded-xl shadow-2xl z-50 overflow-hidden backdrop-blur-xl animate-fadeIn">
               <button *ngFor="let tz of getAvailableTimezones()" (click)="selectTimezone(tz)" class="w-full text-left px-4 py-3 text-[11px] font-bold hover:bg-primary/10 transition-colors uppercase tracking-widest text-on-surface border-b border-outline-variant/5 last:border-0" [class.bg-primary]="tz === selectedTimeZone" [class.text-on-primary]="tz === selectedTimeZone">
                  {{ getTimezoneLabel(tz) }}
               </button>
            </div>
          </div>
        </div>
      </header>

      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        <!-- Filters Sidebar -->
        <aside class="lg:col-span-3 space-y-8 sticky top-28">
          <div class="space-y-6">
            <div>
              <h3 class="font-headline text-xl text-secondary mb-4">Flight Search</h3>
              <div class="h-px w-full bg-gradient-to-r from-outline-variant/30 to-transparent"></div>
            </div>

            <!-- Trip Type Toggle -->
            <div class="flex bg-surface-container-low rounded-lg p-1 border border-outline-variant/10">
              <button (click)="tripType = 'oneway'" class="flex-1 py-2.5 text-xs font-bold uppercase tracking-widest rounded-md transition-all duration-300"
                      [class]="tripType === 'oneway' ? 'bg-primary text-[#1a1400] shadow-lg' : 'text-secondary/60 hover:text-secondary'">
                One Way
              </button>
              <button (click)="tripType = 'roundtrip'" class="flex-1 py-2.5 text-xs font-bold uppercase tracking-widest rounded-md transition-all duration-300"
                      [class]="tripType === 'roundtrip' ? 'bg-primary text-[#1a1400] shadow-lg' : 'text-secondary/60 hover:text-secondary'">
                Round Trip
              </button>
            </div>

            <!-- Search Fields -->
            <div class="space-y-4">
              <div class="space-y-2">
                <label class="font-label text-[0.6875rem] uppercase tracking-[0.1em] text-secondary/50">From</label>
                <input [(ngModel)]="from" class="w-full bg-surface-container-low border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-3 placeholder:text-outline/30" placeholder="Source (e.g. DEL)" />
              </div>

              <!-- Swap Button -->
              <div class="flex justify-center">
                <button (click)="swap()" class="w-8 h-8 rounded-full bg-surface-container-low border border-primary/20 flex items-center justify-center text-primary hover:bg-primary/10 transition-all hover:rotate-180 duration-300">
                  <span class="material-symbols-outlined text-sm">swap_vert</span>
                </button>
              </div>

              <div class="space-y-2">
                <label class="font-label text-[0.6875rem] uppercase tracking-[0.1em] text-secondary/50">To</label>
                <input [(ngModel)]="to" class="w-full bg-surface-container-low border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-3 placeholder:text-outline/30" placeholder="Destination (e.g. BOM)" />
              </div>

              <div class="space-y-2">
                <label class="font-label text-[0.6875rem] uppercase tracking-[0.1em] text-secondary/50">Departure</label>
                <input type="date" [(ngModel)]="date" class="w-full bg-surface-container-low border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-3" />
              </div>

              <!-- Return Date (only for round trip) -->
              @if (tripType === 'roundtrip') {
                <div class="space-y-2 animate-fadeIn">
                  <label class="font-label text-[0.6875rem] uppercase tracking-[0.1em] text-secondary/50">Return</label>
                  <input type="date" [(ngModel)]="returnDate" class="w-full bg-surface-container-low border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-3" />
                </div>
              }

              <!-- Passengers -->
              <div class="space-y-2">
                <label class="font-label text-[0.6875rem] uppercase tracking-[0.1em] text-secondary/50">Passengers</label>
                <div class="flex items-center gap-3">
                  <button (click)="passengers = Math.max(1, passengers - 1)" class="w-10 h-10 rounded-lg bg-surface-container-low border border-outline-variant/20 flex items-center justify-center text-secondary hover:text-primary hover:border-primary/30 transition-all">
                    <span class="material-symbols-outlined text-sm">remove</span>
                  </button>
                  <span class="flex-1 text-center text-lg font-bold text-on-surface">{{ passengers }}</span>
                  <button (click)="passengers = Math.min(9, passengers + 1)" class="w-10 h-10 rounded-lg bg-surface-container-low border border-outline-variant/20 flex items-center justify-center text-secondary hover:text-primary hover:border-primary/30 transition-all">
                    <span class="material-symbols-outlined text-sm">add</span>
                  </button>
                </div>
              </div>

              <button (click)="search()" class="w-full py-3 primary-gradient text-on-primary font-bold text-xs tracking-widest uppercase rounded-lg metallic-glow transition-all flex items-center justify-center gap-2">
                <span class="material-symbols-outlined text-sm">search</span> Search Flights
              </button>
              <button (click)="loadAll()" class="w-full py-3 bg-surface-container-low border border-primary/20 text-secondary font-bold text-xs tracking-widest uppercase rounded-lg hover:bg-primary/5 transition-all">
                Show All Flights
              </button>
            </div>

            <!-- Quick Stats -->
            <div *ngIf="flights.length > 0" class="space-y-3">
              <div class="h-px w-full bg-gradient-to-r from-outline-variant/30 to-transparent"></div>
              <div class="flex items-center justify-between p-3 rounded-lg bg-surface-container-low border border-outline-variant/5">
                <span class="text-sm font-medium text-on-surface-variant">Results</span>
                <span class="text-xs text-primary font-bold">{{ flights.length }}</span>
              </div>
              <div class="flex items-center justify-between p-3 rounded-lg bg-surface-container-low border border-outline-variant/5">
                <span class="text-sm font-medium text-on-surface-variant">Trip Type</span>
                <span class="text-xs text-primary font-bold uppercase">{{ tripType === 'roundtrip' ? 'Round Trip' : 'One Way' }}</span>
              </div>
            </div>
          </div>
        </aside>

        <!-- Results Feed -->
        <div class="lg:col-span-9 space-y-6">
          <!-- Loading State -->
          <div *ngIf="loading" class="flex flex-col items-center justify-center py-20">
            <div class="w-12 h-12 border-2 border-primary/20 border-t-primary rounded-full animate-spin mb-4"></div>
            <p class="text-secondary/60 text-sm">Searching flights...</p>
          </div>

          <!-- Error State -->
          <div *ngIf="!loading && errorMessage" class="champagne-glass border-l-4 border-error p-8 text-center rounded-xl">
            <span class="material-symbols-outlined text-error text-4xl mb-4 block">error</span>
            <h3 class="text-xl font-headline text-on-surface mb-2">{{ errorMessage }}</h3>
            <p class="text-secondary/60 text-sm mb-4">Make sure the backend services are running</p>
            <button (click)="loadAll()" class="px-6 py-3 primary-gradient text-on-primary font-bold text-xs tracking-widest uppercase rounded-lg metallic-glow transition-all">Retry</button>
          </div>

          <!-- Empty State -->
          <div *ngIf="!loading && !errorMessage && flights.length === 0 && searched" class="text-center py-20">
            <span class="material-symbols-outlined text-secondary/30 mb-4 block" style="font-size:64px">flight</span>
            <h3 class="font-headline text-2xl text-on-surface mb-2">No flights found</h3>
            <p class="text-secondary/60 text-sm">Try different dates or destinations</p>
          </div>

          <!-- Initial State -->
          <div *ngIf="!loading && !errorMessage && flights.length === 0 && !searched" class="text-center py-20">
            <span class="material-symbols-outlined text-primary/30 mb-4 block" style="font-size:64px">travel_explore</span>
            <h3 class="font-headline text-2xl text-on-surface mb-2">Discover Your Journey</h3>
            <p class="text-secondary/60 text-sm mb-6">Use the search panel or load all available flights</p>
          </div>

          <!-- Urgency Banner -->
          <div *ngIf="flights.length > 0 || returnFlights.length > 0" class="champagne-glass border-l-4 border-primary p-4 flex items-center justify-between">
            <div class="flex items-center gap-4">
              <span class="material-symbols-outlined text-primary">bolt</span>
              <p class="text-sm text-on-surface">
                <span class="font-bold">{{ flights.length + returnFlights.length }} routes</span> available. Prices may change with demand.
              </p>
            </div>
            <span class="font-label text-[0.6875rem] text-primary font-bold uppercase tracking-widest hidden md:block">Live Pricing</span>
          </div>

          <!-- Outbound Label -->
          <div *ngIf="tripType === 'roundtrip' && flights.length > 0" class="flex items-center gap-3 mt-2">
            <span class="material-symbols-outlined text-primary text-lg">flight_takeoff</span>
            <h3 class="font-headline text-lg text-on-surface">Outbound — {{ from || '...' }} → {{ to || '...' }}</h3>
          </div>

          <!-- Flight Cards -->
          <ng-container *ngFor="let flight of flights; let i = index">
            <div class="bg-surface-container-low rounded-xl overflow-hidden group hover:shadow-2xl transition-all duration-500 border border-outline-variant/5 hover:border-primary/20">
              <div class="p-6 md:p-8">
                <div class="grid grid-cols-1 md:grid-cols-12 gap-6 items-center">
                  <!-- Carrier Info -->
                  <div class="md:col-span-3 flex flex-col gap-3">
                    <div class="flex items-center gap-3">
                      <div class="w-10 h-10 rounded-full bg-surface-container-highest flex items-center justify-center border border-outline-variant/20">
                        <span class="material-symbols-outlined text-primary text-xl">airlines</span>
                      </div>
                      <div>
                        <h4 class="font-bold text-sm text-on-surface">{{ flight.airline || 'SkyHorizon' }}</h4>
                        <p class="text-xs text-secondary/60">{{ flight.flightNumber }}</p>
                      </div>
                    </div>
                    <div class="flex flex-wrap gap-2">
                      <span class="px-2 py-1 bg-primary/10 text-primary text-[10px] uppercase font-bold tracking-tighter rounded">Direct</span>
                      <span class="px-2 py-1 bg-secondary/10 text-secondary text-[10px] uppercase font-bold tracking-tighter rounded">{{ flight.aircraftType || 'Standard' }}</span>
                    </div>
                  </div>

                  <!-- Flight Details -->
                  <div class="md:col-span-5">
                    <div class="flex justify-between items-center relative">
                      <div class="text-center">
                        <p class="font-headline text-2xl md:text-3xl text-on-surface">{{ flight.departureTime | tzDate:'time':selectedTimeZone }}</p>
                        <p class="text-[9px] text-primary/50 font-mono">{{ flight.departureTime | tzDate:'time':'UTC' }} UTC</p>
                        <p class="font-label text-[0.6875rem] text-secondary/50 uppercase tracking-widest">{{ flight.source }}</p>
                      </div>
                      <div class="flex-1 px-4 md:px-6 flex flex-col items-center">
                        <p class="text-[10px] text-secondary/40 font-bold mb-1 uppercase tracking-widest">{{ flight.departureTime | tzDate:'date':selectedTimeZone }}</p>
                        <p class="text-[9px] text-primary/60 font-bold mb-1">{{ getFlightDuration(flight) }}</p>
                        <div class="w-full h-[1px] bg-outline-variant/30 relative">
                          <div class="absolute -top-1 right-0">
                            <span class="material-symbols-outlined text-primary text-xs">flight_takeoff</span>
                          </div>
                        </div>
                      </div>
                      <div class="text-center">
                        <p class="font-headline text-2xl md:text-3xl text-on-surface">{{ flight.arrivalTime | tzDate:'time':selectedTimeZone }}</p>
                        <p class="text-[9px] text-primary/50 font-mono">{{ flight.arrivalTime | tzDate:'time':'UTC' }} UTC</p>
                        <p class="font-label text-[0.6875rem] text-secondary/50 uppercase tracking-widest">{{ flight.destination }}</p>
                      </div>
                    </div>
                  </div>

                  <!-- Pricing & Action -->
                  <div class="md:col-span-4 text-right flex flex-col justify-between h-full md:border-l md:border-outline-variant/10 md:pl-6">
                    <div>
                      <p *ngIf="flight.availableSeats <= 10" class="text-error font-label text-[0.6875rem] uppercase font-bold mb-1">
                        Only {{ flight.availableSeats }} seats left
                      </p>
                      <p class="text-[10px] text-secondary/60 uppercase tracking-widest font-bold mb-1">Starting From</p>
                      <h3 class="text-2xl md:text-3xl font-headline text-primary">{{ flight.startingPrice || flight.price | rupeeCurrency }}</h3>
                      <p class="text-[10px] text-secondary/40 uppercase tracking-widest">per person</p>
                    </div>
                    <button (click)="selectFlight(flight)" [disabled]="flight.availableSeats <= 0"
                        class="mt-4 w-full py-3 md:py-4 primary-gradient text-on-primary font-bold text-sm tracking-widest uppercase metallic-glow transition-all disabled:opacity-40 disabled:cursor-not-allowed rounded-lg">
                      {{ flight.availableSeats > 0 ? 'Reserve' : 'Sold Out' }}
                    </button>
                  </div>
                </div>
              </div>
              <!-- Card Footer -->
              <div class="bg-surface-container-lowest px-6 md:px-8 py-3 flex justify-between items-center">
                <div class="flex gap-6">
                  <span class="text-[10px] text-secondary/40 flex items-center gap-1">
                    <span class="material-symbols-outlined text-[14px]">event_seat</span> {{ flight.availableSeats }} / {{ flight.totalSeats }} seats
                  </span>
                  <span class="text-[10px] text-secondary/40 flex items-center gap-1">
                    <span class="material-symbols-outlined text-[14px]">verified</span> {{ flight.status }}
                  </span>
                </div>
                <span class="text-[10px] text-primary/60 font-bold uppercase tracking-widest">{{ flight.gateNumber !== 'TBA' ? 'Gate ' + flight.gateNumber : '' }}</span>
              </div>
            </div>
          </ng-container>

          <!-- Return Flights Section -->
          <ng-container *ngIf="tripType === 'roundtrip' && returnFlights.length > 0">
            <div class="flex items-center gap-3 mt-6 mb-2">
              <span class="material-symbols-outlined text-primary text-lg">flight_land</span>
              <h3 class="font-headline text-lg text-on-surface">Return — {{ to || '...' }} → {{ from || '...' }}</h3>
            </div>

            <ng-container *ngFor="let flight of returnFlights; let i = index">
              <div class="bg-surface-container-low rounded-xl overflow-hidden group hover:shadow-2xl transition-all duration-500 border border-outline-variant/5 hover:border-primary/20">
                <div class="p-6 md:p-8">
                  <div class="grid grid-cols-1 md:grid-cols-12 gap-6 items-center">
                    <div class="md:col-span-3">
                      <div class="flex items-center gap-3 mb-3">
                        <div class="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
                          <span class="material-symbols-outlined text-primary text-sm">airlines</span>
                        </div>
                        <div>
                          <h4 class="font-bold text-on-surface text-sm">SkyHorizon</h4>
                          <p class="text-[11px] text-secondary/50 font-mono">{{ flight.flightNumber || ('SKY' + flight.flightId) }}</p>
                        </div>
                      </div>
                      <div class="flex gap-2">
                        <span class="px-2 py-0.5 text-[10px] font-bold rounded bg-primary/10 text-primary uppercase tracking-widest">Direct</span>
                        <span class="px-2 py-0.5 text-[10px] font-bold rounded bg-surface-container text-secondary/60 uppercase tracking-widest">{{ flight.class || 'Standard' }}</span>
                      </div>
                    </div>
                    <div class="md:col-span-5">
                      <div class="flex items-center justify-between">
                        <div class="text-center">
                          <p class="text-2xl font-headline text-on-surface">{{ flight.departureTime | tzDate:'time':selectedTimeZone }}</p>
                          <p class="text-[9px] text-primary/50 font-mono">{{ flight.departureTime | tzDate:'time':'UTC' }} UTC</p>
                          <p class="text-[11px] text-secondary/50 uppercase tracking-widest">{{ flight.source }}</p>
                        </div>
                        <div class="flex-1 px-4 flex flex-col items-center">
                          <p class="text-[10px] text-secondary/40 mb-1">{{ flight.departureTime | tzDate:'date':selectedTimeZone }}</p>
                          <p class="text-[9px] text-primary/60 font-bold mb-1">{{ getFlightDuration(flight) }}</p>
                          <div class="w-full h-px bg-gradient-to-r from-transparent via-primary/40 to-transparent relative">
                            <span class="material-symbols-outlined text-primary text-sm absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 bg-surface-container-low px-1">flight_land</span>
                          </div>
                        </div>
                        <div class="text-center">
                          <p class="text-2xl font-headline text-on-surface">{{ flight.arrivalTime ? (flight.arrivalTime | tzDate:'time':selectedTimeZone) : (flight.departureTime | tzDate:'time':selectedTimeZone) }}</p>
                          <p class="text-[9px] text-primary/50 font-mono">{{ (flight.arrivalTime || flight.departureTime) | tzDate:'time':'UTC' }} UTC</p>
                          <p class="text-[11px] text-secondary/50 uppercase tracking-widest">{{ flight.destination }}</p>
                        </div>
                      </div>
                    </div>
                    <div class="md:col-span-4 text-right">
                      <p class="text-[10px] text-secondary/60 uppercase tracking-widest font-bold mb-1">Starting From</p>
                      <p class="text-3xl font-headline text-primary">{{ (flight.startingPrice || flight.price || flight.basePrice) | rupeeCurrency }}</p>
                      <p class="text-[10px] text-secondary/40 font-label uppercase tracking-widest mb-3">Per Person</p>
                      <button (click)="selectFlight(flight)" class="w-full md:w-auto px-8 py-3 primary-gradient text-on-primary font-bold text-xs tracking-widest uppercase rounded-lg metallic-glow transition-all">Reserve</button>
                    </div>
                  </div>
                </div>
                <div class="border-t border-outline-variant/5 px-6 md:px-8 py-3 flex items-center justify-between text-[11px] text-secondary/40">
                  <div class="flex items-center gap-4">
                    <span class="flex items-center gap-1"><span class="material-symbols-outlined text-[14px]">event_seat</span> {{ flight.availableSeats }} seats</span>
                    <span class="flex items-center gap-1"><span class="material-symbols-outlined text-[14px]">schedule</span> {{ flight.status || 'Scheduled' }}</span>
                  </div>
                  <span class="text-primary/60 font-bold uppercase tracking-widest">Gate {{ flight.gate || 'TBD' }}</span>
                </div>
              </div>
            </ng-container>
          </ng-container>

        </div>
      </div>
    </main>


    <!-- Footer -->
    <footer class="w-full py-8 px-8 border-t border-[#f2ca50]/15 flex flex-col md:flex-row justify-between items-center bg-[#0e0e0e] gap-4">
        <div class="flex items-center gap-6">
            <span class="text-lg font-serif text-[#f2ca50]">SkyHorizon</span>
            <span class="text-[10px] font-label text-[#d6c692]/60 tracking-wide uppercase">© 2024 The Celestial Concierge.</span>
        </div>
        <div class="flex gap-8">
            <a class="text-[#d6c692]/60 hover:text-[#f2ca50] text-[10px] font-label uppercase tracking-widest transition-all cursor-pointer">Privacy Policy</a>
            <a class="text-[#d6c692]/60 hover:text-[#f2ca50] text-[10px] font-label uppercase tracking-widest transition-all cursor-pointer">System Status</a>
        </div>
    </footer>
  `,
  styles: [`
    :host { display: block; background: #131313; position: relative; }

    .image-bg {
      position: fixed;
      inset: 0;
      z-index: 0;
      overflow: hidden;
    }
    .image-el {
      width: 100%;
      height: 100%;
      object-fit: cover;
      opacity: 0.5;
    }
    .image-overlay {
      position: absolute;
      inset: 0;
      background: linear-gradient(
        180deg,
        rgba(8, 15, 35, 0.70) 0%,
        rgba(10, 18, 42, 0.35) 40%,
        rgba(8, 15, 35, 0.60) 100%
      );
    }

    nav, main, footer {
      position: relative;
      z-index: 1;
    }
  `]
})
export class SearchComponent implements OnInit {

  from = '';
  to = '';
  date = '';
  returnDate = '';
  tripType: 'oneway' | 'roundtrip' = 'oneway';
  passengers = 1;
  flights: any[] = [];
  returnFlights: any[] = [];
  loading = false;
  searched = false;
  errorMessage = '';
  Math = Math;

  // Timezone Switcher Logic
  selectedTimeZone = 'UTC';
  showTimezones = false;
  availableTimezones = new Set<string>();
  
  private knownTimezoneLabels: Record<string, string> = {
    'UTC': 'UTC',
    'Asia/Kolkata': 'IST (India)',
    'Asia/Dubai': 'Dubai (GST)',
    'America/New_York': 'New York (EST)',
    'Europe/London': 'London (GMT)',
    'Asia/Singapore': 'Singapore (SGT)',
    'Asia/Tokyo': 'Tokyo (JST)',
    'Australia/Sydney': 'Sydney (AEST)',
    'America/Los_Angeles': 'Los Angeles (PST)',
    'America/Chicago': 'Chicago (CST)',
    'Europe/Paris': 'Paris (CET)',
    'Europe/Berlin': 'Frankfurt (CET)',
    'Asia/Hong_Kong': 'Hong Kong (HKT)',
    'Asia/Bangkok': 'Bangkok (ICT)',
    'Asia/Kuala_Lumpur': 'Kuala Lumpur (MYT)',
    'Asia/Qatar': 'Doha (AST)',
    'Asia/Riyadh': 'Riyadh (AST)',
    'America/Toronto': 'Toronto (EST)',
  };

  // Notifications
  showNotifications = false;
  get notifications() { return this.notifService.getNotifications(); }
  get unreadCount() { return this.notifService.getUnreadCount(); }



  constructor(
    private flightService: FlightService,
    private opsService: OperationsService,
    public auth: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService
  ) { }

  toggleNotifications() {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) {
      this.notifService.markAllRead();
    }
  }

  // extract timezones dynamically from fetched flights
  private extractTimezones(flightsArr: any[]) {
    // Only map the ones extracted from flights
    flightsArr.forEach(f => {
      if (f.sourceTimeZone) this.availableTimezones.add(f.sourceTimeZone);
      if (f.destinationTimeZone) this.availableTimezones.add(f.destinationTimeZone);
    });
  }

  private mapAirportsToTimezones(airports: any[]) {
    this.availableTimezones.add('UTC');
    const mapping: Record<string, string> = {
      // Country-level
      'INDIA': 'Asia/Kolkata',
      'UAE': 'Asia/Dubai',
      'UNITED ARAB EMIRATES': 'Asia/Dubai',
      'USA': 'America/New_York',
      'UNITED STATES': 'America/New_York',
      'UK': 'Europe/London',
      'UNITED KINGDOM': 'Europe/London',
      'SINGAPORE': 'Asia/Singapore',
      'JAPAN': 'Asia/Tokyo',
      'AUSTRALIA': 'Australia/Sydney',
      // City-level
      'DUBAI': 'Asia/Dubai',
      'ABU DHABI': 'Asia/Dubai',
      'NEW DELHI': 'Asia/Kolkata',
      'MUMBAI': 'Asia/Kolkata',
      'DELHI': 'Asia/Kolkata',
      'KOLKATA': 'Asia/Kolkata',
      'CHENNAI': 'Asia/Kolkata',
      'BANGALORE': 'Asia/Kolkata',
      'HYDERABAD': 'Asia/Kolkata',
      'NEW YORK': 'America/New_York',
      'LOS ANGELES': 'America/Los_Angeles',
      'CHICAGO': 'America/Chicago',
      'LONDON': 'Europe/London',
      'PARIS': 'Europe/Paris',
      'FRANKFURT': 'Europe/Berlin',
      'TOKYO': 'Asia/Tokyo',
      'SYDNEY': 'Australia/Sydney',
      'HONG KONG': 'Asia/Hong_Kong',
      'BANGKOK': 'Asia/Bangkok',
      'KUALA LUMPUR': 'Asia/Kuala_Lumpur',
      'DOHA': 'Asia/Qatar',
      'RIYADH': 'Asia/Riyadh',
      'TORONTO': 'America/Toronto',
    };

    airports.forEach(a => {
      const city = (a.city || '').toUpperCase();
      const country = (a.country || '').toUpperCase();
      if (mapping[city]) this.availableTimezones.add(mapping[city]);
      else if (mapping[country]) this.availableTimezones.add(mapping[country]);
    });
  }

  getAvailableTimezones(): string[] {
    return Array.from(this.availableTimezones);
  }

  getTimezoneLabel(tz: string): string {
    return this.knownTimezoneLabels[tz] || tz;
  }

  selectTimezone(tz: string): void {
    this.selectedTimeZone = tz;
    this.showTimezones = false;
  }



  ngOnInit() {
    this.availableTimezones.add('UTC'); // Ensure UTC exists
    // Auto-map admin-added airports to time zones dynamically!
    this.flightService.getAirports().subscribe({
      next: (airports) => this.mapAirportsToTimezones(airports),
      error: () => {}
    });

    this.notifService.loadServerNotifications();
    this.route.queryParams.subscribe(params => {
      if (params['from']) this.from = params['from'];
      if (params['to']) this.to = params['to'];
      if (params['date']) this.date = params['date'];
      if (this.from || this.to) this.search();
    });
  }

  loadAll() {
    this.loading = true;
    this.errorMessage = '';
    this.returnFlights = [];
    this.flightService.getAll().subscribe({
      next: (data) => {
        let allFlights = data || [];
        const now = new Date().getTime();
        allFlights = allFlights.filter(f => new Date(f.departureTime).getTime() > now);

        if (this.tripType === 'roundtrip') {
          // Only show flights that have a reverse route available
          allFlights = allFlights.filter(f =>
            allFlights.some(r => r.source === f.destination && r.destination === f.source)
          );
        }
        this.flights = allFlights;
        this.extractTimezones(allFlights);
        this.loading = false;
        this.searched = true;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.searched = true;
        if (err.status === 404) {
          this.flights = [];
        } else {
          this.errorMessage = 'Cannot connect to server';
          this.toast.error('Backend services are not running');
        }
        this.cdr.detectChanges();
      }
    });
  }

  search() {
    if (!this.from?.trim() && !this.to?.trim() && !this.date) {
      this.toast.warning('Please enter flight details to search');
      this.flights = [];
      this.returnFlights = [];
      this.searched = false;
      return;
    }

    this.loading = true;
    this.searched = true;
    this.errorMessage = '';
    this.returnFlights = [];

    const outbound$ = this.flightService.search({ source: this.from, destination: this.to, date: this.date }).pipe(
      catchError(err => {
        if (err.status !== 404) {
          this.errorMessage = 'Cannot connect to server';
          this.toast.error('Backend services are not running');
        }
        return of([]);
      })
    );

    if (this.tripType === 'roundtrip' && this.from && this.to) {
      const return$ = this.flightService.search({ source: this.to, destination: this.from, date: this.returnDate || this.date }).pipe(
        catchError(() => of([]))
      );

      forkJoin({ outbound: outbound$, returnLeg: return$ }).subscribe({
        next: ({ outbound, returnLeg }) => {
          this.flights = outbound || [];
          this.returnFlights = returnLeg || [];

          // Strict Round Trip Validation: Only show if BOTH legs are available
          if (this.flights.length > 0 && this.returnFlights.length === 0) {
            this.flights = []; // Hide outbound if no return is available
            this.toast.warning('No matching return flight found for this round trip.');
          } else if (this.returnFlights.length > 0 && this.flights.length === 0) {
            this.returnFlights = [];
          }

          this.extractTimezones(this.flights);
          this.extractTimezones(this.returnFlights);

          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      outbound$.subscribe({
        next: (data) => {
          this.flights = data || [];
          this.extractTimezones(this.flights);
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  swap() { [this.from, this.to] = [this.to, this.from]; }

  selectFlight(flight: any) {
    if (!this.auth.isLoggedIn) {
      this.toast.warning('Please login to book a flight');
      this.router.navigate(['/login']);
      return;
    }
    this.router.navigate(['/passenger/book', flight.flightId]);
  }

  doLogout() {
    this.auth.logout();
  }

  getFlightDuration(flight: any): string {
    if (!flight.departureTime || !flight.arrivalTime) return '';
    const dep = new Date(flight.departureTime).getTime();
    const arr = new Date(flight.arrivalTime).getTime();
    const diff = arr - dep;
    if (diff <= 0) return '';
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }
}
