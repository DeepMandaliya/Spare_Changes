import { Injectable } from '@angular/core';
import { Apiservice } from './apiservice';
import { Observable, throwError } from 'rxjs';
import { Charity, Transaction, UserPreference, UserStats } from '../Models/models';
import { Authservice } from './authservice';

@Injectable({
  providedIn: 'root'
})
export class Donationservice {
  constructor(private api: Apiservice, private authService: Authservice) { }

  processRoundUp(userId: number): Observable<any> {
    return this.api.post('http://localhost:5292/api/Donation/process-roundup', { userId });
  }

  makeDirectDonation(donationData: any): Observable<any> {
    return this.api.post('http://localhost:5292/api/Donation/make-direct-donation', donationData);
  }

  getUserTransactions(userId: number): Observable<{ transactions: Transaction[], totalDonated: number, transactionCount: number }> {
    return this.api.get(`http://localhost:5292/api/Donation/user-transactions/${userId}`);
  }

  getUserStats(userId: number): Observable<UserStats> {
    return this.api.get(`http://localhost:5292/api/Donation/user-stats/${userId}`);
  }

  updatePreferences(preferences: UserPreference): Observable<any> {
    return this.api.post('http://localhost:5292/api/Donation/update-preferences', preferences);
  }

  getCharities(): Observable<Charity[]> {
    return this.api.get('http://localhost:5292/api/Charities');
  }

  // FIXED: Changed parameter type from plaidItemId to userId and return type to any[]
  getUserPaymentMethods(userId: number): Observable<any[]> {
    return this.api.get<any[]>(`http://localhost:5292/api/Plaid/user-payment-methods/${userId}`);
  
  }

  getCharity(id: number): Observable<Charity> {
    return this.api.get(`http://localhost:5292/api/Charities/${id}`);
  } 

  calculateRoundUp(userId: number): Observable<any> {
    return this.api.get(`http://localhost:5292/api/Donation/calculate-roundup/${userId}`);
  }

  donateRoundUp(roundupRequest: any): Observable<any> {
    if (!roundupRequest.customerEmail) {
      const currentUser = this.authService.getCurrentUser();
      roundupRequest.customerEmail = currentUser?.email;
      
      if (!roundupRequest.customerEmail) {
        return throwError(() => new Error('Email is required for donation'));
      }
    }

    return this.api.post(`http://localhost:5292/api/Donation/donate-roundup`, roundupRequest);
  }
}