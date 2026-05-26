import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { inject } from '@angular/core';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class Navbar {
  @Input() userRole: string | null = null;
  @Input() userName: string | null = null;
  @Output() logout = new EventEmitter<void>();
  @Output() toggleSidebar = new EventEmitter<void>();

  private router = inject(Router);

  onLogout() {
    this.logout.emit();
  }

  onSearch(query: string) {
    if (!query) return;
    
    // Redirect to search or handle specific ID lookup
    // If it's for flight searching, we can go to /search with a query param
    const trimmed = query.trim().toUpperCase();
    if (trimmed.length > 0) {
      if (this.userRole === 'Admin' || this.userRole === 'Staff' || this.userRole === 'Dealer') {
        // For admin, maybe go to a specific flight or just the flights list
        this.router.navigate(['/flights'], { queryParams: { search: trimmed } });
      } else {
        this.router.navigate(['/search'], { queryParams: { flightNumber: trimmed } });
      }
    }
  }
}
