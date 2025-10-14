import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class Apiservice {
   constructor(private http: HttpClient) { }

  get<T>(fullUrl: string): Observable<T> {
    return this.http.get<T>(fullUrl);
  }

  post<T>(fullUrl: string, data: any): Observable<T> {
    return this.http.post<T>(fullUrl, data);
  }

  put<T>(fullUrl: string, data: any): Observable<T> {
    return this.http.put<T>(fullUrl, data);
  }

  delete<T>(fullUrl: string): Observable<T> {
    return this.http.delete<T>(fullUrl);
  }
}