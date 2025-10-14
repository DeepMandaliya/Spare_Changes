import { ChangeDetectorRef, Component } from '@angular/core';
import { Transaction } from '../../Models/models';
import { Authservice } from '../../Services/authservice';
import { Donationservice } from '../../Services/donationservice';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-transactioncomponent',
  imports: [CommonModule,FormsModule,RouterLink],
  templateUrl: './transactioncomponent.html',
  styleUrl: './transactioncomponent.css'
})
export class Transactioncomponent {
 currentUser: any = null;
  transactions: Transaction[] = [];
  isLoading = false;
  totalDonated = 0;
  transactionCount = 0;

  constructor(
    private authService: Authservice,
    private donationService: Donationservice,
    private cdr:ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    if (this.currentUser) {
      this.loadTransactions();
      this.cdr.detectChanges();
    }
  }

  loadTransactions(): void {
    this.isLoading = true;
    this.donationService.getUserTransactions(this.currentUser.id).subscribe({
      next: (response) => {
        this.transactions = response.transactions;
        this.totalDonated = response.totalDonated;
        this.transactionCount = response.transactionCount;
        this.isLoading = false;
      this.cdr.detectChanges();

      },
      error: (error) => {
        console.error('Error loading transactions:', error)
      this.cdr.detectChanges();
        ;
        this.isLoading = false;
      }
    });
  }

  getStatusBadgeClass(status: string): string {
    
    switch (status) {
      
      case 'completed':
        return 'bg-success';
      case 'processing':
        return 'bg-warning';
      case 'pending':
        return 'bg-info';
      case 'failed':
        return 'bg-danger';
      default:
        return 'bg-secondary';
        
    }
  }

  getStatusText(status: string): string {
      this.cdr.detectChanges();

    return status.charAt(0).toUpperCase() + status.slice(1);

  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
