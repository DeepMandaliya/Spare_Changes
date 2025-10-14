import { Component } from '@angular/core';
import { Transaction, User, UserStats } from '../../Models/models';
import { Authservice } from '../../Services/authservice';

import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Donationservice } from '../../Services/donationservice';


import { Donationoptions } from '../donationoptions/donationoptions';


@Component({
  selector: 'app-dashboardcomponent',
  standalone:true,
  imports: [CommonModule,FormsModule,RouterLink, Donationoptions],
  templateUrl: './dashboardcomponent.html',
  styleUrl: './dashboardcomponent.css'
})
export class Dashboardcomponent {
 currentUser: User | null = null;
  userStats: UserStats | null = null;
  recentTransactions: Transaction[] = [];
  isLoading = false;
  activeTab: 'overview' | 'transactions' = 'overview';

  constructor(
    private authService: Authservice,
    private donationService: Donationservice,
    private router:Router
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    if (this.currentUser) {
      this.loadUserStats();
      this.loadRecentTransactions();
    }
  }

  loadUserStats(): void {
    this.isLoading = true;
    if (!this.currentUser) return;

    this.donationService.getUserStats(this.currentUser.id).subscribe({
      next: (stats) => {
        this.userStats = stats;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading stats:', error);
        this.isLoading = false;
      }
    });
  }

  loadRecentTransactions(): void {
    if (!this.currentUser) return;

    this.donationService.getUserTransactions(this.currentUser.id).subscribe({
      next: (response) => {
        this.recentTransactions = response.transactions.slice(0, 5); // Get only 5 most recent
      },
      error: (error) => {
        console.error('Error loading transactions:', error);
      }
    });
  }

  processRoundUp(): void {
    if (!this.currentUser) return;

    this.isLoading = true;
    this.donationService.processRoundUp(this.currentUser.id).subscribe({
      next: (response) => {
        this.isLoading = false;
        alert('Round-up processing started! Check your transactions for updates.');
        this.loadUserStats();
        this.loadRecentTransactions();
      },
      error: (error) => {
        this.isLoading = false;
        alert('Error processing round-up: ' + error);
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  setActiveTab(tab: 'overview' | 'transactions'): void {
    this.activeTab = tab;
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'completed':
        return 'badge-completed';
      case 'processing':
        return 'badge-processing';
      case 'pending':
        return 'badge-pending';
      case 'failed':
        return 'badge-failed';
      default:
        return 'badge-secondary';
    }
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric'
    });
  }

  getGreeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
}
}