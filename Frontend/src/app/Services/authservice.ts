import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, User } from '../Models/models';
import { Apiservice } from './apiservice';

@Injectable({
  providedIn: 'root'
})
export class Authservice {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private api: Apiservice) {
    const storedUser = localStorage.getItem('currentUser');
    if (storedUser) {
      this.currentUserSubject.next(JSON.parse(storedUser));
    }
  }

  register(registerData: RegisterRequest): Observable<AuthResponse> {
    return this.api.post<AuthResponse>('http://localhost:5292/api/Auth/register', registerData).pipe(
      tap(response => {
        this.setCurrentUser(response.user);
      })
    );
  }

  login(loginData: LoginRequest): Observable<AuthResponse> {
    return this.api.post<AuthResponse>('http://localhost:5292/api/Auth/login', loginData).pipe(
      tap(response => {
        this.setCurrentUser(response.user);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('currentUser');
    this.currentUserSubject.next(null);
  }

  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  isAuthenticated(): boolean {
    return this.currentUserSubject.value !== null;
  }

  private setCurrentUser(user: User): void {
    localStorage.setItem('currentUser', JSON.stringify(user));
    this.currentUserSubject.next(user);
  }
}