import { Injectable } from '@angular/core';
import { Apiservice } from './apiservice';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class Webhookservice {
  constructor(private api: Apiservice) { }

  handlePlaidWebhook(webhookData: any): Observable<any> {
    return this.api.post('http://localhost:5292/api/Webhooks/plaid', webhookData);
  }

  handleStripeWebhook(webhookData: any): Observable<any> {
    return this.api.post('http://localhost:5292/api/Webhooks/stripe', webhookData);
  }
}
