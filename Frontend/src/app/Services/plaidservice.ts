import { Injectable } from '@angular/core';
import { Apiservice } from './apiservice';
import { Observable } from 'rxjs';
import { CreateStripeTokenRequest, PaymentMethodsResponse } from '../Models/models';

@Injectable({
  providedIn: 'root'
})
export class Plaidservice {
  constructor(private api: Apiservice) { }

// In your Plaidservice
createLinkToken(clientUserId: string, products?: string[], config?: any): Observable<any> {
  return this.api.post('http://localhost:5292/api/Plaid/create-link-token', {
    clientUserId,
    products: products || ['auth', 'transactions'],
    config: config || {} // Add the config parameter
  });
}

  createLinkTokenForBank(clientUserId: string): Observable<any> {
    return this.api.post('http://localhost:5292/api/Plaid/create-link-token-for-bank', {
      clientUserId
    });
  }

  createLinkTokenForCards(clientUserId: string): Observable<any> {
    return this.api.post('http://localhost:5292/api/Plaid/create-link-token-for-cards', {
      clientUserId
    });
  }

  exchangePublicToken(publicToken: string, userId: number): Observable<any> {
    return this.api.post('http://localhost:5292/api/Plaid/exchange-public-token', {
      publicToken,
      userId
    });
  }

  getPaymentMethods(plaidItemId: string): Observable<PaymentMethodsResponse> {
    return this.api.post<PaymentMethodsResponse>(`http://localhost:5292/api/Plaid/get-payment-methods/${plaidItemId}`, {});
  }

  createStripeBankToken(request: CreateStripeTokenRequest): Observable<any> {
    return this.api.post('http://localhost:5292/api/Plaid/create-stripe-bank-token', request);
  }

  createStripeCardToken(request: CreateStripeTokenRequest): Observable<any> {
    return this.api.post('http://localhost:5292/api/Plaid/create-stripe-card-token', request);
  }

  syncUserInfo(plaidItemId: string): Observable<any> {
    return this.api.post(`http://localhost:5292/api/Plaid/sync-user-info/${plaidItemId}`, {});
  }
  
getUserPaymentMethods(userId: number): Observable<any> {
  return this.api.get(`http://localhost:5292/api/Plaid/user-payment-methods/${userId}`);
}

setDefaultPaymentMethod(userId: number, paymentMethodId: number): Observable<any> {
  return this.api.post(`http://localhost:5292/api/Plaid/users/${userId}/payment-methods/${paymentMethodId}/set-default`, {});
}

activatePaymentMethod(userId: number, paymentMethodId: number): Observable<any> {
  return this.api.post(`http://localhost:5292/api/Plaid/users/${userId}/payment-methods/${paymentMethodId}/activate`, {});
}

// You already have removePaymentMethod, but here it is for reference:
removePaymentMethod(paymentMethodId: number): Observable<any> {
  return this.api.delete(`http://localhost:5292/api/Plaid/user-payment-methods/${paymentMethodId}`);
}

getUserPlaidItems(userId: number): Observable<any> {
  return this.api.get(`http://localhost:5292/api/Plaid/user-plaid-items/${userId}`);
}


getTransactions(accessToken: string, startDate: Date, endDate: Date): Observable<any> {
  const request = {
    access_token: accessToken,
    start_date: startDate.toISOString().split('T')[0],
    end_date: endDate.toISOString().split('T')[0]
  };
  return this.api.post(`http://localhost:5292/api/Plaid/transactions`, request);
}
}