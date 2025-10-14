import { Component } from '@angular/core';
import { RegisterRequest } from '../../Models/models';
import { Authservice } from '../../Services/authservice';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-registercomponent',
  imports: [CommonModule,FormsModule,RouterLink],
  templateUrl: './registercomponent.html',
  styleUrl: './registercomponent.css'
})
export class Registercomponent {
  registerData: RegisterRequest = {
    username: '',
    email: '',
    password: ''
  };
  confirmPassword = '';
  isLoading = false;
  errorMessage = '';
  passwordStrength = '';

  constructor(
    private authService: Authservice,
    private router: Router
  ) {}

  onSubmit(): void {
    if (this.registerData.password !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match';
      return;
    }

    if (this.registerData.password.length < 6) {
      this.errorMessage = 'Password must be at least 6 characters long';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.register(this.registerData).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error || 'Registration failed. Please try again.';
      }
    });
  }

  checkPasswordStrength(): void {
    const password = this.registerData.password;
    if (password.length === 0) {
      this.passwordStrength = '';
      return;
    }

    let strength = 0;
    if (password.length >= 6) strength++;
    if (password.match(/[a-z]/) && password.match(/[A-Z]/)) strength++;
    if (password.match(/\d/)) strength++;
    if (password.match(/[^a-zA-Z\d]/)) strength++;

    switch (strength) {
      case 0:
      case 1:
        this.passwordStrength = 'weak';
        break;
      case 2:
        this.passwordStrength = 'fair';
        break;
      case 3:
        this.passwordStrength = 'good';
        break;
      case 4:
        this.passwordStrength = 'strong';
        break;
    }
  }

  getPasswordStrengthClass(): string {
    switch (this.passwordStrength) {
      case 'weak':
        return 'password-weak';
      case 'fair':
        return 'password-fair';
      case 'good':
        return 'password-good';
      case 'strong':
        return 'password-strong';
      default:
        return '';
    }
  }

  getPasswordStrengthText(): string {
    switch (this.passwordStrength) {
      case 'weak':
        return 'Weak password';
      case 'fair':
        return 'Fair password';
      case 'good':
        return 'Good password';
      case 'strong':
        return 'Strong password';
      default:
        return 'Enter a password';
    }
  }
}
