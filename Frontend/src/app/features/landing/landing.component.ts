import { Component, OnInit, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { FlightService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { Flight } from '../../core/models';
import { RupeeCurrencyPipe } from '../../shared/pipes/rupee-currency/rupee-currency-pipe';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, RupeeCurrencyPipe],
  template: `
    <!-- Fullscreen Background Video -->
    <div class="video-bg">
      <video #bgVideo autoplay muted loop playsinline class="video-el">
        <source src="bg-video.mp4" type="video/mp4" />
      </video>
      <div class="video-overlay"></div>
    </div>

    <!-- Passenger Top Navigation -->
    <nav class="fixed top-0 w-full z-50 bg-[#131313]/50 backdrop-blur-xl flex justify-between items-center px-8 py-4 shadow-[0_4px_30px_rgba(0,0,0,0.5)]">
        <div class="font-serif italic text-2xl text-[#f2ca50] cursor-pointer" routerLink="/">SkyHorizon</div>
        <div class="hidden md:flex items-center space-x-8">
            <a class="font-serif tracking-tight text-lg text-[#f2ca50] border-b border-[#f2ca50]/50 pb-1 cursor-pointer" routerLink="/">Home</a>
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/search">Fleet</a>
            <a class="font-serif tracking-tight text-lg text-[#d6c692]/70 hover:text-[#f2ca50] transition-colors duration-300 cursor-pointer" routerLink="/passenger/bookings">Itinerary</a>
        </div>
        <div class="flex items-center gap-6">
            <button class="text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300">
                <span class="material-symbols-outlined">notifications</span>
            </button>
            <ng-container *ngIf="auth.isLoggedIn; else loggedOutNav">
              <button class="text-[#d6c692]/70 hover:text-[#f2ca50] transition-all duration-300" routerLink="/passenger/profile">
                  <span class="material-symbols-outlined">account_circle</span>
              </button>
              <button (click)="logout()" class="text-error/70 hover:text-error transition-all duration-300">
                  <span class="material-symbols-outlined">logout</span>
              </button>
            </ng-container>
            <ng-template #loggedOutNav>
              <button class="px-4 py-2 text-xs font-bold text-primary border border-primary/30 rounded-full hover:bg-primary/10 transition-colors" routerLink="/login">LOGIN</button>
              <button class="px-4 py-2 text-xs font-bold text-on-primary bg-primary rounded-full hover:shadow-[0_0_15px_rgba(242,202,80,0.3)] transition-all" routerLink="/register">SIGN UP</button>
            </ng-template>
        </div>
    </nav>

    <!-- Hero Section -->
    <section class="relative min-h-[85vh] flex items-center justify-center overflow-hidden pt-20">

      <div class="relative z-10 text-center max-w-4xl mx-auto px-6">
        <span class="font-label text-[0.6875rem] uppercase tracking-[0.2em] text-secondary/60 mb-4 block">Celestial Travel Experience</span>
        <h1 class="font-headline text-6xl md:text-8xl tracking-tight leading-[0.9] text-on-surface mb-6">
          Midnight<br/><span class="italic text-primary">Horizon</span>
        </h1>
        <p class="text-secondary/80 max-w-lg mx-auto font-light leading-relaxed text-lg mb-10">
          Experience seamless booking, real-time tracking, and premium travel management — all in one celestial platform.
        </p>
        <div class="flex gap-4 justify-center flex-wrap">
          <button (click)="scrollToSearch()" class="px-8 py-4 primary-gradient text-on-primary font-bold text-sm tracking-widest uppercase metallic-glow transition-all rounded-lg">
            <span class="flex items-center gap-2">
              <span class="material-symbols-outlined text-sm">flight_takeoff</span>
              Book Your Journey
            </span>
          </button>
          <button routerLink="/search" class="px-8 py-4 bg-surface-container-low border border-primary/20 text-secondary font-bold text-sm tracking-widest uppercase hover:bg-primary/5 transition-all rounded-lg">
            Explore Fleet
          </button>
        </div>

        <!-- Stats Row -->
        <div class="grid grid-cols-3 gap-6 mt-16 max-w-xl mx-auto">
          <div class="text-center">
            <p class="text-3xl font-headline text-primary">{{ flightCount }}+</p>
            <p class="text-[10px] uppercase tracking-[0.2em] text-secondary/40 mt-1">Active Flights</p>
          </div>
          <div class="text-center">
            <p class="text-3xl font-headline text-primary">{{ routeCount }}</p>
            <p class="text-[10px] uppercase tracking-[0.2em] text-secondary/40 mt-1">Global Routes</p>
          </div>
          <div class="text-center">
            <p class="text-3xl font-headline text-primary">{{ airportCount }}</p>
            <p class="text-[10px] uppercase tracking-[0.2em] text-secondary/40 mt-1">Airports</p>
          </div>
        </div>
      </div>
    </section>

    <!-- Search Form Card -->
    <section id="search-section" class="relative z-20 -mt-12 px-4 md:px-8 max-w-5xl mx-auto">
      <form (ngSubmit)="searchFlights()" class="bg-surface-container-low rounded-2xl p-8 border border-outline-variant/10 shadow-2xl">
        <div class="grid grid-cols-1 md:grid-cols-12 gap-4 items-end">
          <div class="md:col-span-3 space-y-2">
            <label class="text-[10px] uppercase tracking-widest text-[#d6c692]/60 font-bold">From</label>
            <div class="relative">
              <input [(ngModel)]="from" name="from" class="w-full bg-surface-container border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 pl-10 placeholder:text-outline/30" placeholder="Delhi (DEL)" type="text" />
              <span class="material-symbols-outlined absolute left-3 top-3 text-primary/40 text-sm">flight_takeoff</span>
            </div>
          </div>
          <div class="md:col-span-1 flex justify-center">
            <button type="button" (click)="swapCities()" class="w-10 h-10 rounded-full border border-primary/20 bg-surface-container-low flex items-center justify-center text-primary hover:bg-primary/10 transition-all hover:rotate-180 duration-300">
              <span class="material-symbols-outlined text-sm">swap_horiz</span>
            </button>
          </div>
          <div class="md:col-span-3 space-y-2">
            <label class="text-[10px] uppercase tracking-widest text-[#d6c692]/60 font-bold">To</label>
            <div class="relative">
              <input [(ngModel)]="to" name="to" class="w-full bg-surface-container border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 pl-10 placeholder:text-outline/30" placeholder="Mumbai (BOM)" type="text" />
              <span class="material-symbols-outlined absolute left-3 top-3 text-primary/40 text-sm">flight_land</span>
            </div>
          </div>
          <div class="md:col-span-2 space-y-2">
            <label class="text-[10px] uppercase tracking-widest text-[#d6c692]/60 font-bold">Date</label>
            <input [(ngModel)]="date" name="date" type="date" class="w-full bg-surface-container border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-3" />
          </div>
          <div class="md:col-span-1 space-y-2">
            <label class="text-[10px] uppercase tracking-widest text-[#d6c692]/60 font-bold">Pax</label>
            <select [(ngModel)]="passengers" name="passengers" class="w-full bg-surface-container border-none focus:ring-1 focus:ring-primary rounded-lg text-sm text-on-surface py-3 px-2">
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="3">3</option>
              <option value="4">4+</option>
            </select>
          </div>
          <div class="md:col-span-2">
            <button type="submit" class="w-full py-3 primary-gradient text-on-primary font-bold text-sm tracking-widest uppercase rounded-lg metallic-glow transition-all flex items-center justify-center gap-2">
              <span class="material-symbols-outlined text-sm">search</span>
              Search
            </button>
          </div>
        </div>
      </form>
    </section>

    <!-- Celestial Destinations Bento Grid -->
    <section class="px-4 md:px-8 max-w-6xl mx-auto mt-20 mb-16">
      <div class="text-center mb-12">
        <span class="font-label text-[0.6875rem] uppercase tracking-[0.2em] text-secondary/60 mb-2 block">Featured Routes</span>
        <h2 class="font-headline text-4xl md:text-5xl text-on-surface">Celestial <span class="italic text-primary">Destinations</span></h2>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-6" *ngIf="featuredFlights.length > 0">
        <!-- Main Bento Item (first flight) -->
        <div class="md:col-span-2 md:row-span-2 bg-surface-container-low rounded-2xl overflow-hidden group border border-outline-variant/5 hover:border-primary/20 transition-all relative min-h-[400px]">
          <div class="absolute inset-0 bg-gradient-to-t from-[#131313] via-[#131313]/60 to-transparent z-[1]"></div>
          <div class="absolute inset-0 bg-gradient-to-br from-primary/5 to-transparent"></div>
          <div class="absolute bottom-0 left-0 right-0 p-8 z-[2]">
            <span class="px-3 py-1 bg-primary/10 text-primary text-[10px] uppercase font-bold tracking-tighter rounded-full border border-primary/20 mb-4 inline-block">Featured Route</span>
            <h3 class="font-headline text-3xl text-on-surface mb-2">
              {{ airportCityForCode(featuredFlights[0].source) }}
              <span class="text-primary mx-3">→</span>
              {{ airportCityForCode(featuredFlights[0].destination) }}
            </h3>
            <div class="flex items-center gap-6 text-sm text-secondary/60 mb-4">
              <span class="flex items-center gap-1"><span class="material-symbols-outlined text-sm text-primary/60">schedule</span> {{ featuredFlights[0].departureTime | date:'dd MMM, HH:mm' }}</span>
              <span class="flex items-center gap-1"><span class="material-symbols-outlined text-sm text-primary/60">airlines</span> {{ featuredFlights[0].airline }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-2xl font-headline text-primary">{{ featuredFlights[0].price | rupeeCurrency }}</span>
              <button (click)="bookFlight(featuredFlights[0])" class="px-6 py-3 primary-gradient text-on-primary font-bold text-xs tracking-widest uppercase metallic-glow transition-all rounded-lg">
                Reserve Now
              </button>
            </div>
          </div>
        </div>

        <!-- Bento Sidebar Cards (remaining flights) -->
        <ng-container *ngFor="let flight of featuredFlights.slice(1, 3)">
          <div class="bg-surface-container-low rounded-2xl p-6 border border-outline-variant/5 hover:border-primary/20 transition-all group">
            <span class="text-[10px] uppercase tracking-[0.2em] text-[#d6c692]/50 block mb-4">{{ flight.flightNumber }}</span>
            <h4 class="font-headline text-xl text-on-surface mb-1">
              {{ airportCityForCode(flight.source) }} <span class="text-primary/60">→</span> {{ airportCityForCode(flight.destination) }}
            </h4>
            <p class="text-xs text-secondary/40 mb-6">{{ flight.departureTime | date:'dd MMM yyyy, HH:mm' }}</p>
            <div class="flex items-center justify-between">
              <span class="text-xl font-headline text-primary">{{ flight.price | rupeeCurrency }}</span>
              <button (click)="bookFlight(flight)" class="text-[10px] text-primary font-bold uppercase tracking-widest hover:underline transition-all flex items-center gap-1">
                Book <span class="material-symbols-outlined text-sm">arrow_forward</span>
              </button>
            </div>
          </div>
        </ng-container>
      </div>
    </section>

    <!-- SkyRewards Section -->
    <section class="px-4 md:px-8 max-w-6xl mx-auto mb-20">
      <div class="bg-gradient-to-br from-surface-container-low to-surface-container-high rounded-2xl p-10 border border-outline-variant/10 relative overflow-hidden">
        <div class="absolute -right-10 -top-10 opacity-5">
          <span class="material-symbols-outlined" style="font-size: 200px !important;">workspace_premium</span>
        </div>
        <div class="relative z-10 grid grid-cols-1 md:grid-cols-2 gap-10 items-center">
          <div>
            <span class="font-label text-[0.6875rem] uppercase tracking-[0.2em] text-primary/60 mb-3 block">Loyalty Program</span>
            <h2 class="font-headline text-4xl text-on-surface mb-4">Sky<span class="italic text-primary">Rewards</span></h2>
            <p class="text-secondary/60 leading-relaxed mb-6">Earn celestial points on every booking. Unlock exclusive perks, priority boarding, and luxury lounge access as you ascend through our tier system.</p>
            <button routerLink="/passenger/profile" class="px-6 py-3 primary-gradient text-on-primary font-bold text-xs tracking-widest uppercase metallic-glow transition-all rounded-lg flex items-center gap-2">
              <span class="material-symbols-outlined text-sm">stars</span>
              View Rewards
            </button>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div class="bg-surface-container-low rounded-xl p-5 border border-primary/10">
              <span class="text-3xl font-headline text-primary">3x</span>
              <p class="text-xs text-secondary/40 mt-2">Points on Business Class</p>
            </div>
            <div class="bg-surface-container-low rounded-xl p-5 border border-primary/10">
              <span class="text-3xl font-headline text-primary">150+</span>
              <p class="text-xs text-secondary/40 mt-2">Lounge Partners</p>
            </div>
            <div class="bg-surface-container-low rounded-xl p-5 border border-primary/10">
              <span class="text-3xl font-headline text-primary">4</span>
              <p class="text-xs text-secondary/40 mt-2">Elite Tiers</p>
            </div>
            <div class="bg-surface-container-low rounded-xl p-5 border border-primary/10">
              <span class="text-3xl font-headline text-primary">∞</span>
              <p class="text-xs text-secondary/40 mt-2">Miles Never Expire</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Footer -->
    <footer class="w-full py-8 px-8 border-t border-[#f2ca50]/15 flex flex-col md:flex-row justify-between items-center bg-[#0e0e0e] gap-4">
        <div class="flex items-center gap-6">
            <span class="text-lg font-serif text-[#f2ca50]">SkyHorizon</span>
            <span class="text-[10px] font-label text-[#d6c692]/60 tracking-wide uppercase">© 2024 The Celestial Concierge.</span>
        </div>
        <div class="flex gap-8">
            <a class="text-[#d6c692]/60 hover:text-[#f2ca50] text-[10px] font-label uppercase tracking-widest transition-all cursor-pointer">Privacy Policy</a>
            <a class="text-[#d6c692]/60 hover:text-[#f2ca50] text-[10px] font-label uppercase tracking-widest transition-all cursor-pointer">System Status</a>
            <a class="text-[#d6c692]/60 hover:text-[#f2ca50] text-[10px] font-label uppercase tracking-widest transition-all cursor-pointer">Helpdesk</a>
        </div>
    </footer>
  `,
  styles: [`
    :host { display: block; background: #131313; position: relative; }

    .video-bg {
      position: fixed;
      inset: 0;
      z-index: 0;
      overflow: hidden;
    }
    .video-el {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .video-overlay {
      position: absolute;
      inset: 0;
      background: linear-gradient(
        180deg,
        rgba(19,19,19,0.85) 0%,
        rgba(19,19,19,0.60) 35%,
        rgba(19,19,19,0.70) 65%,
        rgba(19,19,19,0.90) 100%
      );
    }

    nav, section, footer {
      position: relative;
      z-index: 1;
    }
  `]
})
export class LandingComponent implements OnInit, AfterViewInit {
  @ViewChild('bgVideo') bgVideoRef!: ElementRef<HTMLVideoElement>;

  from = '';
  to = '';
  date = '';
  passengers = '1';

  featuredFlights: Flight[] = [];
  flightCount = 0;
  routeCount = 0;
  airportCount = 0;

  private videoStartTime = 1; // skip first 1 second

  constructor(
    private router: Router,
    private flightService: FlightService,
    public auth: AuthService
  ) { }

  ngAfterViewInit(): void {
    const video = this.bgVideoRef?.nativeElement;
    if (video) {
      video.muted = true;
      video.volume = 0;
      video.playbackRate = 0.6; // slow down the video
      video.currentTime = this.videoStartTime;
      // On each loop, skip back to 1s
      video.addEventListener('timeupdate', () => {
        if (video.currentTime < this.videoStartTime) {
          video.currentTime = this.videoStartTime;
        }
      });
    }
  }

  ngOnInit(): void {
    forkJoin({
      flights: this.flightService.getAll().pipe(catchError(() => of([]))),
      routes: this.flightService.getRoutes().pipe(catchError(() => of([]))),
      airports: this.flightService.getAirports().pipe(catchError(() => of([]))),
    }).subscribe(({ flights, routes, airports }) => {
      this.featuredFlights = [...flights]
        .filter((flight) => flight.status !== 'Deleted' && new Date(flight.departureTime).getTime() > new Date().getTime())
        .sort((left, right) => new Date(left.departureTime).getTime() - new Date(right.departureTime).getTime())
        .slice(0, 3);

      this.flightCount = flights.length;
      this.routeCount = routes.length;
      this.airportCount = airports.length;
    });
  }

  swapCities(): void {
    [this.from, this.to] = [this.to, this.from];
  }

  searchFlights(): void {
    this.router.navigate(['/passenger/search'], {
      queryParams: { from: this.from, to: this.to, date: this.date, passengers: this.passengers }
    });
  }

  bookFlight(flight: Flight): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/login']);
      return;
    }
    this.router.navigate(['/passenger/book', flight.flightId]);
  }

  scrollToSearch(): void {
    document.getElementById('search-section')?.scrollIntoView({ behavior: 'smooth' });
  }

  logout(): void {
    this.auth.logout();
  }

  airportCityForCode(code: string): string {
    return code;
  }
}
