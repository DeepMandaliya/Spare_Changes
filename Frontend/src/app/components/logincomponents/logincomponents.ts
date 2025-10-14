import { Component } from '@angular/core';
import { LoginRequest } from '../../Models/models';
import { Authservice } from '../../Services/authservice';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-logincomponents',
  imports: [CommonModule,FormsModule,RouterLink],
  templateUrl: './logincomponents.html',
  styleUrl: './logincomponents.css'
})
export class Logincomponents {
 loginData: LoginRequest = {
    email: '',
    password: ''
  };
  isLoading = false;
  errorMessage = '';

  constructor(
    private authService: Authservice,
    private router: Router
  ) {}

  onSubmit(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.loginData).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error || 'Login failed. Please check your credentials.';
      }
    });
  }
}
